import "reflect-metadata";
import { Task, System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/driver-proxy.mock";
import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/data-store.mock";
import { expect } from "chai";

import {
    CustomDynamicEventTaskRegisterTask,
    CustomDynamicEventTaskRegisterSettings
} from "../../../../src/tasks/customDynamicEventTaskRegister/customDynamicEventTaskRegister.task";

describe("CustomDynamicEventTaskRegister Task tests", () => {

    let driverMock: DriverProxyMock;
    let dataStoreMock: DataStoreMock;
    let notifications: { type: string; message: any }[];

    beforeEach(() => {
        // Driver definition fixtures: one event template with one property tagged with the "{TAG}" token,
        // and two concrete properties ("Res1.Value" / "Res2.Value") that the token can resolve to.
        // Rebuilt on every test: the task under test mutates these objects in place while registering.
        const propertyRes1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty();
        propertyRes1.Name = "Res1.Value";
        propertyRes1.DevicePropertyId = "dev1";
        propertyRes1.DataType = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType.Integer;
        propertyRes1.AutomationProtocolDataType = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocolDataType();
        (<any>propertyRes1.AutomationProtocolDataType).Name = "int";

        const propertyRes2 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty();
        propertyRes2.Name = "Res2.Value";
        propertyRes2.DevicePropertyId = "dev2";
        propertyRes2.DataType = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType.Integer;
        propertyRes2.AutomationProtocolDataType = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocolDataType();
        (<any>propertyRes2.AutomationProtocolDataType).Name = "int";

        const automationEvent = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent();
        automationEvent.Name = "MyEvent";

        const eventPropertyTemplate = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty();
        eventPropertyTemplate.Name = "MyEvent_{TAG}_Value";
        eventPropertyTemplate.Order = 1;
        (<any>eventPropertyTemplate).AutomationEvent = automationEvent;
        (<any>eventPropertyTemplate).AutomationProperty = { Name: "{TAG}.Value" };

        driverMock = new DriverProxyMock();
        driverMock.setMockDefinitions({
            events: [automationEvent],
            properties: [propertyRes1, propertyRes2],
            commands: [],
            eventProperties: [eventPropertyTemplate]
        });
        driverMock.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol =
            new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocol();
        driverMock.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol.Package = "opcua-mydriver";

        notifications = [];
        driverMock.notifyRaw = async (type: string, message: any): Promise<void> => {
            notifications.push({ type, message });
        };

        dataStoreMock = new DataStoreMock();
    });

    const DEFAULT_SETTINGS: CustomDynamicEventTaskRegisterSettings = {
        eventTemplateName: "MyEvent",
        eventTagToken: "{TAG}",
        eventTagAliasName: "AutomationAlias",
        resourceNamePersistedAlias: "ResourceName",
        resourceTypePersistedAlias: "ResourceType",
        subResourceRelationPersistedAlias: "AutomationControllerResourceAssociations_",
        autoActivate: false
    };

    /**
     * Instructions about the tests
     * It is assumed that there are two tasks:
     *    0 - CustomDynamicEventTaskRegister Task
     *    1 - Mockup task
     *
     * All Outputs of Mock task are connected to the inputs of the CustomDynamicEventTaskRegister task
     * All Outputs of CustomDynamicEventTaskRegister Task are connected to the Mock task inputs
     */
    const customDynamicEventTaskRegisterTestFactory = (
        settings: CustomDynamicEventTaskRegisterSettings,
        trigger: (outputs: Map<string, Task.Output<any>>) => void,
        validate: (changes: Task.Changes) => void
    ): void => {

        EngineTestSuite.createTasks([
            {
                class: CustomDynamicEventTaskRegisterTask,
                id: "0",
                settings
            },
            {
                id: "1",
                class: Task.Task({
                    name: "mockTask"
                })(class MockTask implements Task.TaskInstance {
                    [key: string]: any;
                    _outputs: Map<string, Task.Output<any>> = new Map<string, Task.Output<any>>();

                    async onBeforeInit(): Promise<void> {
                        this["activate"] = new Task.Output<any>();
                        this._outputs.set("activate", this["activate"]);
                    }

                    async onInit(): Promise<void> {
                        trigger(this._outputs);
                    }

                    async onChanges(changes: Task.Changes): Promise<void> {
                        validate(changes);
                    }
                })
            }
        ], [
            { sourceId: "1", outputName: "activate", targetId: "0", inputName: "activate", },
            { sourceId: "0", outputName: "success", targetId: "1", inputName: "success", },
            { sourceId: "0", outputName: "error", targetId: "1", inputName: "error", },
        ],
        driverMock,
        (container) => {
            container.unbind(TYPES.System.PersistedDataStore);
            container.bind(TYPES.System.PersistedDataStore).toConstantValue(dataStoreMock);
        });
    };

    it("should register a single event for the resource when there is no sub-resource relation", (done) => {
        dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);

        customDynamicEventTaskRegisterTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"]) {
                    expect(changes["success"].currentValue).to.equal(true);

                    expect(notifications).to.have.lengthOf(1);
                    expect(notifications[0].type).to.equal("connect.iot.driver.mydriver.registerEvent");
                    expect(notifications[0].message.event.name).to.equal("MyEvent_Res1");
                    expect(notifications[0].message.event.isEnabled).to.equal(true);
                    expect(notifications[0].message.event.properties).to.have.lengthOf(1);
                    expect(notifications[0].message.event.properties[0].name).to.equal("Res1.Value");
                    expect(notifications[0].message.event.properties[0].deviceId).to.equal("dev1");
                    done();
                }
            }
        );
    });

    it("should register one event per sub-resource when a sub-resource relation exists", (done) => {
        dataStoreMock.set("ResourceName", "Parent", System.DataStoreLocation.Persistent);
        dataStoreMock.set("AutomationControllerResourceAssociations_Parent", [
            ["Res1", { AutomationAlias: "Res1" }],
            ["Res2", { AutomationAlias: "Res2" }]
        ], System.DataStoreLocation.Persistent);

        customDynamicEventTaskRegisterTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"]) {
                    expect(changes["success"].currentValue).to.equal(true);

                    expect(notifications).to.have.lengthOf(2);
                    expect(notifications.map(n => n.message.event.name).sort()).to.deep.equal(["MyEvent_Res1", "MyEvent_Res2"]);
                    expect(notifications.find(n => n.message.event.name === "MyEvent_Res1").message.event.properties[0].deviceId).to.equal("dev1");
                    expect(notifications.find(n => n.message.event.name === "MyEvent_Res2").message.event.properties[0].deviceId).to.equal("dev2");
                    done();
                }
            }
        );
    });

    it("should log and emit an error when there is no parent resource stored", (done) => {
        // "ResourceName" was never stored in the data store

        customDynamicEventTaskRegisterTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["error"]) {
                    expect(changes["error"].currentValue).to.be.instanceOf(Error);
                    expect(changes["error"].currentValue.message).to.equal(
                        "Failed to register events in driver communication: Error registering custom events: Unknown Parent Resource"
                    );
                    expect(notifications).to.have.lengthOf(0);
                    done();
                }
            }
        );
    });
});
