import { Task, System, TaskBase, TYPES, DI, Dependencies } from "@criticalmanufacturing/connect-iot-controller-engine";
import { XMLBuilder } from "fast-xml-parser";
import * as cheerio from "cheerio";

/** Default values for settings */

/**
 * @whatItDoes
 *
 * Converts a JSON value into an XML string using fast-xml-parser.
 *
 * @howToUse
 *
 * Provide the JSON input and optionally a root key and builder options, then
 * activate the task. The generated XML is emitted through the `xml` output.
 *
 * ### Inputs
 * * `any` : **activate** - Activates the conversion
 * * `any` : **json** - JSON value to convert to XML
 * * `string` : **key** - Optional root element name for the JSON value
 * * `any` : **options** - Optional fast-xml-parser builder configuration
 *
 * ### Outputs
 *
 * * `string` : **xml** - Generated XML string
 * * `bool` : **success** - Emitted when conversion succeeds
 * * `Error` : **error** - Task error output
 *
 * ### Notes
 * When `key` is empty, the JSON value is used as the XML root. When
 * `options.useCheerio` is true, the generated XML is normalized with Cheerio.
 */
@Task.Task()
export class Json2xmlTask extends TaskBase {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    /** JSON Object input */
    public json: any = undefined;
    /** Key input */
    public key: string = "";
    /** options input object */
    public options: any;

    /** **Outputs** */
    /** XML output */
    public xml: Task.Output<string> = new Task.Output<string>();

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            const jsonInput = this.json;
            const keyInput = this.key;
            const options = this.options;
            this.activate = undefined;

            let rootJSONInput: { [index: string]: any } = {};

            try {
                if (keyInput !== "") {
                    rootJSONInput[keyInput] = jsonInput;
                } else {
                    rootJSONInput = jsonInput;
                }

                this._logger.debug("Converting JSON to XML with key: " + keyInput + " and options: " + JSON.stringify(options) + " ...");
                const builder = new XMLBuilder(options);
                let result: string = builder.build(rootJSONInput);

                if (options?.useCheerio) {
                    this._logger.debug("Using Cheerio for XML formatting");
                    const outcheerio = cheerio.load(result, { xmlMode: true });
                    result = outcheerio.xml();
                }
                this._logger.info("Finished converting JSON to XML");

                this.xml.emit(result);
                this.success.emit(true);

            } catch (error) {
                this.logAndEmitError(`Error converting JSON to XML: ${(error as Error).message}`);
            }
        }
    }
}


