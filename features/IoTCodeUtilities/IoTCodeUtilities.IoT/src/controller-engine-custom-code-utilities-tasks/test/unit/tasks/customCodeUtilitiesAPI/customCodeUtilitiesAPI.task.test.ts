import "reflect-metadata";
import { Container, Task, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { expect } from "chai";

import { CustomCodeUtilitiesAPITask } from "../../../../src/tasks/customCodeUtilitiesAPI/customCodeUtilitiesAPI.task";
import { CustomUtilitiesUtilApi, ID } from "../../../../src/tasks/customCodeUtilitiesAPI/customCodeUtilitiesAPI.task.util.api";

describe("CustomCodeUtilitiesAPI Task tests", () => {

    /**
     * This task declares no inputs/outputs/settings of its own (see task_customCodeUtilitiesAPI.json) and does
     * not override `onChanges`, so activating it never emits `success`/`error` - its only real behaviour is
     * registering the `CustomUtilitiesUtilApi` implementation into the shared `Task.Library` from `onBeforeInit`.
     * `EngineTestSuite.createTasks` runs every task's `onBeforeInit` (and `onInit`) before resolving, so the
     * registration can be asserted right after it resolves, with no mock task/activation needed.
     */
    const createTask = async (preRegisteredImplementation?: any): Promise<Task.Library> => {
        let library: Task.Library;

        await EngineTestSuite.createTasks(
            [{ class: CustomCodeUtilitiesAPITask, id: "0", settings: {} }],
            [],
            undefined,
            (container: Container) => {
                library = container.get<Task.Library>(TYPES.Task.Library);
                if (preRegisteredImplementation !== undefined) {
                    library.addImplementation(ID, preRegisteredImplementation);
                }
            }
        );

        return library;
    };

    it("should initialize without errors", async () => {
        await createTask();
    });

    it("should register the CustomUtilitiesUtilApi implementation on the shared task library", async () => {
        const library = await createTask();

        expect(library.implementations[ID]).to.be.an.instanceof(CustomUtilitiesUtilApi);
    });

    it("should not overwrite an implementation already registered under the same id", async () => {
        const preRegisteredImplementation = { marker: "already-there" };

        const library = await createTask(preRegisteredImplementation);

        expect(library.implementations[ID]).to.equal(preRegisteredImplementation);
    });
});
