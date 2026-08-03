import { Task, System, TaskBase, DI, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";
import { SQLiteManager } from "../../sqlite/SQLiteManager";

/** Default values for settings */
export const SETTINGS_DEFAULTS: StoreSQLiteSettings = {
};

export interface InputStore extends Task.TaskInput {
    /**
    * TableName
    */
    tableName: string;
    /**
    * JSON Id Element
    */
    idElement: string;
    /**
    * Time To Live
    */
    ttl: number;
}

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
 * See {@see StoreSQLiteSettings}
 */
@Task.Task()
export class StoreSQLiteTask extends TaskBase implements StoreSQLiteSettings {

    /**
     * This is the representation of the SQLite manager
     */
    @DI.Inject("GlobalSQLiteManagerHandler")
    private _sqliteManager: SQLiteManager;

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    inputs: InputStore[];

    /** **Outputs** */
    results: Task.Output<Map<string, any[]>> = new Task.Output<Map<string, any[]>>();


    /** Properties Settings */
    /** Information about the example setting */

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            // It is advised to reset the activate to allow being reactivated without the value being different
            this.activate = undefined;
            const stmtValues = new Map<string, any[]>();

            try {
                this._logger.debug("Starting Execution of Store SQLite task");
                for (const storeElement of this.inputs) {
                    if (this[storeElement.name] != null) {
                        const documentId = JSON.parse(this[storeElement.name] ?? {})?.[storeElement.idElement];
                        const data = this[storeElement.name];
                        const tableName = storeElement.tableName != null && storeElement.tableName !== "" ? storeElement.tableName : storeElement.name;
                        const output = this._sqliteManager.insert(documentId, tableName, data, storeElement?.ttl);
                        stmtValues.set(output.id, output.result);
                    }
                }

                this.results.emit(stmtValues);
                this.success.emit(true);
                this._logger.info("Finished Executing Store SQLite task");
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
/** StoreSQLLite Settings object */
export interface StoreSQLiteSettings extends System.TaskDefaultSettings {
}

@Task.TaskModule({
    task: StoreSQLiteTask,
    providers: [
        {
            class: SQLiteManager,
            isSingleton: true,
            symbol: "GlobalSQLiteManagerHandler",
            scope: Task.ProviderScope.Controller,
        }
    ]
})
export class StoreSQLiteModule { }