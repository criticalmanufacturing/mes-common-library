import { Task, System, TaskBase, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";
import moment from "moment";
import DataPlatform = System.LBOS.Cmf.Foundation.BusinessOrchestration.DataPlatform;
import Cmf from "cmf-lbos";
import { Queries } from "../../utilities/queries";
import { ISA95, PostTelemetry, PostTelemetryTag } from "../../utilities/interfaces";
import { SystemCalls } from "../../utilities/systemCalls";

/** Default values for settings */
export const SETTINGS_DEFAULTS: PostNumericTelemetrySettings = {
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
 * See {@see PostNumericTelemetrySettings}
 */
@Task.Task()
export class PostNumericTelemetryTask extends TaskBase implements PostNumericTelemetrySettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    @Task.InputProperty(System.PropertyValueType.ReferenceType)
    public instance: System.LBOS.Cmf.Foundation.BusinessObjects.Entity;

    @Task.InputProperty(Task.TaskValueType.String)
    public class: string;

    @Task.InputProperty(Task.TaskValueType.String)
    public unitOfMeasure: string;

    @Task.InputProperty(Task.TaskValueType.String)
    public parameterName: string;

    @Task.InputProperty(Task.TaskValueType.Object)
    public tags: PostTelemetryTag[];

    @Task.InputProperty(Task.TaskValueType.Decimal)
    public value: number;

    @Task.InputProperty(Task.TaskValueType.DateTime)
    public valueTimestamp: Date;

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
                    this.parameterName,
                    this.unitOfMeasure,
                    this.tags,
                    this.value,
                    this.valueTimestamp.valueOf().toString());

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
        parameterName: string,
        unitOfMeasure: string,
        tags: PostTelemetryTag[],
        value: number,
        valueTimestamp: string
    ): Promise<PostTelemetry> {

        let isa95: ISA95 | undefined;
        if (instance) {
            const isMaterial = instance["$type"].toString() === "Cmf.Navigo.BusinessObjects.Material, Cmf.Navigo.BusinessObjects";

            if (!isMaterial) {
                // Retrieve from cached memory
                isa95 = await this._dataStore.retrieve(`${instance.Name}_ISA95`, undefined);

                if (isa95 == null) {
                    // If it doesn't exist, extract from the MES and store in cache
                    isa95 = await SystemCalls.extractISA95(instance, this._systemAPI, this.logAndEmitError);

                    // Material is a special case where the ISA95 hierarchy is constantly changing, so we don't want to cache it for materials
                    this._dataStore.store(`${instance.Name}_ISA95`, isa95, System.DataStoreLocation.Temporary);
                }
            } else {
                isa95 = await SystemCalls.extractISA95(instance, this._systemAPI, this.logAndEmitError);
            }
        } else {
            this._logger.warning("Instance is null, ISA95 hierarchy will not be included in the telemetry data.");
        }

        return {
            Parameters: [{
                Class: className,
                Name: parameterName,
                UnitOfMeasure: unitOfMeasure,
                NumericValues: [value],
                Timestamps: [valueTimestamp]
            }],
            Tags: tags,
            Material: { Name: isa95?.Material },
            Resource: { Name: isa95?.Resource },
            Area: { Name: isa95?.Area },
            Facility: { Name: isa95?.Facility },
            Site: { Name: isa95?.Site },
            Enterprise: { Name: isa95?.Enterprise },
        };
    }
}

// Add settings here
/** PostNumericTelemetry Settings object */
export interface PostNumericTelemetrySettings extends System.TaskDefaultSettings {
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