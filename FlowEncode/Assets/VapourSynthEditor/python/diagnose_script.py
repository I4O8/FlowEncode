import ast
import difflib
import json
import sys


def build_runtime_index():
    import vapoursynth as vs

    core = vs.core
    core_members = {}
    namespaces = {}

    for name in dir(core):
        if name.startswith("_"):
            continue

        try:
            item = getattr(core, name)
        except Exception:
            continue

        item_type = type(item).__name__
        if item_type == "Plugin":
            namespace = getattr(item, "namespace", name)
            functions = set()
            for function_name in dir(item):
                if function_name.startswith("_"):
                    continue

                try:
                    function = getattr(item, function_name)
                except Exception:
                    continue

                if callable(function):
                    functions.add(function_name)

            namespaces[namespace] = functions
            core_members[namespace] = "namespace"
            continue

        if callable(item):
            core_members[name] = "method"
            continue

        core_members[name] = "property"

    core_version = getattr(vs, "__version__", None)
    if isinstance(core_version, (tuple, list)) and core_version:
        version_label = ".".join(str(part) for part in core_version)
    else:
        version_label = str(core_version or "unknown")

    return {
        "runtimeSummary": f"VapourSynth {version_label} / {len(namespaces)} plugins",
        "coreMembers": core_members,
        "namespaces": namespaces
    }


def build_syntax_diagnostic(ex):
    start_line = ex.lineno or 1
    start_column = ex.offset or 1
    end_line = ex.end_lineno or start_line
    end_column = ex.end_offset or max(start_column + 1, 2)

    return {
        "code": "python-syntax",
        "severity": "error",
        "message": ex.msg or str(ex),
        "startLine": start_line,
        "startColumn": start_column,
        "endLine": end_line,
        "endColumn": end_column,
        "source": "python",
        "relatedText": (ex.text or "").strip()
    }


def assign_parents(root):
    for parent in ast.walk(root):
        for child in ast.iter_child_nodes(parent):
            child.parent = parent


def extract_attribute_chain(node):
    parts = []
    current = node

    while isinstance(current, ast.Attribute):
        parts.append(current.attr)
        current = current.value

    if isinstance(current, ast.Name):
        parts.append(current.id)
        parts.reverse()
        return parts

    return None


def normalize_chain(parts):
    if not parts:
        return None

    if len(parts) >= 2 and parts[0] == "vs" and parts[1] == "core":
        return ["core", *parts[2:]]

    if parts[0] == "core":
        return parts

    return None


def build_unknown_member_message(kind, name, candidates):
    suggestion = difflib.get_close_matches(name, candidates, n=1)
    if suggestion:
        return f"Unknown VapourSynth {kind} '{name}'. Did you mean '{suggestion[0]}'?"

    return f"Unknown VapourSynth {kind} '{name}'."


def locate_segment(node, source, segment):
    segment_text = ast.get_source_segment(source, node) or segment
    relative_index = segment_text.find(segment)
    start_column = node.col_offset + max(relative_index, 0) + 1
    end_column = start_column + len(segment)

    return {
        "startLine": node.lineno,
        "startColumn": start_column,
        "endLine": node.end_lineno or node.lineno,
        "endColumn": end_column
    }


def build_static_diagnostics(tree, source, runtime_index):
    diagnostics = []
    seen = set()

    assign_parents(tree)

    for node in ast.walk(tree):
        if not isinstance(node, ast.Attribute):
            continue

        if isinstance(getattr(node, "parent", None), ast.Attribute):
            continue

        chain = normalize_chain(extract_attribute_chain(node))
        if not chain or len(chain) < 2:
            continue

        if chain[0] != "core":
            continue

        root_member = chain[1]
        if len(chain) == 2:
            if root_member in runtime_index["coreMembers"]:
                continue

            location = locate_segment(node, source, root_member)
            diagnostic = {
                "code": "vapoursynth-core-member",
                "severity": "error",
                "message": build_unknown_member_message(
                    "core member",
                    root_member,
                    list(runtime_index["coreMembers"].keys())),
                "source": "vapoursynth",
                "relatedText": ast.get_source_segment(source, node) or root_member,
                **location
            }
        else:
            namespace = chain[1]
            function = chain[2]
            functions = runtime_index["namespaces"].get(namespace)

            if functions is None:
                location = locate_segment(node, source, namespace)
                diagnostic = {
                    "code": "vapoursynth-namespace",
                    "severity": "error",
                    "message": build_unknown_member_message(
                        "plugin namespace",
                        namespace,
                        list(runtime_index["namespaces"].keys())),
                    "source": "vapoursynth",
                    "relatedText": ast.get_source_segment(source, node) or namespace,
                    **location
                }
            elif function not in functions:
                location = locate_segment(node, source, function)
                diagnostic = {
                    "code": "vapoursynth-function",
                    "severity": "error",
                    "message": build_unknown_member_message(
                        "function",
                        function,
                        sorted(functions)),
                    "source": "vapoursynth",
                    "relatedText": ast.get_source_segment(source, node) or function,
                    **location
                }
            else:
                continue

        key = (
            diagnostic["code"],
            diagnostic["startLine"],
            diagnostic["startColumn"],
            diagnostic["endLine"],
            diagnostic["endColumn"],
            diagnostic["message"]
        )
        if key in seen:
            continue

        seen.add(key)
        diagnostics.append(diagnostic)

    diagnostics.sort(key=lambda item: (
        item["startLine"],
        item["startColumn"],
        item["code"]))
    return diagnostics


def main():
    payload = json.load(sys.stdin)
    file_path = payload.get("filePath") or "<untitled.vpy>"
    content = payload.get("content") or ""

    try:
        tree = ast.parse(content, filename=file_path)
    except SyntaxError as ex:
        result = {
            "isRuntimeReady": False,
            "runtimeSummary": "Python syntax error",
            "diagnostics": [build_syntax_diagnostic(ex)]
        }
        json.dump(result, sys.stdout, ensure_ascii=False)
        return

    try:
        runtime_index = build_runtime_index()
        diagnostics = build_static_diagnostics(tree, content, runtime_index)
        result = {
            "isRuntimeReady": True,
            "runtimeSummary": runtime_index["runtimeSummary"],
            "diagnostics": diagnostics
        }
    except Exception as ex:
        result = {
            "isRuntimeReady": False,
            "runtimeSummary": str(ex),
            "diagnostics": []
        }

    json.dump(result, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
