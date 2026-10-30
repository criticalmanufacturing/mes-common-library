import { Task, System, DI, Utilities, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import { PyodideManagerHandler } from "./pyodide/pyodideManagerHandler";
import { PYODIDE_MANAGER_SYMBOL, PyodideOutputsProxy } from "./pyodide/pyodideManager";
import { PyodideSettings } from "./pyodide/pyodideConfig";
import { decode } from "./utilities/utilities";

/** Auto input port key */
export const AUTO_IN: string = "autoIn";
/** Auto output port key */
export const AUTO_OUT: string = "autoOut";

/** Default values for settings */
export const SETTINGS_DEFAULTS: PythonCodeSettings = {
    pyCode: [],
    pyCodeBase64: "",
    inputs: [],
    outputs: [],
    packages: [],
    contextExpirationInMilliseconds: 10000,
    executionTimeoutMs: 0
};


/**
 * @whatItDoes
 * This task allows writing custom Python code using the GUI and executing it in a sandbox.
 * It uses Pyodide (Python compiled to WebAssembly) for execution.
 *
 * @howToUse
 * Add the needed inputs and outputs to the task.
 * Write the Python code using the code editor in settings.
 * The code must define a class `Code` with a `main(self, inputs, outputs)` method.
 * 'Activate' triggers the execution of the code with the values of inputs
 * received within the same execution context.
 *
 * ### Python code convention
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
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 *
 * ### Outputs
 * * `bool`  : **success** - Triggered when the task executes successfully
 * * `Error` : **error**   - Triggered when the task fails
 *
 * ### Settings
 * See {@see PythonCodeSettings}
 */
@Task.Task()
export class PythonCodeTask extends TaskBase implements PythonCodeSettings {

    private _syncContexts: Map<string, PythonCodeTaskExecutionContext> = new Map();
    private _sandboxedCode: ((inputs: Record<string, unknown>, outputs: PyodideOutputsProxy) => Promise<unknown>) | null = null;

    @DI.Inject(PYODIDE_MANAGER_SYMBOL)
    private _pyodideManager: PyodideManagerHandler;

    /** Settings */
    inputs: Task.TaskInput[];
    outputs: Task.TaskOutput[];
    pyCode: string[];
    pyCodeBase64: string;
    packages: string[];
    contextExpirationInMilliseconds: number;
    executionTimeoutMs: number;

    private isInput(inputName: string): boolean {
        const inputNameWithoutSuffix = Utilities.inputToProperty(inputName);
        return inputName !== "activate" && inputNameWithoutSuffix !== "" && this.inputs.some(input => input.name === inputNameWithoutSuffix);
    }

    private isOutput(outputName: string): boolean {
        const outputNameWithSuffix = Utilities.propertyToOutput(outputName);
        const output: Task.Output<unknown> = this[outputNameWithSuffix];
        return output != null && typeof output.emit === "function";
    }

    override async onChanges(changes: Task.Changes): Promise<void> {
        if (!this._syncContexts.has(this._executionContext.name)) {
            this._syncContexts.set(this._executionContext.name, {
                name: this._executionContext.name,
                expirationTimeout: null,
                values: {}
            });
        }
        const syncContext = this._syncContexts.get(this._executionContext.name);

        if (syncContext.expirationTimeout != null) {
            clearTimeout(syncContext.expirationTimeout);
            syncContext.expirationTimeout = null;
        }

        for (const key in changes) {
            if (this.isInput(key)) {
                const propertyName = Utilities.inputToProperty(key);
                const previousValue = syncContext.values[propertyName];
                if (previousValue != null) {
                    this._logger.debug(`Property '${propertyName}' was already defined for context '${syncContext.name}'. Value lost: ${previousValue}`);
                }
                syncContext.values[propertyName] = changes[key].currentValue;
                this[key] = undefined;
            }
        }

        if (changes["activate"]) {
            this.activate = undefined;
            const currentContext = this._executionContext.current;

            this._syncContexts.delete(syncContext.name);

            try {
                // Build runtime output emitters that preserve the zone context,
                // same as the TS code-execution task
                const runtimeOutputs: PyodideOutputsProxy = {};
                for (const output of this.outputs) {
                    runtimeOutputs[output.name] = {
                        emit: (value: unknown) => {
                            currentContext.run(() => {
                                this[Utilities.propertyToOutput(output.name)].emit(value);
                            });
                        }
                    };
                }

                for (const input of this.inputs) {
                    if (syncContext.values[input.name] == null && input.defaultValue != null) {
                        this._logger.debug(`Setting input '${input.name}' to default: '${input.defaultValue}'.`);
                        syncContext.values[input.name] = input.defaultValue;
                    }
                }

                if (this._sandboxedCode == null) {
                    throw new Error("No Python code defined");
                }

                let result: unknown;
                try {
                    result = await this._sandboxedCode(syncContext.values, runtimeOutputs);
                } catch (err) {
                    const error = err as Error;
                    this._logger.error(`Failed to execute Python code: ${error.message}`);
                    this._logger.error(`Stack Trace: ${error.stack ?? "No Stack"}`);
                    throw err;
                }

                // Re-enter zone after async execution — sandbox destroys the zone context
                currentContext.run(() => {
                    if (result != null && typeof result === 'object') {
                        for (const key in result as Record<string, unknown>) {
                            if (this.isOutput(key)) {
                                const output: Task.Output<unknown> = this[Utilities.propertyToOutput(key)];
                                output.emit((result as Record<string, unknown>)[key]);
                            }
                        }
                    }
                    this.success.emit(true);
                });
            } catch (err) {
                currentContext.run(() => {
                    this.logAndEmitError((err as Error).message)
                });
            }
        } else {
            if (this.contextExpirationInMilliseconds > 0) {
                syncContext.expirationTimeout = setTimeout(() => {
                    this._syncContexts.delete(syncContext.name);
                    this._logger.warning(`Execution context '${syncContext.name}' expired. Lost inputs: ${Object.keys(syncContext.values)}`);
                }, this.contextExpirationInMilliseconds);
            }
        }
    }

    override async onBeforeInit(): Promise<void> {
        if (this.outputs?.length > 0) {
            for (const output of this.outputs) {
                this[Utilities.propertyToOutput(output.name)] = new Task.Output<unknown>();
            }
        }
    }

    override async onInit(): Promise<void> {
        if (!this.pyCodeBase64) {
            return;
        }

        const pythonCode = decode(this.pyCodeBase64);

        try {
            const settings: PyodideSettings = {
                packages: this.packages ?? [],
                executionTimeoutMs: this.executionTimeoutMs
            };

            // Initialize the shared Pyodide runtime (no-op if already initialized by another task)
            await this._pyodideManager.initialize(settings);

            // Compile the Python code into a sandboxed NodeVM function — store it for reuse,
            // same as _sandBoxedCode in the TS code-execution task
            this._sandboxedCode = await this._pyodideManager.compile(pythonCode);
        } catch (err) {
            this._logger.error(`Failed to compile Python script: ${(err as Error).message}`);
        }
    }

    override async onDestroy(): Promise<void> { }
}

// Add settings here
/** PythonCode Settings object */
export interface PythonCodeSettings extends System.TaskDefaultSettings {
    /** Python source code lines (for display/editing in the designer) */
    pyCode: string[];
    /** Python source code encoded in base64 (used at runtime) */
    pyCodeBase64: string;
    /** Python packages to install via micropip (e.g. ["numpy", "pandas"]) */
    packages: string[];
    /** Time in ms an execution context is kept without updates before being discarded */
    contextExpirationInMilliseconds: number;
    /** Maximum time in ms the Python code may run before a timeout error (0 = disabled) */
    executionTimeoutMs: number;
}

/**
 * Holds accumulated input values for a given execution context
 */
export interface PythonCodeTaskExecutionContext {
    expirationTimeout: ReturnType<typeof setTimeout>;
    name: string;
    values: { [key: string]: unknown };
}