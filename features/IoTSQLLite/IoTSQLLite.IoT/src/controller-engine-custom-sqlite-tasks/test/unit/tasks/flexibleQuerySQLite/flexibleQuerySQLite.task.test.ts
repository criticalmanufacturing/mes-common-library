import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as chai from "chai";

import { createTaskTestFactory, SQLiteManagerMock, GLOBAL_SQLITE_MANAGER_SYMBOL } from "../task-test-utils";
import {
    FlexibleQuerySQLiteTask,
    FlexibleQuerySQLiteSettings
} from "../../../../src/tasks/flexibleQuerySQLite/flexibleQuerySQLite.task";

describe("FlexibleQuerySQLite Task tests", () => {

    let sqliteManagerMock: SQLiteManagerMock;

    beforeEach(() => {
        sqliteManagerMock = new SQLiteManagerMock();
    });

    const flexibleQuerySQLiteTestFactory = createTaskTestFactory<FlexibleQuerySQLiteSettings>(
        FlexibleQuerySQLiteTask,
        {
            tableName: "orders",
            criteria: {
                jsonFilters: [{ operator: "equals", path: "status", value: "open" }],
                limit: 10
            }
        } as FlexibleQuerySQLiteSettings,
        (container) => {
            container.bind(GLOBAL_SQLITE_MANAGER_SYMBOL).toConstantValue(sqliteManagerMock);
        },
        [{ sourceId: "0", outputName: "result", targetId: "1", inputName: "result" }]
    );

    it("should search using the configured table name and criteria, and emit the result", (done) => {
        const rows = [{ id: "1", data: { status: "open" } }];
        sqliteManagerMock.flexibleSearchImpl = () => rows;

        flexibleQuerySQLiteTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["success"]?.currentValue).to.equal(true);
                chai.expect(changes["result"]?.currentValue).to.deep.equal(rows);

                chai.expect(sqliteManagerMock.calls).to.have.length(1);
                chai.expect(sqliteManagerMock.calls[0].method).to.equal("flexibleSearch");
                chai.expect(sqliteManagerMock.calls[0].args).to.deep.equal([
                    "orders",
                    { jsonFilters: [{ operator: "equals", path: "status", value: "open" }], limit: 10 }
                ]);

                done();
            });
    });

    it("should emit an error and not success when the search fails", (done) => {
        sqliteManagerMock.flexibleSearchImpl = () => { throw new Error("search failed"); };

        flexibleQuerySQLiteTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["error"]?.currentValue).to.be.instanceOf(Error);
                chai.expect((changes["error"].currentValue as Error).message).to.include("search failed");
                chai.expect(changes["success"]).to.not.exist;

                done();
            });
    });
});
