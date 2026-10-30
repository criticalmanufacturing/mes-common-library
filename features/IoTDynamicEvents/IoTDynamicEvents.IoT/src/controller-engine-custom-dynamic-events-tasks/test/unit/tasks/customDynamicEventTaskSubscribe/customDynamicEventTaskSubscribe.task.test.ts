import "reflect-metadata";
import { Task, System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/driver-proxy.mock";
import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/data-store.mock";
import { expect } from "chai";

import {
    CustomDynamicEventTaskSubscribeTask,
    CustomDynamicEventTaskSubscribeSettings
} from "../../../../src/tasks/customDynamicEventTaskSubscribe/customDynamicEventTaskSubscribe.task";

describe("CustomDynamicEventTaskSubscribe Task tests", () => {

    let driverMock: DriverProxyMock;
    let dataStoreMock: DataStoreMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
        driverMock.setMockDefinitions({ events: [], properties: [], commands: [], eventProperties: [] });
        driverMock.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol =
            new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocol();
        driverMock.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol.Package = "opcua-mydriver";

        dataStoreMock = new DataStoreMock();
    });

    const DEFAULT_SETTINGS: CustomDynamicEventTaskSubscribeSettings = {
        eventTemplateName: "MyEvent",
        resourceNamePersistedAlias: "ResourceName",
        resourceTypePersistedAlias: "ResourceType",
        subResourceRelationPersistedAlias: "AutomationControllerResourceAssociations_",
        autoActivate: false
    };

    /**
     * Instructions about the tests
     * It is assumed that there are two tasks:
     *    0 - CustomDynamicEventTaskSubscribe Task
     *    1 - Mockup task
     *
     * All Outputs of Mock task are connected to the inputs of the CustomDynamicEventTaskSubscribe task
     * All Outputs of CustomDynamicEventTaskSubscribe Task are connected to the Mock task inputs
     */
    const customDynamicEventTaskSubscribeTestFactory = (
        settings: CustomDynamicEventTaskSubscribeSettings,
        trigger: (outputs: Map<string, Task.Output<any>>) => void,
        validate: (changes: Task.Changes) => void
    ): void => {

        EngineTestSuite.createTasks([
            {
                class: CustomDynamicEventTaskSubscribeTask,
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

    it("should subscribe and, once an event arrives, emit the resource/value outputs (no sub-resource relation)", (done) => {
        dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);
        dataStoreMock.set("ResourceType", "RT1", System.DataStoreLocation.Persistent);

        let subscribed = false;

        customDynamicEventTaskSubscribeTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"] && !subscribed) {
                    subscribed = true;
                    // Simulate the driver notifying the event the task just subscribed to
                    driverMock.notifyRaw("connect.iot.driver.mydriver.registerEvent.MyEvent_Res1", {
                        eventName: "MyEvent_Res1",
                        propertyValues: [{ propertyName: "Value", value: 123, originalValue: 123 }]
                    });
                    return;
                }
                if (changes["value"] !== undefined) {
                    // "success" already switched to true right after subscribing (above); re-emitting the same
                    // value does not surface as a "change" again, so it is not asserted in this second batch.
                    expect(changes["value"].currentValue).to.deep.equal([{ Name: "Value", Value: 123 }]);
                    expect(changes["eventTemplate"].currentValue).to.equal("MyEvent");
                    expect(changes["resource"].currentValue).to.equal("Res1");
                    expect(changes["resourceType"].currentValue).to.equal("RT1");
                    done();
                }
            }
        );
    });

    it("should subscribe to one event per sub-resource and resolve each resource's own type", (done) => {
        dataStoreMock.set("ResourceName", "Parent", System.DataStoreLocation.Persistent);
        dataStoreMock.set("AutomationControllerResourceAssociations_Parent", [
            ["Res1", { ResourceType: "RT1" }],
            ["Res2", { ResourceType: "RT2" }]
        ], System.DataStoreLocation.Persistent);

        let subscribed = false;

        customDynamicEventTaskSubscribeTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"] && !subscribed) {
                    subscribed = true;
                    driverMock.notifyRaw("connect.iot.driver.mydriver.registerEvent.MyEvent_Res2", {
                        eventName: "MyEvent_Res2",
                        propertyValues: [{ propertyName: "Value", value: 456, originalValue: 456 }]
                    });
                    return;
                }
                if (changes["value"] !== undefined) {
                    expect(changes["resource"].currentValue).to.equal("Res2");
                    expect(changes["resourceType"].currentValue).to.equal("RT2");
                    expect(changes["value"].currentValue).to.deep.equal([{ Name: "Value", Value: 456 }]);
                    done();
                }
            }
        );
    });

    it("should not emit outputs for an event with no property values", (done) => {
        dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);

        let subscribed = false;

        customDynamicEventTaskSubscribeTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"] && !subscribed) {
                    subscribed = true;
                    driverMock.notifyRaw("connect.iot.driver.mydriver.registerEvent.MyEvent_Res1", {
                        eventName: "MyEvent_Res1",
                        propertyValues: []
                    });

                    setTimeout(() => done(), 300);
                    return;
                }
                if (changes["value"] !== undefined) {
                    throw new Error("Should not have emitted any outputs for an empty event");
                }
            }
        );
    });

    it("should emit an error when the driver subscription fails", (done) => {
        dataStoreMock.set("ResourceName", "Res1", System.DataStoreLocation.Persistent);
        driverMock.subscribeRaw = () => { throw new Error("boom"); };

        customDynamicEventTaskSubscribeTestFactory(
            DEFAULT_SETTINGS,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["error"]) {
                    expect(changes["error"].currentValue).to.be.instanceOf(Error);
                    expect(changes["error"].currentValue.message).to.equal(
                        "Something went wrong performing the Subscribe action"
                    );
                    done();
                }
            }
        );
    });
});
