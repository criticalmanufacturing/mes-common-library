import { System } from "@criticalmanufacturing/connect-iot-controller-engine";

export interface RoslynOutputEmission {
    name: string;
    value: unknown;
}

export interface RoslynExecutionResponse {
    result: unknown;
    outputs: RoslynOutputEmission[];
    error?: string;
}

export interface RoslynExecutionContext {
    invoke(method: string, bridgeArguments: Record<string, unknown>): Promise<unknown>;
}

export interface RoslynExecutionManagerOptions {
    /** Keep a pool of warm host processes and reuse them across executions instead of spawning one per call. */
    warmPoolEnabled: boolean;
    /** Number of warm host processes to keep alive when {@link warmPoolEnabled} is true. */
    warmPoolSize: number;
}

export interface RoslynExecutionManager {
    initialize(pageName: string, taskId: string, options?: RoslynExecutionManagerOptions): Promise<void>;
    execute(assemblyBase64: string, inputs: Record<string, unknown>, timeoutMs: number, context?: RoslynExecutionContext): Promise<RoslynExecutionResponse>;
}

/** Version of the browser compiler/catalog contracts accepted by the execution host. */
export const ROSLYN_CATALOG_VERSION = "1";
export const ROSLYN_EXECUTION_MANAGER_SYMBOL = Symbol("RoslynExecutionManager");