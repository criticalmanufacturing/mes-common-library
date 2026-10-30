import { Task, System, TaskBase, DI } from "@criticalmanufacturing/connect-iot-controller-engine";
import { SQLiteManager } from "../../sqlite/SQLiteManager";

/** Default values for settings */
export const SETTINGS_DEFAULTS: QuerySQLiteSettings = {
    query: "",
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
 * See {@see QuerySQLiteSettings}
 */
@Task.Task()
export class QuerySQLiteTask extends TaskBase implements QuerySQLiteSettings {

    /**
     * This is the representation of the SQLite manager
     */
    @DI.Inject("GlobalSQLiteManagerHandler")
    private _sqliteManager: SQLiteManager;

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    public query: string;
    public params: any[] = [];

    /** **Outputs** */
    public result = new Task.Output<any>();

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

                this._logger.info("Starting Execution of Query SQLite task");
                const queryResult = this._sqliteManager.rawQuery(this.query, this.params);
                this.result.emit(queryResult);
                this.success.emit(true);

                this._logger.info("Finished Executing Query SQLite task");
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
/** QuerySQLite Settings object */
export interface QuerySQLiteSettings extends System.TaskDefaultSettings {
    query: string;
}

@Task.TaskModule({
    task: QuerySQLiteTask,
    providers: [
        {
            class: SQLiteManager,
            isSingleton: true,
            symbol: "GlobalSQLiteManagerHandler",
            scope: Task.ProviderScope.Controller,
        }
    ]
})
export class QuerySQLiteModule { }