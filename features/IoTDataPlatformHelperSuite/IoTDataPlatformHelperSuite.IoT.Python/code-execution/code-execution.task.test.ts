import "reflect-metadata";
import {
    Task, System, TYPES, DI, Dependencies, Container
} from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/driver-proxy.mock";
import { expect } from "chai";

/**
 * We need to fetch the Execution Context node implementation that uses zone.js
 * Instead of creating an explicit dependency to the controller (which was nonsense) or to Zon.js (and replicate the implementation)
 * We considered this "magic" import as the less bad
 */
import { ExecutionContextZoneJS } from "@criticalmanufacturing/connect-iot-controller/dist/src/dependencies/executionContext.zonejs";



import { CodeExecutionModule } from "../../../../src/lib/tasks/code-execution/code-execution-node.task-module";

import {
    CodeExecutionSettings,
    encode
} from "../../../../src/lib/tasks/code-execution/code-execution.task";

/**
 * Converts the property name into the appropriate input
 * @param name property name
 */
export const inp: (name: string) => string = (name: string) => `${name}In`;
/**
 * Converts the property name into the appropriate output
 * @param name property name
 */
export const out: (name: string) => string = (name: string) => `${name}Out`;

describe("CodeExecution Task tests", () => {

    /*
    *               +-----------------------------+       +---------------------------+
    *               | ==mockTask (1)==            |       | ==CodeExecution (0)==     |
    *    (*1) ----> | () input1        output1 () | ----> | () input1      output1 () | ----> (*1)
    *    (*2) ----> | () input2        output2 () | ----> | () input2      output2 () | ----> (*2)
    *               |                             |       |---------------------------|
    *    (*3) ----> | () success      activate () | ----> | () activate    success () | ----> (*3)
    *    (*4) ----> | () error                    |       |                  error () | ----> (*4)
    *               +-----------------------------+       +---------------------------+
    */

    const CustomMockTask = (setup: Function, trigger: Function, validate: Function): Task.TaskInstance => {
        class MockTask implements Task.TaskInstance {
            [key: string]: any;
            private _outputs: Map<string, Task.Output<any>> = new Map<string, Task.Output<any>>();

            @DI.Inject(TYPES.System.PersistedDataStore)
            public dataStore: System.DataStore;

            @DI.Inject(TYPES.Task.Library)
            public taskCodeExecutionLibs: Task.Library;

            @DI.Inject(TYPES.Dependencies.ExecutionContext)
            public executionContext: Dependencies.ExecutionContext;

            // Some setup for the test
            async onBeforeInit(): Promise<void> {
                this.activate = new Task.Output<any>();
                this._outputs.set("activate", this.activate);
                this.output1 = new Task.Output<any>();
                this._outputs.set("output1", this.output1);
                this.output2 = new Task.Output<any>();
                this._outputs.set("output2", this.output2);

                await setup(this);
            }

            // Trigger the test
            async onInit(): Promise<void> {
                await trigger(this._outputs, this);
            }

            // Validate the results
            async onChanges(changes: Task.Changes): Promise<void> {
                await validate(changes, this);
                this.success = undefined;
                this.error = undefined;
                this.input1 = undefined;
                this.input2 = undefined;
            }
        }
        return <any>MockTask;
    };

    const _defaultSettings: CodeExecutionSettings = {
        tsCode: [],
        jsCodeBase64: "",
        inputs: [
            { name: "input1", valueType: { type: System.PropertyValueType.Integer } },
            { name: "input2", valueType: { type: System.PropertyValueType.Integer } },
        ],
        outputs: [
            { name: "output1", valueType: { type: System.PropertyValueType.Integer } },
            { name: "output2", valueType: { type: System.PropertyValueType.Integer } },
        ],
        executionExpirationInMilliseconds: 0,
        contextExpirationInMilliseconds: 0
    };

    let driverMock: DriverProxyMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
    });

    const codeExecutionTestFactory = (customSettings: any | undefined,
        setup: Function,
        trigger: Function,
        validate: Function): void => {
        const codeExecutionTaskDefinition = {
            id: "0",
            class: CodeExecutionModule,
            settings: Object.assign({}, _defaultSettings, customSettings),
        };

        const mockTaskDefinition = {
            id: "1",
            class: Task.Task({
                name: "mockTask"
            })(CustomMockTask(setup, trigger, validate))
        };

        EngineTestSuite.createTasks([
            codeExecutionTaskDefinition,
            mockTaskDefinition,
        ], [
            { sourceId: "1", outputName: `activate`, targetId: "0", inputName: "activate" },
            { sourceId: "0", outputName: `success`, targetId: "1", inputName: "success" },
            { sourceId: "0", outputName: `error`, targetId: "1", inputName: "error" },
            { sourceId: "1", outputName: "output1", targetId: "0", inputName: inp("input1") },
            { sourceId: "1", outputName: "output2", targetId: "0", inputName: inp("input2") },
            { sourceId: "0", outputName: out("output1"), targetId: "1", inputName: "input1" },
            { sourceId: "0", outputName: out("output2"), targetId: "1", inputName: "input2" },
        ],
            driverMock,
            (container: Container) => {
                container.rebind<Dependencies.ExecutionContext>(TYPES.Dependencies.ExecutionContext).to(ExecutionContextZoneJS).inSingletonScope();
            });
    };

    it("should execute 2 scripts in parallel", (done) => {
        const code = `
            Object.defineProperty(exports, "__esModule", { value: true });
            class CustomCode {
                async sleep(ms) {
                    return new Promise(resolve => setTimeout(resolve, ms));
                }
                async main(inputs) {                    
                    await this.sleep(1000);
                    return {
                        output1: inputs.input1,
                        output2: inputs.input2
                    };
                }
            }
            exports.default = CustomCode;
        `;

        let count = 0;

        codeExecutionTestFactory(
            { jsCodeBase64: encode(code) }, // customSettings
            () => {
                // Setup
            },
            (outputs: Map<string, Task.Output<any>>) => {
                // Trigger outputs
                outputs.get("activate").emit(true);
                outputs.get("output1").emit(1);
                outputs.get("output2").emit(2);

                setTimeout(() => {
                    outputs.get("activate").emit(true);
                    outputs.get("output1").emit(1);
                    outputs.get("output2").emit(3);
                }, 250);
            }, (changes: Task.Changes) => {
                try {
                    // Validate the inputs
                    if (count === 0) {
                        count++;
                        expect(changes.success).to.exist;
                        expect(changes.success.currentValue).to.equal(true);
                        expect(changes.input1).to.exist;
                        expect(changes.input1.currentValue).to.equal(1);
                        expect(changes.input2).to.exist;
                        expect(changes.input2.currentValue).to.equal(2);
                    } else {
                        expect(changes.success).to.exist;
                        expect(changes.success.currentValue).to.equal(true);
                        expect(changes.input1).to.exist;
                        expect(changes.input1.currentValue).to.equal(1);
                        expect(changes.input2).to.exist;
                        expect(changes.input2.currentValue).to.equal(3);
                        done();
                    }
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should group inputs by execution context ('zone aware')", (done) => {
        const code = `
            Object.defineProperty(exports, "__esModule", { value: true });
            class CustomCode {
                async main(inputs) {
                    return { output1: inputs.input1 };
                }
            }
            exports.default = CustomCode;
        `;

        const VALUE1: number = 1;
        const VALUE2: number = 2;
        const context1Name: string = "context 1";
        const context2Name: string = "context 2";
        let numberOfChanges: number = 0;

        codeExecutionTestFactory(
            {
                jsCodeBase64: encode(code),
            },
            () => {
                // Setup
            },
            (outputs: Map<string, Task.Output<any>>, mockTask: Task.TaskInstance) => {
                // Trigger outputs
                const context1 = mockTask.executionContext.fork({
                    name: context1Name,
                    properties: {}
                });
                const context2 = mockTask.executionContext.fork({
                    name: context2Name,
                    properties: {}
                });

                context1.run(() => {
                    setTimeout(() => {
                        outputs.get("output1").emit(VALUE1);
                    });
                    setTimeout(() => {
                        outputs.get("activate").emit(true);
                    }, 400);
                });
                context2.run(() => {
                    setTimeout(() => {
                        outputs.get("output1").emit(VALUE2);
                        outputs.get("activate").emit(true);
                    }, 200);
                });
            },
            (changes: Task.Changes, mockTask: Task.TaskInstance) => {
                try {
                    // Validate the inputs
                    if (numberOfChanges === 0) {
                        numberOfChanges++;

                        expect(mockTask.executionContext).to.exist;
                        expect(mockTask.executionContext.name).to.equal(context2Name);

                        expect(changes.error).to.not.exist;
                        expect(changes.success).to.exist;
                        expect(changes.input1).to.exist;
                        expect(changes.input1.currentValue).to.equal(VALUE2);
                        expect(changes.input2).to.not.exist;

                    } else {
                        expect(mockTask.executionContext).to.exist;
                        expect(mockTask.executionContext.name).to.equal(context1Name);

                        expect(changes.error).to.not.exist;
                        expect(changes.success).to.exist;
                        expect(changes.input1).to.exist;
                        expect(changes.input1.currentValue).to.equal(VALUE1);
                        expect(changes.input2).to.not.exist;

                        done();
                    }
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should remove expired inputs", (done) => {
        const code = `
            Object.defineProperty(exports, "__esModule", { value: true });
            class CustomCode {
                async main(inputs) {
                    var outputs = {};
                    if (inputs.input1 != null) {
                        outputs.output1 = inputs.input1
                    }
                    if (inputs.input2 != null) {
                        outputs.output2 = inputs.input2
                    }
                    return outputs;
                }
            }
            exports.default = CustomCode;
        `;

        const VALUE1: number = 1;
        const VALUE2: number = 2;
        const context1Name: string = "context 1";
        const context2Name: string = "context 2";
        let numberOfChanges: number = 0;

        codeExecutionTestFactory(
            {
                jsCodeBase64: encode(code),
                contextExpirationInMilliseconds: 250
            },
            () => {
                // Setup
            },
            (outputs: Map<string, Task.Output<any>>, mockTask: Task.TaskInstance) => {
                // Trigger outputs
                const context1 = mockTask.executionContext.fork({
                    name: context1Name,
                    properties: {}
                });
                const context2 = mockTask.executionContext.fork({
                    name: context2Name,
                    properties: {}
                });

                context1.run(() => {
                    outputs.get("output1").emit(VALUE1);
                    setTimeout(() => {
                        outputs.get("activate").emit(true);
                        outputs.get("output2").emit(VALUE1);
                    }, 500);
                });
                context2.run(() => {
                    setTimeout(() => {
                        outputs.get("output1").emit(VALUE2);
                        outputs.get("activate").emit(true);
                    }, 200);
                });
            },
            (changes: Task.Changes, mockTask: Task.TaskInstance) => {
                try {
                    // Validate the inputs
                    if (numberOfChanges === 0) {
                        numberOfChanges++;

                        expect(mockTask.executionContext).to.exist;
                        expect(mockTask.executionContext.name).to.equal(context2Name);
                        expect(changes.error).to.not.exist;
                        expect(changes.success).to.exist;
                        expect(changes.input1).to.exist;
                        expect(changes.input1.currentValue).to.equal(VALUE2);
                        expect(changes.input2).to.not.exist;

                    } else {
                        expect(mockTask.executionContext).to.exist;
                        expect(mockTask.executionContext.name).to.equal(context1Name);
                        expect(changes.error).to.not.exist;
                        expect(changes.success).to.exist;
                        expect(changes.input1).to.not.exist;
                        expect(changes.input2).to.exist;
                        expect(changes.input2.currentValue).to.equal(VALUE1);

                        done();
                    }
                } catch (err) {
                    done(err);
                }
            }
        );
    });
    // Failling test
    // eslint-disable-next-line
    it.skip("should emit error if script is synchronous and exceeds execution timeout", (done) => {
        const code = `
            Object.defineProperty(exports, "__esModule", { value: true });
            class CustomCode {
                async main(inputs) {
                    while(true) { }
                }
            }
            exports.default = CustomCode;
        `;

        codeExecutionTestFactory(
            {
                jsCodeBase64: encode(code),
                executionExpirationInMilliseconds: 50
            },
            () => {
                // Setup
            },
            (outputs: Map<string, Task.Output<any>>) => {
                // Trigger outputs
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                try {
                    // Validate the inputs
                    expect(changes.success).to.not.exist;
                    expect(changes.error).to.exist;
                    expect(changes.error.currentValue).to.exist;
                    expect(changes.error.currentValue.message).to.exist;
                    expect(changes.error.currentValue.message).to.equal("Script execution timed out.");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should emit error if no code is set", (done) => {

        codeExecutionTestFactory(
            {
                jsCodeBase64: "",
                executionExpirationInMilliseconds: 50
            },
            () => {
                // Setup
            },
            (outputs: Map<string, Task.Output<any>>) => {
                // Trigger outputs
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                // Validate the inputs
                try {
                    expect(changes.success).to.not.exist;
                    expect(changes.error).to.exist;
                    expect(changes.error.currentValue).to.exist;
                    expect(changes.error.currentValue.message).to.exist;
                    expect(changes.error.currentValue.message).to.equal("No code defined");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the default api (e.g. store and retrieve data from the data store)", (done) => {
        const code = `
            Object.defineProperty(exports, "__esModule", { value: true });
            class default_1 {
                constructor(framework) {
                    this.framework = framework;
                }
                /*
                * Entry point of the class (IMPORTANT: don't change the signature of this method)
                * Should return an object containing the values for each output to emit
                * If necessary, use the parameter "outputs" to emit data while running the code.
                */
                async main(inputs, outputs) {
                    const retrievedValue = await this.framework.dataStore.retrieve(inputs.input1, 0);
                    await this.framework.dataStore.store(inputs.input1, inputs.input2, "Persistent");
                    return { output1: retrievedValue };
                }
            }
            exports.default = default_1;
        `;

        const IDENTIFIER: string = "value1";
        const DEFAULT_VALUE: number = 0;
        const INITIAL_VALUE: number = 1;
        const CHANGED_VALUE: number = 2;

        codeExecutionTestFactory(
            {
                jsCodeBase64: encode(code),
            },
            () => {
                // Setup
            },
            async (outputs: Map<string, Task.Output<any>>, mockTask: Task.TaskInstance) => {
                // Store values on "outer" data store
                await mockTask.dataStore.store(IDENTIFIER, INITIAL_VALUE, System.DataStoreLocation.Persistent);

                // Emit values to be stored in the data store (identifier and value)
                outputs.get("activate").emit(true);
                outputs.get("output1").emit(IDENTIFIER);
                outputs.get("output2").emit(CHANGED_VALUE);
            }, async (changes: Task.Changes, mockTask: Task.TaskInstance) => {
                try {
                    // Validate the inputs
                    expect(changes.error).to.not.exist;
                    expect(changes.success).to.exist;

                    // Retrieved value1 before "inner" store
                    expect(changes.input1).to.exist;
                    expect(changes.input1.currentValue).to.equal(INITIAL_VALUE);

                    // Retrieved value1 from data store should have changed
                    let retrievedValue = await mockTask.dataStore.retrieve(IDENTIFIER, DEFAULT_VALUE);
                    expect(retrievedValue).to.equal(CHANGED_VALUE);

                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the custom api (injected libraries)", (done) => {
        const code = `
            Object.defineProperty(exports, "__esModule", { value: true });
            class default_1 {
                constructor(framework) {
                    this.framework = framework;
                }
                /*
                * Entry point of the class (IMPORTANT: don't change the signature of this method)
                * Should return an object containing the values for each output to emit
                * If necessary, use the parameter "outputs" to emit data while running the code.
                */
                async main(inputs, outputs) {
                    const result = await this.framework.customLib.add(inputs.input1, inputs.input2);
                    return { output1: result };
                }
            }
            exports.default = default_1;
        `;

        class CustomLib {
            public add(a: number, b: number): number {
                return a + b;
            }
        }
        const CUSTOM_LIB_ID: string = "customLib";
        const customLibObj = new CustomLib();

        const VALUE1: number = 1;
        const VALUE2: number = 2;

        codeExecutionTestFactory(
            {
                jsCodeBase64: encode(code),
            },
            (mockTask: Task.TaskInstance) => {
                // Setup
                mockTask.taskCodeExecutionLibs.addImplementation(CUSTOM_LIB_ID, customLibObj);
            },
            (outputs: Map<string, Task.Output<any>>) => {
                // Trigger outputs
                outputs.get("activate").emit(true);
                outputs.get("output1").emit(VALUE1);
                outputs.get("output2").emit(VALUE2);
            }, (changes: Task.Changes) => {
                try {
                    // Validate the inputs
                    expect(changes.error).to.not.exist;
                    expect(changes.success).to.exist;
                    expect(changes.input1).to.exist;
                    expect(changes.input1.currentValue).to.equal(VALUE1 + VALUE2);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

});
