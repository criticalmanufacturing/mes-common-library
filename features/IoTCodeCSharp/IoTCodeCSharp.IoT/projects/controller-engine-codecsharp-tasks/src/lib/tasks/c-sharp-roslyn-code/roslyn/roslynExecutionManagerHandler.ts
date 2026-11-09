declare const __dirname: string;
declare const require: (id: string) => unknown;
declare const process: { cwd(): string; hrtime: { bigint(): bigint } };
declare const Buffer: { from(value: string, encoding: string): { toString(encoding: string): string } };

interface NodeFS {
    existsSync(path: string): boolean;
    mkdirSync(path: string, options?: { recursive?: boolean }): void;
    writeFileSync(path: string, data: { toString(encoding: string): string } | string): void;
    unlinkSync(path: string): void;
    readdirSync(path: string): string[];
    statSync(path: string): { mtimeMs: number };
}
interface NodePath { join(...parts: string[]): string; }
interface NodeCrypto { randomUUID(): string; }

import { injectable } from "inversify";
import { RoslynExecutionContext, RoslynExecutionManager, RoslynExecutionManagerOptions, RoslynExecutionResponse } from "./roslynExecutionManager";
import { RoslynHostPool } from "./roslynHostPool";
import { RoslynHostWorker } from "./roslynHostWorker";
import { Container, Dependencies, DI, System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";

const DEFAULT_WARM_POOL_SIZE = 3;
// Safety net for DLLs orphaned by task nodes other than this one (e.g. left behind by a
// different task that died mid-execution during a restart): anything this old is stale.
const STALE_ASSEMBLY_MAX_AGE_MS = 60 * 60 * 1000;
// Transient Windows EPERM on a freshly written DLL (AV scan, indexer) clears in well under a
// second, so a handful of backing-off retries is plenty before falling back to the stale sweep.
const ASSEMBLY_UNLINK_MAX_RETRIES = 5;
const ASSEMBLY_UNLINK_RETRY_DELAY_MS = 100;

@injectable()
export class RoslynExecutionManagerHandler implements RoslynExecutionManager {
    private _initialized = false;
    private _cacheDirectory: string;
    private _hostAssemblyPath: string;
    private _pool: RoslynHostPool | null = null;
    private _pageName: string;
    private _taskId: string;

    @DI.Inject(TYPES.Dependencies.Logger)
    private _logger: Dependencies.Logger;

    public async initialize(pageName: string, taskId: string, options?: RoslynExecutionManagerOptions): Promise<void> {
        if (this._initialized) { return; }

        const fs = require("fs") as NodeFS;
        const path = require("path") as NodePath;
        this._cacheDirectory = path.join(process.cwd(), ".roslyn-code-cache");
        this._hostAssemblyPath = this._resolveHostAssemblyPath(fs, path);
        this._pageName = this._sanitizeForFilename(pageName);
        this._taskId = this._sanitizeForFilename(taskId);
        fs.mkdirSync(this._cacheDirectory, { recursive: true });
        this._cleanStaleAssemblies(fs, path);

        if (!fs.existsSync(this._hostAssemblyPath)) {
            throw new Error(`Roslyn execution host was not found at '${this._hostAssemblyPath}'.`);
        }

        await this._checkDotnetAvailable();

        if (options?.warmPoolEnabled) {
            const size = Math.max(1, options.warmPoolSize || DEFAULT_WARM_POOL_SIZE);
            this._pool = new RoslynHostPool(size, () => new RoslynHostWorker(this._hostAssemblyPath, this._cacheDirectory));
        }

        this._initialized = true;
    }

    private _checkDotnetAvailable(): Promise<void> {
        const { spawn } = require("child_process") as { spawn(command: string, args: string[], options: Record<string, unknown>): { on(event: "error" | "exit", callback: (...args: unknown[]) => void): void } };
        return new Promise<void>((resolve, reject) => {
            const child = spawn("dotnet", ["--version"], { cwd: this._cacheDirectory });
            child.on("error", reject);
            child.on("exit", (code: number | null) => code === 0 ? resolve() : reject(new Error(`'dotnet --version' exited with code ${code}.`)));
        });
    }

    private _sanitizeForFilename(value: string | undefined): string {
        return (value ?? "").replace(/[^A-Za-z0-9_-]/g, "_");
    }

    /**
     * Sweeps `.roslyn-code-cache` for DLLs abandoned by a process that died mid-execution
     * (e.g. killed during a restart, so the `finally` unlink in `execute()` never ran). Anything
     * belonging to this task node is stale by construction, since no execution for this node
     * could be in flight while it's only now (re)initializing; anything else is removed once it's
     * older than `STALE_ASSEMBLY_MAX_AGE_MS`, as a safety net for orphans left by other nodes.
     */
    private _cleanStaleAssemblies(fs: NodeFS, path: NodePath): void {
        let entries: string[];
        try {
            entries = fs.readdirSync(this._cacheDirectory);
        } catch {
            return;
        }

        const ownPrefix = `Code-${this._pageName}-${this._taskId}-`;
        let removedOwn = 0;
        let removedAged = 0;

        for (const entry of entries) {
            if (!entry.startsWith("Code-") || !entry.endsWith(".dll")) {
                continue;
            }

            const fullPath = path.join(this._cacheDirectory, entry);
            const isOwn = entry.startsWith(ownPrefix);
            let isAged = false;
            if (!isOwn) {
                try {
                    isAged = Date.now() - fs.statSync(fullPath).mtimeMs > STALE_ASSEMBLY_MAX_AGE_MS;
                } catch {
                    continue;
                }
            }

            if (!isOwn && !isAged) {
                continue;
            }

            try {
                fs.unlinkSync(fullPath);
                if (isOwn) { removedOwn++; } else { removedAged++; }
            } catch { /* best effort cleanup */ }
        }

        if (removedOwn > 0 || removedAged > 0) {
            this._logger?.debug(`[RoslynExecutionManagerHandler:${this._pageName}-${this._taskId}] cleaned up stale assemblies: own=${removedOwn} aged=${removedAged}`);
        }
    }

    private _resolveHostAssemblyPath(fs: NodeFS, path: NodePath): string {
        // The number of directory levels between this file and the package root differs between
        // environments: in a debug/local build it sits deep under the ng-packagr output
        // (dist/.../lib/tasks/c-sharp-roslyn-code/roslyn/...), while `cmf pack` bundles the
        // node-side code with ncc into a single <package root>/src/index.js, one level below the
        // package root. Rather than hardcode either depth (and break the other), walk upward from
        // __dirname checking each level for a sibling `roslyn-host` folder.
        const candidates: string[] = [];
        let current = __dirname;
        for (let hops = 0; hops <= 6; hops++) {
            candidates.push(
                path.join(current, "roslyn-host", "RoslynCode.Host.dll"),
                path.join(current, "roslyn-host", "publish", "RoslynCode.Host.dll")
            );
            current = path.join(current, "..");
        }
        return candidates.find(candidate => fs.existsSync(candidate)) ?? candidates[0];
    }

    public async execute(assemblyBase64: string, inputs: Record<string, unknown>, timeoutMs: number, context?: RoslynExecutionContext): Promise<RoslynExecutionResponse> {
        if (!this._initialized) {
            throw new Error("RoslynExecutionManager is not initialized.");
        }
        if (!assemblyBase64) {
            throw new Error("No browser-compiled C# assembly was saved for this task.");
        }

        const fs = require("fs") as NodeFS;
        const path = require("path") as NodePath;
        const crypto = require("crypto") as NodeCrypto;
        const executionId = crypto.randomUUID();
        const tStart = this._now();

        fs.mkdirSync(this._cacheDirectory, { recursive: true });
        const tMkdir = this._now();
        // Each execution gets its own file: concurrent calls may run on concurrent `dotnet`
        // processes, and a shared, fixed filename would race a writeFileSync here against another
        // call's process still reading the file, causing EBUSY under load.
        const assemblyPath = path.join(this._cacheDirectory, `Code-${this._pageName}-${this._taskId}-${executionId}.dll`);
        fs.writeFileSync(assemblyPath, Buffer.from(assemblyBase64, "base64"));
        const tWrite = this._now();
        let tDotnet: number;
        let tParse: number;

        try {
            const response = await this._executeOnWorker(assemblyPath, inputs, timeoutMs, context);
            tDotnet = this._now();
            tParse = tDotnet;
            return response;
        } finally {
            const tUnlinkStart = this._now();
            const deleted = this._tryDeleteAssembly(fs, assemblyPath);
            const tEnd = this._now();
            this._logger.debug(
                `[RoslynExecutionManagerHandler:${executionId}] pool=${this._pool != null} ` +
                `mkdir=${this._ms(tStart, tMkdir)} write=${this._ms(tMkdir, tWrite)} ` +
                `dotnet=${this._ms(tWrite, tDotnet ?? tUnlinkStart)} parse=${this._ms(tDotnet, tParse ?? tDotnet)} ` +
                `cleanup=${this._ms(tUnlinkStart, tEnd)}${deleted ? "" : " (deferred)"} total=${this._ms(tStart, tEnd)}`
            );
        }
    }

    /**
     * Deletes one execution's DLL, retrying in the background if the first attempt fails.
     *
     * The host no longer keeps a handle on this file (it loads the assembly from memory, see
     * `Program.cs`), but on Windows a just-written DLL can still be briefly held by something
     * outside our control — Defender/AV scanning it, the search indexer, a backup agent — which
     * surfaces here as a transient `EPERM`. Retrying a few times off the hot path clears those;
     * anything that somehow survives all retries is swept by `_cleanStaleAssemblies` on the next
     * initialize, so a permanent failure still can't leak files indefinitely.
     *
     * @returns whether the file was already gone on the first, synchronous attempt.
     */
    private _tryDeleteAssembly(fs: NodeFS, assemblyPath: string): boolean {
        if (this._unlinkOnce(fs, assemblyPath)) { return true; }

        let attempt = 0;
        const retry = () => {
            attempt++;
            if (this._unlinkOnce(fs, assemblyPath)) {
                this._logger?.debug(`[RoslynExecutionManagerHandler] deferred cleanup of '${assemblyPath}' succeeded on attempt ${attempt + 1}.`);
                return;
            }
            if (attempt >= ASSEMBLY_UNLINK_MAX_RETRIES) {
                this._logger?.warning(`[RoslynExecutionManagerHandler] could not delete '${assemblyPath}' after ${attempt + 1} attempts; leaving it for the stale-assembly sweep.`);
                return;
            }
            setTimeout(retry, ASSEMBLY_UNLINK_RETRY_DELAY_MS * attempt);
        };
        setTimeout(retry, ASSEMBLY_UNLINK_RETRY_DELAY_MS);
        return false;
    }

    /** One delete attempt; true when the file is gone afterwards, including when it never existed. */
    private _unlinkOnce(fs: NodeFS, assemblyPath: string): boolean {
        try {
            fs.unlinkSync(assemblyPath);
            return true;
        } catch {
            return !fs.existsSync(assemblyPath);
        }
    }

    private async _executeOnWorker(
        assemblyPath: string,
        inputs: Record<string, unknown>,
        timeoutMs: number,
        context?: RoslynExecutionContext
    ): Promise<RoslynExecutionResponse> {
        if (this._pool != null) {
            const worker = await this._pool.acquire();
            try {
                const response = await worker.execute(assemblyPath, inputs, timeoutMs, context);
                this._pool.release(worker);
                return response;
            } catch (error) {
                this._pool.release(worker);
                throw error;
            }
        }

        const worker = new RoslynHostWorker(this._hostAssemblyPath, this._cacheDirectory);
        try {
            return await worker.execute(assemblyPath, inputs, timeoutMs, context);
        } finally {
            worker.dispose();
        }
    }

    private _now(): number {
        return Number(process.hrtime.bigint()) / 1e6;
    }

    private _ms(from: number | undefined, to: number | undefined): string {
        return from == null || to == null ? "n/a" : `${(to - from).toFixed(1)}ms`;
    }
}
