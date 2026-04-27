import json
import os
import sys
import traceback
from collections import OrderedDict
from contextlib import contextmanager

import numpy as np
import vapoursynth as vs

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")
    sys.stderr.reconfigure(encoding="utf-8", errors="backslashreplace")
except Exception:
    pass

PROTOCOL_STDOUT = sys.stdout


def emit(payload):
    PROTOCOL_STDOUT.write(json.dumps(payload, ensure_ascii=False) + "\n")
    PROTOCOL_STDOUT.flush()


@contextmanager
def redirect_script_stdout():
    previous_stdout = sys.stdout

    try:
        sys.stdout = sys.stderr
        yield
    finally:
        sys.stdout = previous_stdout


def format_prop_value(value):
    if isinstance(value, bytes):
        try:
            return value.decode("utf-8", errors="replace")
        except Exception:
            return repr(value)

    if isinstance(value, (list, tuple)):
        return ", ".join(format_prop_value(item) for item in value)

    return str(value)


def matrix_prop_to_string(value):
    return {
        0: "rgb",
        1: "709",
        4: "fcc",
        5: "470bg",
        6: "170m",
        7: "240m",
        8: "ycgco",
        9: "2020ncl",
        10: "2020cl",
        12: "chromancl",
        13: "chromacl",
        14: "ictcp",
    }.get(int(value))


def range_prop_to_string(value):
    return {
        0: "full",
        1: "limited",
    }.get(int(value))


