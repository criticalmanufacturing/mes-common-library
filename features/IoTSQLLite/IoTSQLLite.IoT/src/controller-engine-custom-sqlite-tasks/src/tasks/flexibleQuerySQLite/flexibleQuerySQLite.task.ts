import { Task, System, TaskBase, DI } from "@criticalmanufacturing/connect-iot-controller-engine";
import { SQLiteManager } from "../../sqlite/SQLiteManager";

/** Default values for settings */
export const SETTINGS_DEFAULTS: FlexibleQuerySQLiteSettings = {
    tableName: "",
    criteria: null,
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
 * See {@see FlexibleQuerySQLiteSettings}
 */
@Task.Task()
export class FlexibleQuerySQLiteTask extends TaskBase implements FlexibleQuerySQLiteSettings {

    /**
     * This is the representation of the SQLite manager
     */
    @DI.Inject("GlobalSQLiteManagerHandler")
    private _sqliteManager: SQLiteManager;

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    /** TableName */
    public tableName: string = "";
    /** Criteria */
    public criteria: {
        jsonFilters: {
            operator: string;
            path: string;
            value: any;
        }[],
        limit: number
    } = null;

    /** **Outputs** */
    /** Result */
    public result: Task.Output<any> = new Task.Output<any>();

    /** Properties Settings */

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            // It is advised to reset the activate to allow being reactivated without the value being different
            this.activate = undefined;

            try {

                this._logger.info("Starting Execution of Flexible Query SQLite task");
                const queryResult = this._sqliteManager.flexibleSearch(this.tableName, this.criteria);
                this.result.emit(queryResult);
                this.success.emit(true);

                this._logger.info("Finished Executing Flexible Query SQLite task");
            } catch (e) {
                this.logAndEmitError(e.message);
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
}

// Add settings here
/** FlexibleQuerySQLite Settings object */
export interface FlexibleQuerySQLiteSettings extends System.TaskDefaultSettings {
    /** Information about the example setting */
    tableName: string;
    criteria: any;
}

@Task.TaskModule({
    task: FlexibleQuerySQLiteTask,
    providers: [
        {
            class: SQLiteManager,
            isSingleton: true,
            symbol: "GlobalSQLiteManagerHandler",
            scope: Task.ProviderScope.Controller,
        }
    ]
})
export class FlexibleQuerySQLiteModule { }