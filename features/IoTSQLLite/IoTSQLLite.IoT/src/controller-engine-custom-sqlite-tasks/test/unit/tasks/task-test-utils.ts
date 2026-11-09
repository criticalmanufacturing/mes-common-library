import "reflect-metadata";
import { Task } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
// import { DataStoreMock } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/dataStore.mock";

/**
 * DI symbol every SQLite task binds its `SQLiteManager` to (see each task's `@Task.TaskModule` provider).
 * `EngineTestSuite.createTasks` builds the task class directly, bypassing the module's providers, so tests
 * must bind this symbol themselves (typically to a {@link SQLiteManagerMock}) via a container callback.
 */
export const GLOBAL_SQLITE_MANAGER_SYMBOL = "GlobalSQLiteManagerHandler";

/** A single recorded call made against a {@link SQLiteManagerMock}. */
export interface SQLiteManagerMockCall {
    method: string;
    args: any[];
}

/**
 * Lightweight stand-in for `SQLiteManager` used by the SQLite tasks' unit tests, so tests don't need a real
 * `better-sqlite3` database on disk.
 *
 * Every call is recorded in {@link calls} so a test can assert on what a task passed down, and each method's
 * behavior can be overridden per test by reassigning the matching `<method>Impl` property (e.g. to return
 * canned rows, or to throw and exercise a task's error path).
 */
export class SQLiteManagerMock {
    public calls: SQLiteManagerMockCall[] = [];

    public insertImpl: (id: string, documentType: string, data: object, ttlSeconds?: number) => any =
        (id: string) => ({ id: id || "generated-id", result: { changes: 1 }, existed: false });
    public queryByTypeImpl: (documentType: string, limit?: number) => any = () => [];
    public queryByPathImpl: (documentType: string, jsonPath: string, value: string) => any = () => [];
    public queryNestedArrayImpl: (documentType: string, arrayPath: string, itemField: string, itemValue: string) => any = () => [];
    public flexibleSearchImpl: (documentType: string, criteria: any) => any = () => [];
    public rawQueryImpl: (query: string, params: any[]) => any = () => [];

    private record(method: string, args: any[]): void {
        this.calls.push({ method, args });
    }

    insert(...args: any[]): any {
        this.record("insert", args);
        return (this.insertImpl as any)(...args);
    }

    queryByType(...args: any[]): any {
        this.record("queryByType", args);
        return (this.queryByTypeImpl as any)(...args);
    }

    queryByPath(...args: any[]): any {
        this.record("queryByPath", args);
        return (this.queryByPathImpl as any)(...args);
    }

    queryNestedArray(...args: any[]): any {
        this.record("queryNestedArray", args);
        return (this.queryNestedArrayImpl as any)(...args);
    }

    flexibleSearch(...args: any[]): any {
        this.record("flexibleSearch", args);
        return (this.flexibleSearchImpl as any)(...args);
    }

    rawQuery(...args: any[]): any {
        this.record("rawQuery", args);
        return (this.rawQueryImpl as any)(...args);
    }
}

/**
 * Signature shared by every `<task>TestFactory` used across this package's task unit tests.
 *
 * `trigger` fires once the mock task is initialized (use it to emit inputs into the task under test).
 * `validate` runs on every change reported back by the task under test (use it to assert on the outputs).
 */
// eslint-disable-next-line @typescript-eslint/ban-types
export type TaskTestFactory<TSettings> = (settings: TSettings | undefined, trigger: Function, validate: Function) => void;

/**
 * Builds the `<task>TestFactory` used by a task's unit tests.
 *
 * It wires up two tasks through EngineTestSuite:
 *    0 - The task under test
 *    1 - A mock task
 *
 * All outputs of the mock task are connected to the inputs of the task under test, and all outputs of the
 * task under test are connected back to the mock task's inputs.
 *
 * You, as the test developer, trigger the outputs necessary for the task under test to be activated and
 * validate the changes it reports back.
 *
 * Note: This is just an example about how to unit test a task. Not mandatory to use this method!
 *
 * @param taskClass The task class under test.
 * @param defaultSettings The settings used when a test does not provide its own.
 * @param containerCallback Optional hook to change the DI container (e.g. bind mocks) before the tasks are created.
 * @param extraLinks Additional links to wire beyond the default `activate`/`success`/`error` set - e.g. a
 * task's own `result` output back into the mock, so tests can assert on `changes["result"]`. Without this,
 * asserting on a link that was never wired reads back as `undefined` and throws inside the engine's internal
 * change-processing, which is swallowed silently and just hangs the test until it times out.
 */
export function createTaskTestFactory<TSettings>(
    taskClass: any,
    defaultSettings: TSettings,
    containerCallback: (containerId: any) => void = () => {
        // Change what you need in the container
        // Example:
        // containerId.unbind(TYPES.System.PersistedDataStore);
        // containerId.bind(TYPES.System.PersistedDataStore).toConstantValue(dataStoreMock);
    },
    extraLinks: Task.LinkDefinition[] = []
): TaskTestFactory<TSettings> {

    // eslint-disable-next-line @typescript-eslint/ban-types
    return (settings: TSettings | undefined, trigger: Function, validate: Function): void => {

        const taskDefinition = {
            class: taskClass,
            id: "0",
            settings: (settings || defaultSettings)
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
                        // Create other custom outputs (for the Mock task) here
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
            { sourceId: "1", outputName: `activate`, targetId: "0", inputName: "activate", },
            { sourceId: "0", outputName: `success`, targetId: "1", inputName: "success", },
            { sourceId: "0", outputName: `error`, targetId: "1", inputName: "error", },
            ...extraLinks
        ],
            undefined,
            containerCallback);
    };
}
