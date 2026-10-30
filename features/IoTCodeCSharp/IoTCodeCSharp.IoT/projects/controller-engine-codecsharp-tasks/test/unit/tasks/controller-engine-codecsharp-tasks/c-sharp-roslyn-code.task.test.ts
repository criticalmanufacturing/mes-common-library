import "reflect-metadata";
import { System, Task, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import * as chai from "chai";
import type { interfaces } from "inversify";

import {
    CSharpRoslynCodeSettings,
    CSharpRoslynCodeTask,
    ROSLYN_SETTINGS_DEFAULTS
} from "../../../../src/lib/tasks/c-sharp-roslyn-code/c-sharp-roslyn-code.task";
import { ROSLYN_EXECUTION_MANAGER_SYMBOL, RoslynExecutionContext, RoslynExecutionResponse } from "../../../../src/lib/tasks/c-sharp-roslyn-code/roslyn/roslynExecutionManager";

const ANY_TYPE: Task.TaskComplexValueType = { type: Task.TaskValueType.Object };
const STRING_TYPE: Task.TaskComplexValueType = { type: Task.TaskValueType.String };
type Execute = (inputs: Record<string, unknown>, context?: RoslynExecutionContext) => Promise<RoslynExecutionResponse>;
type ExtraLink = { sourceId: string; outputName: string; targetId: string; inputName: string };

const MINIMAL_CSHARP_WITH_FRAMEWORK = [
    "using System.Text.Json.Nodes;",
    "using System.Threading.Tasks;",
    "using RoslynCode.Contracts;",
    "",
    "public sealed class Code",
    "{",
    "    private readonly Framework _framework;",
    "",
    "    public Code(Framework framework) => _framework = framework;",
    "",
    "    public Task<JsonObject?> Main(JsonObject inputs, Outputs outputs)",
    "    {",
    "        _framework.Logger.Debug(\"Hello from Roslyn C#\");",
    "        outputs.Emit(\"message\", JsonValue.Create(\"Hello World\"));",
    "        return Task.FromResult<JsonObject?>(null);",
    "    }",
    "}"
].join("\n");

const INTEGRATION_CSHARP = [
    "using System.Text.Json.Nodes;",
    "using System.Threading.Tasks;",
    "using RoslynCode.Contracts;",
    "",
    "public sealed class Code",
    "{",
    "    private readonly Framework _framework;",
    "    public Code(Framework framework) => _framework = framework;",
    "",
    "    public async Task<JsonObject?> Main(JsonObject inputs, Outputs outputs)",
    "    {",
    "        var value = inputs[\"value\"]?.GetValue<int>() ?? 0;",
    "        _framework.Logger.Debug(\"hello from Roslyn C#\");",
    "        await _framework.DataStore.Set(\"stored-value\", JsonValue.Create(value));",
    "        var storedValue = await _framework.DataStore.Get(\"stored-value\");",
    "        outputs.Emit(\"out\", storedValue?.DeepClone());",
    "        return new JsonObject { [\"doubled\"] = value * 2 };",
    "    }",
    "}"
].join("\n");

const LBO_SYSTEM_CALL_CSHARP = [
    "using System.Text.Json.Nodes;",
    "using System.Threading.Tasks;",
    "using RoslynCode.Contracts;",
    "using Cmf.Navigo.BusinessObjects;",
    "using Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects;",
    "using Cmf.Navigo.BusinessOrchestration.ContainerManagement.OutputObjects;",
    "",
    "public sealed class Code",
    "{",
    "    private readonly Framework _framework;",
    "    public Code(Framework framework) => _framework = framework;",
    "",
    "    public async Task<JsonObject?> Main(JsonObject inputs, Outputs outputs)",
    "    {",
    "        var container = new Container { CapacityForm = \"Pallet\", HoldCount = 3 };",
    "        var lboInput = new EmptyContainerInput { Container = container };",
    "        var systemLbo = await _framework.System.Call<EmptyContainerOutput>(lboInput);",
    "        return new JsonObject { [\"systemLbo\"] = systemLbo?.Container?.CapacityForm };",
    "    }",
    "}"
].join("\n");


function artifactSettings(settings: Partial<CSharpRoslynCodeSettings>): CSharpRoslynCodeSettings {
    return {
        ...ROSLYN_SETTINGS_DEFAULTS,
        csCodeBase64: btoa(MINIMAL_CSHARP_WITH_FRAMEWORK),
        roslynAssemblyBase64: "fixture-assembly",
        roslynSourceHash: "fixture-hash",
        ...settings
    };
}

function createTaskTest(
    settings: Partial<CSharpRoslynCodeSettings>,
    execute: Execute,
    trigger: (outputs: Map<string, Task.Output<unknown>>) => void,
    validate: (changes: Task.Changes) => void,
    extraLinks: ExtraLink[] = [],
    configureContainer?: (container: interfaces.Container) => void
): void {
    EngineTestSuite.createTasks(
        [
            { class: CSharpRoslynCodeTask, id: "0", settings: artifactSettings(settings) },
            {
                id: "1",
                class: Task.Task({ name: "mockTask" })(
                    class MockTask implements Task.TaskInstance {
                        [key: string]: unknown;
                        private readonly _outputs = new Map<string, Task.Output<unknown>>();

                        async onBeforeInit(): Promise<void> {
                            this["activate"] = new Task.Output<unknown>();
                            this._outputs.set("activate", this["activate"] as Task.Output<unknown>);
                            for (const link of extraLinks.filter(item => item.sourceId === "1")) {
                                if (!this._outputs.has(link.outputName)) {
                                    this[link.outputName] = new Task.Output<unknown>();
                                    this._outputs.set(link.outputName, this[link.outputName] as Task.Output<unknown>);
                                }
                            }
                        }

                        async onInit(): Promise<void> { trigger(this._outputs); }
                        async onChanges(changes: Task.Changes): Promise<void> { validate(changes); }
                    }
                )
            }
        ],
        [
            { sourceId: "1", outputName: "activate", targetId: "0", inputName: "activate" },
            { sourceId: "0", outputName: "success", targetId: "1", inputName: "success" },
            { sourceId: "0", outputName: "error", targetId: "1", inputName: "error" },
            ...extraLinks
        ],
        undefined,
        (container) => {
            if (container.isBound(ROSLYN_EXECUTION_MANAGER_SYMBOL)) {
                container.unbind(ROSLYN_EXECUTION_MANAGER_SYMBOL);
            }
            container.bind(ROSLYN_EXECUTION_MANAGER_SYMBOL).toConstantValue({
                initialize: async () => { },
                execute: async (_assembly: string, inputs: Record<string, unknown>, _timeoutMs: number, context?: RoslynExecutionContext) => execute(inputs, context)
            });
            configureContainer?.(container);
        }
    );
}

describe("CSharpRoslynCode Task tests", () => {
    it("should emit success when activated with no-op code", (done) => {
        createTaskTest({}, async () => ({ result: null, outputs: [] }),
            outputs => outputs.get("activate").emit(true),
            changes => { if (changes["success"]) { chai.expect(changes["success"].currentValue).to.equal(true); done(); } });
    });

    it("should emit error when the saved artifact is absent", (done) => {
        createTaskTest({ roslynAssemblyBase64: "" }, async () => ({ result: null, outputs: [] }),
            outputs => outputs.get("activate").emit(true),
            changes => { if (changes["error"]) { chai.expect(changes["error"].currentValue).to.be.instanceOf(Error); done(); } });
    });

    it("should emit error when the execution host throws", (done) => {
        createTaskTest({}, async () => { throw new Error("boom"); },
            outputs => outputs.get("activate").emit(true),
            changes => { if (changes["error"]) { chai.expect((changes["error"].currentValue as Error).message).to.include("boom"); done(); } });
    });

    it("should forward accumulated inputs to the persisted assembly", (done) => {
        let capturedInputs: Record<string, unknown> | null = null;
        createTaskTest({ inputs: [{ name: "value", valueType: ANY_TYPE }] }, async inputs => {
            capturedInputs = { ...inputs };
            return { result: null, outputs: [] };
        }, outputs => {
            outputs.get("valueIn").emit(42);
            outputs.get("activate").emit(true);
        }, changes => { if (changes["success"]) { chai.expect(capturedInputs).to.deep.equal({ value: 42 }); done(); } },
            [{ sourceId: "1", outputName: "valueIn", targetId: "0", inputName: "valueIn" }]);
    });

    it("should use input defaults when wires were not triggered", (done) => {
        let capturedInputs: Record<string, unknown> | null = null;
        createTaskTest({ inputs: [{ name: "value", valueType: ANY_TYPE, defaultValue: 99 }] }, async inputs => {
            capturedInputs = { ...inputs };
            return { result: null, outputs: [] };
        }, outputs => outputs.get("activate").emit(true),
            changes => { if (changes["success"]) { chai.expect(capturedInputs?.["value"]).to.equal(99); done(); } });
    });

    it("should use defaults with a framework-aware saved source", (done) => {
        let capturedInputs: Record<string, unknown> | null = null;
        createTaskTest({
            csCodeBase64: btoa(MINIMAL_CSHARP_WITH_FRAMEWORK),
            inputs: [{ name: "value", valueType: ANY_TYPE, defaultValue: 99 }],
            outputs: [{ name: "message", valueType: STRING_TYPE }]
        }, async inputs => {
            capturedInputs = { ...inputs };
            return { result: null, outputs: [{ name: "message", value: "Hello World" }] };
        }, outputs => outputs.get("activate").emit(true),
            changes => { if (changes["success"]) { chai.expect(capturedInputs?.["value"]).to.equal(99); done(); } });
    });

    it("should forward integration-style result and emitted output values", (done) => {
        createTaskTest({
            csCodeBase64: btoa(INTEGRATION_CSHARP),
            outputs: [{ name: "doubled", valueType: ANY_TYPE }, { name: "out", valueType: ANY_TYPE }]
        }, async () => ({ result: { doubled: 42 }, outputs: [{ name: "out", value: 21 }] }), outputs => outputs.get("activate").emit(true),
            changes => {
                if (changes["doubledReceived"] && changes["outReceived"]) {
                    chai.expect(changes["doubledReceived"].currentValue).to.equal(42);
                    chai.expect(changes["outReceived"].currentValue).to.equal(21);
                    done();
                }
            }, [
            { sourceId: "0", outputName: "doubledOut", targetId: "1", inputName: "doubledReceived" },
            { sourceId: "0", outputName: "outOut", targetId: "1", inputName: "outReceived" }
        ]);
    });

    it("should emit custom output values returned by the assembly", (done) => {
        createTaskTest({ outputs: [{ name: "result", valueType: ANY_TYPE }] }, async () => ({ result: { result: 84 }, outputs: [] }),
            outputs => outputs.get("activate").emit(true),
            changes => { if (changes["resultReceived"]) { chai.expect(changes["resultReceived"].currentValue).to.equal(84); done(); } },
            [{ sourceId: "0", outputName: "resultOut", targetId: "1", inputName: "resultReceived" }]);
    });

    it("should emit custom output values through the assembly Outputs contract", (done) => {
        createTaskTest({ outputs: [{ name: "proxyValue", valueType: ANY_TYPE }] }, async () => ({
            result: null,
            outputs: [{ name: "proxyValue", value: 77 }]
        }), outputs => outputs.get("activate").emit(true),
            changes => { if (changes["received"]) { chai.expect(changes["received"].currentValue).to.equal(77); done(); } },
            [{ sourceId: "0", outputName: "proxyValueOut", targetId: "1", inputName: "received" }]);
    });

    describe("_framework.System.Call bridging", () => {
        // What the Roslyn host actually puts on the wire for `new EmptyContainerInput { Container = ... }`:
        // a plain JSON object in the LBO's original PascalCase, identified by the "$type" discriminator
        // Newtonsoft writes for it (see SystemApi.SerializerSettings in ExecutionContracts.cs).
        const EMPTY_CONTAINER_INPUT_TYPE = "Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput, Cmf.Navigo.BusinessOrchestration";
        const EmptyContainerInput = System.LBOS.Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput;

        it("should forward a systemCall bridge invocation to the injected CMF System API", (done) => {
            let capturedInput: unknown;
            const systemApi = {
                call: async (input: unknown) => { capturedInput = input; return { accepted: true }; }
            } as unknown as System.SystemAPI;

            createTaskTest({ outputs: [{ name: "systemResult", valueType: ANY_TYPE }] },
                async (_inputs, context) => {
                    const systemResult = await context.invoke("systemCall", {
                        input: { "$type": EMPTY_CONTAINER_INPUT_TYPE, Container: { Name: "CNT-1" } }
                    });
                    return { result: { systemResult }, outputs: [] };
                },
                outputs => outputs.get("activate").emit(true),
                changes => {
                    if (changes["systemResultReceived"]) {
                        // The task must hand SystemAPI.call a real cmf-lbos instance: the HTTP method and
                        // URL of the request are read off the input's constructor, so a plain object
                        // parsed from the host's JSON would resolve neither.
                        chai.expect(capturedInput).to.be.instanceOf(EmptyContainerInput);
                        chai.expect((capturedInput as { Container: { Name: string } }).Container.Name).to.equal("CNT-1");
                        chai.expect(changes["systemResultReceived"].currentValue).to.deep.equal({ accepted: true });
                        done();
                    }
                },
                [{ sourceId: "0", outputName: "systemResultOut", targetId: "1", inputName: "systemResultReceived" }],
                (container) => {
                    container.unbind(TYPES.System.API);
                    container.bind(TYPES.System.API).toConstantValue(systemApi);
                });
        });

        it("should emit error when the CMF System API rejects the call", (done) => {
            const systemApi = {
                call: async () => { throw new Error("LBO call failed"); }
            } as unknown as System.SystemAPI;

            createTaskTest({},
                async (_inputs, context) => {
                    await context.invoke("systemCall", { input: { "$type": EMPTY_CONTAINER_INPUT_TYPE } });
                    return { result: null, outputs: [] };
                },
                outputs => outputs.get("activate").emit(true),
                changes => { if (changes["error"]) { chai.expect((changes["error"].currentValue as Error).message).to.include("LBO call failed"); done(); } },
                [],
                (container) => {
                    container.unbind(TYPES.System.API);
                    container.bind(TYPES.System.API).toConstantValue(systemApi);
                });
        });

        it("should emit error when the systemCall input cannot be resolved to an LBO", (done) => {
            const systemApi = {
                call: async () => ({ accepted: true })
            } as unknown as System.SystemAPI;

            createTaskTest({},
                async (_inputs, context) => {
                    await context.invoke("systemCall", { input: { "$type": "Cmf.Navigo.BusinessOrchestration.NotAnLbo.InputObjects.MissingInput, Cmf.Navigo.BusinessOrchestration" } });
                    return { result: null, outputs: [] };
                },
                outputs => outputs.get("activate").emit(true),
                changes => { if (changes["error"]) { chai.expect((changes["error"].currentValue as Error).message).to.include("unknown LBO input type"); done(); } },
                [],
                (container) => {
                    container.unbind(TYPES.System.API);
                    container.bind(TYPES.System.API).toConstantValue(systemApi);
                });
        });

        it("should complete the full cycle for LBO-based source persisted on the task", (done) => {
            let capturedInput: unknown;
            const systemApi = {
                call: async (input: unknown) => { capturedInput = input; return { accepted: true }; }
            } as unknown as System.SystemAPI;
            const lboRequest = { "$type": EMPTY_CONTAINER_INPUT_TYPE, Container: { CapacityForm: "Pallet", HoldCount: 3 } };

            createTaskTest({
                csCodeBase64: btoa(LBO_SYSTEM_CALL_CSHARP),
                outputs: [{ name: "systemLbo", valueType: ANY_TYPE }]
            },
                async (_inputs, context) => {
                    const systemLbo = await context.invoke("systemCall", { input: lboRequest });
                    return { result: { systemLbo }, outputs: [] };
                },
                outputs => outputs.get("activate").emit(true),
                changes => {
                    if (changes["systemLboReceived"]) {
                        const container = (capturedInput as { Container: { CapacityForm: string; HoldCount: number } }).Container;
                        chai.expect(capturedInput).to.be.instanceOf(EmptyContainerInput);
                        chai.expect(container.CapacityForm).to.equal("Pallet");
                        chai.expect(container.HoldCount).to.equal(3);
                        chai.expect(changes["systemLboReceived"].currentValue).to.deep.equal({ accepted: true });
                        done();
                    }
                },
                [{ sourceId: "0", outputName: "systemLboOut", targetId: "1", inputName: "systemLboReceived" }],
                (container) => {
                    container.unbind(TYPES.System.API);
                    container.bind(TYPES.System.API).toConstantValue(systemApi);
                });
        });
    });
});