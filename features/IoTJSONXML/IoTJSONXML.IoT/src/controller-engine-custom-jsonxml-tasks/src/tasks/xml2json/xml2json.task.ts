import { Task, TaskBase } from "@criticalmanufacturing/connect-iot-controller-engine";
import { XMLParser } from 'fast-xml-parser';

/**
 * @whatItDoes
 *
 * Converts an XML string into a JSON object using fast-xml-parser.
 *
 * @howToUse
 *
 * Provide the XML input and optionally parser options, then activate the task.
 * The parsed JSON is emitted through the `json` output. If parsing fails, the
 * task emits the error through the `error` output instead.
 *
 * ### Inputs
 * * `any` : **activate** - Activates the conversion
 * * `string` : **xml** - XML string to convert to JSON
 * * `any` : **options** - Optional fast-xml-parser configuration
 *
 * ### Outputs
 *
 * * `object` : **json** - Parsed JSON object
 * * `bool` : **success** - Emitted when conversion succeeds
 * * `Error` : **error** - Emitted when conversion fails
 *
 * ### Notes
 * The `options` input is passed directly to `XMLParser`.
 */
@Task.Task()
export class Xml2jsonTask extends TaskBase {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    /** XML input as string */
    public xml: string = undefined;
    /** options input object */
    public options: any;

    /** **Outputs** */
    /** json object */
    public json: Task.Output<object> = new Task.Output<object>();

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes['activate']) {
            const options = this.options;
            this.activate = undefined;

            try {
                this._logger.debug("Converting XML to JSON...");
                const parser = new XMLParser(options);
                const json = parser.parse(this.xml);
                this._logger.info("Finished converting XML to JSON");

                this.json.emit(json);
                this.success.emit(true);

            } catch (error) {
                this.logAndEmitError(`Error converting XML to JSON: ${(error as Error).message}`);
            }
        }
    }
}
