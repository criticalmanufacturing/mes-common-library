import "reflect-metadata";
import {
    Task, System, TYPES, Dependencies, Container
} from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite, { TestTaskDefinition } from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/data-store.mock";
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

describe("CodeExecution Api (dataStore) tests", () => {

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
    let dataStoreMock: DataStoreMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
        dataStoreMock = new DataStoreMock();
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
                container.unbind(TYPES.System.PersistedDataStore);
                container.bind(TYPES.System.PersistedDataStore).toConstantValue(dataStoreMock);
            });
    };

    it("should be able to retrieve an existing value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const retrievedValue = await this.framework.dataStore.retrieve("value", 0);

            return { output1: retrievedValue };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 11111, System.DataStoreLocation.Temporary);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(11111);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to retrieve the default value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const retrievedValue = await this.framework.dataStore.retrieve("unknownValue", 123);

            return { output1: retrievedValue };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 11111, System.DataStoreLocation.Temporary);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal(123);
                    expect(dataStoreMock.get("value", System.DataStoreLocation.Temporary)).to.equal(11111);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to store a temporary value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.store("value", 22, "Temporary");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock.get("value", System.DataStoreLocation.Temporary)).to.equal(22);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to store a new temporary value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.store("value", 24, "Temporary");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Temporary);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock.get("value", System.DataStoreLocation.Temporary)).to.equal(24);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to store a persistent value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.store("value", 33, "Persistent");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock.get("value", System.DataStoreLocation.Persistent)).to.equal(33);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to store a new persistent value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.store("value", 34, "Persistent");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 33, System.DataStoreLocation.Persistent);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock.get("value", System.DataStoreLocation.Persistent)).to.equal(34);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to delete a temporary value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.delete("value", "Temporary");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Temporary);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock["_temporaryDataStore"].size).to.eq(0);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to delete a persistent value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.delete("value", "Persistent");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Persistent);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock["_persistedDataStore"].size).to.eq(0);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to clear a temporary value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.clear("Temporary");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Temporary);
                dataStoreMock.set("value1", 1, System.DataStoreLocation.Temporary);
                dataStoreMock.set("value2", 1, System.DataStoreLocation.Persistent);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock["_temporaryDataStore"].size).to.eq(0);
                    expect(dataStoreMock["_persistedDataStore"].size).to.eq(1);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to clear a persistent value", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.dataStore.clear("Persistent");

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Persistent);
                dataStoreMock.set("value1", 1, System.DataStoreLocation.Persistent);
                dataStoreMock.set("value2", 1, System.DataStoreLocation.Temporary);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(dataStoreMock["_persistedDataStore"].size).to.eq(0);
                    expect(dataStoreMock["_temporaryDataStore"].size).to.eq(1);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to list identifiers of temporary store", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = this.framework.dataStore.listKeys("Temporary");

            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Temporary);
                dataStoreMock.set("anotherValue", 23, System.DataStoreLocation.Temporary);
                dataStoreMock.set("noReturn", 23, System.DataStoreLocation.Persistent);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.deep.equal(["value", "anotherValue"]);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to list identifiers of persistent store", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const result = this.framework.dataStore.listKeys("Persistent");

            return { output1: result };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                dataStoreMock.set("value", 23, System.DataStoreLocation.Persistent);
                dataStoreMock.set("anotherValue", 23, System.DataStoreLocation.Persistent);
                dataStoreMock.set("noReturn", 23, System.DataStoreLocation.Temporary);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.deep.equal(["value", "anotherValue"]);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });
});
