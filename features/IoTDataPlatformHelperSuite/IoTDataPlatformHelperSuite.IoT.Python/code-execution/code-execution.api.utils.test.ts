import "reflect-metadata";
import {
    Task, System, TYPES, Dependencies, Container, DI
} from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite, { TestTaskDefinition } from "@criticalmanufacturing/connect-iot-controller-engine/test";
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

import Foundation = System.LBOS.Cmf.Foundation;
import Navigo = System.LBOS.Cmf.Navigo;
import DEEActionGroup = Foundation.Common.DynamicExecutionEngine.ActionGroup;
import EmptyContainerInput = Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput;


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

describe("CodeExecution Api (utils) tests", () => {

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

    @DI.Injectable()
    class SystemAPIMock implements System.SystemAPI {
        private _numberOfCalls: number = 0;

        public get numberOfCalls(): number {
            return this._numberOfCalls;
        }

        public numberOfFails: number = 0;
        public errorMessage: string = "";

        subscribeActionGroup(actionGroup: DEEActionGroup | string, callback: System.SystemEventCallback): void {
            throw new Error("Unexpected call");
        }
        unsubscribeActionGroup(actionGroup: DEEActionGroup | string, callback: System.SystemEventCallback): void {
            throw new Error("Unexpected call");
        }
        async call(input: Foundation.BusinessOrchestration.BaseInput): Promise<any> {
            this._numberOfCalls++;
            if (this.numberOfCalls <= this.numberOfFails) {
                throw new Error(this.errorMessage);
            }
            if (input instanceof EmptyContainerInput) {
                const output: any = new System.LBOS.Cmf.Foundation.BusinessOrchestration.BaseOutput();
                output.Message = "Success message";
                output.Container = input.Container;
                return output;
            } else {
                throw new Error("Unexpected call");
            }
        }
    }

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

    const codeTemplate = `
        Object.defineProperty(exports, "__esModule", { value: true });
        class CustomCode {
            constructor(framework) {
                this.framework = framework;
            }
            async main(inputs) {
                <CODE>
            }
        }
        exports.default = CustomCode;
    `;

    let driverMock: DriverProxyMock;
    let systemAPIMock: SystemAPIMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
        systemAPIMock = new SystemAPIMock();
    });

    const codeExecutionTestFactory = (customSettings: any | undefined,
        trigger: Function,
        validate: Function,
        customMockTaskDefinition?: TestTaskDefinition<any>): void => {
        const codeExecutionTaskDefinition = {
            id: "0",
            class: CodeExecutionModule,
            settings: Object.assign({}, _defaultSettings, customSettings),
        };

        const mockTaskDefinition = customMockTaskDefinition || {
            id: "1",
            class: Task.Task({
                name: "mockTask"
            })(class MockTask implements Task.TaskInstance {
                [key: string]: any;
                _outputs: Map<string, Task.Output<any>> = new Map<string, Task.Output<any>>();

                async onBeforeInit(): Promise<void> {
                    this["activate"] = new Task.Output<any>();
                    this._outputs.set("activate", this["activate"]);
                    this["output1"] = new Task.Output<any>();
                    this._outputs.set("output1", this["output1"]);
                    this["output2"] = new Task.Output<any>();
                    this._outputs.set("output2", this["output2"]);
                }

                // Validate the results
                async onChanges(changes: Task.Changes): Promise<void> {
                    validate(changes);
                    this["success"] = undefined;
                    this["error"] = undefined;
                    this["input1"] = undefined;
                    this["input2"] = undefined;
                }

                // Trigger the test
                async onInit(): Promise<void> {
                    trigger(this._outputs);
                }
            })
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
                container.rebind(TYPES.System.API).toConstantValue(systemAPIMock);
            });
    };

    it("should be able to convert a string", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("str", "String", "def");
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal("str");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert to a default string", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType(undefined, "String", "def");
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal("def");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert an integer", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("123", "Integer", "321");
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(123);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a default integer", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("a123", "Integer", 321);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(321);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a long", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("12345", "Long", 3210);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(12345);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a default long", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("a12345", "Integer", 3210);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(3210);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a decimal", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("123.45", "Decimal", 32.10);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(123.45);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a default decimal", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("a12345", "Decimal", 32.10);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(32.10);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a boolean", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("true", "Boolean", false);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a boolean II", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("1", "Boolean", false);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a boolean III", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("yes", "Boolean", false);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a boolean IV", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("y", "Boolean", false);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a boolean V", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("t", "Boolean", false);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a default boolean", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType(undefined, "Boolean", false);
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(false);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert an object", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("{\\"x\\":\\"y\\"}", "Object", {});
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue.x).to.equal("y");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to convert a default object", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = await this.framework.utils.convertValueToType("{x:y}", "Object", {"y": 123});
            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue.y).to.equal(123);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to sleep for 2 seconds", (done) => {
        const timeToSleep = 2000;
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.utils.sleep(${timeToSleep});
            return { output1: true };
        `);

        let timestampReference: Date;
        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                timestampReference = new Date();
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    const elapsed = (new Date().getTime() - timestampReference.getTime());
                    expect(elapsed >= timeToSleep).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the retry mechanism for system calls", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const code = async () => {
                const serviceInput = new this.framework.LBOS.Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput();
                serviceInput.IgnoreLastServiceId = true;
                serviceInput.Container = new this.framework.LBOS.Cmf.Navigo.BusinessObjects.Container();
                serviceInput.Container.Name = "Container001";
                return await this.framework.system.call(serviceInput);
            };
            const res = await this.framework.utils.ExecuteWithSystemErrorRetry(this.framework.logger, inputs.input1, inputs.input2, code);
            return { output1: res };
        `);
        systemAPIMock.numberOfFails = 3;
        systemAPIMock.errorMessage = "The data for object Container001 has changed since last viewed. Please refresh the object.";

        // Zone.js catches uncaught promises and wraps it to a new Error object with some additional information
        // We need to disable it to not pollute the test results console with an error message
        Zone[Zone.__symbol__("ignoreConsoleErrorUncaughtError")] = true;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("output1").emit(10); // number of retries
                values.get("output2").emit(1); // milliseconds between retries
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"]).to.exist;
                    expect(changes["input1"].currentValue.Container).to.exist;
                    expect(changes["input1"].currentValue.Container.Name).to.equal("Container001");
                    expect(systemAPIMock.numberOfCalls).to.equal(systemAPIMock.numberOfFails + 1);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the retry mechanism for system calls II", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const code = async () => {
                const serviceInput = new this.framework.LBOS.Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput();
                serviceInput.IgnoreLastServiceId = true;
                serviceInput.Container = new this.framework.LBOS.Cmf.Navigo.BusinessObjects.Container();
                serviceInput.Container.Name = "Container001";
                return await this.framework.system.call(serviceInput);
            };
            const res = await this.framework.utils.ExecuteWithSystemErrorRetry(this.framework.logger, inputs.input1, inputs.input2, code);
            return { output1: res };
        `);
        systemAPIMock.numberOfFails = 3;
        systemAPIMock.errorMessage = "Error: connect ECONNREFUSED ";

        // Zone.js catches uncaught promises and wraps it to a new Error object with some additional information
        // We need to disable it to not pollute the test results console with an error message
        Zone[Zone.__symbol__("ignoreConsoleErrorUncaughtError")] = true;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("output1").emit(10); // number of retries
                values.get("output2").emit(1); // milliseconds between retries
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"]).to.exist;
                    expect(changes["input1"].currentValue.Container).to.exist;
                    expect(changes["input1"].currentValue.Container.Name).to.equal("Container001");
                    expect(systemAPIMock.numberOfCalls).to.equal(systemAPIMock.numberOfFails + 1);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the retry mechanism for system calls III", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const code = async () => {
                const serviceInput = new this.framework.LBOS.Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput();
                serviceInput.IgnoreLastServiceId = true;
                serviceInput.Container = new this.framework.LBOS.Cmf.Navigo.BusinessObjects.Container();
                serviceInput.Container.Name = "Container001";
                return await this.framework.system.call(serviceInput);
            };
            const res = await this.framework.utils.ExecuteWithSystemErrorRetry(this.framework.logger, inputs.input1, inputs.input2, code);
            return { output1: res };
        `);
        systemAPIMock.numberOfFails = 3;
        systemAPIMock.errorMessage = "NOT A SYSTEM ERROR";

        // Zone.js catches uncaught promises and wraps it to a new Error object with some additional information
        // We need to disable it to not pollute the test results console with an error message
        Zone[Zone.__symbol__("ignoreConsoleErrorUncaughtError")] = true;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("output1").emit(10); // number of retries
                values.get("output2").emit(1); // milliseconds between retries
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"]).to.not.exist;
                    expect(changes["input1"]).to.not.exist;
                    expect(changes["error"]).to.exist;
                    expect(changes["error"].currentValue.message).to.equal(systemAPIMock.errorMessage);
                    expect(systemAPIMock.numberOfCalls).to.equal(1);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the retry mechanism for system calls IV", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const code = async () => {
                const serviceInput = new this.framework.LBOS.Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput();
                serviceInput.IgnoreLastServiceId = true;
                serviceInput.Container = new this.framework.LBOS.Cmf.Navigo.BusinessObjects.Container();
                serviceInput.Container.Name = "Container001";
                return await this.framework.system.call(serviceInput);
            };
            const res = await this.framework.utils.ExecuteWithSystemErrorRetry(this.framework.logger, inputs.input1, inputs.input2, code);
            return { output1: res };
        `);
        systemAPIMock.numberOfFails = 10;
        systemAPIMock.errorMessage = "Error: connect ECONNREFUSED ";
        let timestampReference: Date;
        const numberOfRetries = 5;
        const millisecondsBetweenRetries = 100;

        // Zone.js catches uncaught promises and wraps it to a new Error object with some additional information
        // We need to disable it to not pollute the test results console with an error message
        Zone[Zone.__symbol__("ignoreConsoleErrorUncaughtError")] = true;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                timestampReference = new Date();
                values.get("output1").emit(numberOfRetries); // number of retries
                values.get("output2").emit(millisecondsBetweenRetries); // milliseconds between retries
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"]).to.not.exist;
                    expect(changes["input1"]).to.not.exist;
                    expect(changes["error"]).to.exist;
                    expect(changes["error"].currentValue.message).to.equal(systemAPIMock.errorMessage);
                    expect(systemAPIMock.numberOfCalls).to.equal(5);
                    const elapsed = (new Date().getTime() - timestampReference.getTime());
                    expect(elapsed).to.be.greaterThan(numberOfRetries * millisecondsBetweenRetries);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the retry mechanism", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            let n = 0;
            const code = async () => {
                n++;
                if (n < 5) {
                    throw new Error(\`Attempt \${n} failed\`);
                }
                return n;
            };
            const res = await this.framework.utils.ExecuteWithRetry(this.framework.logger, inputs.input1, inputs.input2, code);
            return { output1: res };
        `);

        // Zone.js catches uncaught promises and wraps it to a new Error object with some additional information
        // We need to disable it to not pollute the test results console with an error message
        Zone[Zone.__symbol__("ignoreConsoleErrorUncaughtError")] = true;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("output1").emit(10); // number of retries
                values.get("output2").emit(1); // milliseconds between retries
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"]).to.exist;
                    expect(changes["input1"].currentValue).to.equal(5);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to use the retry mechanism II", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            let n = 0;
            const code = async () => {
                n++;
                if (n < 5) {
                    throw new Error(\`Attempt \${n} failed\`);
                }
                return n;
            };
            const res = await this.framework.utils.ExecuteWithRetry(this.framework.logger, inputs.input1, inputs.input2, code);
            return { output1: res };
        `);
        const numberOfRetries = 4;
        const millisecondsBetweenRetries = 100;
        let timestampReference: Date;

        // Zone.js catches uncaught promises and wraps it to a new Error object with some additional information
        // We need to disable it to not pollute the test results console with an error message
        Zone[Zone.__symbol__("ignoreConsoleErrorUncaughtError")] = true;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                timestampReference = new Date();
                values.get("output1").emit(numberOfRetries); // number of retries
                values.get("output2").emit(millisecondsBetweenRetries); // milliseconds between retries
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"]).to.not.exist;
                    expect(changes["input1"]).to.not.exist;
                    expect(changes["error"]).to.exist;
                    expect(changes["error"].currentValue.message).to.equal(`Attempt ${numberOfRetries} failed`);
                    const dif = (new Date().getTime() - timestampReference.getTime());
                    expect(dif).to.be.greaterThan(numberOfRetries * millisecondsBetweenRetries);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

});
