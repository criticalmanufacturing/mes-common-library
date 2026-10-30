declare const process: { on(event: "exit", listener: () => void): void };

import { RoslynHostWorker } from "./roslynHostWorker";

/**
 * A small, fixed-size pool of warm {@link RoslynHostWorker}s. Callers `acquire()` an idle worker
 * (queuing, FIFO, if none is idle) and `release()` it back when done. A worker that dies (crash,
 * kill on timeout) is transparently replaced so the pool always settles back to its configured
 * size, and every worker is force-killed if the controller process itself exits, since child
 * processes are not reliably auto-killed when their parent exits on every platform.
 */
export class RoslynHostPool {
    private readonly _all = new Set<RoslynHostWorker>();
    private readonly _idle: RoslynHostWorker[] = [];
    private readonly _waiters: Array<(worker: RoslynHostWorker) => void> = [];
    private _shuttingDown = false;

    constructor(private readonly _size: number, private readonly _spawn: () => RoslynHostWorker) {
        for (let i = 0; i < this._size; i++) {
            this._spawnAndAdd();
        }
        process.on("exit", () => this.disposeAll());
    }

    public acquire(): Promise<RoslynHostWorker> {
        const worker = this._idle.pop();
        if (worker != null) {
            return Promise.resolve(worker);
        }
        return new Promise(resolve => this._waiters.push(resolve));
    }

    /** Returns a worker to the idle pool (or hands it straight to the next queued waiter). */
    public release(worker: RoslynHostWorker): void {
        if (!worker.isAlive || !this._all.has(worker)) {
            // Already reported through onExit, which replaced it — nothing to return.
            return;
        }
        const waiter = this._waiters.shift();
        if (waiter != null) {
            waiter(worker);
            return;
        }
        this._idle.push(worker);
    }

    public disposeAll(): void {
        this._shuttingDown = true;
        for (const worker of this._all) {
            worker.kill();
        }
        this._all.clear();
        this._idle.length = 0;
    }

    private _spawnAndAdd(): void {
        const worker = this._spawn();
        worker.onExit = () => this._onWorkerExit(worker);
        this._all.add(worker);
        this._idle.push(worker);
        this._drainWaiters();
    }

    private _onWorkerExit(worker: RoslynHostWorker): void {
        this._all.delete(worker);
        const idleIndex = this._idle.indexOf(worker);
        if (idleIndex >= 0) {
            this._idle.splice(idleIndex, 1);
        }
        if (!this._shuttingDown) {
            this._spawnAndAdd();
        }
    }

    private _drainWaiters(): void {
        while (this._idle.length > 0 && this._waiters.length > 0) {
            const worker = this._idle.pop();
            const waiter = this._waiters.shift();
            if (worker != null && waiter != null) {
                waiter(worker);
            }
        }
    }
}
