import "reflect-metadata";
import { Container, Task, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite from "@criticalmanufacturing/connect-iot-controller-engine/dist/test";
import { expect } from "chai";

import { CustomCodeUtilitiesObjectTranslatorTask } from "../../../../src/tasks/customCodeUtilitiesObjectTranslator/customCodeUtilitiesObjectTranslator.task";
import { CustomCodeUtilitiesObjectTranslator, ID } from "../../../../src/tasks/customCodeUtilitiesObjectTranslator/customCodeUtilitiesObjectTranslator.task.util";

describe("CustomCodeUtilitiesObjectTranslator Task tests", () => {
    const createTask = async (preRegisteredImplementation?: any): Promise<Task.Library> => {
        let library: Task.Library;

        await EngineTestSuite.createTasks(
            [{ class: CustomCodeUtilitiesObjectTranslatorTask, id: "0", settings: {} }],
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

    it("should register the object translator implementation on the shared task library", async () => {
        const library = await createTask();

        expect(library.implementations[ID]).to.be.an.instanceof(CustomCodeUtilitiesObjectTranslator);
    });

    it("should not overwrite an implementation already registered under the same id", async () => {
        const preRegisteredImplementation = { marker: "already-there" };

        const library = await createTask(preRegisteredImplementation);

        expect(library.implementations[ID]).to.equal(preRegisteredImplementation);
    });
});
