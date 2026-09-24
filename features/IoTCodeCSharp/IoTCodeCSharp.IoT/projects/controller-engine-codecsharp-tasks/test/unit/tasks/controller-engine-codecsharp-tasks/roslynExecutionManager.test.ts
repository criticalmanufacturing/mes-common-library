import "reflect-metadata";
import * as chai from "chai";
import { Container } from "inversify";
import { System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import { LoggerMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/logger.mock";

import { RoslynExecutionManagerHandler } from "../../../../src/lib/tasks/c-sharp-roslyn-code/roslyn/roslynExecutionManagerHandler";
import { RoslynExecutionContext } from "../../../../src/lib/tasks/c-sharp-roslyn-code/roslyn/roslynExecutionManager";

// eslint-disable-next-line @typescript-eslint/no-var-requires
const fs = require("fs") as { readFileSync(path: string): { toString(encoding: string): string } };
// eslint-disable-next-line @typescript-eslint/no-var-requires
const path = require("path") as { join(...parts: string[]): string };
// eslint-disable-next-line @typescript-eslint/no-var-requires
const process = require("process") as { cwd(): string };

const projectRoot = process.cwd();
const hostAssembly = path.join(projectRoot, "roslyn-host", "publish", "RoslynCode.Host.dll");
const sampleAssembly = path.join(projectRoot, "roslyn-host", "RoslynCode.Sample", "bin", "Release", "net8.0", "RoslynCode.Sample.dll");

// Mirrors how a task's real DI container binds these tokens for a `Local`-scope provider
// (see taskContainer.controlFlow.js): Task.Definition/PageName identify the owning task node,
// Dependencies.Injector is the container itself, and Dependencies.Logger is the task's logger.
function createManagerContainer(taskId: string, pageName: string): Container {
    const container = new Container();
    container.bind(TYPES.Dependencies.Logger).to(LoggerMock).inSingletonScope();
    container.bind(TYPES.Task.Definition).toConstantValue({ id: taskId });
    container.bind(TYPES.Task.PageName).toConstantValue(pageName);
    container.bind(TYPES.Dependencies.Injector).toConstantValue(container);
    container.bind(RoslynExecutionManagerHandler).toSelf();
    return container;
}

describe("RoslynExecutionManager integration", function () {
    this.timeout(30_000);

    function createManager(): RoslynExecutionManagerHandler {
        const manager = createManagerContainer("test-task", "TestPage").get(RoslynExecutionManagerHandler);
        const instance = manager as unknown as Record<string, unknown>;
        instance["_initialized"] = true;
        instance["_cacheDirectory"] = path.join(projectRoot, ".roslyn-code-cache-test");
        instance["_hostAssemblyPath"] = hostAssembly;
        instance["_pageName"] = "TestPage";
        instance["_taskId"] = "test-task";
        return manager;
    }

    it("should find the execution host using the default path resolution", async () => {
        const manager = createManagerContainer("test-task", "TestPage").get(RoslynExecutionManagerHandler);

        await manager.initialize("TestPage", "test-task");

        const instance = manager as unknown as Record<string, unknown>;
        chai.expect(instance["_hostAssemblyPath"]).to.equal(hostAssembly);
    });

    it("should execute a persisted assembly and return results and emitted outputs", async () => {
        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");

        const response = await createManager().execute(assemblyBase64, { value: 21 }, 10_000);

        chai.expect(response.result).to.deep.equal({ doubledFromResult: 42 });
        chai.expect(response.outputs).to.deep.equal([{ name: "doubledFromEmit", value: 42 }]);
    });

    it("should execute the same persisted assembly repeatedly", async () => {
        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");
        const manager = createManager();

        for (const value of [0, 1, 9]) {
            const response = await manager.execute(assemblyBase64, { value }, 10_000);
            chai.expect(response.result).to.deep.equal({ doubledFromResult: value * 2 });
        }
    });

    it("should route framework calls through the controller bridge", async () => {
        const calls: string[] = [];
        const values = new Map<string, unknown>();
        const bridge: RoslynExecutionContext = {
            invoke: async (method, bridgeArguments) => {
                calls.push(method);
                switch (method) {
                    case "logDebug": return null;
                    case "dataStoreSet": values.set(String(bridgeArguments["key"]), bridgeArguments["value"]); return null;
                    case "dataStoreGet": return values.get(String(bridgeArguments["key"]));
                    case "messageBusPublish": return null;
                    // Only real BaseOutput properties (e.g. "message") survive the typed Call<BaseOutput> deserialization.
                    case "systemCall": return { accepted: true, message: "ok" };
                    default: throw new Error(`Unexpected bridge operation: ${method}`);
                }
            }
        };

        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");
        const response = await createManager().execute(assemblyBase64, { value: 21, bridge: true }, 10_000, bridge);

        chai.expect(calls).to.deep.equal(["logDebug", "dataStoreSet", "dataStoreGet", "messageBusPublish", "systemCall"]);
        chai.expect(response.result).to.deep.equal({ stored: 21, system: { message: "ok" } });
    });

    it("should forward a genuine LBO object built from Cmf.LightBusinessObjects.dll through the systemCall bridge", async () => {
        let capturedRequest: Record<string, unknown> | null = null;
        const bridge: RoslynExecutionContext = {
            invoke: async (method, bridgeArguments) => {
                if (method !== "systemCall") { throw new Error(`Unexpected bridge operation: ${method}`); }
                capturedRequest = bridgeArguments["input"] as Record<string, unknown>;
                return { container: { capacityForm: "Pallet", holdCount: 3 } };
            }
        };

        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");
        const response = await createManager().execute(assemblyBase64, { value: 0, lbo: true }, 10_000, bridge);

        // The .NET Code creates a Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput,
        // populated with a real Cmf.Navigo.BusinessObjects.Container from Cmf.LightBusinessObjects.dll, and calls
        // _framework.System.Call<EmptyContainerOutput>(lboInput). The request keeps the LBO's original PascalCase
        // property names (defaults omitted) plus the "$type" discriminator the controller resolves the LBO from;
        // the sample Code re-serializes the typed response with its own camelCase settings before returning it.
        chai.expect(capturedRequest?.["$type"]).to.equal(
            "Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput, Cmf.Navigo.BusinessOrchestration");
        const container = capturedRequest?.["Container"] as Record<string, unknown>;
        chai.expect(container).to.exist;
        chai.expect(container["CapacityForm"]).to.equal("Pallet");
        chai.expect(container["HoldCount"]).to.equal(3);
        chai.expect(response.result).to.deep.equal({ systemLbo: { container: { capacityForm: "Pallet", holdCount: 3 } } });
    });

    it("should route utility and driver calls through the controller bridge", async () => {
        const calls: string[] = [];
        const bridge: RoslynExecutionContext = {
            invoke: async (method) => {
                calls.push(method);
                switch (method) {
                    case "utilsConvertValueToType": return 21;
                    case "driverAvailable": return true;
                    case "driverExecuteCommand": return { completed: true };
                    case "driverConnect":
                    case "driverGetProperties":
                    case "driverSetProperties":
                    case "driverSendRaw":
                    case "driverNotifyRaw":
                    case "driverRegisterCustomDriverDefinitions":
                    case "driverDisconnect": return null;
                    default: throw new Error(`Unexpected bridge operation: ${method}`);
                }
            }
        };

        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");
        const response = await createManager().execute(assemblyBase64, { value: 21, driver: true }, 10_000, bridge);

        chai.expect(calls).to.deep.equal([
            "utilsConvertValueToType", "driverAvailable", "driverConnect", "driverExecuteCommand", "driverGetProperties",
            "driverSetProperties", "driverSendRaw", "driverNotifyRaw", "driverRegisterCustomDriverDefinitions", "driverDisconnect"
        ]);
        chai.expect(response.result).to.deep.equal({ converted: 21, command: { completed: true } });
    });

    it("should reject an absent persisted assembly", async () => {
        let error: Error | null = null;
        try {
            await createManager().execute("", {}, 10_000);
        } catch (caught) {
            error = caught as Error;
        }

        chai.expect(error?.message).to.equal("No browser-compiled C# assembly was saved for this task.");
    });
});

describe("RoslynExecutionManager warm pool", function () {
    this.timeout(30_000);

    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const childProcess = require("child_process") as { spawn: (...args: unknown[]) => unknown };
    const originalSpawn = childProcess.spawn;
    let spawnCount = 0;

    beforeEach(() => {
        spawnCount = 0;
        childProcess.spawn = (...args: unknown[]) => {
            spawnCount++;
            return originalSpawn(...args);
        };
    });

    afterEach(() => {
        childProcess.spawn = originalSpawn;
    });

    async function createWarmManager(poolSize: number): Promise<RoslynExecutionManagerHandler> {
        const manager = createManagerContainer("test-task", "TestPage").get(RoslynExecutionManagerHandler);
        await manager.initialize("TestPage", "test-task", { warmPoolEnabled: true, warmPoolSize: poolSize });
        return manager;
    }

    it("reuses the same warm processes across many executions instead of spawning one per call", async () => {
        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");
        const manager = await createWarmManager(2);
        // +1 accounts for the `dotnet --version` preflight check `initialize()` also runs.
        const spawnCountAfterWarmup = spawnCount;
        chai.expect(spawnCountAfterWarmup).to.equal(3);

        for (const value of [0, 1, 2, 3, 4, 5]) {
            const response = await manager.execute(assemblyBase64, { value }, 10_000);
            chai.expect(response.result).to.deep.equal({ doubledFromResult: value * 2 });
        }

        chai.expect(spawnCount).to.equal(spawnCountAfterWarmup);
    });

    it("survives a per-request execution error without killing or replacing the warm worker", async () => {
        const assemblyBase64 = fs.readFileSync(sampleAssembly).toString("base64");
        const manager = await createWarmManager(1);
        const spawnCountAfterWarmup = spawnCount;

        let error: Error | null = null;
        try {
            // Not a valid .NET assembly - the host must report this as a per-request error
            // without exiting, so the warm worker survives for the next call.
            await manager.execute(Buffer.from("not a real dll").toString("base64"), {}, 10_000);
        } catch (caught) {
            error = caught as Error;
        }
        chai.expect(error).to.not.be.null;

        const response = await manager.execute(assemblyBase64, { value: 10 }, 10_000);
        chai.expect(response.result).to.deep.equal({ doubledFromResult: 20 });
        chai.expect(spawnCount).to.equal(spawnCountAfterWarmup);
    });
});