# IoTDataPlatformHelperSuite.IoT.Python

A Critical Manufacturing Connect IoT deployment package (`Cmf.Community.IoTDataPlatformHelperSuite.IoT.Python`) that adds a **"Python Code" task** to the Connect IoT Controller Engine. It lets flow designers write custom Python logic that runs inside a Controller Engine flow, as a Python-based alternative to the built-in JavaScript "Code Execution" task.

The Python code runs in a sandboxed [Pyodide](https://pyodide.org/) (CPython compiled to WebAssembly) runtime on the Node.js-hosted Controller Engine, with dynamically configurable inputs/outputs like any other IoT task.

## Package layout

- `projects/controller-engine-python-tasks/` — the Angular library that implements the task (the actual source root; despite the outer folder's `.IoT.Python` name, this is a TypeScript/Angular package, not a Python project).
  - `src/lib/tasks/python-code/` — the Python Code task implementation.
  - `metadata/` — Connect IoT package metadata used by the platform to discover/register the task.
- `cmfpackage.json` / `ui.xml` — CM deployment framework packaging metadata for installing this as an IoT package.

## Feature: Python Code task

### What it does

Adds a task named **"Python Code"** to the Controller Engine flow designer. The task author:

1. Adds the inputs/outputs the task needs, via auto-linkable ports in the designer.
2. Writes Python source in a Monaco-based code editor in the task's settings panel, following a fixed convention:

```python
class Code:
    def __init__(self, framework):
        self.logger = framework['logger']

    async def main(self, inputs, outputs):
        value = inputs['myInput']
        self.logger.info(f"Processing: {value}")
        outputs.myOutput.emit(value * 2)
        return { 'myOutput': value * 2 }
```

3. At runtime, activating the task (via the `activate` input) collects the inputs received within the same execution context and calls `main(inputs, outputs)` on an instance of `Code`, emitting `success` or `error` outputs depending on the result.

### Design-time vs. runtime

The task is split into browser and Node modules so it behaves correctly depending on where it's hosted:

- **Browser** (design-time preview) — binds a `PyodideManagerBrowser` stub that warns that Python execution isn't available in the browser.
- **Node** (actual runtime, on the Controller) — binds `PyodideManagerHandler`, which loads Pyodide, installs any requested `micropip` packages, and executes the compiled Python function.

### Sandbox and security

`PyodideManagerHandler` initializes Pyodide and then blocks `import js` / `import pyodide_js` inside the sandbox, preventing user-written Python code from reaching the Node.js/global JavaScript scope directly. Instead, it exposes a fixed Python "bootstrap" API that bridges into the Controller Engine's own JS/TS APIs through explicit proxy functions injected into Pyodide's globals:

| Python API (via `framework[...]`) | Bridges to |
|---|---|
| `logger` | Controller Engine logger |
| `data_store` (`get`/`set`) | `System.DataStore` |
| `message_bus` (`send_request`/`publish`) | `Communication.MessageBus` |
| `system` (`call`) | `System.SystemAPI` |
| `utils` (`sleep`, `stringify`, `convert_value_to_type`, `execute_with_retry`, `execute_with_system_error_retry`) | Controller Engine utility functions |
| `lbos` | MES business objects (`System.LBOS`) |
| `driver` (`connect`, `disconnect`, `execute_command`, `get_properties`, `set_properties`, `send_raw`, `notify_raw`, `register_custom_driver_definitions`) | `System.DriverProxy` (only when the task has an equipment driver attached) |

### Settings

- `pyCode` / `pyCodeBase64` — the Python source, edited as plain text and persisted base64-encoded.
- `packages` — extra Python packages to install via `micropip` before running the code.
- `contextExpirationInMilliseconds` — how long collected input values for an execution context are kept before being discarded.
- `executionTimeoutMs` — timeout applied to code execution.

### Developer experience

The settings panel's Monaco editor registers a custom autocomplete/IntelliSense provider for the `framework` object, so task authors get code completion for `self.logger`, `self.data_store`, `self.message_bus`, `self.system`, `self.utils`, `self.driver`, and `self.lbos` while writing Python code in the designer.

## Tech stack

- Angular 17 library (`ng-packagr`), built as a Connect IoT "Tasks Package".
- [`pyodide`](https://pyodide.org/) for in-process Python execution.
- `@criticalmanufacturing/connect-iot-controller-engine` and `cmf-core-connect-iot` for task/designer integration.
- `inversify` for dependency injection of the Pyodide manager (browser vs. node implementation).
- Mocha/Chai (+ nyc) for unit tests, Karma/Jasmine for Angular-level tests.

## Tests

Unit tests live under `projects/controller-engine-python-tasks/test/unit/tasks/python-code/` and cover the task's activation/output flow plus each bridged API surface individually (data store, logger, message bus, system, utils).

> **Note:** A `code-execution/` folder at the repo root contains test files (`code-execution.api.*.test.ts`) that reference source paths which do not exist in this package (they point at a `code-execution` task module, not `python-code`). These appear to be leftover reference/scaffolding files from the JavaScript "Code Execution" task this package was modeled after, and are not part of this package's buildable source — worth confirming with the team whether they should be removed.
