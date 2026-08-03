import { injectable, inject, Container } from "inversify";
import { CommunicationState, PropertyValuePair } from "@criticalmanufacturing/connect-iot-driver";
import { Property, EventOccurrence, PropertyValue, Command, Event as EquipmentEvent, DeviceDriverBase, CommandParameter } from "@criticalmanufacturing/connect-iot-driver";
import { MTConnectCommunicationSettings, mTConnectDefaultCommunicationSettings, validateCommunicationParameters } from "./communicationSettings";
import { validateProperties, validateEvents, validateEventProperties, validateCommands, validateCommandParameters } from "./extendedData/index";
import { Utils } from "@criticalmanufacturing/connect-iot-common";
import { TYPES } from "./types";
import { MTConnectHandler } from "./mTConnect/mTConnectHandler";
import { ExtensionHandler } from "./extensions";
import { Expression } from "jsonata";

@injectable()
export class MTConnectDeviceDriver extends DeviceDriverBase {
    private _communicationSettings: MTConnectCommunicationSettings;
    private _container: Container;

    @inject(TYPES.Injector)
    private _parentContainer: Container;

    private _mTConnectHandler: MTConnectHandler;
    private _extensionHandler: ExtensionHandler;
    // List of custom events registered
    private _customEvents: Map<string, EquipmentEvent> = new Map<string, EquipmentEvent>();
    private _jsonata: any;

    public constructor() {
        super();

        // Should redirect Execute Command to Set Property Values instead?
        this.__useSetValuesInsteadOfExecute = false;
    }

    /**
     * Initialize the driver. Used to prepare all components from containers, register events, etc.
     * Note: Called by the driverBase
     */
    public async initializeDriver(): Promise<void> {
        // JSONata is an ESM package so we need to loaded it
        this._jsonata = await import("jsonata");

        if (!this._container) {
            this._container = new Container();
            this._container.parent = this._parentContainer;

            this._mTConnectHandler = this._container.get<MTConnectHandler>(TYPES.MTConnectHandler);
            this._extensionHandler = this._container.get<ExtensionHandler>(TYPES.ExtensionHandler);
        }

        // Initialize the specific driver
        await this._mTConnectHandler.initialize(this._container);
        await this._extensionHandler.initialize(this._container);

        this.subscribeEvents();
    }

    /**
     * Sync lifecycle with Handler Hooks
     */
    private subscribeEvents(): void {
        this._mTConnectHandler.removeAllListeners();
        this._mTConnectHandler.on("connected", async () => {
            this.logger.debug("Connected");
            this.setCommunicationState(CommunicationState.Setup);
        });
        this._mTConnectHandler.on("disconnected", () => {
            this.logger.debug("Disconnected");
            this.setCommunicationState(CommunicationState.Disconnected);
        });
    }
    /**
     * Notification regarding the communication parameters being available.
     * Validate the integrity of the values
     * Note: Called by the driverBase
     * @param communication Communication settings object
     */
    public async setCommunicationConfiguration(communication: any): Promise<void> {
        this._communicationSettings = Object.assign({}, mTConnectDefaultCommunicationSettings, communication);

        // eslint-disable-next-line
        const pJson = require("../package.json");
        validateCommunicationParameters(pJson, this._communicationSettings);

        // Prepare the extended data
        validateProperties(pJson, this.configuration.properties);
        validateEvents(pJson, this.configuration.events);
        validateEventProperties(pJson, this.configuration.events);
        validateCommands(pJson, this.configuration.commands);
        validateCommandParameters(pJson, this.configuration.commands);

        // Initialize assembly
        this._mTConnectHandler.setPackageJson(pJson);
        this._mTConnectHandler.setConfiguration(this.configuration, this._communicationSettings);
    }

    /**
     * Connect to the equipment.
     * Note: Called by the driverBase
     */
    public async connectToDevice(): Promise<void> {
        this.setCommunicationState(CommunicationState.Connecting);

        try {
            // Connect to the equipment

            // Provide an empty object as a fallback to avoid errors
            const { $id, ...cleanCommunicationSettings } = this._communicationSettings as any || {};
            this.logger.info(`Using the following configurations: ${JSON.stringify(cleanCommunicationSettings, undefined, " ")}`);

            // edge-js is initialized here because driver needs to read the sdkVersion before using it
            await this._mTConnectHandler.initializeEdge();
            // Connect to the equipment
            await this._mTConnectHandler.connect(this._communicationSettings);

            // Notify the communication was a success and it is now ready for the setup process
            this.setCommunicationState(CommunicationState.Setup);
        } catch (error) {
            this.logger.error(`Failed to connect to device: ${error.message}`);
            this.setCommunicationState(CommunicationState.ConnectingFailed);
        }
    }

