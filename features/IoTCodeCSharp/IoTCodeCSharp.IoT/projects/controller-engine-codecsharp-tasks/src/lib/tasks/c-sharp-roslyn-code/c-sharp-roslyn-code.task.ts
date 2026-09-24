import { Task, System, Dependencies, Communication, DI, TYPES, Utilities, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import { ROSLYN_CATALOG_VERSION, ROSLYN_EXECUTION_MANAGER_SYMBOL, RoslynExecutionContext, RoslynExecutionManager } from "./roslyn/roslynExecutionManager";

export const ROSLYN_AUTO_IN = "autoIn";
export const ROSLYN_AUTO_OUT = "autoOut";

export interface CSharpRoslynCodeSettings extends System.TaskDefaultSettings {
    csCode: string[];
    csCodeBase64: string;
    roslynAssemblyBase64: string;
    roslynSourceHash: string;
    roslynCatalogVersion: string;
    inputs: Task.TaskInput[];
    outputs: Task.TaskOutput[];
    contextExpirationInMilliseconds: number;
    executionTimeoutMs: number;
    warmPoolEnabled: boolean;
    warmPoolSize: number;
}

export const ROSLYN_SETTINGS_DEFAULTS: CSharpRoslynCodeSettings = {
    csCode: [],
    csCodeBase64: "",
    roslynAssemblyBase64: "",
    roslynSourceHash: "",
    roslynCatalogVersion: ROSLYN_CATALOG_VERSION,
    inputs: [],
    outputs: [],
    contextExpirationInMilliseconds: 10000,
    executionTimeoutMs: 0,
    warmPoolEnabled: false,
    warmPoolSize: 3
};

interface ExecutionContext {
    expirationTimeout: ReturnType<typeof setTimeout>;
    values: Record<string, unknown>;
}

@Task.Task()
export class CSharpRoslynCodeTask extends TaskBase implements CSharpRoslynCodeSettings {
    @DI.Inject(ROSLYN_EXECUTION_MANAGER_SYMBOL)
    private _executionManager: RoslynExecutionManager;

    private readonly _contexts = new Map<string, ExecutionContext>();
    private _ready = false;

    csCode: string[];
    csCodeBase64: string;
    roslynAssemblyBase64: string;
    roslynSourceHash: string;
    roslynCatalogVersion: string;
    inputs: Task.TaskInput[];
    outputs: Task.TaskOutput[];
    contextExpirationInMilliseconds: number;
    executionTimeoutMs: number;
    warmPoolEnabled: boolean;
    warmPoolSize: number;

    override async onBeforeInit(): Promise<void> {
        for (const output of this.outputs ?? []) {
            this[Utilities.propertyToOutput(output.name)] = new Task.Output<unknown>();
        }

        if (!this.csCodeBase64 || !this.roslynAssemblyBase64 || !this.roslynSourceHash) {
            this._logger?.error("C# Roslyn Code has no browser-compiled artifact. Open and save the task in the designer.");
            return;
        }
        if (this.roslynCatalogVersion !== ROSLYN_CATALOG_VERSION) {
            this._logger?.error("C# Roslyn Code was compiled with an incompatible reference catalog. Open and save the task in the designer.");
            return;
        }

        try {
            const page = this._container.get<string>(TYPES.Task.PageName);
            const taskId = this._logger["_sourceId"] as string ?? "";

            await this._executionManager.initialize(page, taskId, { warmPoolEnabled: this.warmPoolEnabled, warmPoolSize: this.warmPoolSize });
            this._ready = true;
        } catch (error) {
            this._logger?.error(`Failed to initialize the C# Roslyn execution host: ${(error as Error).message}`);
        }
    }

    override async onChanges(changes: Task.Changes): Promise<void> {
        const contextName = this._executionContext.name;
        let context = this._contexts.get(contextName);
        if (context == null) {
            context = { expirationTimeout: null, values: {} };
            this._contexts.set(contextName, context);
        }
        if (context.expirationTimeout != null) {
            clearTimeout(context.expirationTimeout);
            context.expirationTimeout = null;
        }

        for (const propertyName in changes) {
            if (this._isInput(propertyName)) {
                context.values[Utilities.inputToProperty(propertyName)] = changes[propertyName].currentValue;
                this[propertyName] = undefined;
            }
        }

        if (!changes["activate"]) {
            if (this.contextExpirationInMilliseconds > 0) {
                context.expirationTimeout = setTimeout(() => this._contexts.delete(contextName), this.contextExpirationInMilliseconds);
            }
            return;
        }

        this.activate = undefined;
        this._contexts.delete(contextName);
        const currentContext = this._executionContext.current;
        try {
            if (!this._ready) {
                throw new Error("C# Roslyn Code is not ready. Open and save the task in the designer, then restart the controller.");
            }
            for (const input of this.inputs ?? []) {
                if (context.values[input.name] == null && input.defaultValue != null) {
                    context.values[input.name] = input.defaultValue;
                }
            }

            const response = await this._executionManager.execute(this.roslynAssemblyBase64, context.values, this.executionTimeoutMs, this._frameworkContext(currentContext));
            currentContext.run(() => {
                for (const output of response.outputs ?? []) {
                    this._emitOutput(output.name, output.value);
                }
                if (response.result != null && typeof response.result === "object") {
                    for (const [name, value] of Object.entries(response.result as Record<string, unknown>)) {
                        this._emitOutput(name, value);
                    }
                }
                this.success.emit(true);
            });
        } catch (error) {
            currentContext.run(() => this.logAndEmitError((error as Error).message));
        }
    }

    private _isInput(propertyName: string): boolean {
        const name = Utilities.inputToProperty(propertyName);
        return propertyName !== "activate" && name !== "" && (this.inputs ?? []).some(input => input.name === name);
    }

    private _emitOutput(name: string, value: unknown): void {
        const output = this[Utilities.propertyToOutput(name)] as Task.Output<unknown>;
        if (output != null && typeof output.emit === "function") {
            output.emit(value);
        } else {
            this._logger.warning(`C# Roslyn Code emitted to unknown output: ${name}`);
        }
    }

    private _frameworkContext(currentContext: Dependencies.ExecutionContext): RoslynExecutionContext {
        return {
            // Bridge calls arrive from a raw child_process stdout event, outside any zone, so the
            // ambient execution context must be explicitly restored here (mirroring onChanges' own
            // currentContext.run for its response handling) - otherwise, whichever task's zone is
            // merely "current" on the event loop at that moment gets tagged in the log output. With
            // execution now this fast (see the warm pool), two concurrently-activated tasks' bridge
            // calls land close enough together that this reliably swaps their log attribution.
            invoke: (method, bridgeArguments) => currentContext.run(async () => {
                switch (method) {
                    case "logInfo": this._logger.info(String(bridgeArguments["message"])); return null;
                    case "logWarning": this._logger.warning(String(bridgeArguments["message"])); return null;
                    case "logError": this._logger.error(String(bridgeArguments["message"])); return null;
                    case "logDebug": this._logger.debug(String(bridgeArguments["message"])); return null;
                    case "dataStoreGet": return await this._dataStore.retrieve(String(bridgeArguments["key"]), null);
                    case "dataStoreSet": await this._dataStore.store(String(bridgeArguments["key"]), bridgeArguments["value"], System.DataStoreLocation.Temporary); return null;
                    case "messageBusSendRequest": return await this._messageBus.sendRequest(String(bridgeArguments["subject"]), bridgeArguments["message"], this._optionalTimeout(bridgeArguments["timeoutMs"]));
                    case "messageBusPublish": this._messageBus.publish(String(bridgeArguments["subject"]), bridgeArguments["message"]); return null;
                    case "systemCall":
                        // eslint-disable-next-line @typescript-eslint/no-explicit-any
                        return await this._systemAPI.call(this._toLboInput(bridgeArguments["input"]) as any);
                    case "utilsConvertValueToType":
                        return Utilities.convertValueToType(bridgeArguments["value"], String(bridgeArguments["toType"]), bridgeArguments["defaultValue"], Boolean(bridgeArguments["throwOnError"]));
                    case "driverAvailable": return this._driverProxy != null;
                    case "driverConnect": await this._driverProxy.connect(); return null;
                    case "driverDisconnect": await this._driverProxy.disconnect(); return null;
                    case "driverExecuteCommand": return await this._driverProxy.executeCommand(
                        bridgeArguments["command"] as any,
                        this._asMap(bridgeArguments["parameters"]),
                        this._optionalTimeout(bridgeArguments["timeoutMs"])
                    );
                    case "driverGetProperties": return await this._driverProxy.getProperties(bridgeArguments["properties"] as any[]);
                    // The JSON bridge cannot preserve AutomationProperty map keys.
                    // eslint-disable-next-line @typescript-eslint/no-explicit-any
                    case "driverSetProperties": return await this._driverProxy.setProperties(this._asMap(bridgeArguments["propertiesValues"]) as any);
                    case "driverSendRaw": return await this._driverProxy.sendRaw(String(bridgeArguments["type"]), bridgeArguments["content"], this._optionalTimeout(bridgeArguments["timeoutMs"]));
                    case "driverNotifyRaw": await this._driverProxy.notifyRaw(String(bridgeArguments["type"]), bridgeArguments["content"]); return null;
                    case "driverRegisterCustomDriverDefinitions": await this._driverProxy.registerCustomDriverDefinitions(bridgeArguments["custom"] as any); return null;
                    default: throw new Error(`Unsupported C# Roslyn framework operation: ${method}`);
                }
            })
        };
    }

    // The Roslyn host only ever hands back a plain object parsed from JSON, whose "$type" names the
    // LBO input class it stands for but whose `constructor` is just `Object`. SystemAPI.call resolves
    // the actual HTTP method/URL for the request from static metadata (_CMFInternal_HTTPMethod/
    // _CMFInternal_URLSuffix) on `input.constructor`, so a plain object always resolves both as
    // undefined and the request goes out to "/undefined" - hence it needs to become a real instance
    // of the matching cmf-lbos class first, the same way execute-service-call's task resolves one from
    // an LBO's full type name.
    private _toLboInput(rawInput: unknown): System.LBOS.Cmf.Foundation.BusinessOrchestration.BaseInput {
        if (rawInput == null || typeof rawInput !== "object") {
            throw new Error("C# Roslyn Code system call requires an input object.");
        }
        const typeDiscriminator = (rawInput as Record<string, unknown>)["$type"];
        if (typeof typeDiscriminator !== "string") {
            throw new Error("C# Roslyn Code system call input is missing a '$type' discriminator.");
        }
        const fullNamespace = typeDiscriminator.split(",")[0].trim();
        const ctor = this._resolveLboConstructor(fullNamespace);
        if (ctor == null) {
            throw new Error(`C# Roslyn Code system call: unknown LBO input type '${fullNamespace}'.`);
        }
        return new ctor(rawInput);
    }

    private _resolveLboConstructor(fullNamespace: string): (new (props?: unknown) => System.LBOS.Cmf.Foundation.BusinessOrchestration.BaseInput) | null {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        let node: any = System.LBOS.Cmf;
        for (const segment of fullNamespace.split(".")) {
            if (segment === "Cmf") {
                continue;
            }
            node = node?.[segment];
            if (node == null) {
                return null;
            }
        }
        return typeof node === "function" ? node : null;
    }

    private _optionalTimeout(value: unknown): number | undefined {
        return value == null ? undefined : Number(value);
    }

    private _asMap(value: unknown): Map<string, unknown> {
        return value instanceof Map ? value : new Map(Object.entries((value as Record<string, unknown>) ?? {}));
    }
}