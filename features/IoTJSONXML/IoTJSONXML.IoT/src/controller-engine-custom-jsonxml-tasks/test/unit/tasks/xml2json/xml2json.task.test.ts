import "reflect-metadata";
import { Task, TaskBaseSettings } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import * as chai from "chai";

import { Xml2jsonTask } from "../../../../src/tasks/xml2json/xml2json.task";

describe("Xml2json Task tests", () => {

    interface Xml2jsonInputs {
        xml?: string;
        options?: any;
    }

    // eslint-disable-next-line @typescript-eslint/ban-types
    const xml2jsonTestFactory = (inputs: Xml2jsonInputs, trigger: Function, validate: Function): void => {

        const taskDefinition = {
            class: Xml2jsonTask,
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
                        this["xml"] = new Task.Output<string>();
                        this._outputs.set("xml", this["xml"]);
                        this["options"] = new Task.Output<any>();
                        this._outputs.set("options", this["options"]);
                    }

                    // Trigger the test
                    async onInit(): Promise<void> {
                        this["xml"].emit(inputs.xml);
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
            { sourceId: "1", outputName: "xml", targetId: "0", inputName: "xml" },
            { sourceId: "1", outputName: "options", targetId: "0", inputName: "options" },
            { sourceId: "0", outputName: "json", targetId: "1", inputName: "json" },
            { sourceId: "0", outputName: "success", targetId: "1", inputName: "success" },
            { sourceId: "0", outputName: "error", targetId: "1", inputName: "error" },
        ], undefined);
    };

    it("should convert an XML string into a JSON object", (done) => {
        xml2jsonTestFactory(
            { xml: "<record><name>John</name><age>30</age></record>" },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (!changes["success"]) {
                    return;
                }
                chai.expect(changes["json"].currentValue).to.deep.equal({ record: { name: "John", age: 30 } });
                chai.expect(changes["success"].currentValue).to.equal(true);
                done();
            });
    });

    it("should apply the provided parser options when converting", (done) => {
        xml2jsonTestFactory(
            { xml: "<record><name>John</name><age>30</age></record>", options: { ignoreAttributes: true, parseTagValue: false } },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (!changes["success"]) {
                    return;
                }
                chai.expect(changes["json"].currentValue).to.deep.equal({ record: { name: "John", age: "30" } });
                chai.expect(changes["success"].currentValue).to.equal(true);
                done();
            });
    });

    it("should emit an error when the XML input cannot be parsed", (done) => {
        xml2jsonTestFactory(
            { xml: undefined },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                if (!changes["error"]) {
                    return;
                }
                chai.expect(changes["error"].currentValue).to.be.an.instanceof(Error);
                chai.expect(changes["error"].currentValue.message).to.include("Error converting XML to JSON:");
                done();
            });
    });
});
