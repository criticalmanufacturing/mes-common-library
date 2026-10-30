import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import * as chai from "chai";

import { createTaskTestFactory, SQLiteManagerMock, GLOBAL_SQLITE_MANAGER_SYMBOL } from "../task-test-utils";
import {
    QuerySQLiteNestedArrayTask,
    QuerySQLiteNestedArraySettings
} from "../../../../src/tasks/querySQLiteNestedArray/querySQLiteNestedArray.task";

describe("QuerySQLiteNestedArray Task tests", () => {

    let sqliteManagerMock: SQLiteManagerMock;

    beforeEach(() => {
        sqliteManagerMock = new SQLiteManagerMock();
    });

    const querySQLiteNestedArrayTestFactory = createTaskTestFactory<QuerySQLiteNestedArraySettings>(
        QuerySQLiteNestedArrayTask,
        {
            tableName: "orders",
            arrayPath: "items",
            itemField: "sku",
            itemValue: "ABC-1"
        } as QuerySQLiteNestedArraySettings,
        (container) => {
            container.bind(GLOBAL_SQLITE_MANAGER_SYMBOL).toConstantValue(sqliteManagerMock);
        },
        [{ sourceId: "0", outputName: "result", targetId: "1", inputName: "result" }]
    );

    it("should query the configured nested array and emit the result", (done) => {
        const rows = [{ id: "1", data: { items: [{ sku: "ABC-1" }] } }];
        sqliteManagerMock.queryNestedArrayImpl = () => rows;

        querySQLiteNestedArrayTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["success"]?.currentValue).to.equal(true);
                chai.expect(changes["result"]?.currentValue).to.deep.equal(rows);

                chai.expect(sqliteManagerMock.calls).to.have.length(1);
                chai.expect(sqliteManagerMock.calls[0].method).to.equal("queryNestedArray");
                chai.expect(sqliteManagerMock.calls[0].args).to.deep.equal(["orders", "items", "sku", "ABC-1"]);

                done();
            });
    });

    it("should emit an error and not success when the query fails", (done) => {
        sqliteManagerMock.queryNestedArrayImpl = () => { throw new Error("nested query failed"); };

        querySQLiteNestedArrayTestFactory(undefined,
            (outputs: Map<string, Task.Output<any>>) => {
                outputs.get("activate").emit(true);
            }, (changes: Task.Changes) => {
                chai.expect(changes["error"]?.currentValue).to.be.instanceOf(Error);
                chai.expect((changes["error"].currentValue as Error).message).to.include("nested query failed");
                chai.expect(changes["success"]).to.not.exist;

                done();
            });
    });
});
