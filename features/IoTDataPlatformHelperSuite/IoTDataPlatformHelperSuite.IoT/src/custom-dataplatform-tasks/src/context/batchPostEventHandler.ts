import {
    DI,
    System,
    TYPES,
    Dependencies,
    DataStoreLocation,
    QueueManagerType
} from "@criticalmanufacturing/connect-iot-controller-engine";
import {
    TYPES as COMMON_TYPES,
    Configuration,
    Logger
} from "@criticalmanufacturing/connect-iot-common";
import { BatchedTelemetryEvent } from "../utilities/interfaces";
import { SystemCalls } from "../utilities/systemCalls";

/**
 * Batch-posting settings a task may want to override. Any field left `undefined` falls back to
 * the `controller` section of the automation controller's config.json, then to the hard-coded
 * DEFAULT_SETTINGS below.
 */
export interface BatchPostSettings {
    /** Application name used when posting to DataPlatform; also identifies the batch queue */
    applicationName?: string;
    /** Flush pending events on this interval (ms) */
    intervalMs?: number;
    /** Flush immediately once this many events are pending, instead of waiting for the timer */
    batchSize?: number;
    /** NumberOfRetries sent on the PostMultipleIoTEvents input itself */
    numberOfRetries?: number;
}

/** Everything BatchPostEventHandler needs to flush a batch of queued telemetry events */
export interface BatchPostConfig extends BatchPostSettings {
    iotRetries?: number;
    iotSleepBetweenRetries?: number;
}

interface ResolvedBatchPostConfig extends Required<BatchPostSettings> {
    iotRetries: number;
    iotSleepBetweenRetries: number;
    systemProxy: System.SystemProxy;
    logger: Dependencies.Logger;
}

interface BatchGroup {
    config: ResolvedBatchPostConfig;
    timer: ReturnType<typeof setInterval>;
    pendingCount: number;
    flushing: boolean;
}

const QUEUE_PREFIX = "BatchPostEvents_";

/** Hard-coded fallbacks, used only when neither the task nor controller config.json define a value */
const DEFAULT_SETTINGS: Required<BatchPostSettings> = {
    applicationName: "ConnectIoTUNSConnector",
    intervalMs: 5000,
    batchSize: 50,
    numberOfRetries: 1
};

/**
 * Queues telemetry events per applicationName in the persisted DataStore and posts them
 * in a single PostMultipleIoTEvents call on a timer, instead of one DataPlatform call per event.
 * Registered as a Controller-scoped singleton so all task instances share the same queues/timers.
 *
 * Batch tuning settings follow a 3-tier precedence: an explicit value passed by the task wins,
 * otherwise the `controller` section of the automation controller's config.json is used, and
 * finally the DEFAULT_SETTINGS above.
 */
@DI.Injectable()
export class BatchPostEventHandler {

    @DI.Inject(TYPES.System.PersistedDataStore)
    private _dataStore: System.DataStore;

    @DI.Inject(COMMON_TYPES.Configuration)
    private _configuration: Configuration.Configuration;

    @DI.Inject(TYPES.System.Proxy)
    private _systemProxy: System.SystemProxy;

    @DI.Inject(COMMON_TYPES.Logger)
    private _logger: Logger;

    private _groups: Map<string, BatchGroup> = new Map();

    /**
     * Queue an event for batched posting. Persisted immediately so it survives a controller
     * restart. The first call for a given applicationName starts its flush timer; later calls
     * for the same application reuse it and just refresh the flush settings.
     */
    public async enqueue(config: BatchPostConfig, event: BatchedTelemetryEvent): Promise<void> {
        const resolved = this.resolveConfig(config);
        const key = resolved.applicationName;

        await this._dataStore.storeInQueue(this.queueName(key), event, { location: DataStoreLocation.Persistent });

        let group = this._groups.get(key);
        if (!group) {
            group = {
                config: resolved,
                pendingCount: 0,
                flushing: false,
                timer: setInterval(() => { void this.flush(key); }, resolved.intervalMs)
            };
            this._groups.set(key, group);
        } else {
            group.config = resolved;
        }

        group.pendingCount++;

        if (group.pendingCount >= group.config.batchSize) {
            void this.flush(key);
        }
    }

    /**
     * Resolves a single batch setting: the task's explicit value, then the `controller` section
     * of the controller's config.json, then the hard-coded default.
     */
    private resolveSetting<K extends keyof BatchPostSettings>(taskValue: BatchPostSettings[K], key: K): Required<BatchPostSettings>[K] {
        if (taskValue != null) {
            return taskValue as Required<BatchPostSettings>[K];
        }
        const configValue = this._configuration?.data?.controller?.dataplatform?.[key];
        return configValue != null ? configValue : DEFAULT_SETTINGS[key];
    }

    private resolveConfig(config: BatchPostConfig): ResolvedBatchPostConfig {
        return {
            applicationName: this.resolveSetting(config.applicationName, "applicationName"),
            intervalMs: this.resolveSetting(config.intervalMs, "intervalMs"),
            batchSize: this.resolveSetting(config.batchSize, "batchSize"),
            numberOfRetries: this.resolveSetting(config.numberOfRetries, "numberOfRetries"),
            iotRetries: config.iotRetries,
            iotSleepBetweenRetries: config.iotSleepBetweenRetries,
            systemProxy: this._systemProxy,
            logger: this._logger
        };
    }

    /** Drains and posts all events currently queued for the given applicationName */
    private async flush(key: string): Promise<void> {
        const group = this._groups.get(key);
        if (!group || group.flushing) {
            return;
        }

        group.flushing = true;
        try {
            const queueName = this.queueName(key);
            const events: BatchedTelemetryEvent[] = [];
            let item = await this._dataStore.retrieveFromQueue(queueName, QueueManagerType.FIFO, null);
            while (item != null && events) {
                events.push(item);
                item = await this._dataStore.retrieveFromQueue(queueName, QueueManagerType.FIFO, null);
            }
            group.pendingCount = 0;

            if (events.length === 0) {
                return;
            }

            try {
                await SystemCalls.postTelemetryBatch(
                    events,
                    group.config.applicationName,
                    group.config.numberOfRetries,
                    group.config.iotRetries,
                    group.config.iotSleepBetweenRetries,
                    group.config.systemProxy,
                    group.config.logger
                );
                group.config.logger.info(`Batch of ${events.length} event(s) posted successfully to Dataplatform`);
            } catch (error) {
                group.config.logger.error(`Error posting batch to Dataplatform: ${String(error)}`);
            }
        } finally {
            group.flushing = false;
        }
    }

    private queueName(key: string): string {
        return `${QUEUE_PREFIX}${key}`;
    }

    /** Stops all flush timers without posting remaining queued events. Intended for tests/shutdown. */
    public dispose(): void {
        for (const group of this._groups.values()) {
            clearInterval(group.timer);
        }
        this._groups.clear();
    }
}
