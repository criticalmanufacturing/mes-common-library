import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import * as chai from "chai";

import { SQLiteManagerMock, GLOBAL_SQLITE_MANAGER_SYMBOL } from "../task-test-utils";
import {
    StoreSQLiteTask,
    StoreSQLiteSettings
} from "../../../../src/tasks/storeSQLite/storeSQLite.task";

/**
 * Wires up StoreSQLiteTask (id "0") against a MockTask (id "1").
 *
 * Each entry of the `inputs` setting is a *dynamic* port: the task reads its data straight off
 * `this[storeElement.name]` (no decorator, no "In" suffix) - see StoreSQLiteTask.onChanges. `extraLinks`
 * lets a test feed one of those ad-hoc inputs from the mock task.
 */
function createTaskTest(
    settings: Partial<StoreSQLiteSettings>,
    sqliteManagerMock: SQLiteManagerMock,
    trigger: (outputs: Map<string, Task.Output<any>>) => void,
    validate: (changes: Task.Changes) => void,
    extraLinks: Task.LinkDefinition[] = []
): void {
    EngineTestSuite.createTasks(
        [
            { class: StoreSQLiteTask, id: "0", settings: settings as StoreSQLiteSettings },
            {
                id: "1",
                class: Task.Task({ name: "mockTask" })(
                    class MockTask implements Task.TaskInstance {
                        [key: string]: any;
                        _outputs: Map<string, Task.Output<any>> = new Map<string, Task.Output<any>>();

                        async onBeforeInit(): Promise<void> {
                            this["activate"] = new Task.Output<any>();
                            this._outputs.set("activate", this["activate"]);

                            for (const link of extraLinks.filter(l => l.sourceId === "1")) {
                                if (!this._outputs.has(link.outputName)) {
                                    this[link.outputName] = new Task.Output<any>();
                                    this._outputs.set(link.outputName, this[link.outputName]);
                                }
                            }
                        }

                        async onInit(): Promise<void> {
                            trigger(this._outputs);
                        }

                        async onChanges(changes: Task.Changes): Promise<void> {
                            validate(changes);
                        }
                    }
                )
            }
        ],
        [
            { sourceId: "1", outputName: "activate", targetId: "0", inputName: "activate" },
            { sourceId: "0", outputName: "success", targetId: "1", inputName: "success" },
            { sourceId: "0", outputName: "error", targetId: "1", inputName: "error" },
            { sourceId: "0", outputName: "results", targetId: "1", inputName: "results" },
            ...extraLinks
        ],
        undefined,
        (container) => {
            container.bind(GLOBAL_SQLITE_MANAGER_SYMBOL).toConstantValue(sqliteManagerMock);
        }
    );
}

describe("StoreSQLite Task tests", () => {

    let sqliteManagerMock: SQLiteManagerMock;

    beforeEach(() => {
        sqliteManagerMock = new SQLiteManagerMock();
    });

    it("should insert every configured input that received data, and emit the results map", (done) => {
        const inputs = [{ name: "orderData", tableName: "orders", idElement: "id", ttl: 60 }];
        const orderPayload = JSON.stringify({ id: "42", status: "open" });
        let capturedResults: Map<string, any> | undefined;

        createTaskTest(
            { inputs } as unknown as StoreSQLiteSettings,
            sqliteManagerMock,
            (outputs) => {
                outputs.get("orderData").emit(orderPayload);
                outputs.get("activate").emit(true);
            },
            (changes) => {
                if (changes["results"] != null) {
                    capturedResults = changes["results"].currentValue;
                }
                if (changes["success"] == null) {
                    return;
                }

                chai.expect(changes["success"].currentValue).to.equal(true);

                chai.expect(sqliteManagerMock.calls).to.have.length(1);
                chai.expect(sqliteManagerMock.calls[0].method).to.equal("insert");
                chai.expect(sqliteManagerMock.calls[0].args).to.deep.equal(["42", "orders", orderPayload, 60]);

                chai.expect(capturedResults?.get("42")).to.deep.equal({ changes: 1 });

                done();
            },
            [{ sourceId: "1", outputName: "orderData", targetId: "0", inputName: "orderData" }]
        );
    });

    it("should fall back to the input's own name as the table name when tableName is not set", (done) => {
        const inputs = [{ name: "telemetry", idElement: "id" }];
        const payload = JSON.stringify({ id: "1" });

        createTaskTest(
            { inputs } as unknown as StoreSQLiteSettings,
            sqliteManagerMock,
            (outputs) => {
                outputs.get("telemetry").emit(payload);
                outputs.get("activate").emit(true);
            },
            (changes) => {
                if (changes["success"] == null) {
                    return;
                }

                chai.expect(sqliteManagerMock.calls[0].args[1]).to.equal("telemetry");
                done();
            },
            [{ sourceId: "1", outputName: "telemetry", targetId: "0", inputName: "telemetry" }]
        );
    });

    it("should skip configured inputs that never received data", (done) => {
        const inputs = [
            { name: "orderData", tableName: "orders", idElement: "id" },
            { name: "neverSent", tableName: "other", idElement: "id" }
        ];
        const orderPayload = JSON.stringify({ id: "42" });

        createTaskTest(
            { inputs } as unknown as StoreSQLiteSettings,
            sqliteManagerMock,
            (outputs) => {
                outputs.get("orderData").emit(orderPayload);
                outputs.get("activate").emit(true);
            },
            (changes) => {
                if (changes["success"] == null) {
                    return;
                }

                chai.expect(sqliteManagerMock.calls).to.have.length(1);
                chai.expect(sqliteManagerMock.calls[0].args).to.deep.equal(["42", "orders", orderPayload, undefined]);
                done();
            },
            [{ sourceId: "1", outputName: "orderData", targetId: "0", inputName: "orderData" }]
        );
    });

    it("should emit an error and not success when the insert fails", (done) => {
        sqliteManagerMock.insertImpl = () => { throw new Error("insert failed"); };

        const inputs = [{ name: "orderData", tableName: "orders", idElement: "id" }];

        createTaskTest(
            { inputs } as unknown as StoreSQLiteSettings,
            sqliteManagerMock,
            (outputs) => {
                outputs.get("orderData").emit(JSON.stringify({ id: "42" }));
                outputs.get("activate").emit(true);
            },
            (changes) => {
                if (changes["error"] == null) {
                    return;
                }

                chai.expect(changes["error"].currentValue).to.be.instanceOf(Error);
                chai.expect((changes["error"].currentValue as Error).message).to.include("insert failed");
                chai.expect(changes["success"]).to.not.exist;
                done();
            },
            [{ sourceId: "1", outputName: "orderData", targetId: "0", inputName: "orderData" }]
        );
    });
});
