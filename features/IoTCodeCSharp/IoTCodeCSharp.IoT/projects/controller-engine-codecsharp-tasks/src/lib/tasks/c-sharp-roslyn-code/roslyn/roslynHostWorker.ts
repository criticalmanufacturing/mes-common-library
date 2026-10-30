declare const process: { env: Record<string, string | undefined> };
declare const require: (id: string) => unknown;

import { RoslynExecutionContext, RoslynExecutionResponse } from "./roslynExecutionManager";

interface NodeReadable { on(event: "data", listener: (chunk: unknown) => void): void; }
interface NodeWritable { write(data: string): void; end(): void; }
interface NodeChildProcess {
    stdin: NodeWritable | null;
    stdout: NodeReadable | null;
    stderr: NodeReadable | null;
    on(event: "error", listener: (error: Error) => void): void;
    on(event: "exit", listener: (code: number | null) => void): void;
    kill(): void;
}

interface PendingRequest {
    resolve(response: RoslynExecutionResponse): void;
    reject(error: Error): void;
    context?: RoslynExecutionContext;
    pending: string;
    stdout: string;
    stderr: string;
    timer: ReturnType<typeof setTimeout> | null;
    timedOut: boolean;
    timeoutMs: number;
}

/**
 * Wraps a single `dotnet RoslynCode.Host.dll` process, which since the host's Program.cs was
 * changed to loop reading one JSON request per stdin line, can serve more than one execution
 * before exiting: {@link execute} may be called repeatedly on the same instance. A one-shot
 * caller just calls {@link execute} once and then {@link dispose}; a pool keeps calling
 * {@link execute} across many calls and only disposes on teardown.
 */
export class RoslynHostWorker {
    /** Notified once, when the underlying process exits for any reason (crash, kill, graceful EOF). */
    public onExit: ((worker: RoslynHostWorker) => void) | null = null;

    private readonly _child: NodeChildProcess;
    private _pending: PendingRequest | null = null;
    private _dead = false;
    private _disposed = false;

    constructor(hostAssemblyPath: string, cwd: string) {
        const { spawn } = require("child_process") as { spawn(command: string, args: string[], options: Record<string, unknown>): NodeChildProcess };
        this._child = spawn("dotnet", [hostAssemblyPath], { cwd, env: { ...process.env, NODE_OPTIONS: "" } });
        this._child.stdout?.on("data", chunk => this._onStdout(String(chunk)));
        this._child.stderr?.on("data", chunk => {
            if (this._pending != null) {
                this._pending.stderr += String(chunk);
            }
        });
        this._child.on("error", error => this._onExit(error, null));
        this._child.on("exit", code => this._onExit(null, code));
    }

    public get isAlive(): boolean {
        return !this._dead;
    }

    public execute(assemblyPath: string, inputs: Record<string, unknown>, timeoutMs: number, context?: RoslynExecutionContext): Promise<RoslynExecutionResponse> {
        if (this._dead) {
            return Promise.reject(new Error("Roslyn host worker is no longer alive."));
        }
        if (this._pending != null) {
            return Promise.reject(new Error("Roslyn host worker is already executing a request."));
        }
        return new Promise<RoslynExecutionResponse>((resolve, reject) => {
            const timer = timeoutMs > 0
                ? setTimeout(() => {
                    if (this._pending != null) {
                        this._pending.timedOut = true;
                    }
                    this._child.kill();
                }, timeoutMs)
                : null;
            this._pending = { resolve, reject, context, pending: "", stdout: "", stderr: "", timer, timedOut: false, timeoutMs };
            this._child.stdin?.write(JSON.stringify({ assemblyPath, inputs: inputs ?? {} }) + "\n");
        });
    }

    /** Ends the request stream so the host exits gracefully after finishing any in-flight request. */
    public dispose(): void {
        this._disposed = true;
        if (!this._dead) {
            try { this._child.stdin?.end(); } catch { /* ignore */ }
        }
    }

    /** Terminates the process immediately, without waiting for it to finish or exit gracefully. */
    public kill(): void {
        this._disposed = true;
        try { this._child.kill(); } catch { /* ignore */ }
    }

    private _onStdout(chunk: string): void {
        if (this._pending == null) {
            return;
        }
        const request = this._pending;
        request.pending += chunk;
        let newline: number;
        while ((newline = request.pending.indexOf("\n")) >= 0) {
            const line = request.pending.substring(0, newline).trim();
            request.pending = request.pending.substring(newline + 1);
            if (!line) {
                continue;
            }
            try {
                const message = JSON.parse(line) as { id?: string; method?: string; arguments?: Record<string, unknown> };
                if (message.id != null && message.method != null && request.context != null) {
                    const id = message.id;
                    request.context.invoke(message.method, message.arguments ?? {})
                        .then(result => this._child.stdin?.write(JSON.stringify({ id, result }) + "\n"))
                        .catch((error: Error) => this._child.stdin?.write(JSON.stringify({ id, error: error.message }) + "\n"));
                    continue;
                }
            } catch {
                // Not a bridge call line — fall through and treat it as the final response line below.
            }
            this._settle(line);
            return;
        }
    }

    private _settle(responseLine: string): void {
        const request = this._pending;
        if (request == null) {
            return;
        }
        this._pending = null;
        if (request.timer != null) {
            clearTimeout(request.timer);
        }
        let response: RoslynExecutionResponse;
        try {
            response = JSON.parse(responseLine) as RoslynExecutionResponse;
        } catch {
            request.reject(new Error(`Roslyn execution host returned invalid JSON: ${responseLine}`));
            return;
        }
        if (response.error) {
            request.reject(new Error(response.error));
            return;
        }
        request.resolve(response);
    }

    private _onExit(error: Error | null, code: number | null): void {
        if (this._dead) {
            return;
        }
        this._dead = true;
        const request = this._pending;
        this._pending = null;
        if (request != null) {
            if (request.timer != null) {
                clearTimeout(request.timer);
            }
            if (request.timedOut) {
                request.reject(new Error(`Roslyn execution timed out after ${request.timeoutMs}ms.`));
            } else if (error != null) {
                request.reject(error);
            } else {
                request.reject(new Error(`Roslyn execution host exited with code ${code}: ${request.stderr || request.stdout}`));
            }
        }
        this.onExit?.(this);
    }
}
