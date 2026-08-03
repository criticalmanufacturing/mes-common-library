import {
    Task,
    Dependencies,
    System,
    Utilities,
    TaskBase,
    TYPES,
    DataStoreLocation
} from "@criticalmanufacturing/connect-iot-controller-engine";

import {
    Buffer
} from "buffer";

import {
    Moment
} from "moment";

import { SystemCalls } from "../../utilities/systemCalls";
import { ISA95, PostTelemetry, PostTelemetryParameter } from "../../utilities/interfaces";
import { BatchPostEventHandler } from "../../context/batchPostEventHandler";
import { inject } from "inversify";
import Cmf from "cmf-lbos";

/**
 * @whatItDoes
 *
 * This task does something ... describe here
 *
 * @howToUse
 *
 * yada yada yada
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 *
 * ### Outputs
 *
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see EquipmentEventWithDataplatformSettings}
 */
@Task.Task()
export class EquipmentEventWithDataplatformTask extends TaskBase implements EquipmentEventWithDataplatformSettings {

    @inject(TYPES.Task.Definition)
    private _taskDefinition: any;

    @inject("GlobalBatchPostEventHandler")
    private batchPostEventHandler: BatchPostEventHandler;

    @inject(TYPES.Values.Controller)
    private _controller: Cmf.Foundation.BusinessObjects.AutomationController;

    private _pageName: any;

    /** Selected event (new mode) */
    _selectedEvent?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent;
    /** Event to listen (if any) */
    _event?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent;
    /** List of events registered for notifications. If empty, all events will be triggered */
    _events: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent[] = [];
    /** Automation Events Properties */
    _automationEventProperties: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty[];
    /** Auto activate the event listeners */
    _autoActivate: boolean = true;
    /** Working mode of the event listening */
    _workingMode: EquipmentEventWorkingMode;
    /** When workingMode is OneEvent, this will be the number of ms to wait for the event. On timeout, event listening will be disabled */
    _waitTimeout: number;
    /** Output settings */
    _outputs: EquipmentEventOutputSettings[];
    /** Accept all events flag */
    _acceptAllEvents: boolean = false;
    /** Custom events flag */
    _customEvents: boolean = false;
    /** Message Name (when custom) */
    _message: EventComboBox;
    /** Message Full Name (when custom) */
    _messageFullName: string;

    /** If Event is to be posted to DP */
    _isToPostToDP: boolean = true;
    _resolveISA95OnEachEvent: boolean = true;

    /** If the task should continue executing and emit success even if it fails to post to Dataplatform */
    _continueOnDPError: boolean = true;

    /** Application name used when posting to DataPlatform */
    _applicationName: string = "ConnectIoT";

    /** If events should be queued and posted in batches instead of posting each one immediately */
    _isToBatchPost: boolean = false;
    /**
     * Timer interval (ms) at which pending batched events are flushed to the Data Platform.
     * If not set, falls back to the `controller` section of the controller's config.json,
     * then to BatchPostEventHandler's own hard-coded default.
     */
    _batchIntervalMs?: number;
    /**
     * Maximum number of pending events before an immediate flush is triggered, regardless of the
     * timer. Same fallback order as `_batchIntervalMs`.
     */
    _batchSize?: number;
    /** NumberOfRetries sent on the PostMultipleIoTEvents input itself. Same fallback order as `_batchIntervalMs`. */
    _batchNumberOfRetries?: number;

    /** Flag indicating if the listeners were already registered or not */
    private _activated: boolean = false;
    private _instance: System.LBOS.Cmf.Foundation.BusinessObjects.Entity;

    /** event that occurred output */
    @Task.OutputProperty(System.PropertyValueType.ReferenceType)
    public event: Task.Output<System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent> =
        new Task.Output<System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent>();
    /** Timestamp of the occurrence */
    @Task.OutputProperty(Task.TaskValueType.DateTime)
    public timestamp: Task.Output<Date> = new Task.Output<Date>();
    /** Event occurred Raw Data */
    @Task.OutputProperty(Task.TaskValueType.Object)
    public eventRawData: Task.Output<object> = new Task.Output<object>();

    private _waitTimer: ReturnType<typeof setTimeout>;

