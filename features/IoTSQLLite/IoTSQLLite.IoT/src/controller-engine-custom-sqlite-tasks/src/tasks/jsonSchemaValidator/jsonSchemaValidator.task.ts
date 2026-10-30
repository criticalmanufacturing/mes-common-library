import { Task, System, TaskBase, DI, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";

/** Default values for settings */
export const SETTINGS_DEFAULTS: JSONSchemaValidatorSettings = {
    throwOnSchemaValidationFailure: false,
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
 * See {@see JSONSchemaValidatorSettings}
 */
@Task.Task()
export class JSONSchemaValidatorTask extends TaskBase implements JSONSchemaValidatorSettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */

    /** **Outputs** */

    /** Properties Settings */
    _schemas: SchemaSettings[];
    throwOnSchemaValidationFailure: boolean;

    private ajv: any;

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            this.activate = undefined;

            let hasErrors = false;
            try {
                // iterate defined schemas
                for (const schema of this._schemas) {
                    // If there's data for this schema
                    const jsonData = this[schema.name];
                    if (jsonData != null) {
                        // Validate against defined schema
                        const validate = this.ajv.compile(schema.schema);
                        if (validate(JSON.parse(jsonData))) {
                            (this[Utilities.propertyToOutput(schema.name)] as Task.Output<object>).emit(this[schema.name]);
                        } else
                            // Check if we should throw an error when schema is not matched
                            if (this.throwOnSchemaValidationFailure) {
                                hasErrors = true;
                                this.logAndEmitError(`Error when validating schema for ${validate.errors}: `);
                            }
                    }
                }

                if (!hasErrors) {
                    this.success.emit(true);
                }
            } catch (e) {
                this.logAndEmitError(e.message);
            }
        }
    }

    /** Right after settings are loaded, create the needed dynamic outputs. */
    public override async onBeforeInit(): Promise<void> {
        if (this._schemas != null && this._schemas.length > 0) {
            for (const schema of this._schemas) {
                this[Utilities.propertyToOutput(schema.name)] = new Task.Output<any>();
            }
        }
    }

    /** Initialize this task, register any event handler, etc */
    public override async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);
        this.ajv = await import("ajv");
    }

    /** Cleanup internal data, unregister any event handler, etc */
    public override async onDestroy(): Promise<void> {
    }
}

// Add settings here
/** JSONSchemaValidator Settings object */
export interface JSONSchemaValidatorSettings extends System.TaskDefaultSettings {
    throwOnSchemaValidationFailure: boolean;
}

export interface SchemaSettings {
    name: string;
    schema: object;
}