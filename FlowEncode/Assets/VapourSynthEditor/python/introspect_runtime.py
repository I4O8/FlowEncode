import inspect
import json
import sys


def format_annotation(annotation):
    if annotation is inspect.Signature.empty:
        return ""

    try:
        return inspect.formatannotation(annotation)
    except Exception:
        return str(annotation)


def build_parameter_descriptor(parameter):
    return {
        "name": parameter.name,
        "label": str(parameter),
        "documentation": ""
    }


def build_function_descriptor(namespace_name, plugin_name, function_name, function):
    signature = inspect.signature(function)
    return {
        "name": function_name,
        "qualifiedName": f"core.{namespace_name}.{function_name}",
        "signatureLabel": f"{function_name}{signature}",
        "returnType": format_annotation(signature.return_annotation),
        "parameters": [
            build_parameter_descriptor(parameter)
            for parameter in signature.parameters.values()
        ],
        "documentation": plugin_name or ""
    }


def build_runtime_snapshot():
    import vapoursynth as vs

    core = vs.core
    namespaces = []
    core_members = []

    for name in sorted(dir(core)):
        if name.startswith("_"):
            continue

        try:
            item = getattr(core, name)
        except Exception:
            continue

        item_type = type(item).__name__
        if item_type == "Plugin":
            try:
                plugin_namespace = getattr(item, "namespace", name)
            except Exception:
                plugin_namespace = name

            try:
                plugin_identifier = getattr(item, "identifier", "")
            except Exception:
                plugin_identifier = ""

            try:
                plugin_display_name = getattr(item, "name", plugin_namespace)
            except Exception:
                plugin_display_name = plugin_namespace

            functions = []
            for function_name in sorted(dir(item)):
                if function_name.startswith("_"):
                    continue

                try:
                    function = getattr(item, function_name)
                except Exception:
                    continue

                if not callable(function):
                    continue

                try:
                    descriptor = build_function_descriptor(
                        plugin_namespace,
                        plugin_display_name,
                        function_name,
                        function)
                except Exception:
                    descriptor = {
                        "name": function_name,
                        "qualifiedName": f"core.{plugin_namespace}.{function_name}",
                        "signatureLabel": function_name,
                        "returnType": "",
                        "parameters": [],
                        "documentation": plugin_display_name or ""
                    }

                functions.append(descriptor)

            namespaces.append({
                "name": plugin_namespace,
                "identifier": plugin_identifier,
                "displayName": plugin_display_name,
                "functions": functions
            })
            core_members.append({
                "name": plugin_namespace,
                "kind": "namespace",
                "detail": plugin_identifier or plugin_display_name,
                "documentation": plugin_display_name or ""
            })
            continue

        if callable(item):
            core_members.append({
                "name": name,
                "kind": "method",
                "detail": item_type,
                "documentation": ""
            })
            continue

        core_members.append({
            "name": name,
            "kind": "property",
            "detail": item_type,
            "documentation": ""
        })

    core_version = getattr(vs, "__version__", None)
    if isinstance(core_version, (tuple, list)) and core_version:
        core_version_label = ".".join(str(part) for part in core_version)
    else:
        core_version_label = str(core_version or "unknown")

    return {
        "isRuntimeReady": True,
        "runtimeSummary": f"VapourSynth {core_version_label} / {len(namespaces)} plugins",
        "coreMembers": core_members,
        "namespaces": namespaces
    }


def main():
    try:
        payload = build_runtime_snapshot()
    except Exception as ex:
        payload = {
            "isRuntimeReady": False,
            "runtimeSummary": str(ex),
            "coreMembers": [],
            "namespaces": []
        }

    json.dump(payload, sys.stdout, ensure_ascii=False)


if __name__ == "__main__":
    main()
