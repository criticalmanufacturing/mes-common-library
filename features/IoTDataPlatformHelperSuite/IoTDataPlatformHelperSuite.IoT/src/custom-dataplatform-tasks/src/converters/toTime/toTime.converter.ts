import { Converter } from "@criticalmanufacturing/connect-iot-controller-engine";
import ms = require("ms");

/**
 * @whatItDoes
 *
 * Converts a human-readable time string (e.g. `"2s"`, `"1.5h"`, `"2 days"`) into
 * milliseconds using the `ms` library. Numeric values are passed through as-is.
 *
 * ### Inputs
 * * `string | number` - A time expression accepted by `ms` (e.g. `"2s"`, `"1m"`, `"3h"`)
 *   or a plain number of milliseconds.
 *
 * ### Output
 * * `number` - Milliseconds corresponding to the input expression.
 *
 */
@Converter.Converter({
    name: "toTime",
    input: "string",
    output: "long",
    parameters: {},
})
export class ToTimeConverter implements Converter.ConverterInstance<string | number, number> {

    transform(value: string | number, parameters: { [key: string]: any }): number {
        if (typeof value === "number") {
            if (value === 0) return new Date().getTime();
            return value;
        }

        let result: number | undefined;
        try {
            result = ms(value as Parameters<typeof ms>[0]);
        } catch (err) {
            throw new Error(`invalid time expression "${value}": ${(err as Error).message}`);
        }

        if (result === undefined) {
            throw new Error(`invalid result for time expression "${value}"`);
        }

        return new Date().getTime() + result;
    }
}
