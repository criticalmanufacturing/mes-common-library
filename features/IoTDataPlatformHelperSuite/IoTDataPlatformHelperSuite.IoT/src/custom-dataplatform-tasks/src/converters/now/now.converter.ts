import type { Dependencies } from "@criticalmanufacturing/connect-iot-controller-engine";
import { Converter, DI, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";

export enum NowFormat {
    EpochMs = "epoch_ms",
    EpochSeconds = "epoch_seconds",
    ISO = "iso",
    UTC = "utc",
    Local = "local",
    Date = "date",
    Time = "time",
    WithTimezone = "timezone",
}

/**
 * @whatItDoes
 *
 * Returns the current date/time in the specified format.
 *
 * ### Parameters
 * * `format` - Output format (default: `epoch_ms`):
 *   - `epoch_ms`      → milliseconds since Unix epoch (number)
 *   - `epoch_seconds` → seconds since Unix epoch (number)
 *   - `iso`           → ISO 8601 string, e.g. "2024-01-15T10:30:00.000Z"
 *   - `utc`           → UTC string, e.g. "Mon, 15 Jan 2024 10:30:00 GMT"
 *   - `local`         → locale-aware date+time string
 *   - `date`          → date-only ISO string, e.g. "2024-01-15"
 *   - `time`          → time-only ISO string, e.g. "10:30:00.000"
 *   - `timezone`      → ISO 8601 with named timezone offset (requires `timezone` param)
 * * `timezone` - IANA timezone name, e.g. "America/New_York" (used with `timezone` format)
 *
 */
@Converter.Converter({
    name: "now",
    input: "any",
    output: "string",
    parameters: {
        format: NowFormat.EpochMs,
        timezone: "",
    },
})
export class NowConverter implements Converter.ConverterInstance<any, number | string> {

    transform(value: any, parameters: { format?: string; timezone?: string }): number | string {
        const now = new Date();
        const format = parameters?.format ?? NowFormat.EpochMs;
        const timezone = parameters?.timezone;

        switch (format) {
            case NowFormat.EpochMs:
                return now.getTime();

            case NowFormat.EpochSeconds:
                return Math.floor(now.getTime() / 1000);

            case NowFormat.ISO:
                return now.toISOString();

            case NowFormat.UTC:
                return now.toUTCString();

            case NowFormat.Local:
                return now.toLocaleString();

            case NowFormat.Date:
                return now.toISOString().split("T")[0];

            case NowFormat.Time:
                return now.toISOString().split("T")[1].replace("Z", "");

            case NowFormat.WithTimezone:
                if (timezone) {
                    return now.toLocaleString("en-US", {
                        timeZone: timezone, hour12: false,
                        year: "numeric", month: "2-digit", day: "2-digit",
                        hour: "2-digit", minute: "2-digit", second: "2-digit"
                    });
                }
                return now.toISOString();

            default:
                return now.getTime();
        }
    }
}