    /**
     * Disconnect the communication with the equipment
     * Note: Called by the driverBase
     */
    public async disconnectFromDevice(): Promise<void> {
        this.setCommunicationState(CommunicationState.Disconnecting);

        try {
            // Disconnect to the equipment
            await (this._mTConnectHandler.disconnect());
        } catch (error) {
            this.logger.error(`Failed to disconnect from device: ${error.message}`);
        }

        this.setCommunicationState(CommunicationState.Disconnected);
    }

    /**
     * Notification that the setup process was a success
     * Note: Called by the driverBase
     */
    public async setupCompleted(): Promise<void> {
        // Since the setup was a success, set the state to Communicating
        await this.setCommunicationState(CommunicationState.Communicating);

        // Register events from the Driver Definition
        this._mTConnectHandler.setDriverDefinitionsEvents(this.configuration);

        // Set listener to the Driver event occurrences
        this._mTConnectHandler.on("onMTConnectEventOccurrence", this.onEventOccurrence.bind(this));
    }

    /**
     * Request the equipment for values of the properties
     * Note: Called by the driverBase
     * @param properties List of properties to get values
     */
    public async getValues(properties: Property[]): Promise<PropertyValue[]> {
        const results: PropertyValue[] = [];

        // Request the equipment for values
        // ...
        // Foreach result:
        /*
            let propertyValue: PropertyValue = {
                propertyName: property.name,
                originalValue: value,
                value: this.convertValueFromDevice(value, property.deviceType, property.dataType),
            };
            results.push(propertyValue);
        */

        return (results);
    }

    /**
     * Set the value of properties in the equipment.
     * Note: Called by the driverBase
     * @param propertiesAndValues List of properties and new values
     */
    public async setValues(propertiesAndValues: PropertyValuePair[]): Promise<boolean> {
        // Request the equipment to define new values
        // ...

        return (true);
    }

    /**
     * Send a command to the equipment. Depending on some settings, different messages can be sent.
     * Note: Called by the driverBase
     * @param command Command to send
     * @param parameters List of parameters to use
     */
    public async execute(command: Command, parameters: Map<CommandParameter, any>): Promise<any> {
        // Execute the command in the equipment
        const commandParameters: Map<string, any> = new Map<string, any>();
        for (const [key, value] of parameters) {
            if (value != null && String(value).trim() !== "") {
                commandParameters.set(key.name, this.convertValueToDevice(value, key.deviceType, (key.defaultValue as any)));
            }
        }
        const reply = await this._mTConnectHandler.executeCommand(command.deviceId, commandParameters);

        return (reply); // Or the command result
    }

    /**
     * Handle the communication state changes
     * Note: Called by the driverBase
     * @param previousState Previous state
     * @param newState New state
     */
    public async notifyCommunicationStateChanged(previousState: CommunicationState, newState: CommunicationState): Promise<void> {
        // Add any specific handling here
    }

    /**
     * Register a custom/Extension Event to listen to
     * Usually these events are registered from custom events coming from controller custom tasks
     * @param event Event data to register
     */
    public registerCustomEvent(event: EquipmentEvent): void {

        if (this._customEvents.has(event.name)) {
            this.logger.warning(`Custom event '${event.name}' was already registered. Ignoring this registration.`);
        } else {

            this.logger.info(`Registering custom event '${event.name}', for topic '${event.deviceId}', associated with '${event.properties.length}' properties`);
            this._customEvents.set(event.name, event);

            const count = Array.from(this._customEvents.values()).filter((v: EquipmentEvent) => v.deviceId === event.deviceId).length;

            // Only register the first event by type (deviceId)
            if (count === 1) {
                this._mTConnectHandler.registerEvent(event.deviceId);
            } else {
                this.logger.debug(`Event Topic '${event.deviceId}' already have '${count - 1}' other custom events registered. Not subscribing in broker`);
            }
        }
    }

    /**
     * Unregister a custom event previously registered
     * @param event Name of the event to unregister
     */
    public unregisterCustomEvent(eventName: string): void {

        const eventData = this._customEvents.get(eventName);
        if (eventData != null) {
            this.logger.info(`UnRegistering custom event '${eventName}' with topic '${eventData.deviceId}'`);

            this._customEvents.delete(eventName);
            const count = Array.from(this._customEvents.values()).filter((v: EquipmentEvent) => v.deviceId === eventData.deviceId).length;

            // Only unregister when there are no more events of that type (deviceId)
            if (count === 0) {
                this._mTConnectHandler.unregisterEvent(eventData.deviceId);
            } else {
                this.logger.debug(`Event Topic '${eventData.deviceId}' still has '${count}' other custom events registered`);
            }
        } else {
            this.logger.warning(`Custom Event '${eventName}' was not previously registered. Ignoring this call.`);
        }
    }

