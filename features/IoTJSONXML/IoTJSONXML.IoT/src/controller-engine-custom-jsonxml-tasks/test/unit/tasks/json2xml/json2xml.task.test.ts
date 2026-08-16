import "reflect-metadata";
import { Task, TaskBaseSettings } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import * as chai from "chai";

import { Json2xmlTask } from "../../../../src/tasks/json2xml/json2xml.task";

describe("Json2xml Task tests", () => {

    interface Json2xmlInputs {
        json?: any;
        key?: string;
        options?: any;
    }

    // eslint-disable-next-line @typescript-eslint/ban-types
    const json2xmlTestFactory = (inputs: Json2xmlInputs, trigger: Function, validate: Function): void => {

        const taskDefinition = {
            class: Json2xmlTask,
            id: "0",
            settings: {
                retries: 0,
                sleepBetweenRetries: 0
            } as TaskBaseSettings
        };

        EngineTestSuite.createTasks([
            taskDefinition,
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
                        this["json"] = new Task.Output<any>();
                        this._outputs.set("json", this["json"]);
                        this["key"] = new Task.Output<string>();
                        this._outputs.set("key", this["key"]);
                        this["options"] = new Task.Output<any>();
                        this._outputs.set("options", this["options"]);
                    }

                    // Trigger the test
                    async onInit(): Promise<void> {
                        this["json"].emit(inputs.json);
                        this["key"].emit(inputs.key ?? "");
                        this["options"].emit(inputs.options);
                        trigger(this._outputs);
                    }

                    // Validate the results
                    async onChanges(changes: Task.Changes): Promise<void> {
                        validate(changes);
                    }
                })
            }
        ], [
            { sourceId: "1", outputName: "activate", targetId: "0", inputName: "activate" },
            { sourceId: "1", outputName: "json", targetId: "0", inputName: "json" },
            { sourceId: "1", outputName: "key", targetId: "0", inputName: "key" },
            { sourceId: "1", outputName: "options", targetId: "0", inputName: "options" },
            { sourceId: "0", outputName: "xml", targetId: "1", inputName: "xml" },
            { sourceId: "0", outputName: "success", targetId: "1", inputName: "success" },
            { sourceId: "0", outputName: "error", targetId: "1", inputName: "error" },
        ], undefined);
    };

    it("should convert a JSON object into XML wrapped by the given key", (done) => {
        json2xmlTestFactory(
            { json: { name: "John", age: 30 }, key: "record" },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (!changes["success"]) {
                    return;
                }
                chai.expect(changes["xml"].currentValue).to.equal("<record><name>John</name><age>30</age></record>");
                chai.expect(changes["success"].currentValue).to.equal(true);
                done();
            });
    });

    it("should convert a JSON object into XML without wrapping when key is empty", (done) => {
        json2xmlTestFactory(
            { json: { root: { name: "John", age: 30 } }, key: "" },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (!changes["success"]) {
                    return;
                }
                chai.expect(changes["xml"].currentValue).to.equal("<root><name>John</name><age>30</age></root>");
                chai.expect(changes["success"].currentValue).to.equal(true);
                done();
            });
    });

    it("should convert the JSON to XML using Cheerio when the useCheerio option is set", (done) => {
        json2xmlTestFactory(
            { json: { name: "John", age: 30 }, key: "record", options: { useCheerio: true } },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (!changes["success"]) {
                    return;
                }
                chai.expect(changes["xml"].currentValue).to.equal("<record><name>John</name><age>30</age></record>");
                chai.expect(changes["success"].currentValue).to.equal(true);
                done();
            });
    });
});
