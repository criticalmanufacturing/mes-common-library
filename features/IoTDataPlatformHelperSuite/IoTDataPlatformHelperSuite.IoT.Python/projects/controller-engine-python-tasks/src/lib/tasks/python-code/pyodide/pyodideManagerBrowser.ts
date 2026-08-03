import { injectable } from "inversify";
import { PyodideSettings } from "./pyodideConfig";
import { PyodideManager, PyodideOutputsProxy } from "./pyodideManager";

/**
 * Mock Class for Browser
 */
@injectable()
export class PyodideManagerBrowser implements PyodideManager {

    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    public async initialize(settings: PyodideSettings): Promise<void> {
        console.warn("*** BROWSER MODE ***");
        console.warn("Code Execution is not possible to be used inside the browser, otherwise it would be initialized");
    }

    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    public async compile(code: string):
        Promise<(inputs: Record<string, unknown>, outputs: PyodideOutputsProxy) => Promise<unknown>> {
        console.warn("*** BROWSER MODE ***");
        console.warn("Code Execution is not possible to be used inside the browser, otherwise it would be initialized");
        return null;
    }

    public async destroy(): Promise<void> {
        console.warn("*** BROWSER MODE ***");
        console.warn("Code Execution is not possible to be used inside the browser, otherwise it would be initialized");
    }
}