    /**
     * Handle the driver event notification. Trigger it to the controller if the trigger property was changed
     * Note: This is just as an example... This code is not being called anywhere
     * @param eventId Id of the event (systemId)
     * @param values List of values of the event registered
     */
    private async onEventOccurrence(
        eventId: string,
        eventOccurrence: {
            values: { values: object };
            occurrenceTimeStamp: Date
        }): Promise<void> {

        // Values values was done so as to leave the responsibility of the event parsing to the property path
        let evtOccurValue = new Map<string, any>(Object.entries(eventOccurrence.values.values));

        const registeredEvents = Array.from(this._customEvents.values()).filter((evt: EquipmentEvent) => evt.deviceId === eventId && evt.isEnabled === true);
        if (registeredEvents.length > 0) {
            if (evtOccurValue) {
                for (const evt of registeredEvents) {
                    this._extensionHandler.handleEventOccurrence(
                        evt.name,
                        eventOccurrence.occurrenceTimeStamp,
                        await this.parseEventProperties(evt, evtOccurValue));
                }
            }
        }

        const event = this.configuration.events.find(e => e.systemId === eventId);
        if (event && event.isEnabled) {
            const results: PropertyValue[] = [];
            evtOccurValue = await this.parseEventProperties(event, evtOccurValue);

            // Fill results and check if the trigger properties have been the cause of the event occurrence
            if (evtOccurValue) {
                for (const eventProperty of event.properties) {
                    if (evtOccurValue.has(eventProperty.deviceId)) {
                        const value: any = evtOccurValue.get(eventProperty.deviceId);

                        const propertyValue: PropertyValue = {
                            propertyName: eventProperty.name,
                            originalValue: value,
                            value: this.convertValueFromDevice(value, eventProperty.deviceType, eventProperty.dataType),
                        };

                        results.push(propertyValue);
                    } else {
                        throw new Error(`Value for property '${eventProperty.name}' was not received in the event data`);
                    }
                }
            }

            // Raise event to controller
            const occurrence: EventOccurrence = {
                timestamp: new Date(),
                eventDeviceId: event.deviceId,
                eventName: event.name,
                eventSystemId: event.systemId,
                propertyValues: results
            };

            this.emit("eventOccurrence", occurrence);
        }
    }

    /**
     * Using jsonata expression construct the event occurrence
     * @param evt Event definition
     * @param evtOccurValue Event Occurrence
     * @param values
     * @returns
     */
    private async parseEventProperties(evt: EquipmentEvent, evtOccurValue: Map<string, any>): Promise<Map<string, any>> {
        const values = new Map<string, any>();
        for (const prop of evt.properties) {
            if (prop.extendedData.expression != null && prop.extendedData.expression !== "") {

                // eslint-disable-next-line no-inner-declarations
                function mapToParsedObject(map: Map<string, string>): Record<string, any> {
                    return Object.fromEntries(
                        Array.from(map.entries()).map(([key, value]) => {
                            try {
                                return [key, JSON.parse(value)];
                            } catch {
                                return [key, value]; // fallback if it's not valid JSON
                            }
                        })
                    );
                }

                await (this._jsonata(prop.extendedData.expression) as Expression).evaluate(mapToParsedObject(evtOccurValue), undefined, (error, result) => {
                    if (error != null) {
                        this.logger.error(`JSONata evaluation failed for property '${prop.name}' on event '${evt.name}': ${error.message}`);
                    } else {
                        values.set(prop.deviceId, result);
                    }
                });
            }
        }
        return values;
    }

    /**
     * Convert value received from device to system
     * Note: No conversion is being done at the moment!
     * @param raw value
     * @param fromType original value type (device)
     * @param toType destination value type (system)
     */
    private convertValueFromDevice(raw: any, fromType: string, toType: string): any {
        if (raw == null) {
            return (undefined);
        }

        // Convert the value (this is an example)
        raw = Utils.convertValueToType(raw, toType);

        // return same thing (could not convert it?)
        return (raw);
    }

    /**
     * Convert value received from system to device.
     * Note: No conversion is being done at the moment!
     * @param raw raw value
     * @param fromType original value type (system)
     * @param toType destination value type (device)
     */
    private convertValueToDevice(raw: any, fromType: string, toType: string): any {
        if (raw == null) {
            return (undefined);
        }

        // Convert the value
        // ...

        // return same thing (could not convert it?)
        return (raw);
    }
}