    private onEventHandler: System.EquipmentEventOccurrenceCallback<any> = async (occurrence: System.EquipmentEventOccurrence<any>): Promise<void> => {
        const newExecutionContext = this._executionContext.fork({
            properties: {
                originalEvent: occurrence.event,
                originalTimeStamp: occurrence.timestamp,
                originalRaw: occurrence
            } as EquipmentEventZoneContext
        });
        newExecutionContext.run(async () => {
            this._logger.debug(`Event '${occurrence.event.Name}' received from DriverProxy`);

            // trigger event and timestamp
            this.event.emit(occurrence.event);
            this.timestamp.emit(occurrence.timestamp.toDate());
            this.eventRawData.emit(occurrence);

            // if on one event mode deactivate event
            if (this._workingMode === EquipmentEventWorkingMode.OneEvent) {
                this.deActivateEventsListening();
            }

            for (const property of this._outputs ?? []) {
                const value = occurrence.values.find(v => v.property.Name === property.name);
                if (value == null) {
                    this._logger.warning(`No value for property '${property.name}'`);
                } else {
                    let outputValue: any = undefined;

                    switch (property.outputType) {
                        case EquipmentEventOutputType.RawValue:
                            this._logger.debug(`Emitting raw value for property '${property.name}': ${Utilities.objectToString(value.originalValue)}`);

                            if (value.originalValue && (typeof value.originalValue === "object") && value.originalValue.type) {
                                switch (value.originalValue.type) {
                                    case "Buffer": outputValue = Buffer.from(value.originalValue.data); break;
                                    default: outputValue = value.originalValue; break;
                                }
                            } else {
                                outputValue = value.originalValue;
                            }

                            break;
                        default:
                            this._logger.debug(`Emitting property value '${property.name}'='${value.value}'`);
                            outputValue = value.value;
                            break;
                    }

                    (this[`${property.name}`] as Task.Output<any>).emit(outputValue);
                }
            }

            if (this._isToPostToDP) {
                try {

                    this._logger.debug(`Posting event '${occurrence.event.Name}' to Dataplatform`);

                    // Build Parameters/Tags from outputs flagged for DataPlatform
                    const timestamp = occurrence.timestamp.valueOf().toString();
                    const data: PostTelemetry = { Parameters: [], Tags: [] };
                    for (const property of this._outputs?.filter(o => o.isToPostToDP) ?? []) {
                        const value = occurrence.values.find(v => v.property.Name === property.name);
                        if (value != null) {
                            const prop = this._driverProxy?.automationControllerDriverDefinition
                                ?.AutomationDriverDefinition?.Properties
                                ?.find(p => p.Name === property.name);
                            const name = (prop?.ExtendedData as any)?.path ?? property.name;

                            const param: PostTelemetryParameter = {
                                Class: property.class ?? "Sensor",
                                Name: name,
                                UnitOfMeasure: property.unitOfMeasure,
                                NumericValues: [value.value],
                                Timestamps: [timestamp]
                            };
                            if (typeof value.value === "number") {
                                param.NumericValues = [value.value];
                            } else {
                                param.StringValues = [Utilities.objectToString(value.value)];
                            }

                            data.Parameters.push(param);
                        }
                    }

                    data.Tags.push({ Key: "EventName", Value: occurrence.event.Name });
                    data.Tags.push({ Key: "EventOccurrenceTimestamp", Value: timestamp });
                    data.Tags.push({ Key: "WorkflowPage", Value: this._pageName });
                    data.Tags.push({ Key: "TaskId", Value: this._taskDefinition?.id ?? "unknown" });
                    data.Tags.push({ Key: "TaskName", Value: this._taskDefinition?.name ?? this._taskDefinition?.reference?.name ?? "unknown" });
                    data.Tags.push({ Key: "ExecutionContext", Value: this._executionContext?.name ?? "unknown" });

                    data.Tags.push({ Key: "ControllerId", Value: this._controller?.Id ?? "unknown" });
                    data.Tags.push({ Key: "ControllerName", Value: this._controller?.Name ?? "unknown" });

                    this._instance = this._container.get<System.LBOS.Cmf.Foundation.BusinessObjects.Entity>(TYPES.Values.Entity);
                    data.Tags.push({ Key: "InstanceId", Value: this._instance?.Id ?? "unknown" });
                    data.Tags.push({ Key: "InstanceName", Value: this._instance?.Name ?? "unknown" });

                    // Resolve ISA-95 hierarchy from the current instance
                    if (this._instance != null) {
                        let isa95: ISA95 = {};
                        if (this._resolveISA95OnEachEvent) {
                            isa95 = await SystemCalls.extractISA95(
                                this._instance,
                                this._systemAPI,
                                this.logAndEmitError.bind(this)
                            );

                        } else {
                            let instanceResolution: Map<string, ISA95> = await this._dataStore.retrieve("InstanceResolution", null);

                            if (instanceResolution?.[`${this._instance.Name}_${this._instance["$type"].toString()}`]) {
                                isa95 = instanceResolution?.[`${this._instance.Name}_${this._instance["$type"].toString()}`];
                            } else {
                                isa95 = await SystemCalls.extractISA95(
                                    this._instance,
                                    this._systemAPI,
                                    this.logAndEmitError.bind(this)
                                );

                                if (!instanceResolution) {
                                    instanceResolution = new Map<string, ISA95>();
                                }
                                instanceResolution.set(`${this._instance.Name}_${this._instance["$type"].toString()}`, isa95);
                                this._dataStore.store("InstanceResolution", instanceResolution, DataStoreLocation.Temporary);
                            }
                        }
                        if (isa95.Material) { data.Material = { Name: isa95.Material }; }
                        if (isa95.Resource) { data.Resource = { Name: isa95.Resource }; }
                        if (isa95.Area) { data.Area = { Name: isa95.Area }; }
                        if (isa95.Facility) { data.Facility = { Name: isa95.Facility }; }
                        if (isa95.Site) { data.Site = { Name: isa95.Site }; }
                        if (isa95.Enterprise) { data.Enterprise = { Name: isa95.Enterprise }; }
                    }

                    if (this._isToBatchPost) {
                        await this.batchPostEventHandler.enqueue(
                            {
                                applicationName: this._applicationName,
                            },
                            { data, eventTime: timestamp }
                        );
                    } else {
                        await SystemCalls.postTelemetry(
                            data,
                            this._applicationName,
                            false,
                            this.retries,
                            this.sleepBetweenRetries,
                            1,
                            this._systemProxy,
                            this._logger
                        );
                    }
                } catch (error) {
                    if (!this._continueOnDPError) {
                        this.logAndEmitError(`Error posting to Dataplatform: ${Utilities.objectToString(error)}`);
                    }
                }
            }

            // Trigger the success to notify the finish of processing
            this.success.emit(true);
        });
    };