class PreviewSession:
    def __init__(self, startup):
        self.source_file_path = startup.get("sourceFilePath") or "<preview.vpy>"
        self.display_name = startup.get("displayName") or os.path.basename(self.source_file_path)
        self.content = startup.get("content") or ""
        self.working_directory = startup.get("workingDirectory") or os.getcwd()
        self.namespace = {}
        self.video_outputs = OrderedDict()
        self.render_clips = {}

        self._evaluate_script()

    def _evaluate_script(self):
        previous_cwd = os.getcwd()
        previous_sys_path = list(sys.path)

        source_directory = os.path.dirname(self.source_file_path) if self.source_file_path else ""
        namespace = {
            "__name__": "__flowencode_vapoursynth_preview__",
            "__file__": self.source_file_path,
            "__package__": None,
        }

        try:
            vs.clear_outputs()

            if self.working_directory and os.path.isdir(self.working_directory):
                os.chdir(self.working_directory)

            if source_directory and os.path.isdir(source_directory):
                sys.path.insert(0, source_directory)

            with redirect_script_stdout():
                exec(compile(self.content, self.source_file_path, "exec"), namespace)

            outputs = OrderedDict(sorted(vs.get_outputs().items()))
            for index, output in outputs.items():
                if isinstance(output, vs.VideoOutputTuple):
                    self.video_outputs[index] = output
        finally:
            os.chdir(previous_cwd)
            sys.path[:] = previous_sys_path

        if not self.video_outputs:
            raise RuntimeError("The script did not expose any video outputs.")

        self.namespace = namespace

    def describe_outputs(self):
        descriptions = []

        for index, output in self.video_outputs.items():
            clip = output.clip
            fmt = clip.format
            descriptions.append(
                {
                    "index": int(index),
                    "name": f"Output {index}",
                    "width": int(clip.width),
                    "height": int(clip.height),
                    "totalFrames": int(clip.num_frames),
                    "fpsNumerator": int(clip.fps.numerator),
                    "fpsDenominator": int(clip.fps.denominator),
                    "formatName": getattr(fmt, "name", "Unknown") if fmt is not None else "Unknown",
                    "bitsPerSample": int(getattr(fmt, "bits_per_sample", 0) or 0) if fmt is not None else 0,
                }
            )

        return descriptions

    def _prepare_render_clip(self, output_index):
        if output_index in self.render_clips:
            return self.render_clips[output_index]

        output = self.video_outputs[output_index]
        clip = output.clip
        fmt = clip.format
        if fmt is None:
            raise RuntimeError(f"Output {output_index} has no video format.")

        if fmt.color_family == vs.RGB and fmt.id == vs.RGB24:
            render_clip = clip
        else:
            with redirect_script_stdout():
                props = clip.get_frame(0).props if clip.num_frames > 0 else {}
            resize_attempts = []
            prop_matrix = matrix_prop_to_string(props["_Matrix"]) if "_Matrix" in props else None
            prop_range = range_prop_to_string(props["_ColorRange"]) if "_ColorRange" in props else None

            if prop_matrix and prop_range:
                resize_attempts.append({"matrix_in_s": prop_matrix, "range_in_s": prop_range})
            if prop_matrix:
                resize_attempts.append({"matrix_in_s": prop_matrix})
            if fmt.color_family not in (vs.RGB, vs.GRAY):
                resize_attempts.append({"matrix_in_s": "709", "range_in_s": "limited"})
                resize_attempts.append({"matrix_in_s": "709"})

            resize_attempts.append({})

            last_error = None
            render_clip = None
            for resize_kwargs in resize_attempts:
                try:
                    with redirect_script_stdout():
                        render_clip = clip.resize.Bicubic(format=vs.RGB24, **resize_kwargs)
                        render_clip = render_clip.std.CopyFrameProps(clip)
                    break
                except Exception as error:
                    last_error = error

            if render_clip is None:
                raise last_error or RuntimeError(f"Unable to convert output {output_index} to RGB24.")

        self.render_clips[output_index] = render_clip
        return render_clip

    def render_frame(self, output_index, frame_number, raw_path):
        if output_index not in self.video_outputs:
            raise RuntimeError(f"Output {output_index} does not exist.")

        with redirect_script_stdout():
            render_clip = self._prepare_render_clip(output_index)
        if frame_number < 0 or frame_number >= render_clip.num_frames:
            raise RuntimeError(f"Frame {frame_number} is outside output {output_index}.")

        with redirect_script_stdout():
            frame = render_clip.get_frame(frame_number)
        red = np.asarray(frame[0])
        green = np.asarray(frame[1])
        blue = np.asarray(frame[2])
        alpha = np.full(red.shape, 255, dtype=np.uint8)
        bgra = np.dstack((blue, green, red, alpha))

        os.makedirs(os.path.dirname(raw_path), exist_ok=True)
        bgra.tofile(raw_path)

        props = []
        for key in sorted(frame.props.keys()):
            try:
                props.append({"key": str(key), "value": format_prop_value(frame.props[key])})
            except Exception:
                props.append({"key": str(key), "value": "<unavailable>"})

        pict_type = frame.props.get("_PictType")
        frame_type = format_prop_value(pict_type) if pict_type is not None else None

        return {
            "type": "frame",
            "outputIndex": int(output_index),
            "frameNumber": int(frame_number),
            "width": int(frame.width),
            "height": int(frame.height),
            "rawPixelPath": raw_path,
            "frameType": frame_type,
            "properties": props,
        }


def main():
    if len(sys.argv) < 2:
        emit({"type": "error", "message": "Preview host startup payload is missing."})
        return 1

    startup_path = sys.argv[1]

    try:
        with open(startup_path, "r", encoding="utf-8") as file:
            startup = json.load(file)

        session = PreviewSession(startup)
        emit({"type": "ready", "outputs": session.describe_outputs()})
    except Exception as error:
        traceback.print_exc(file=sys.stderr)
        emit({"type": "error", "message": str(error)})
        return 1

    for line in sys.stdin:
        if not line:
            break

        line = line.strip()
        if not line:
            continue

        command = None
        try:
            command = json.loads(line)
            command_name = command.get("command")

            if command_name == "close":
                return 0

            if command_name == "renderFrame":
                request_id = int(command.get("requestId", 0))
                response = session.render_frame(
                    int(command["outputIndex"]),
                    int(command["frameNumber"]),
                    command["rawPath"],
                )
                response["requestId"] = request_id
                emit(response)
                continue

            emit({"type": "error", "message": f"Unsupported command: {command_name}"})
        except Exception as error:
            traceback.print_exc(file=sys.stderr)
            emit(
                {
                    "type": "error",
                    "requestId": int(command.get("requestId", 0)) if isinstance(command, dict) else 0,
                    "message": str(error),
                }
            )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
