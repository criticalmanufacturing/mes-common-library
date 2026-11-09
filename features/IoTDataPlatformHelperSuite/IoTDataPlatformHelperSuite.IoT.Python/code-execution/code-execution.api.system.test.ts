import "reflect-metadata";
import {
    Task, TYPES, Dependencies, Container, System
} from "@criticalmanufacturing/connect-iot-controller-engine";
import EngineTestSuite, { TestTaskDefinition } from "@criticalmanufacturing/connect-iot-controller-engine/test";
import { DriverProxyMock } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/driver-proxy.mock";
import { SystemAPIMock } from "@criticalmanufacturing/connect-iot-controller-engine/test/mocks/system-api.mock";
import { expect } from "chai";
/**
 * We need to fetch the Execution Context node implementation that uses zone.js
 * Instead of creating an explicit dependency to the controller (which was nonsense) or to Zon.js (and replicate the implementation)
 * We considered this "magic" import as the less bad
 */
import { ExecutionContextZoneJS } from "@criticalmanufacturing/connect-iot-controller/dist/src/dependencies/executionContext.zonejs";



import { CodeExecutionModule } from "../../../../src/lib/tasks/code-execution/code-execution-node.task-module";

import {
    CodeExecutionSettings,
    encode
} from "../../../../src/lib/tasks/code-execution/code-execution.task";


/**
 * Converts the property name into the appropriate input
 * @param name property name
 */
export const inp: (name: string) => string = (name: string) => `${name}In`;

/**
 * Converts the property name into the appropriate output
 * @param name property name
 */
export const out: (name: string) => string = (name: string) => `${name}Out`;

describe("CodeExecution Api (system) tests", () => {

    /*
    *               +-----------------------------+       +---------------------------+
    *               | ==mockTask (1)==            |       | ==CodeExecution (0)==     |
    *    (*1) ----> | () input1        output1 () | ----> | () input1      output1 () | ----> (*1)
    *    (*2) ----> | () input2        output2 () | ----> | () input2      output2 () | ----> (*2)
    *               |                             |       |---------------------------|
    *    (*3) ----> | () success      activate () | ----> | () activate    success () | ----> (*3)
    *    (*4) ----> | () error                    |       |                  error () | ----> (*4)
    *               +-----------------------------+       +---------------------------+
    */

    const _defaultSettings: CodeExecutionSettings = {
        tsCode: [],
        jsCodeBase64: "",
        inputs: [
            { name: "input1", valueType: { type: System.PropertyValueType.Integer } },
            { name: "input2", valueType: { type: System.PropertyValueType.Integer } },
        ],
        outputs: [
            { name: "output1", valueType: { type: System.PropertyValueType.Integer } },
            { name: "output2", valueType: { type: System.PropertyValueType.Integer } },
        ],
        executionExpirationInMilliseconds: 0,
        contextExpirationInMilliseconds: 0
    };

    const codeTemplate = `
        Object.defineProperty(exports, "__esModule", { value: true });
        class CustomCode {
            constructor(framework) {
                this.framework = framework;
            }
            async main(inputs) {
                <CODE>
            }
        }
        exports.default = CustomCode;
    `;

    let systemAPIMock: SystemAPIMock;
    let driverMock: DriverProxyMock;

    beforeEach(() => {
        driverMock = new DriverProxyMock();
        systemAPIMock = new SystemAPIMock();
    });

    const codeExecutionTestFactory = (customSettings: any | undefined,
        trigger: Function,
        validate: Function,
        customMockTaskDefinition?: TestTaskDefinition<any>): void => {
        const codeExecutionTaskDefinition = {
            id: "0",
            class: CodeExecutionModule,
            settings: Object.assign({}, _defaultSettings, customSettings),
        };

        const mockTaskDefinition = customMockTaskDefinition || {
            id: "1",
            class: Task.Task({
                name: "mockTask"
            })(class MockTask implements Task.TaskInstance {
                [key: string]: any;
                _outputs: Map<string, Task.Output<any>> = new Map<string, Task.Output<any>>();

                async onBeforeInit(): Promise<void> {
                    this["activate"] = new Task.Output<any>();
                    this._outputs.set("activate", this["activate"]);
                    this["output1"] = new Task.Output<any>();
                    this._outputs.set("output1", this["output1"]);
                    this["output2"] = new Task.Output<any>();
                    this._outputs.set("output2", this["output2"]);
                }

                // Validate the results
                async onChanges(changes: Task.Changes): Promise<void> {
                    validate(changes);
                    this["success"] = undefined;
                    this["error"] = undefined;
                    this["input1"] = undefined;
                    this["input2"] = undefined;
                }

                // Trigger the test
                async onInit(): Promise<void> {
                    trigger(this._outputs);
                }
            })
        };

        EngineTestSuite.createTasks([
            codeExecutionTaskDefinition,
            mockTaskDefinition,
        ], [
            { sourceId: "1", outputName: `activate`, targetId: "0", inputName: "activate" },
            { sourceId: "0", outputName: `success`, targetId: "1", inputName: "success" },
            { sourceId: "0", outputName: `error`, targetId: "1", inputName: "error" },
            { sourceId: "1", outputName: "output1", targetId: "0", inputName: inp("input1") },
            { sourceId: "1", outputName: "output2", targetId: "0", inputName: inp("input2") },
            { sourceId: "0", outputName: out("output1"), targetId: "1", inputName: "input1" },
            { sourceId: "0", outputName: out("output2"), targetId: "1", inputName: "input2" },
        ],
            driverMock,
            (container: Container) => {
                container.rebind<Dependencies.ExecutionContext>(TYPES.Dependencies.ExecutionContext).to(ExecutionContextZoneJS).inSingletonScope();
                container.unbind(TYPES.System.API);
                container.bind(TYPES.System.API).toConstantValue(systemAPIMock);
            });
    };

    it("should be able to make a system call (lbos)", (done) => {
        const code = codeTemplate.replace("<CODE>", `
           const serviceInput = new this.framework.LBOS.Cmf.Navigo.BusinessOrchestration.ContainerManagement.InputObjects.EmptyContainerInput();
           serviceInput.IgnoreLastServiceId = true;
           serviceInput.Container = new this.framework.LBOS.Cmf.Navigo.BusinessObjects.Container();
           serviceInput.Container.Name = "Container001";
           const res = await this.framework.system.call(serviceInput);
           return { output1: res };
        `);

        codeExecutionTestFactory({ jsCodeBase64: encode(code), },
            (values: Map<string, Task.Output<any>>) => {
                values.get("activate").emit(true);
            },
            (changes: Task.Changes) => {
                try {
                    expect(changes["success"].currentValue).to.equal(true);
                    expect(changes["input1"]).to.exist;
                    expect(changes["input1"].currentValue.Message).to.equal("Success message");
                    expect(changes["input1"].currentValue.Container.Name).to.equal("Container001");
                    done();
                } catch (err) {
                    done(err);
                }
            }
        );
    });
});
