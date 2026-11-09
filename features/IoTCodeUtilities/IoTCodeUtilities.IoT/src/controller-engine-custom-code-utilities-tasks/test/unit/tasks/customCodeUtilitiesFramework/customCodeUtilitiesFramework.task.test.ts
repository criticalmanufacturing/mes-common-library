import "reflect-metadata";
import { Container, Task, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { expect } from "chai";

import { CustomCodeUtilitiesFrameworkTask } from "../../../../src/tasks/customCodeUtilitiesFramework/customCodeUtilitiesFramework.task";
import { CustomCodeUtilitiesFramework, ID } from "../../../../src/tasks/customCodeUtilitiesFramework/customCodeUtilitiesFramework.task.util";

describe("CustomCodeUtilitiesFramework Task tests", () => {
    const createTask = async (preRegisteredImplementation?: any): Promise<Task.Library> => {
        let library: Task.Library;

        await EngineTestSuite.createTasks(
            [{ class: CustomCodeUtilitiesFrameworkTask, id: "0", settings: {} }],
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

    it("should register the framework utility on the shared task library", async () => {
        const library = await createTask();

        expect(library.implementations[ID]).to.be.an.instanceof(CustomCodeUtilitiesFramework);
    });

    it("should not overwrite an implementation already registered under the same id", async () => {
        const preRegisteredImplementation = { marker: "already-there" };

        const library = await createTask(preRegisteredImplementation);

        expect(library.implementations[ID]).to.equal(preRegisteredImplementation);
    });
});
