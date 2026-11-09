import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/dist/test/mocks/driver-proxy.mock";
import { expect } from "chai";

import {
    CustomDriverSubscribeTask,
    CustomDriverSubscribeSettings
} from "../../../../src/tasks/customDriverSubscribe/customDriverSubscribe.task";

describe("CustomDriverSubscribe Task tests", () => {

    let driverMock: DriverProxyMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
    });

    /**
     * Instructions about the tests
     * It is assumed that there are two tasks:
     *    0 - CustomDriverSubscribe Task
     *    1 - Mockup task
     *
     * CustomDriverSubscribe is placed first in the array so that it can subscribe to the
     * driver communication (during its own onInit) before the Mock task's onInit fires the trigger.
     *
     * All Outputs of Mock task are connected to the inputs of the CustomDriverSubscribe task
     * All Outputs of CustomDriverSubscribe Task are connected to the Mock task inputs
     */
    const customDriverSubscribeTestFactory = (
        settings: CustomDriverSubscribeSettings,
        trigger: (outputs: Map<string, Task.Output<any>>) => void,
        validate: (changes: Task.Changes) => void
    ): void => {

        EngineTestSuite.createTasks([
            {
                class: CustomDriverSubscribeTask,
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

                    // Trigger the test
                    async onInit(): Promise<void> {
                        trigger(this._outputs);
                    }

                    // Validate the results
                    async onChanges(changes: Task.Changes): Promise<void> {
                        validate(changes);
                    }
                })
            }
        ], [
            { sourceId: "1", outputName: "activate", targetId: "0", inputName: "activate", },
            { sourceId: "0", outputName: "success", targetId: "1", inputName: "success", },
            { sourceId: "0", outputName: "error", targetId: "1", inputName: "error", },
            { sourceId: "0", outputName: "type", targetId: "1", inputName: "type", },
            { sourceId: "0", outputName: "message", targetId: "1", inputName: "message", },
        ],
        driverMock);
    };

    it("should auto-subscribe on init and emit success when autoActivate is true", (done) => {
        customDriverSubscribeTestFactory(
            { autoActivate: true, messageType: "TypeA" } as CustomDriverSubscribeSettings,
            () => {
                // No manual trigger needed: the task subscribes automatically during its own onInit
            },
            (changes: Task.Changes) => {
                if (changes["success"]) {
                    expect(changes["success"].currentValue).to.equal(true);
                    done();
                }
            }
        );
    });

    it("should subscribe and emit success when manually activated (autoActivate false)", (done) => {
        customDriverSubscribeTestFactory(
            { autoActivate: false, messageType: "TypeA" } as CustomDriverSubscribeSettings,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"]) {
                    expect(changes["success"].currentValue).to.equal(true);
                    done();
                }
            }
        );
    });

    it("should subscribe to every trimmed, non-empty message type in a comma-separated list", (done) => {
        let sawSuccess = false;

        customDriverSubscribeTestFactory(
            { autoActivate: false, messageType: " TypeA , TypeB ," } as CustomDriverSubscribeSettings,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["success"] && !sawSuccess) {
                    sawSuccess = true;
                    // Simulate a message on each configured (trimmed) type
                    driverMock.notifyRaw("TypeA", { hello: "a" });
                    return;
                }
                if (changes["type"] !== undefined) {
                    if (changes["type"].currentValue === "TypeA") {
                        expect(changes["message"].currentValue).to.deep.equal({ hello: "a" });
                        driverMock.notifyRaw("TypeB", { hello: "b" });
                    } else {
                        expect(changes["type"].currentValue).to.equal("TypeB");
                        expect(changes["message"].currentValue).to.deep.equal({ hello: "b" });
                        done();
                    }
                }
            }
        );
    });

    it("should log and emit an error when the driver subscription fails", (done) => {
        driverMock.subscribeRaw = () => { throw new Error("boom"); };

        customDriverSubscribeTestFactory(
            { autoActivate: false, messageType: "TypeA" } as CustomDriverSubscribeSettings,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (changes["error"]) {
                    expect(changes["error"].currentValue).to.be.instanceOf(Error);
                    expect(changes["error"].currentValue.message).to.equal(
                        "Failed to subscribe topic 'TypeA' in driver communication: boom"
                    );
                    done();
                }
            }
        );
    });
});
