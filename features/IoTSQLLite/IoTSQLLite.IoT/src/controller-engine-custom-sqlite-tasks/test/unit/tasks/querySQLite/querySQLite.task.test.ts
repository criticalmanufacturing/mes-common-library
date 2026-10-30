import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as chai from "chai";

import { createTaskTestFactory, SQLiteManagerMock, GLOBAL_SQLITE_MANAGER_SYMBOL } from "../task-test-utils";
import {
    QuerySQLiteTask,
    QuerySQLiteSettings
} from "../../../../src/tasks/querySQLite/querySQLite.task";

describe("QuerySQLite Task tests", () => {

    let sqliteManagerMock: SQLiteManagerMock;

    beforeEach(() => {
        sqliteManagerMock = new SQLiteManagerMock();
    });

    const querySQLiteTestFactory = createTaskTestFactory<QuerySQLiteSettings>(
        QuerySQLiteTask,
        {
            query: "SELECT * FROM orders WHERE id = ?",
            params: ["42"]
        } as QuerySQLiteSettings,
        (container) => {
            container.bind(GLOBAL_SQLITE_MANAGER_SYMBOL).toConstantValue(sqliteManagerMock);
        },
        [{ sourceId: "0", outputName: "result", targetId: "1", inputName: "result" }]
    );

    it("should run the configured raw query and emit the result", (done) => {
        const rows = [{ id: "42", name: "Widget" }];
        sqliteManagerMock.rawQueryImpl = () => rows;

        querySQLiteTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["success"]?.currentValue).to.equal(true);
                chai.expect(changes["result"]?.currentValue).to.deep.equal(rows);

                chai.expect(sqliteManagerMock.calls).to.have.length(1);
                chai.expect(sqliteManagerMock.calls[0].method).to.equal("rawQuery");
                chai.expect(sqliteManagerMock.calls[0].args).to.deep.equal([
                    "SELECT * FROM orders WHERE id = ?",
                    ["42"]
                ]);

                done();
            });
    });

    it("should emit an error and not success when the query fails", (done) => {
        sqliteManagerMock.rawQueryImpl = () => { throw new Error("syntax error"); };

        querySQLiteTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["error"]?.currentValue).to.be.instanceOf(Error);
                chai.expect((changes["error"].currentValue as Error).message).to.include("syntax error");
                chai.expect(changes["success"]).to.not.exist;

                done();
            });
    });
});
