import { injectable } from "inversify";
import { RoslynExecutionManager, RoslynExecutionManagerOptions, RoslynExecutionResponse } from "./roslynExecutionManager";
import { System } from "@criticalmanufacturing/connect-iot-controller-engine";

/** Execution occurs only in the controller Node runtime. */
@injectable()
export class RoslynExecutionManagerBrowser implements RoslynExecutionManager {
    public async initialize(pageName: string, taskId: string, options?: RoslynExecutionManagerOptions): Promise<void> { }

    public async execute(_assemblyBase64: string, _inputs: Record<string, unknown>, _timeoutMs: number): Promise<RoslynExecutionResponse> {
        throw new Error("C# Roslyn Code execution is only available on the controller runtime.");
    }
}