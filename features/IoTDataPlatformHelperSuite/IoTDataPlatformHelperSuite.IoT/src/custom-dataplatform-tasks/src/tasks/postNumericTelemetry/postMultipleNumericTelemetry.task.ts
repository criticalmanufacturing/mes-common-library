import { Task, System, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import moment from "moment";
import { ISA95, PostTelemetry, PostTelemetryParameter, PostTelemetryTag } from "../../utilities/interfaces";
import { SystemCalls } from "../../utilities/systemCalls";

/** Default values for settings */
export const SETTINGS_DEFAULTS: PostMultipleNumericTelemetrySettings = {
    applicationName: "MES",
    eventTime: null,
    retries: 30,
    sleepBetweenRetries: 1000,
    ignoreLastServiceId: false,
    numberOfRetries: 30,
};

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
 *
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see PostMultipleNumericTelemetrySettings}
 */
@Task.Task()
export class PostMultipleNumericTelemetryTask extends TaskBase implements PostMultipleNumericTelemetrySettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    @Task.InputProperty(System.PropertyValueType.ReferenceType)
    public instance: System.LBOS.Cmf.Foundation.BusinessObjects.Entity;

    @Task.InputProperty(Task.TaskValueType.String)
    public class: string;

    @Task.InputProperty(Task.TaskValueType.String)
    public unitsOfMeasure: string[];

    @Task.InputProperty(Task.TaskValueType.String)
    public parametersName: string[];

    @Task.InputProperty(Task.TaskValueType.Object)
    public tags: PostTelemetryTag[];

    @Task.InputProperty(Task.TaskValueType.Decimal)
    public values: number[];

    @Task.InputProperty(Task.TaskValueType.DateTime)
    public valuesTimestamp: Date[];

    /** **Outputs** */

    /** Properties Settings */
    applicationName: string = SETTINGS_DEFAULTS.applicationName;
    eventTime: moment.Moment = SETTINGS_DEFAULTS.eventTime;
    retries: number = SETTINGS_DEFAULTS.retries;
    sleepBetweenRetries: number = SETTINGS_DEFAULTS.sleepBetweenRetries;
    ignoreLastServiceId: boolean = SETTINGS_DEFAULTS.ignoreLastServiceId;
    numberOfRetries: number = SETTINGS_DEFAULTS.numberOfRetries;

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            // It is advised to reset the activate to allow being reactivated without the value being different
            this.activate = undefined;

            const data =
                await this.createData(
                    this.instance,
                    this.class,
                    this.parametersName,
                    this.unitsOfMeasure,
                    this.tags,
                    this.values,
                    this.valuesTimestamp);


            const output = await SystemCalls.postTelemetry(
                data,
                this.applicationName,
                this.ignoreLastServiceId,
                this.retries,
                this.sleepBetweenRetries,
                this.numberOfRetries,
                this._systemProxy,
                this._logger);


            if (output != null) {
                if (output.HasErrors === false) {
                    this.success.emit(true);
                    this._logger.info("Event posted successfully");
                } else if (output.HasErrors === true) {
                    const error = output.ErrorList?.size > 0 ? [...output.ErrorList.values()].join("; ") : "Unknown error";
                    this.logAndEmitError(error);
                }
            }
        }
    }

    /** Right after settings are loaded, create the needed dynamic outputs. */
    public override async onBeforeInit(): Promise<void> {
    }

    /** Initialize this task, register any event handler, etc */
    public override async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);
    }

    /** Cleanup internal data, unregister any event handler, etc */
    public override async onDestroy(): Promise<void> {
    }

    private async createData(
        instance: System.LBOS.Cmf.Foundation.BusinessObjects.Entity,
        className: string,
        parametersName: string[],
        unitsOfMeasure: string[],
        tags: PostTelemetryTag[],
        valuesToPost: number[],
        valuesTimestamp: Date[] | Date
    ): Promise<PostTelemetry> {

        // Retrieve from cached memory
        let isa95: ISA95 | undefined = await this._dataStore.retrieve(`${instance.Name}_ISA95`, undefined);

        if (isa95 == null) {
            // If it doesn't exist, extract from the MES and store in cache
            isa95 = await SystemCalls.extractISA95(instance, this._systemAPI, this.logAndEmitError);
            this._dataStore.store(`${instance.Name}_ISA95`, isa95, System.DataStoreLocation.Temporary);
        }

        const data: PostTelemetryParameter[] = [];
        for (let index = 0; index < parametersName.length; index++) {
            const timestamp = !Array.isArray(valuesTimestamp) ?
                new Date(valuesTimestamp).valueOf().toString() :
                new Date(valuesTimestamp[index]).valueOf().toString();

            data.push({
                Class: className,
                Name: parametersName[index],
                UnitOfMeasure: unitsOfMeasure[index],
                NumericValues: [valuesToPost[index]],
                Timestamps: [timestamp]
            });
        }

        return {
            Parameters: data,
            Tags: tags,
            Material: { Name: isa95.Material },
            Resource: { Name: isa95.Resource },
            Area: { Name: isa95.Area },
            Facility: { Name: isa95.Facility },
            Site: { Name: isa95.Site },
            Enterprise: { Name: isa95.Enterprise },
        };
    }
}

// Add settings here
/** PostMultipleNumericTelemetry Settings object */
export interface PostMultipleNumericTelemetrySettings extends System.TaskDefaultSettings {
    /** Application name */
    applicationName: string;
    /** Event Time */
    eventTime: moment.Moment;
    /** Number of retries until a good answer is received from System */
    retries: number;
    /** Number of milliseconds to wait between retries */
    sleepBetweenRetries: number;
    /* Should  the system ignore the last service id */
    ignoreLastServiceId: boolean,
    /* Number of retries on the DEE execution */
    numberOfRetries: number;
}