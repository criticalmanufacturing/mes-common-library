import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as chai from "chai";

import { createTaskTestFactory, SQLiteManagerMock, GLOBAL_SQLITE_MANAGER_SYMBOL } from "../task-test-utils";
import {
    QuerySQLiteByPathTask,
    QuerySQLiteByPathSettings
} from "../../../../src/tasks/querySQLiteByPath/querySQLiteByPath.task";

describe("QuerySQLiteByPath Task tests", () => {

    let sqliteManagerMock: SQLiteManagerMock;

    beforeEach(() => {
        sqliteManagerMock = new SQLiteManagerMock();
    });

    const querySQLiteByPathTestFactory = createTaskTestFactory<QuerySQLiteByPathSettings>(
        QuerySQLiteByPathTask,
        {
            tableName: "orders",
            jsonPath: "status",
            value: "open"
        } as QuerySQLiteByPathSettings,
        (container) => {
            container.bind(GLOBAL_SQLITE_MANAGER_SYMBOL).toConstantValue(sqliteManagerMock);
        },
        [{ sourceId: "0", outputName: "result", targetId: "1", inputName: "result" }]
    );

    it("should query by the configured table name, json path and value, and emit the result", (done) => {
        const rows = [{ id: "1", data: { status: "open" } }];
        sqliteManagerMock.queryByPathImpl = () => rows;

        querySQLiteByPathTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["success"]?.currentValue).to.equal(true);
                chai.expect(changes["result"]?.currentValue).to.deep.equal(rows);

                chai.expect(sqliteManagerMock.calls).to.have.length(1);
                chai.expect(sqliteManagerMock.calls[0].method).to.equal("queryByPath");
                chai.expect(sqliteManagerMock.calls[0].args).to.deep.equal(["orders", "status", "open"]);

                done();
            });
    });

    it("should emit an error and not success when the query fails", (done) => {
        sqliteManagerMock.queryByPathImpl = () => { throw new Error("path query failed"); };

        querySQLiteByPathTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["error"]?.currentValue).to.be.instanceOf(Error);
                chai.expect((changes["error"].currentValue as Error).message).to.include("path query failed");
                chai.expect(changes["success"]).to.not.exist;

                done();
            });
    });
});
