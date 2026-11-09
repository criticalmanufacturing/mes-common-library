import "reflect-metadata";
import { Task, System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/driver-proxy.mock";
import { Buffer } from "buffer";
import { expect } from "chai";

import {
    EquipmentEventWithDataplatformTask,
    EquipmentEventOutputType,
    EquipmentEventWorkingMode,
} from "../../../../src/tasks/equipmentEventWithDataplatform/equipmentEventWithDataplatform.task";
import { BatchPostEventHandler } from "../../../../src/context/batchPostEventHandler";

describe("Equipment Event tests", () => {

    let driverMock: DriverProxyMock;
    const variable = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty();
    const raw = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty();
    const event1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent();
    const eventVariable1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty();
    const eventRaw1 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty();
    const event2 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent();
    const eventVariable2 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty();
    const eventRaw2 = new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty();

    before(() => {
        variable.Id = "variable";
        variable.DevicePropertyId = "variable";
        variable.Name = "variable";

        raw.Id = "raw";
        raw.DevicePropertyId = "raw";
        raw.Name = "raw";

        event1.Id = "1001";
        event1.Name = "onDataAvailable1";
        event1.DeviceEventId = "12";
        event1.IsEnabled = true;

        eventVariable1.Id = "evtProp11";
        eventVariable1.Name = eventVariable1.Id;
        eventVariable1.Order = 1;
        (<any>eventVariable1).AutomationEvent = event1;
        (<any>eventVariable1).AutomationProperty = variable;

        eventRaw1.Id = "evtProp12";
        eventRaw1.Name = eventRaw1.Id;
        eventRaw1.Order = 2;
        (<any>eventRaw1).AutomationEvent = event1;
        (<any>eventRaw1).AutomationProperty = raw;


        event2.Id = "1002";
        event2.Name = "onDataAvailable2";
        event2.DeviceEventId = "22";
        event2.IsEnabled = true;

        eventVariable2.Id = "evtProp21";
        eventVariable2.Name = eventVariable2.Id;
        eventVariable2.Order = 1;
        (<any>eventVariable2).AutomationEvent = event2;
        (<any>eventVariable2).AutomationProperty = variable;

        eventRaw2.Id = "evtProp22";
        eventRaw2.Name = eventRaw2.Id;
        eventRaw2.Order = 2;
        (<any>eventRaw2).AutomationEvent = event2;
        (<any>eventRaw2).AutomationProperty = raw;
    });

    beforeEach(() => {
        driverMock = new DriverProxyMock();
        driverMock.setMockDefinitions({
            events: [event1, event2],
            properties: [variable, raw],
            commands: [],
            eventProperties: [eventVariable1, eventRaw1, eventVariable2, eventRaw2],
        });
    });

    const equipmentEventTestFactory = (
        events: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent[],
        eventProperties: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty[],
        workingMode: EquipmentEventWorkingMode.OneEvent,
        trigger: Function,
        validate: (changes: Task.Changes
        ) => void): void => {
        /**
         *  +------------------------+      +------------------+
         *  | ==EquipmentEvent (1)== |      |==mockTask (0)==  |
         *  |               event () | ---> | () testEvent     |
         *  |           timestamp () | ---> | () testTimestamp |
         *  |           $variable () | ---> | () testVariable  |
         *  |                $raw () | ---> | () testRaw       |
         *  +------------------------+      +------------------+
         */

        EngineTestSuite.createTasks([
            {
                class: EquipmentEventWithDataplatformTask,
                id: "1",
                settings: {
                    _events: events,
                    _outputs: [
                        { name: eventVariable1.Name, outputType: EquipmentEventOutputType.Value },
                        { name: eventRaw1.Name, outputType: EquipmentEventOutputType.RawValue },
                    ],
                    _automationEventsProperties: eventProperties,
                    _workingMode: workingMode
                }
            },
            {
                id: "0",
                class: Task.Task({
                    name: "mockTask"
                })(class MockTask implements Task.TaskInstance {
                    // Input (validate results)
                    testEvent: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent;
                    testTimestamp: Date;
                    testVariable: any;
                    testRaw: any;

                    async onChanges(changes: Task.Changes): Promise<void> {
                        if (validate) {
                            validate(changes);
                        }
                    }

                    async onInit(): Promise<void> {
                    }

                    async onDestroy(): Promise<void> {
                    }
                })
            }
        ], [
            { sourceId: "1", outputName: "event", targetId: "0", inputName: "testEvent", },
            { sourceId: "1", outputName: "timestamp", targetId: "0", inputName: "testTimestamp", },
            { sourceId: "1", outputName: "$evtProp11", targetId: "0", inputName: "testVariable", },
            { sourceId: "1", outputName: "$evtProp12", targetId: "0", inputName: "testRaw", },
        ], driverMock, (container) => {
            // Normally registered by @Task.TaskModule's providers when the task is loaded through
            // the package manager. EngineTestSuite.createTasks constructs the task class directly,
            // bypassing that, so the "GlobalBatchPostEventHandler" symbol has to be bound by hand.
            container.bind("GlobalBatchPostEventHandler").toConstantValue(new BatchPostEventHandler());
            container.bind(TYPES.Values.Controller).toConstantValue({ Id: "TestController", Name: "TestController" });
        });

        if (trigger) {
            setTimeout(() => {
                trigger();
            }, 500);
        }
    };

    it("should be able to trigger on an event occurrence", (done) => {
        equipmentEventTestFactory([event1], [eventVariable1, eventRaw1], EquipmentEventWorkingMode.OneEvent, () => {
            const values = [
                {
                    property: variable,
                    value: 123,
                    originalValue: Buffer.from([1, 2, 3])
                },
                {
                    property: raw,
                    value: 321,
                    originalValue: Buffer.from([3, 2, 1])
                }
            ];

            driverMock.triggerEventOccurrence(event1, new Date(), values);
        }, (changes: Task.Changes) => {
            const eventValue = changes["testEvent"];
            const timestampValue = changes["testTimestamp"];
            const variableValue = changes["testVariable"];
            const rawValue = changes["testRaw"];

            // Validations
            expect(eventValue).to.exist;
            expect(timestampValue).to.exist;
            expect(variableValue).not.to.exist; // No output should exist
            expect(rawValue).not.to.exist;

            expect(eventValue.currentValue.DeviceEventId).to.equal(event1.DeviceEventId);

            done();
        });
    });

    it("should be able to trigger on an event occurrence when all are registered", (done) => {
        equipmentEventTestFactory([], [], EquipmentEventWorkingMode.OneEvent, () => {
            const values = [
                {
                    property: variable,
                    value: 123,
                    originalValue: Buffer.from([1, 2, 3])
                },
                {
                    property: raw,
                    value: 321,
                    originalValue: Buffer.from([3, 2, 1])
                }
            ];

            driverMock.triggerEventOccurrence(event1, new Date(), values);
        }, (changes: Task.Changes) => {
            const eventValue = changes["testEvent"];
            const timestampValue = changes["testTimestamp"];
            const variableValue = changes["testVariable"];
            const rawValue = changes["testRaw"];

            // Validations
            expect(eventValue).to.exist;
            expect(timestampValue).to.exist;
            expect(variableValue).not.to.exist; // No output should exist
            expect(rawValue).not.to.exist;

            expect(eventValue.currentValue.DeviceEventId).to.equal(event1.DeviceEventId);

            done();
        });
    });

    it("should be able to trigger on an event occurrence when all are registered (other event)", (done) => {
        equipmentEventTestFactory([], [], EquipmentEventWorkingMode.OneEvent, () => {
            const values = [
                {
                    property: variable,
                    value: 123,
                    originalValue: Buffer.from([1, 2, 3])
                },
                {
                    property: raw,
                    value: 321,
                    originalValue: Buffer.from([3, 2, 1])
                }
            ];

            driverMock.triggerEventOccurrence(event2, new Date(), values);
        }, (changes: Task.Changes) => {
            const eventValue = changes["testEvent"];
            const timestampValue = changes["testTimestamp"];
            const variableValue = changes["testVariable"];
            const rawValue = changes["testRaw"];

            // Validations
            expect(eventValue).to.exist;
            expect(timestampValue).to.exist;
            expect(variableValue).not.to.exist; // No output should exist
            expect(rawValue).not.to.exist;

            expect(eventValue.currentValue.DeviceEventId).to.equal(event2.DeviceEventId);

            done();
        });
    });

});
