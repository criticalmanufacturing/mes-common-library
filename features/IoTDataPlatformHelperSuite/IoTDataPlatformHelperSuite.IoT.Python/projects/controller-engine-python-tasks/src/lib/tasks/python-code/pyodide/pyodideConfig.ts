/**
 * Settings for the Pyodide execution manager
 */
export interface PyodideSettings {
    /**
     * Python packages to install via micropip on initialization.
     * These are installed once when the manager initializes.
     */
    packages: string[];

    /**
     * Maximum time in milliseconds allowed for a single code execution.
     * 0 means no timeout (disabled).
     */
    executionTimeoutMs: number;
}
