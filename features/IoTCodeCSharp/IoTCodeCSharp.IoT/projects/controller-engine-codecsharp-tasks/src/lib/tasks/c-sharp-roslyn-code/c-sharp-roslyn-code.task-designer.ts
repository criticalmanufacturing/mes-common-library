import { Injectable } from "@angular/core";
import { AutoLinkResult, LinkingPort, TaskDesigner, TaskDesignerContainerService, TaskDesignerInstance, TaskProtocol } from "cmf-core-connect-iot";
import { Task, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";
import { CSharpRoslynCodeSettings, ROSLYN_AUTO_IN, ROSLYN_AUTO_OUT } from "./c-sharp-roslyn-code.task";

@TaskDesigner({
    name: "C# Roslyn Code",
    iconClass: "icon-core-tasks-connect-iot-lg-codeexecution",
    inputs: {
        [ROSLYN_AUTO_IN]: <Task.TaskType>{
            friendlyName: "Link here to add new",
            type: Task.AUTO
        },
        activate: Task.INPUT_ACTIVATE
    },
    outputs: {
        [ROSLYN_AUTO_OUT]: <Task.TaskType>{
            friendlyName: "Link from here to add new",
            type: Task.AUTO
        },
        success: Task.OUTPUT_SUCCESS,
        error: Task.OUTPUT_ERROR
    },
    protocol: TaskProtocol.All,
    driverOptional: true
})
@Injectable()
export class CSharpRoslynCodeDesigner implements TaskDesignerInstance, CSharpRoslynCodeSettings {
    csCode: string[];
    csCodeBase64: string;
    roslynAssemblyBase64: string;
    roslynSourceHash: string;
    roslynCatalogVersion: string;
    inputs: Task.TaskInput[];
    outputs: Task.TaskOutput[];
    contextExpirationInMilliseconds: number;
    executionTimeoutMs: number;
    warmPoolEnabled: boolean;
    warmPoolSize: number;

    constructor(private _container: TaskDesignerContainerService) { }

    private async processAutoPortLink(
        autoPort: string,
        linkingPort: LinkingPort,
        inputs: Task.TaskInputs,
        outputs: Task.TaskOutputs
    ): Promise<AutoLinkResult> {
        const rt: AutoLinkResult = { messages: [] };

        const inputValidation = this._container.autoPortLinkValidation(Utilities.propertyToInput(linkingPort.name), inputs, outputs);
        const outputValidation = this._container.autoPortLinkValidation(Utilities.propertyToOutput(linkingPort.name), inputs, outputs);

        rt.messages = rt.messages.concat(inputValidation).concat(outputValidation);

        if (rt.messages.length === 0) {
            const entry = {
                name: linkingPort.name,
                valueType: Object.assign({ friendlyName: linkingPort.name }, linkingPort.type)
            };

            if (autoPort === ROSLYN_AUTO_IN) {
                this.inputs.push(entry);
                rt.port = { name: Utilities.propertyToInput(linkingPort.name) };
            } else if (autoPort === ROSLYN_AUTO_OUT) {
                this.outputs.push(entry);
                rt.port = { name: Utilities.propertyToOutput(linkingPort.name) };
            }
        }

        return rt;
    }

    public async onGetInputs(inputs: Task.TaskInputs): Promise<Task.TaskInputs> {
        if (this.inputs) {
            for (const input of this.inputs) {
                const inputName = Utilities.propertyToInput(input.name);
                inputs[inputName] = this._container.getPortName(input);
                this[inputName] = input.defaultValue;
            }
        }
        return inputs;
    }

    public async onGetOutputs(outputs: Task.TaskOutputs): Promise<Task.TaskOutputs> {
        if (this.outputs) {
            for (const output of this.outputs) {
                const outputName = Utilities.propertyToOutput(output.name);
                outputs[outputName] = this._container.getPortName(output);
            }
        }
        return outputs;
    }

    public async onAutoInputLink(
        autoPort: string,
        outputPort: LinkingPort,
        inputs: Task.TaskInputs,
        outputs: Task.TaskOutputs
    ): Promise<AutoLinkResult> {
        return this.processAutoPortLink(autoPort, outputPort, inputs, outputs);
    }

    public async onAutoOutputLink(
        autoPort: string,
        inputPort: LinkingPort,
        inputs: Task.TaskInputs,
        outputs: Task.TaskOutputs
    ): Promise<AutoLinkResult> {
        return this.processAutoPortLink(autoPort, inputPort, inputs, outputs);
    }
}