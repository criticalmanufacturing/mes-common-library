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



import {
    AutomationControllerDriverDefinition,
    AutomationProperty,
    AutomationEvent,
    AutomationEventProperty,
    AutomationCommand,
    AutomationCommandParameter,
    EquipmentPropertyValue
} from "../../../../src/lib/tasks/code-execution/execution-engine/v100/intellisense/driver-proxy";


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

describe("CodeExecution Api (driver) tests", () => {

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

    const p1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty();
    const e1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent();
    const e1p1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty();
    const c1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommand();
    const c1p1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommandParameter();

    before(() => {
        p1.Id = "p1Id";
        p1.DevicePropertyId = "p1Id2";
        p1.Name = "p1Name";
        p1.Description = "p1Description";
        p1.DataType = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType.Decimal;
        p1.AutomationProtocolDataType = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocolDataType();
        p1.AutomationProtocolDataType.Name = "protocolDataType";
        p1.IsReadable = true;
        p1.IsWritable = true;
        p1.ExtendedData = '{"x": 1.23 }';

        e1.Id = "e1Id";
        e1.Name = "e1Name";
        e1.DeviceEventId = "e1Id2";
        e1.Description = "e1Description";
        e1.IsEnabled = false;
        e1.ExtendedData = '{"y": 12.3 }';

        e1p1.Name = "e1p1Name";
        e1p1.Id = "e1p1Id";
        e1p1.Description = "e1p1Description";
        e1p1.Order = 12;
        e1p1.AutomationProperty = p1;
        e1p1.AutomationEvent = e1;

        c1.Id = "c1Id";
        c1.DeviceCommandId = "c1Id2";
        c1.Name = "c1Name";
        c1.ExtendedData = '{"z": 123.0 }';

        c1p1.Name = "c1p1Name";
        c1p1.Id = "c1p1Id";
        c1p1.Order = 21;
        c1p1.DataType = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType.String;
        c1p1.AutomationProtocolDataType = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocolDataType();
        c1p1.AutomationProtocolDataType.Name = "protocolDataType2";
        c1p1.IsMandatory = true;
        c1p1.DefaultValue = "1234";
        c1p1.ExtendedData = '{"a": 1234.0 }';

        c1.Parameters = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommandParameterCollection();
        c1.Parameters.push(c1p1);
    });


    let driverMock: DriverProxyMock;
    let dataStoreMock: DataStoreMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
        driverMock.sleepsEnabled = false;
        driverMock.controllerId = "TestController0001";
        driverMock.setMockDefinitions({
            events: [e1],
            properties: [p1],
            commands: [c1],
            eventProperties: [e1p1],
        });

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

    it("should be able to connect to a device", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.driver.connect();

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                expect(driverMock.isConnected).to.equal(false);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(driverMock.isConnected).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to disconnect from a device", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.driver.disconnect();

            return { };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                expect(driverMock.isConnected).to.equal(false);
                driverMock.connect();
                expect(driverMock.isConnected).to.equal(true);
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(driverMock.isConnected).to.equal(false);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to send notification to driver", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            await this.framework.driver.notifyRaw("subject", "value");

            return { };
        `);
        let messageReceived = false;

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                driverMock.subscribeRaw("subject", (message) => {
                    expect(message.type).to.equal("subject");
                    expect(message.content).to.equal("value");
                    messageReceived = true;
                });

                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(messageReceived).to.equal(true);
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to send request to driver", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const reply = await this.framework.driver.sendRaw("echo", "value");

            return { output1: reply };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal("reply_value");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to get driver definitions", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const reply = this.framework.driver.automationControllerDriverDefinition;
            // console.log(reply);
            return { output1: reply };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    const dataReceived: AutomationControllerDriverDefinition = changes["input1"].currentValue;
                    // Validate received data
                    expect(dataReceived).to.exist;
                    expect(dataReceived.AutomationControllerId).to.equal(driverMock.automationControllerDriverDefinition.AutomationControllerId);
                    expect(dataReceived.AutomationDriverDefinition).to.exist;

                    expect(dataReceived.AutomationDriverDefinition.Properties).to.exist;
                    expect(dataReceived.AutomationDriverDefinition.Properties.length).to.equal(1);
                    const receivedProperty: AutomationProperty = dataReceived.AutomationDriverDefinition.Properties[0];
                    expect(receivedProperty.Id).to.equal(p1.Id);
                    expect(receivedProperty.Name).to.equal(p1.Name);
                    expect(receivedProperty.Description).to.equal(p1.Description);
                    expect(receivedProperty.DevicePropertyId).to.equal(p1.DevicePropertyId);
                    expect(receivedProperty.DataType).to.equal(p1.DataType);
                    expect(receivedProperty.AutomationProtocolDataType.Name).to.equal(p1.AutomationProtocolDataType.Name);
                    expect(receivedProperty.ExtendedData).to.equal(p1.ExtendedData);
                    expect(receivedProperty.IsReadable).to.equal(p1.IsReadable);
                    expect(receivedProperty.IsWritable).to.equal(p1.IsWritable);

                    expect(dataReceived.AutomationDriverDefinition.Events).to.exist;
                    expect(dataReceived.AutomationDriverDefinition.Events.length).to.equal(1);
                    const receivedEvent: AutomationEvent = dataReceived.AutomationDriverDefinition.Events[0];
                    expect(receivedEvent.Id).to.equal(e1.Id);
                    expect(receivedEvent.Name).to.equal(e1.Name);
                    expect(receivedEvent.Description).to.equal(e1.Description);
                    expect(receivedEvent.DeviceEventId).to.equal(e1.DeviceEventId);
                    expect(receivedEvent.ExtendedData).to.equal(e1.ExtendedData);
                    expect(receivedEvent.IsEnabled).to.equal(e1.IsEnabled);

                    expect(dataReceived.AutomationDriverDefinition.EventProperties).to.exist;
                    expect(dataReceived.AutomationDriverDefinition.EventProperties.length).to.equal(1);
                    const receivedEventProperty: AutomationEventProperty = dataReceived.AutomationDriverDefinition.EventProperties[0];
                    expect(receivedEventProperty.Id).to.equal(e1p1.Id);
                    expect(receivedEventProperty.Name).to.equal(e1p1.Name);
                    expect(receivedEventProperty.Description).to.equal(e1p1.Description);
                    expect(receivedEventProperty.Order).to.equal(e1p1.Order);
                    expect(receivedEventProperty.AutomationEvent).to.exist;
                    expect(receivedEventProperty.AutomationProperty).to.exist;
                    expect(receivedEventProperty.AutomationEvent.Id).to.equal(e1.Id);
                    expect(receivedEventProperty.AutomationEvent.Name).to.equal(e1.Name);
                    expect(receivedEventProperty.AutomationProperty.Id).to.equal(p1.Id);
                    expect(receivedEventProperty.AutomationProperty.Name).to.equal(p1.Name);

                    expect(dataReceived.AutomationDriverDefinition.Commands).to.exist;
                    expect(dataReceived.AutomationDriverDefinition.Commands.length).to.equal(1);
                    const receivedCommand: AutomationCommand = dataReceived.AutomationDriverDefinition.Commands[0];
                    expect(receivedCommand.Id).to.equal(c1.Id);
                    expect(receivedCommand.Name).to.equal(c1.Name);
                    expect(receivedCommand.Description).to.equal(c1.Description);
                    expect(receivedCommand.DeviceCommandId).to.equal(c1.DeviceCommandId);
                    expect(receivedCommand.ExtendedData).to.equal(c1.ExtendedData);

                    expect(receivedCommand.Parameters).to.exist;
                    expect(receivedCommand.Parameters.length).to.equal(1);
                    const receivedCommandParameter: AutomationCommandParameter = receivedCommand.Parameters[0];
                    expect(receivedCommandParameter.Id).to.equal(c1p1.Id);
                    expect(receivedCommandParameter.Name).to.equal(c1p1.Name);
                    expect(receivedCommandParameter.Description).to.equal(c1p1.Description);
                    expect(receivedCommandParameter.Order).to.equal(c1p1.Order);
                    expect(receivedCommandParameter.DataType).to.equal(c1p1.DataType);
                    expect(receivedCommandParameter.DefaultValue).to.equal(c1p1.DefaultValue);
                    expect(receivedCommandParameter.ExtendedData).to.equal(c1p1.ExtendedData);
                    expect(receivedCommandParameter.AutomationProtocolDataType.Name).to.equal(c1p1.AutomationProtocolDataType.Name);
                    expect(receivedCommandParameter.IsMandatory).to.equal(c1p1.IsMandatory);

                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to execute a command in driver", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const definitions = this.framework.driver.automationControllerDriverDefinition.AutomationDriverDefinition;
            const command = definitions.Commands[0];
            const parameters = new Map();
            parameters.set("a", "b");
            const reply = await this.framework.driver.executeCommand(command, parameters);

            return { output1: reply };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"].currentValue).to.equal("a=b");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to get properties in driver", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const definitions = this.framework.driver.automationControllerDriverDefinition.AutomationDriverDefinition;
            const property = definitions.Properties[0];
            const reply = await this.framework.driver.getProperties([property]);

            return { output1: reply };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                driverMock.setProperty(p1, "Hello World");

                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);

                    expect(changes["input1"].currentValue).to.exist;
                    expect(changes["input1"].currentValue.length).to.equal(1);
                    const value: EquipmentPropertyValue = changes["input1"].currentValue[0];
                    expect(value.property.Name).to.equal(p1.Name);
                    expect(value.property.Id).to.equal(p1.Id);
                    expect(value.value).to.equal("Hello World");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

    it("should be able to set properties in driver", (done) => {
        const code = codeTemplate.replace("<CODE>", `
            const definitions = this.framework.driver.automationControllerDriverDefinition.AutomationDriverDefinition;
            const property = definitions.Properties[0];
            const values = new Map();
            values.set(property, "Good Bye");
            const reply = await this.framework.driver.setProperties(values);

            return { output1: reply };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            async (values: Map<string, Task.Output<any>>) => {
                await driverMock.setProperty(p1, "Hello World");

                values.get("activate").emit(true);
            },
            async (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);

                    expect(changes["input1"].currentValue).to.exist;
                    expect(changes["input1"].currentValue).to.equal(true);
                    const newValue = await driverMock.getProperty(p1);
                    expect(newValue.property.Name).to.equal(p1.Name);
                    expect(newValue.property.Id).to.equal(p1.Id);
                    expect(newValue.value).to.equal("Good Bye");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });

});
