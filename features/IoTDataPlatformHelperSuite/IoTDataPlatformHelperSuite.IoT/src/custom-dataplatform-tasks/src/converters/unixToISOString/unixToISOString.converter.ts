import { Converter, DI, Dependencies, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";

/**
 * @whatItDoes
 *
 * >>TODO: Add description
 *
 */
@Converter.Converter({
    name: "unixToISOString",
    input: "number",
    output: "string",
    parameters: {}
})
export class UnixToISOStringConverter implements Converter.ConverterInstance<number, string> {

    @DI.Inject(TYPES.Dependencies.Logger)
    private _logger: Dependencies.Logger;

    /**
     * Converts a Unix timestamp (milliseconds since the epoch) to an ISO 8601 string.
     * @param value number value
     * @param parameters Transformation parameters
     */
    transform(value: number, parameters: { [key: string]: any; }): string {
        const isoString = new Date(value).toISOString();
        this._logger.debug(`Converting Unix timestamp ${value} to ISO string: ${isoString}`);
        return isoString;
    }
}
