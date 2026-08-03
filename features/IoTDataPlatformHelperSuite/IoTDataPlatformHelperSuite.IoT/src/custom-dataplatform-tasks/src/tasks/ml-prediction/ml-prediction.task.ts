import {
    Task,
    System,
    TaskBase
} from "@criticalmanufacturing/connect-iot-controller-engine";
import superagent from "superagent";

/**
 * @whatItDoes
 *
 * This task does something ... describe here
 *
 * @howToUse
 *
 * yada yada yada
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 *
 * ### Outputs
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see MlPredictionSettings}
 */
@Task.Task()
export class MlPredictionTask extends TaskBase implements MlPredictionSettings {
    /** **Inputs** */

    /** **Outputs** */

    /** Result of the prediction */
    @Task.OutputProperty()
    public result: Task.Output<any> = new Task.Output<any>();

    /** Settings */
    inputs: Task.TaskInput[];
    outputs: Task.TaskOutput[];

    /** Properties Settings */
    mlModelName: string;
    mlModelRevision: string;
    mlModelPredictionEndpoint: string;

    /**
     * Calls the ML agent with the given input parameters and returns the output.
     *
     * @param {Map<string, any>} inputParameters - The input parameters for the function.
     * @return {Promise<Map<string, any>>} - The output of the function as a Promise.
     */
    private async exec(inputParameters: Map<string, any>): Promise<Map<string, any>> {
        const output: Map<string, any> = new Map<string, any>();
        const parameters: string = JSON.stringify(inputParameters);

        this._logger.debug("Calling ML Agent with the following parameters: " + parameters);

        try {
            const res = await superagent
                .post(this.mlModelPredictionEndpoint + "/ml/predict")
                .set("Content-Type", "application/json")
                .send(parameters);

            output.set("Result", res.body);
        } catch (err: any) {
            output.set("Error", err.message);
        }

        return output;
    }

    /**
     * Calls the ML Agent to get a prediction
     */
    private async getPrediction(): Promise<void> {
        this._logger.info("Getting an ML Prediction from the ML Agent ...");

        if (this.mlModelName != null && this.mlModelName.length > 0) {
            if (this.mlModelRevision != null && this.mlModelRevision.length > 0) {
                this._logger.info(`ML Model: ${this.mlModelName} - ${this.mlModelRevision}`);

                const inputForMLExec: Map<string, any> = new Map<string, any>();

                inputForMLExec.set("ML_ModelName", this.mlModelName);
                inputForMLExec.set("ML_ModelRevision", this.mlModelRevision);
                inputForMLExec.set("ML_SysProperties_EventId", this["eventId"]);

                this.inputs.forEach(input => {
                    inputForMLExec.set(input.name, this[input.name]);
                });

                const inputObj: any = {};
                inputForMLExec.forEach((value, key) => {
                    inputObj[key] = value;
                });

                this._logger.debug("Calling ML Agent: " + this.mlModelName + "[" + this.mlModelRevision + "]");

                const outputMap: Map<string, any> = await this.exec(inputObj);

                if (outputMap.get("Result") != null) {
                    this.result.emit(outputMap.get("Result"));
                    this._logger.debug("result: " + JSON.stringify(outputMap.get("Result")));

                    this.outputs?.forEach(output => {
                        this[output.name].emit(outputMap.get("Result")[output.name]);
                        this._logger.debug(output.name + ": " + JSON.stringify(outputMap.get("Result")[output.name]));
                    });

                    this.success.emit(true);
                    this._logger.info("ML Prediction done successfully");
                } else if (outputMap.get("Error") != null) {
                    this.logAndEmitError(outputMap.get("Error"));
                }
            } else {
                this.logAndEmitError("'mlModelRevision' not defined");
            }
        } else {
            this.logAndEmitError("'mlModelName' not defined");
        }
    }

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            // It is advised to reset the activate to allow being reactivated without the value being different
            this.activate = undefined;

            await this.getPrediction();
        }
    }

    /** Right after settings are loaded, create the needed dynamic outputs. */
    async onBeforeInit(): Promise<void> {
        if (this.inputs != null && this.inputs.length > 0) {
            for (const input of this.inputs) {
                this[input.name] = input.defaultValue;
            }
        }
        if (this.outputs) {
            for (const output of this.outputs) {
                this[output.name] = new Task.Output<any>();
            }
        }
    }
}

// Add settings here
/** MlPrediction Settings object */
export interface MlPredictionSettings extends System.TaskDefaultSettings {
    /** ML Model Name */
    mlModelName: string;

    /** ML Model Revision */
    mlModelRevision: string;

    /** ML Model Prediction Endpoint */
    mlModelPredictionEndpoint: string;
}

export interface MlPredictionInputSettings extends System.Property {
    /** Name that will appear in Task */
    name: string;

    /** Friendly Name */
    friendlyName?: string;

    /** Value Type */
    valueType: Task.TaskComplexValueType;

    /** Default value that will be used if no other was retrieved */
    defaultValue?: any;
}