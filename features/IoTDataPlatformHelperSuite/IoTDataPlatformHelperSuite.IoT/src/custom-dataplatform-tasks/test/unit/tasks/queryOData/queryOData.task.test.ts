import "reflect-metadata";
import "mocha";
import { Task, System } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import * as chai from "chai";
import buildQuery from "odata-query";

import {
    QueryODataTask,
    QueryODataSettings
} from "../../../../src/tasks/queryOData/queryOData.task";

/**
 * Converts the property name into the appropriate input
 * @param name property name
 */
export const inputSuffix: (name: string) => string = (name: string) => `${name}In`;

describe("QueryOData Task tests", () => {
    type TaskTrigger = (outputs: Map<string, Task.Output<any>>) => void;
    type TaskValidate = (changes: Task.Changes) => void;

    const baseSettings: QueryODataSettings = {
        rawQuery: "",
        rawMethod: "",
        folder: "CDM",
        dataset: "Resource",
        defaultSelect: "",
        defaultFilter: "",
        defaultExpand: "",
        defaultOrderBy: "",
        defaultTop: undefined,
        inputs: []
    };

    const originalEnv = {
        hostUrl: process.env["HOSTURL"],
        systemAccessToken: process.env["SYSTEMACCESSTOKEN"],
    };

    let originalFetch: typeof globalThis.fetch;

    beforeEach(() => {
        process.env["HOSTURL"] = "http://test-host";
        process.env["SYSTEMACCESSTOKEN"] = "test-token";
        originalFetch = globalThis.fetch;
    });

    afterEach(() => {
        process.env["HOSTURL"] = originalEnv.hostUrl;
        process.env["SYSTEMACCESSTOKEN"] = originalEnv.systemAccessToken;
        globalThis.fetch = originalFetch;
    });

    const queryODataTestFactory = (settings: QueryODataSettings, trigger: TaskTrigger, validate: TaskValidate): void => {

        const taskDefinition = {
            class: QueryODataTask,
            id: "0",
            settings,
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
                        this["rawQuery"] = new Task.Output<any>();
                        this._outputs.set("rawQuery", this["rawQuery"]);
                        this["rawMethod"] = new Task.Output<any>();
                        this._outputs.set("rawMethod", this["rawMethod"]);
                        this["select"] = new Task.Output<any>();
                        this._outputs.set("select", this["select"]);
                        this["filter"] = new Task.Output<any>();
                        this._outputs.set("filter", this["filter"]);
                        this["expand"] = new Task.Output<any>();
                        this._outputs.set("expand", this["expand"]);
                        this["orderBy"] = new Task.Output<any>();
                        this._outputs.set("orderBy", this["orderBy"]);
                        this["top"] = new Task.Output<any>();
                        this._outputs.set("top", this["top"]);
                        this["skip"] = new Task.Output<any>();
                        this._outputs.set("skip", this["skip"]);
                        this["count"] = new Task.Output<any>();
                        this._outputs.set("count", this["count"]);
                        this["search"] = new Task.Output<any>();
                        this._outputs.set("search", this["search"]);

                        // example autoport defined in the template, simulating an input defined in settings
                        this["machineName"] = new Task.Output<any>();
                        this._outputs.set("machineName", this["machineName"]);

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
            // Inputs from MockTask to QueryODataTask
            { sourceId: "1", outputName: `activate`, targetId: "0", inputName: "activate", },

            { sourceId: "1", outputName: `rawQuery`, targetId: "0", inputName: "rawQuery", },
            { sourceId: "1", outputName: `rawMethod`, targetId: "0", inputName: "rawMethod", },

            { sourceId: "1", outputName: `select`, targetId: "0", inputName: "select", },
            { sourceId: "1", outputName: `filter`, targetId: "0", inputName: "filter", },
            { sourceId: "1", outputName: `expand`, targetId: "0", inputName: "expand", },
            { sourceId: "1", outputName: `orderBy`, targetId: "0", inputName: "orderBy", },
            { sourceId: "1", outputName: `top`, targetId: "0", inputName: "top", },
            { sourceId: "1", outputName: `skip`, targetId: "0", inputName: "skip", },
            { sourceId: "1", outputName: `count`, targetId: "0", inputName: "count", },
            { sourceId: "1", outputName: `search`, targetId: "0", inputName: "search", },

            { sourceId: "1", outputName: `machineName`, targetId: "0", inputName: inputSuffix("machineName"), },

            // Outputs from QueryODataTask to MockTask for validation
            { sourceId: "0", outputName: `value`, targetId: "1", inputName: "value", },
            { sourceId: "0", outputName: `countResponse`, targetId: "1", inputName: "countResponse", },
            { sourceId: "0", outputName: `nextLink`, targetId: "1", inputName: "nextLink", },
            { sourceId: "0", outputName: `rawResponse`, targetId: "1", inputName: "rawResponse", },

            { sourceId: "0", outputName: `success`, targetId: "1", inputName: "success", },
            { sourceId: "0", outputName: `error`, targetId: "1", inputName: "error", },
        ],
            undefined,
            () => undefined);
    };

    it("should replace default values in OData with inputs", (done) => {
        const payload = {
            value: [{ id: 1 }],
            "@odata.count": 5,
            "@odata.nextLink": "next-link"
        };

        globalThis.fetch = (async (url: string, options: RequestInit) => {
            chai.expect(url).to.equal("http://test-host/datamanager/odata/CDM/ResourceAlarm?$select=Enterprise,Site,Facility,Area,Resource,CategoryCode,Type,Severity,Cause&$filter=Resource eq 'SomeMachine'");
            chai.expect(options.method).to.equal("GET");
            chai.expect(options.headers).to.deep.equal({
                Accept: "application/json; odata.metadata=minimal; odata.streaming=true; charset=utf-8",
                Authorization: "Bearer test-token"
            });

            return {
                json: async () => payload,
            } as Response;
        }) as typeof globalThis.fetch;

        const received = {
            rawResponse: false,
            value: false,
            countResponse: false,
            nextLink: false,
            success: false,
        };

        queryODataTestFactory({
            ...baseSettings,
            defaultSelect: "Enterprise,Site,Facility,Area,Resource,CategoryCode,Type,Severity,Cause",
            defaultFilter: "Resource eq '{machineName}'",
            inputs: [
                { name: "machineName", valueType: { type: System.PropertyValueType.String } },
            ],
            dataset: "ResourceAlarm",
        },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("machineName").emit("SomeMachine");

                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                if (changes["rawResponse"]) {
                    chai.expect(changes["rawResponse"].currentValue).to.deep.equal(payload);
                    received.rawResponse = true;
                }

                if (changes["value"]) {
                    chai.expect(changes["value"].currentValue).to.deep.equal(payload.value);
                    received.value = true;
                }

                if (changes["countResponse"]) {
                    chai.expect(changes["countResponse"].currentValue).to.equal(5);
                    received.countResponse = true;
                }

                if (changes["nextLink"]) {
                    chai.expect(changes["nextLink"].currentValue).to.equal("next-link");
                    received.nextLink = true;
                }

                if (changes["success"]) {
                    chai.expect(changes["success"].currentValue).to.equal(true);
                    received.success = true;
                }

                if (changes["error"]) {
                    done(new Error(`Unexpected error output: ${changes["error"].currentValue}`));
                    return;
                }

                if (Object.values(received).every(Boolean)) {
                    done();
                }
            });
    });


    it("should build an OData query from settings defaults when rawQuery is empty", (done) => {
        const payload = {
            value: [],
        };

        const expectedQuery = buildQuery({
            select: "Name,Id",
            filter: undefined,
            expand: undefined,
            orderBy: undefined,
            top: 25,
            skip: undefined,
            count: undefined,
            search: undefined,
        });

        globalThis.fetch = (async (url: string, options: RequestInit) => {
            chai.expect(url).to.equal(`http://test-host/datamanager/odata/CDM/Resource${expectedQuery}`);
            chai.expect(options.method).to.equal("GET");
            return {
                json: async () => payload,
            } as Response;
        }) as typeof globalThis.fetch;

        queryODataTestFactory({
            ...baseSettings,
            defaultSelect: "Name,Id",
            defaultTop: 25,
        },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                if (changes["error"]) {
                    done(new Error(`Unexpected error output: ${changes["error"].currentValue}`));
                    return;
                }

                if (changes["success"]) {
                    chai.expect(changes["success"].currentValue).to.equal(true);
                    done();
                }
            });
    });


    it("should emit error when fetch fails", (done) => {
        globalThis.fetch = (async () => {
            throw new Error("network failure");
        }) as typeof globalThis.fetch;

        queryODataTestFactory({
            ...baseSettings,
            rawQuery: "SomeEntity",
        },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                if (changes["success"]) {
                    done(new Error("Unexpected success output"));
                    return;
                }

                if (changes["error"]) {
                    chai.expect(String(changes["error"].currentValue)).to.contain("network failure");
                    done();
                }
            });
    });

    it("should prioritize runtime inputs over defaults for OData query parts", (done) => {
        const payload = {
            value: [{ id: "result-1" }],
        };

        const expectedQuery = buildQuery({
            select: ["RuntimeName", "Id"],
            filter: "Status eq 'Active'",
            expand: "Children",
            orderBy: "Name asc",
            top: 3,
            skip: 2,
            count: true,
            search: "robot",
        });

        globalThis.fetch = (async (url: string, options: RequestInit) => {
            chai.expect(url).to.equal(`http://test-host/datamanager/odata/CDM/Resource${expectedQuery}`);
            chai.expect(options.method).to.equal("GET");
            return {
                json: async () => payload,
            } as Response;
        }) as typeof globalThis.fetch;

        const received = {
            value: false,
            countResponse: false,
            nextLink: false,
            success: false,
        };

        queryODataTestFactory({
            ...baseSettings,
            defaultSelect: "DefaultName",
            defaultTop: 10,
        },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("select").emit(["RuntimeName", "Id"]);
                outputs.get("filter").emit("Status eq 'Active'");
                outputs.get("expand").emit("Children");
                outputs.get("orderBy").emit("Name asc");
                outputs.get("top").emit(3);
                outputs.get("skip").emit(2);
                outputs.get("count").emit(true);
                outputs.get("search").emit("robot");
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                if (changes["error"]) {
                    done(new Error(`Unexpected error output: ${changes["error"].currentValue}`));
                    return;
                }

                if (changes["value"]) {
                    chai.expect(changes["value"].currentValue).to.deep.equal(payload.value);
                    received.value = true;
                }

                if (changes["countResponse"]) {
                    chai.expect(changes["countResponse"].currentValue).to.equal(null);
                    received.countResponse = true;
                }

                if (changes["nextLink"]) {
                    chai.expect(changes["nextLink"].currentValue).to.equal(null);
                    received.nextLink = true;
                }

                if (changes["success"]) {
                    chai.expect(changes["success"].currentValue).to.equal(true);
                    received.success = true;
                }

                if (Object.values(received).every(Boolean)) {
                    done();
                }
            });
    });

    it("should accept rawQuery and rawMethod from inputs", (done) => {
        globalThis.fetch = (async (url: string, options: RequestInit) => {
            chai.expect(url).to.equal("http://test-host/datamanager/odata/Orders?$top=2");
            chai.expect(options.method).to.equal("PATCH");
            return {
                json: async () => ({ value: [] }),
            } as Response;
        }) as typeof globalThis.fetch;

        queryODataTestFactory({
            ...baseSettings,
        },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("rawQuery").emit("Orders?$top=2");
                outputs.get("rawMethod").emit("PATCH");
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                if (changes["error"]) {
                    done(new Error(`Unexpected error output: ${changes["error"].currentValue}`));
                    return;
                }

                if (changes["success"]) {
                    chai.expect(changes["success"].currentValue).to.equal(true);
                    done();
                }
            });
    });

    it("should use top input with value 0 instead of defaultTop", (done) => {
        const payload = { value: [] };

        const expectedQuery = buildQuery({
            select: ["Name"],
            filter: undefined,
            expand: undefined,
            orderBy: undefined,
            top: 0,
            skip: undefined,
            count: undefined,
            search: undefined,
        });

        globalThis.fetch = (async (url: string) => {
            chai.expect(url).to.equal(`http://test-host/datamanager/odata/CDM/Resource${expectedQuery}`);
            return {
                json: async () => payload,
            } as Response;
        }) as typeof globalThis.fetch;

        queryODataTestFactory({
            ...baseSettings,
            defaultSelect: "Name",
            defaultTop: 50,
        },
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("top").emit(0);
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                if (changes["error"]) {
                    done(new Error(`Unexpected error output: ${changes["error"].currentValue}`));
                    return;
                }

                if (changes["success"]) {
                    chai.expect(changes["success"].currentValue).to.equal(true);
                    done();
                }
            });
    });
});
