// Ambient declaration so the Angular library build (types: []) can type-check
// this Node.js CommonJS global without requiring @types/node in the designer build.
declare const require: (id: string) => unknown;

/** Minimal typing for the Pyodide runtime object */
interface PyodideRuntime {
    runPythonAsync(code: string): Promise<unknown>;
    loadPackage(pkg: string): Promise<void>;
    pyimport(name: string): any;
    toPy(value: unknown): unknown;
    globals: { set(key: string, value: unknown): void };
    ffi: { PyProxy: new (...args: unknown[]) => unknown };
}

import { injectable } from "inversify";
import { DataStoreLocation, Dependencies, DI, System, TYPES, Communication, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";
import { PyodideSettings } from "./pyodideConfig";
import { PyodideManager, PyodideOutputsProxy } from "./pyodideManager";

/**
 * Manages the Pyodide runtime lifecycle, package installation, and Python execution.
 * Registered as a Controller-scoped singleton so all tasks in a controller share one Pyodide instance.
 *
 * Python isolation is enforced by Pyodide itself — `import js` and `import pyodide_js` are
 * replaced with empty namespaces in initialize(), preventing user Python code from reaching
 * the Node.js global scope. The JS bridge callbacks exposed to Python are data-only (no eval,
 * no require), so there is no meaningful attack surface even if a Pyodide escape occurred.
 *
 * The Python code is expected to follow this convention:
 * ```python
 * class Code:
 *     def __init__(self, framework):
 *         self.logger = framework['logger']
 *
 *     async def main(self, inputs, outputs):
 *         value = inputs['myInput']
 *         self.logger.info(f"Processing: {value}")
 *         outputs.myOutput.emit(value * 2)
 *         return { 'myOutput': value * 2 }
 * ```
 */
@injectable()
export class PyodideManagerHandler implements PyodideManager {

    @DI.Inject(TYPES.Dependencies.Logger)
    private _logger: Dependencies.Logger;

    @DI.Inject(TYPES.System.PersistedDataStore)
    private _dataStore: System.DataStore;

    @DI.Inject(TYPES.System.MessageBus)
    private _messageBus: Communication.MessageBus;

    @DI.Inject(TYPES.System.API)
    private _systemApi: System.SystemAPI;

    @DI.Inject(TYPES.System.Driver)
    @DI.Optional()
    private _driver: System.DriverProxy;

    private _pyodide: PyodideRuntime | null = null;
    private _initialized: boolean = false;

    /**
     * Python bootstrap — defines the proxy classes available to user code.
     * Embedded into every execution run so the classes are always in scope.
     *
     * Bridge callables (_log_fn, _emit_fn, …) are injected into Python's own
     * global namespace via pyodide.globals.set() before each execution.
     * No `import js` is used, so the Python sandbox has no access to the JS
     * global scope (globalThis) and cannot reach process, require, or any other
     * Node.js runtime API.
     */
    private readonly _bootstrapPython = `
class _Logger:
    def info(self, msg):    _log_fn("info",    str(msg))
    def warning(self, msg): _log_fn("warning", str(msg))
    def error(self, msg):   _log_fn("error",   str(msg))
    def debug(self, msg):   _log_fn("debug",   str(msg))

class _Output:
    def __init__(self, name):
        self._name = name
    def emit(self, value):
        _emit_fn(self._name, value)

class _Outputs:
    def __getattr__(self, name):
        return _Output(name)

class _DataStore:
    async def get(self, key):
        return await _dataStore_retrieve_fn(key)
    async def set(self, key, value):
        await _dataStore_store_fn(key, value)

class _MessageBus:
    async def send_request(self, subject, msg, timeout=None):
        return await _messageBus_sendRequest_fn(subject, msg, timeout)
    def publish(self, subject, msg):
        _messageBus_publish_fn(subject, msg)

class _System:
    async def call(self, input):
        return await _system_call_fn(input)

class _Utils:
    async def sleep(self, ms):
        import asyncio
        await asyncio.sleep(ms / 1000)
    def stringify(self, value):
        return _utils_stringify_fn(value)
    def convert_value_to_type(self, value, to_type, default_value=None, throw_on_error=False):
        return _utils_convertValueToType_fn(value, to_type, default_value, throw_on_error)
    async def execute_with_retry(self, logger, attempts, sleep_between_attempts, code):
        return await _utils_executeWithRetry_fn(attempts, sleep_between_attempts, code)
    async def execute_with_system_error_retry(self, logger, attempts, sleep_between_attempts, code):
        return await _utils_executeWithSystemErrorRetry_fn(attempts, sleep_between_attempts, code)

class _Driver:
    async def connect(self):
        await _driver_connect_fn()
    async def disconnect(self):
        await _driver_disconnect_fn()
    async def execute_command(self, command, parameters, timeout=None):
        return await _driver_executeCommand_fn(command, parameters, timeout)
    async def get_properties(self, properties):
        return await _driver_getProperties_fn(properties)
    async def set_properties(self, properties_values):
        return await _driver_setProperties_fn(properties_values)
    async def send_raw(self, type, content, timeout=None):
        return await _driver_sendRaw_fn(type, content, timeout)
    async def notify_raw(self, type, content):
        await _driver_notifyRaw_fn(type, content)
    async def register_custom_driver_definitions(self, custom):
        await _driver_registerCustomDriverDefinitions_fn(custom)
`;

    /**
     * Python runner suffix — instantiates the user's Code class and calls main().
     * Appended after the user's code on every execution.
     */
    private readonly _runnerSuffix = `
_framework = {'logger': _Logger(), 'data_store': _DataStore(), 'message_bus': _MessageBus(), 'system': _System(), 'utils': _Utils(), 'lbos': _lbos}
if _driver_available:
    _framework['driver'] = _Driver()
_code_obj = Code(_framework)
_result = await _code_obj.main(_inputs, _Outputs())
_result
`;

    public async initialize(settings: PyodideSettings): Promise<void> {
        if (this._initialized) {
            this._logger.warning("PyodideManager already initialized — skipping.");
            return;
        }

        this._logger.debug("Initializing Pyodide runtime...");

        try {
            // eslint-disable-next-line @typescript-eslint/no-require-imports, @typescript-eslint/no-var-requires
            const { loadPyodide } = require("pyodide") as { loadPyodide: (options?: Record<string, unknown>) => Promise<PyodideRuntime> };
            this._pyodide = await loadPyodide();

            if (settings.packages?.length > 0) {
                this._logger.info(`Installing Python packages: ${settings.packages.join(", ")}`);
                await this._pyodide.loadPackage("micropip");
                const micropip = this._pyodide.pyimport("micropip");
                await micropip.install(settings.packages);
                this._logger.info("Python packages installed.");
            }

            // Replace `js` and `pyodide_js` with empty namespaces so user Python
            // code cannot access the Node.js global scope (globalThis) via either
            // `import js` or `import pyodide_js` (both map to globalThis in Pyodide).
            // This must happen AFTER micropip package installation because micropip's
            // compat layer imports from `pyodide_js` at module load time.
            await this._pyodide.runPythonAsync(`
import sys, types
_blocked = types.SimpleNamespace()
sys.modules['js'] = _blocked
sys.modules['pyodide_js'] = _blocked
`);

            this._initialized = true;
            this._logger.info("Pyodide runtime ready.");
        } catch (err) {
            this._logger.error(`Failed to initialize Pyodide: ${(err as Error).message}`);
            throw err;
        }
    }

    /**
     * Returns an async function that runs the given Python code against Pyodide.
     * Call once per task init and invoke on each activation.
     *
     * Bridge callables are injected into Python's global namespace via
     * pyodide.globals.set() — not onto globalThis — keeping the Python sandbox
     * isolated from Node.js runtime APIs.
     */
    public async compile(
        code: string
    ): Promise<(inputs: Record<string, unknown>, outputs: PyodideOutputsProxy) => Promise<unknown>> {
        if (!this._initialized || this._pyodide == null) {
            throw new Error("PyodideManager is not initialized. Call initialize() first.");
        }

        const pyodide = this._pyodide;

        const logger = this._logger;
        const dataStore = this._dataStore;
        const messageBus = this._messageBus;
        const systemApi = this._systemApi;
        const driver = this._driver;

        const isPyProxy = (v: unknown): boolean => v instanceof pyodide.ffi.PyProxy;
        const toJs = (v: unknown): unknown => isPyProxy(v) ? (v as any).toJs() : v;
        const bootstrapPython = this._bootstrapPython;
        const runnerSuffix = this._runnerSuffix;

        return async (inputs: Record<string, unknown>, outputs: PyodideOutputsProxy): Promise<unknown> => {

            pyodide.globals.set("_log_fn", (level: string, msg: string) => {
                if (level === "info") { logger.info(msg); }
                else if (level === "warning") { logger.warning(msg); }
                else if (level === "error") { logger.error(msg); }
                else if (level === "debug") { logger.debug(msg); }
            });

            pyodide.globals.set("_emit_fn", (name: string, value: unknown) => {
                const jsVal = toJs(value);
                if ((outputs as any)[name] != null) {
                    (outputs as any)[name].emit(jsVal);
                } else {
                    logger.warning("Python emitted to unknown output: " + name);
                }
            });

            pyodide.globals.set("_dataStore_retrieve_fn", async (key: string) =>
                // `undefined` (not `null`) so Pyodide converts a missing key to Python `None` —
                // JS `null` awaited from Python surfaces as `pyodide.ffi.JsNull`, not `None`.
                dataStore.retrieve(key, undefined)
            );

            pyodide.globals.set("_dataStore_store_fn", async (key: string, value: unknown) =>
                dataStore.store(key, value, DataStoreLocation.Temporary)
            );

            pyodide.globals.set("_messageBus_sendRequest_fn", async (subject: string, msg: unknown, timeout: number | null) =>
                messageBus.sendRequest(subject, toJs(msg), timeout ?? undefined)
            );

            pyodide.globals.set("_messageBus_publish_fn", (subject: string, msg: unknown) => {
                messageBus.publish(subject, toJs(msg));
            });

            pyodide.globals.set("_system_call_fn", async (input: unknown) =>
                systemApi.call(toJs(input) as any)
            );

            pyodide.globals.set("_utils_convertValueToType_fn", (value: unknown, toType: string, defaultValue: unknown, throwOnError: boolean) =>
                Utilities.convertValueToType(toJs(value), toType, defaultValue, throwOnError)
            );

            pyodide.globals.set("_utils_executeWithRetry_fn", async (attempts: number, sleepMs: number, codePyProxy: any) =>
                Utilities.ExecuteWithRetry(logger, attempts, sleepMs, async () => await codePyProxy())
            );

            pyodide.globals.set("_utils_executeWithSystemErrorRetry_fn", async (attempts: number, sleepMs: number, codePyProxy: any) =>
                Utilities.ExecuteWithSystemErrorRetry(logger, attempts, sleepMs, async () => await codePyProxy())
            );

            pyodide.globals.set("_utils_stringify_fn", (value: unknown) =>
                JSON.stringify(toJs(value))
            );

            pyodide.globals.set("_driver_available", driver != null);

            if (driver != null) {
                pyodide.globals.set("_driver_connect_fn", async () => driver.connect());
                pyodide.globals.set("_driver_disconnect_fn", async () => driver.disconnect());
                pyodide.globals.set("_driver_executeCommand_fn", async (command: unknown, parameters: unknown, timeout: number | null) => {
                    const jsParams = toJs(parameters);
                    return driver.executeCommand(
                        toJs(command) as any,
                        jsParams instanceof Map ? jsParams : new Map(Object.entries((jsParams as any) ?? {})),
                        timeout ?? undefined
                    );
                });
                pyodide.globals.set("_driver_getProperties_fn", async (properties: unknown) =>
                    driver.getProperties(toJs(properties) as any[])
                );
                pyodide.globals.set("_driver_setProperties_fn", async (propertiesValues: unknown) => {
                    const jsProps = toJs(propertiesValues);
                    return driver.setProperties(jsProps instanceof Map ? jsProps : new Map(Object.entries((jsProps as any) ?? {})));
                });
                pyodide.globals.set("_driver_sendRaw_fn", async (type: string, content: unknown, timeout: number | null) =>
                    driver.sendRaw(type, toJs(content), timeout ?? undefined)
                );
                pyodide.globals.set("_driver_notifyRaw_fn", async (type: string, content: unknown) =>
                    driver.notifyRaw(type, toJs(content))
                );
                pyodide.globals.set("_driver_registerCustomDriverDefinitions_fn", async (custom: unknown) =>
                    driver.registerCustomDriverDefinitions(toJs(custom) as any)
                );
            }

            pyodide.globals.set("_lbos", System.LBOS);
            pyodide.globals.set("_inputs", pyodide.toPy(inputs));

            const fullCode = bootstrapPython + "\n" + code + "\n" + runnerSuffix;
            const result = await pyodide.runPythonAsync(fullCode);

            return isPyProxy(result) ? (result as any).toJs() : result;
        };
    }

    public async destroy(): Promise<void> {
        this._pyodide = null;
        this._initialized = false;
        this._logger.info("PyodideManager destroyed.");
    }
}