    /**
     * Notification that some input value was changed
     * When activate = true, register listeners and activate timeout timer, if applicable
     * else, disable the listeners
     * @param changes List of input changes occurred
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {

            // Allow notification for the same value
            this.activate = undefined;

            const valueConverted = Utilities.convertValueToType(changes["activate"].currentValue, Task.TaskValueType.Boolean);

            if (valueConverted === true) {
                await this.activateEventsListening();
            } else {
                await this.deActivateEventsListening();
            }
        }
    }

    /**
     * Prepare the internal data
     */
    public override async onBeforeInit(): Promise<void> {
        // Just to keep compatibility with the Angular version, map any _event into the _events
        if (this._event != null) {
            this._events = [this._event];
        }

        if (this._events && this._driverProxy) {
            if (this._events.length > 1) {
                throw new Error("Multiple events is not yet supported!");
            }

            if (this._events.length === 0) {
                // No outputs will be available. It is up to the "designer" to know the structure of the event received
            } else {
                for (const property of this._outputs ?? []) {
                    if (property) {
                        this[`${property.name}`] = new Task.Output<any>();
                    }
                }
            }
        }
        this._autoActivate = Utilities.convertValueToType(this._autoActivate, Task.TaskValueType.Boolean, true);
    }

    /** Initialize this task, register the events if autoActivate mode is on */
    public override async onInit(): Promise<void> {
        this._waitTimeout = Number(this._waitTimeout);

        // Subscribe to receive the ISA-95 entity instance associated with this driver
        const driverName = this._driverProxy?.automationControllerDriverDefinition?.AutomationDriverDefinition?.Name;
        if (driverName) {
            const instanceProxy = this._container.get<System.InstanceProxy>(TYPES.Task.InstanceProxy);
            instanceProxy.subscribe(driverName, (model) => {
                this._instance = model.instance;
            });
        }

        if (this._autoActivate) {
            await this.activateEventsListening();
        }

        this._pageName = this._container.get<string>(TYPES.Task.PageName);
    }

    /** Cleanup internal data and deactivate all event notifiers */
    public override async onDestroy(): Promise<void> {
        if (Array.isArray(this._events) && this._events.length > 0) {
            for (const property of this._outputs ?? []) {
                if (property) {
                    (this[`${property.name}`] as Task.Output<any>)?.destroy();
                    delete this[`${property.name}`];
                }
            }

            await this.deActivateEventsListening();
        }
    }

    /** Unsubscribe event notification */
    private async deActivateEventsListening(): Promise<void> {
        if (this._activated) {
            this._activated = false;

            this._logger.debug("Deactivating event listening");

            if (this._events) {
                this._driverProxy.unsubscribeEvents(this._events, this.onEventHandler);
                if (this._workingMode === EquipmentEventWorkingMode.OneEvent && this._waitTimeout > 0 && this._waitTimer) {
                    clearTimeout(this._waitTimer);
                }
            }
        }
    }

