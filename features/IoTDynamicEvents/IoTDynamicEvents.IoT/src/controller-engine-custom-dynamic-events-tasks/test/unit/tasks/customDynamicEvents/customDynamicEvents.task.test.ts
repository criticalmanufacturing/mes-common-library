import "reflect-metadata";
import { Task, System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/driver-proxy.mock";
import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/data-store.mock";
import { expect } from "chai";

import {
    CustomDynamicEventsTask,
    CustomDynamicEventsSettings
} from "../../../../src/tasks/customDynamicEvents/customDynamicEvents.task";
import { ActionType } from "../../../../src/types/types";

describe("CustomDynamicEvents Task tests", () => {

    let driverMock: DriverProxyMock;
    let dataStoreMock: DataStoreMock;
    let notifications: { type: string; message: any }[];

    beforeEach(() => {
        // Driver definition fixtures: one event template with one property tagged with the "{TAG}" token,
        // and a concrete "Res1.Value" property that the token can resolve to.
        // Rebuilt on every test: the task under test mutates these objects in place while registering.
        const propertyRes1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty();
        propertyRes1.Name = "Res1.Value";
        propertyRes1.DevicePropertyId = "dev1";
        propertyRes1.DataType = System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType.Integer;
        propertyRes1.AutomationProtocolDataType = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocolDataType();
        (<any>propertyRes1.AutomationProtocolDataType).Name = "int";

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
            properties: [propertyRes1],
            commands: [],
            eventProperties: [eventPropertyTemplate]
        });
        driverMock.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol =
            new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocol();
        driverMock.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol.Package = "opcua-mydriver";

        // Populated only by tests that override driverMock.notifyRaw to capture "Register" notifications.
        // Left as the real DriverProxyMock implementation otherwise, so "Subscribe" tests can use
        // driverMock.notifyRaw(...) to simulate an incoming driver message reaching a subscribed callback.
        notifications = [];

        dataStoreMock = new DataStoreMock();
    });

    const REGISTER_SETTINGS: CustomDynamicEventsSettings = {
        eventTemplateName: "MyEvent",
        eventTagToken: "{TAG}",
        eventTagAliasName: "AutomationAlias",
        resourceTypePersistedAlias: "ResourceType",
        autoActivate: false,
        actionType: ActionType.Register
    };

    const SUBSCRIBE_SETTINGS: CustomDynamicEventsSettings = {
        eventTemplateName: "MyEvent",
        eventTagToken: "{TAG}",
        eventTagAliasName: "AutomationAlias",
        resourceTypePersistedAlias: "ResourceType",
        autoActivate: false,
        actionType: ActionType.Subscribe
    };

    /**
     * Instructions about the tests
     * It is assumed that there are two tasks:
     *    0 - CustomDynamicEvents Task
     *    1 - Mockup task
     *
     * All Outputs of Mock task are connected to the inputs of the CustomDynamicEvents task
     * All Outputs of CustomDynamicEvents Task are connected to the Mock task inputs
     */
    const customDynamicEventsTestFactory = (
        settings: CustomDynamicEventsSettings,
        trigger: (outputs: Map<string, Task.Output<any>>) => void,
        validate: (changes: Task.Changes) => void
    ): void => {

        EngineTestSuite.createTasks([
            {
                class: CustomDynamicEventsTask,
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
            { sourceId: "0", outputName: "resource", targetId: "1", inputName: "resource", },
            { sourceId: "0", outputName: "resourceType", targetId: "1", inputName: "resourceType", },
            { sourceId: "0", outputName: "eventTemplate", targetId: "1", inputName: "eventTemplate", },
            { sourceId: "0", outputName: "value", targetId: "1", inputName: "value", },
        ],
        driverMock,
        (container) => {
            container.unbind(TYPES.System.PersistedDataStore);
            container.bind(TYPES.System.PersistedDataStore).toConstantValue(dataStoreMock);
        });
    };

    describe("actionType = Register", () => {

        it("should register the event for the resource and emit success", (done) => {
            dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);
            driverMock.notifyRaw = async (type: string, message: any): Promise<void> => {
                notifications.push({ type, message });
            };

            customDynamicEventsTestFactory(
                REGISTER_SETTINGS,
                (outputs: Map<string, Task.Output<any>>) => {
                    outputs.get("activate").emit(true);
                },
                (changes: Task.Changes) => {
                    if (changes["success"]) {
                        expect(changes["success"].currentValue).to.equal(true);

                        expect(notifications).to.have.lengthOf(1);
                        // Unlike the standalone CustomDynamicEventTaskRegister task, this combined task's
                        // "Register" branch never resolves the "_DRIVER_" token in the notification subject.
                        expect(notifications[0].type).to.equal("connect.iot.driver._DRIVER_.registerEvent");
                        expect(notifications[0].message.event.name).to.equal("MyEvent_Res1");
                        expect(notifications[0].message.event.properties[0].name).to.equal("Res1.Value");
                        expect(notifications[0].message.event.properties[0].deviceId).to.equal("dev1");
                        done();
                    }
                }
            );
        });

        it("should emit an error when there is no parent resource stored", (done) => {
            driverMock.notifyRaw = async (type: string, message: any): Promise<void> => {
                notifications.push({ type, message });
            };

            customDynamicEventsTestFactory(
                REGISTER_SETTINGS,
                (outputs: Map<string, Task.Output<any>>) => {
                    outputs.get("activate").emit(true);
                },
                (changes: Task.Changes) => {
                    if (changes["error"]) {
                        expect(changes["error"].currentValue).to.be.instanceOf(Error);
                        expect(changes["error"].currentValue.message).to.equal(
                            "Something went wrong performing the 'Register' action : Error registering custom events: Unknown Parent Resource"
                        );
                        expect(notifications).to.have.lengthOf(0);
                        done();
                    }
                }
            );
        });
    });

    describe("actionType = Subscribe", () => {

        it("should subscribe and emit outputs once a matching event is received", (done) => {
            dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);
            dataStoreMock.set("ResourceType", "RT1", System.DataStoreLocation.Persistent);

            customDynamicEventsTestFactory(
                SUBSCRIBE_SETTINGS,
                (outputs: Map<string, Task.Output<any>>) => {
                    outputs.get("activate").emit(true);
                    // Subscribing does not emit "success" by itself for this task/action, so simulate the
                    // driver event shortly after activation instead of waiting on a "success" change.
                    setTimeout(() => {
                        driverMock.notifyRaw("connect.iot.driver.mydriver.registerEvent.MyEvent_Res1", {
                            eventName: "MyEvent_Res1",
                            propertyValues: [{ propertyName: "Value", value: 123, originalValue: 123 }]
                        });
                    }, 200);
                },
                (changes: Task.Changes) => {
                    if (changes["value"] !== undefined) {
                        expect(changes["value"].currentValue).to.deep.equal([{ Name: "Value", Value: 123 }]);
                        expect(changes["eventTemplate"].currentValue).to.equal("MyEvent");
                        expect(changes["resource"].currentValue).to.equal("Res1");
                        expect(changes["resourceType"].currentValue).to.equal("RT1");
                        expect(changes["success"].currentValue).to.equal(true);
                        done();
                    }
                }
            );
        });

        it("should emit an error when the driver subscription fails", (done) => {
            dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);
            driverMock.subscribeRaw = () => { throw new Error("boom"); };

            customDynamicEventsTestFactory(
                SUBSCRIBE_SETTINGS,
                (outputs: Map<string, Task.Output<any>>) => {
                    outputs.get("activate").emit(true);
                },
                (changes: Task.Changes) => {
                    if (changes["error"]) {
                        expect(changes["error"].currentValue).to.be.instanceOf(Error);
                        expect(changes["error"].currentValue.message).to.equal(
                            "Something went wrong performing the 'Subscribe' action : boom"
                        );
                        done();
                    }
                }
            );
        });
    });
});
