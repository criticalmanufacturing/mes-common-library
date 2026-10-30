import { Task, System, TaskBase, DI } from "@criticalmanufacturing/connect-iot-controller-engine";
import { SQLiteManager } from "../../sqlite/SQLiteManager";

/** Default values for settings */
export const SETTINGS_DEFAULTS: QuerySQLiteNestedArraySettings = {
    tableName: "",
    arrayPath: "",
    itemField: "",
    itemValue: "",
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
 * See {@see QuerySQLiteNestedArraySettings}
 */
@Task.Task()
export class QuerySQLiteNestedArrayTask extends TaskBase implements QuerySQLiteNestedArraySettings {

    /**
     * This is the representation of the SQLite manager
     */
    @DI.Inject("GlobalSQLiteManagerHandler")
    private _sqliteManager: SQLiteManager;

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    /** tableName */
    public tableName: string = "";
    /** arrayPath */
    public arrayPath: string = "";
    /** itemField */
    public itemField: string = "";
    /** itemValue */
    public itemValue: string = "";

    /** **Outputs** */
    /** results */
    public result: Task.Output<object> = new Task.Output<object>();

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

                this._logger.info("Starting Execution of Nested Query SQLite task");
                const queryResult = this._sqliteManager.queryNestedArray(this.tableName, this.arrayPath, this.itemField, this.itemValue);
                this.result.emit(queryResult);
                this.success.emit(true);

                this._logger.info("Finished Executing Nested Query SQLite task");
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
/** QuerySQLiteNestedArray Settings object */
export interface QuerySQLiteNestedArraySettings extends System.TaskDefaultSettings {
    /** Default Table Name */
    tableName: string;
    /** Default Array Path */
    arrayPath: string;
    /** Default Item Field */
    itemField: string;
    /** Default Item Value */
    itemValue: string;
}

@Task.TaskModule({
    task: QuerySQLiteNestedArrayTask,
    providers: [
        {
            class: SQLiteManager,
            isSingleton: true,
            symbol: "GlobalSQLiteManagerHandler",
            scope: Task.ProviderScope.Controller,
        }
    ]
})
export class QuerySQLiteNestedArrayModule { }