    /** Subscribe event notification */
    private async activateEventsListening(): Promise<void> {
        if (!this._activated) {
            this._activated = true;

            this._logger.debug("Activating event listening");

            if (this._events) {
                this._driverProxy.subscribeEvents(this._events, this.onEventHandler);
                if (this._workingMode === EquipmentEventWorkingMode.OneEvent && this._waitTimeout > 0) {
                    // Timeout when the event is not triggered in time
                    this._waitTimer = setTimeout(() => {
                        if (this._workingMode === EquipmentEventWorkingMode.OneEvent) {
                            /** Deactivate Events*/
                            this.deActivateEventsListening();
                            /** Set Error */
                            this.error.emit(new Error("Timeout occurred"));
                            this._logger.error(`Timeout: Event not received after ${this._waitTimeout} ms. Deactivating listener`);
                        }
                    }, this._waitTimeout);
                }
            }
        }
    }
}

/** EquipmentEvent task settings*/
export interface EquipmentEventWithDataplatformSettings {
    /** Selected event (new mode) */
    _selectedEvent?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent;
    /** Event to listen (if any) */
    _event?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent;
    /** Events being listened that can trigger this task. Empty for all events */
    _events: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent[];
    /** Automation Events Properties */
    _automationEventProperties: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty[];
    /** Auto activate the event listeners */
    _autoActivate: boolean;
    /** Task behavior when an event occurs */
    _workingMode: EquipmentEventWorkingMode;
    /** Number of ms to wait for an event, when the workingMode is set to only one event triggering */
    _waitTimeout: number;

    /** List of output values and respective expected value */
    _outputs: EquipmentEventOutputSettings[];
    /** Accept all events flag */
    _acceptAllEvents: boolean;
    /** Custom events flag */
    _customEvents: boolean;
    /** Message Name (if custom) */
    _message: EventComboBox;
    /** Message Full Name (if custom) */
    _messageFullName: string;
}

/** Custom Events Combo Box */
export interface EventComboBox {
    /** Display Name for an event */
    name: string
}

/** EquipmentEvent task output settings */
export interface EquipmentEventOutputSettings extends System.Property {
    /** AutomationProperty only used for migration to 10x */
    property?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProperty;
    /** Name that will appear in Task */
    name: string;
    /** Device property id */
    propertyId: string;
    /** Default value that will be used if no other was retrieved */
    defaultValue: any;
    /** Device type */
    deviceType: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationProtocolDataType;
    /** Value Type */
    valueType: Task.TaskComplexValueType;
    /** Type of data to emit */
    outputType: EquipmentEventOutputType;
    /** Property Type (e.g. FileName, FullPath, CreationTime, etc.) only used for templates*/
    propertyType?: string;
    /** If Property is to be posted to DP */
    isToPostToDP: boolean;
    /** Unit of measure used when the property is posted to DP as a numeric Parameter */
    unitOfMeasure?: string;
    /** Class, accepted values Sensor State Property */
    class?: PostTelemetryAcceptedClass;
}

/** EquipmentEvent task output value data to emit*/
export enum EquipmentEventOutputType {
    /** Equipment data value converted to the defined datatype */
    Value = "Value",
    /** Raw value received from the equipment */
    RawValue = "RawValue"
}

/** EquipmentEvent task working mode */
export enum EquipmentEventWorkingMode {
    /** Receive all events triggered from the event, while the task is active */
    AlwaysActive = "AlwaysActive",
    /** Receive one event and disable the task right after. Timeout can be defined for this mode. */
    OneEvent = "OneEvent"
}

/**
 * Enum describing the accepted classes for telemetry parameters when posting to DataPlatform
 */
export enum PostTelemetryAcceptedClass {
    /** A continuous measurement from a physical sensor — things like temperature, pressure, voltage, humidity. These are typically numeric, time-series values that change frequently. */
    Sensor = "Sensor",
    /** A discrete status or mode of the equipment — e.g., Idle, Running, Error, Alarm. These represent the operational condition of the machine at a point in time. */
    State = "State",
    /** A configuration or descriptive attribute of the equipment — e.g., a recipe name, a set-point value, a software version. These tend to change less frequently and describe what the equipment is set to, rather than what it's measuring or doing. */
    Property = "Property"
}

/**
 * Execution context
 */
export interface EquipmentEventZoneContext extends Dependencies.ExecutionContextSpecificationProperties {
    originalEvent: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent,
    originalTimeStamp: Moment,
    originalRaw: System.EquipmentEventOccurrence<any>
}

@Task.TaskModule({
    task: EquipmentEventWithDataplatformTask,
    providers: [
        {
            class: BatchPostEventHandler,
            isSingleton: true, // Should this component be a Singleton for the whole Controller
            symbol: "GlobalBatchPostEventHandler", // Name that will be used to Inject the container in the dependency injection
            scope: Task.ProviderScope.Controller, // Injection scope (Local; WorkflowPlan; Controller)
        }
    ]
})
export class EquipmentEventWithDataplatformModule { }