import { Task, System, TaskBase, Utilities, Dependencies } from "@criticalmanufacturing/connect-iot-controller-engine";
import { ActionType, EventOccurrence, SubResourcesInformation } from "../../types/types";

/** Default values for settings */
export const SETTINGS_DEFAULTS: CustomDynamicEventsSettings = {
    eventTemplateName: '',
    eventTagToken: '',
    eventTagAliasName: '',
    resourceTypePersistedAlias: 'ResourceType',
    autoActivate: true,
    actionType: ActionType.Register
};

/**
 * @whatItDoes
 *
 * Registers or subscribes to custom automation events in the driver
 * communication, depending on the configured `actionType`. Registration
 * resolves the event template and replaces its tag token for the configured
 * resource or related sub-resources. Subscription listens for those events
 * and emits their values and resource metadata.
 *
 * @howToUse
 *
 * Configure the event template, tag settings, and action before triggering
 * the task with `activate`. When `actionType` is `Subscribe` and
 * `autoActivate` is enabled, subscriptions are refreshed after a driver
 * connection is restored.
 *
 * ### Inputs
 * * `any` : **activate** - Register or subscribe to the configured custom automation events
 *
 * ### Outputs
 *
 * * `string` : **resource** - Resource name associated with a received event
 * * `string` : **resourceType** - Resource type associated with a received event
 * * `string` : **eventTemplate** - Configured event template name
 * * `any` : **value** - Received event property values as name/value pairs
 * * `bool` : **success** - Triggered after registration or when an event is processed
 * * `Error` : **error** - Triggered when the configured operation cannot be completed
 *
 * ### Settings
 * * `string` : **eventTemplateName** - Name of the custom event template
 * * `string` : **eventTagToken** - Token in the event template that identifies the tag placeholder
 * * `string` : **eventTagAliasName** - Alias containing the tag value used during registration
 * * `string` : **resourceTypePersistedAlias** - Persisted alias containing the resource type
 * * `boolean` : **autoActivate** - Refresh subscriptions after a driver reconnection
 * * `ActionType` : **actionType** - Operation to perform: `Register` or `Subscribe`
 */
@Task.Task()
export class CustomDynamicEventsTask extends TaskBase implements CustomDynamicEventsSettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */


    /** **Outputs** */
    public resource: Task.Output<string> = new Task.Output<string>();
    public resourceType: Task.Output<string> = new Task.Output<string>();
    public eventTemplate: Task.Output<string> = new Task.Output<string>();
    public value: Task.Output<any> = new Task.Output<any>();

    /** To keep the reference to the subscribed callback*/
    private _subscribedReferences: Map<string, any> = new Map<string, any>();
    private _eventsRegistered: Array<string> = new Array<string>();
    private _subResourceInformation: any = {};
    private _resourceResourceType: string = '';
    private _productEventsNamingConvention: string = 'connect.iot.driver._DRIVER_.registerEvent';

    /** Properties Settings */
    /** Information about the example setting */
    eventTemplateName: string = '';
    eventTagToken: string = '';
    eventTagAliasName: string = '';
    resourceTypePersistedAlias: string = 'ResourceType';
    autoActivate: boolean = true;
    actionType: ActionType = ActionType.Register;

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes['activate']) {
            this.activate = undefined;
            try {
                if (this.actionType === ActionType.Register) {
                    await this.registerEvent();
                    this.success.emit(true);
                }
                if (this.actionType === ActionType.Subscribe) {
                    await this.subscribeEvents();
                }
            } catch (e) {
                throw new Error(`Something went wrong performing the '${this.actionType}' action : ${(e as Error).message}`);
            }
        }
    }

    /** Right after settings are loaded, create the needed dynamic outputs. */
    public override async onBeforeInit(): Promise<void> {
    }

    /** Initialize this task, register any event handler, etc */
    public override async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);
        try {
            if (this.actionType === ActionType.Subscribe) {
                this.autoActivate = Utilities.convertValueToType(this.autoActivate, Task.TaskValueType.Boolean, true);
                if (this._driverProxy != null) {
                    this._driverProxy.on('driverConnected', this.handleOnConnected);
                }
            }
        } catch (e) {
            throw new Error(`Error subscribing events: ${(e as Error).message}`);
        }
    }

    /** Cleanup internal data, unregister any event handler, etc */
    public override async onDestroy(): Promise<void> {
        this._driverProxy.off('driverConnected', this.handleOnConnected);
        await this.unsubscribeEvents();
    }

    /** Registers the events with the related properties.
     *  ActionType = Register
     */
    private async registerEvent() {
        try {

            const resourceName: string = await this._dataStore.retrieve('ResourceName', '');

            if (resourceName === null || resourceName === '') {
                throw new Error('Unknown Parent Resource');
            }

            this._subResourceInformation = new Map<string, SubResourcesInformation>(
                await this._dataStore.retrieve('AutomationControllerResourceAssociations_' + resourceName, []));

            // Retrieve Event Template
            // Retrieve Event Template
            this['_templateEventName'] = this.eventTemplateName;

            // #region Retrieve Templates

            const eventTemplate = this._driverProxy.automationControllerDriverDefinition.
                AutomationDriverDefinition.Events.find((event) => event.Name === this.eventTemplateName);

            const eventPropertiesTemplate = this._driverProxy.automationControllerDriverDefinition.
                AutomationDriverDefinition.EventProperties.filter((eventProperty) => eventProperty.AutomationEvent.Name === this.eventTemplateName);

            const generalProperties = this._driverProxy.automationControllerDriverDefinition.AutomationDriverDefinition.Properties;

            if (this._subResourceInformation !== null && this._subResourceInformation.size > 0) {

                // Will register an event for each subresource
                for (const [key, value] of this._subResourceInformation.entries()) {

                    if (value[this.eventTagAliasName] == null || value[this.eventTagAliasName] === '') {
                        value[this.eventTagAliasName] = key;
                    }

                    // #region Deal With Referencing issues

                    let eventSubResourceProperties: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEventProperty[] = [];
                    let propertiesForIteration: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationPropertyCollection =
                        new System.LBOS.Cmf.Foundation.BusinessObjects.AutomationPropertyCollection();

                    eventSubResourceProperties = JSON.parse(JSON.stringify(eventPropertiesTemplate));
                    propertiesForIteration = JSON.parse(JSON.stringify(generalProperties));

                    // #endregion Deal With Referencing issues

                    const properties: any[] = [];

                    eventSubResourceProperties.
                        forEach(eventProperty => {

                            // Find our tag from the driver definition
                            const automationProperty = propertiesForIteration.find((eventGeneralProperty) =>
                                eventGeneralProperty.Name === eventProperty.AutomationProperty.Name.
                                    replace(this.eventTagToken, value[this.eventTagAliasName]));

                            if (automationProperty != null) {

                                // Replace Template Tag with real tag
                                eventProperty.AutomationProperty = automationProperty;
                                eventProperty.Name = eventProperty.Name.replace(this.eventTagToken, value[this.eventTagAliasName]);

                                properties.push({
                                    name: automationProperty.Name,
                                    deviceId: automationProperty.DevicePropertyId,
                                    dataType: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType[automationProperty.DataType],
                                    deviceType: automationProperty.AutomationProtocolDataType.Name,
                                    extendedData: {
                                        isTrigger: true
                                    }
                                });
                            } else {
                                this._logger.warning(`No Tag Configured for tag template ${eventProperty.Name} and resource ${key}`);
                            }
                        });

                    // Register Event in the Driver
                    await this._driverProxy.notifyRaw(this._productEventsNamingConvention, {
                        event: {
                            name: `${eventTemplate.Name}_${key}`,
                            isEnabled: true,
                            properties: properties,
                        }
                    });

                    this._logger.info(`Registering the event ${eventTemplate.Name}_${key}`);

                    this._logger.debug(`Registering the event ${eventTemplate.Name}_${key} with
                        properties ${JSON.stringify(properties.map(prop => {
                        const rObj = {};
                        rObj[prop.name] = prop.deviceId;
                        return rObj;
                    }))}`);

                    this._eventsRegistered.push(`${eventTemplate.Name}_${key}`);
                }
            } else {

                this._logger.debug('No SubResource relation found, will assume resource name');

                let automationAlias = await this._dataStore.retrieve(this.eventTagAliasName, '');
                this._resourceResourceType = await this._dataStore.retrieve('ResourceType', '');

                if (automationAlias == null || automationAlias === '') {
                    automationAlias = resourceName;
                }

                // Will register an event the Resource
                const eventResourceProperties = eventPropertiesTemplate;
                const properties: any[] = [];

                eventResourceProperties.forEach(eventProperty => {

                    // Find our tag from the driver definition
                    const automationProperty = this._driverProxy.automationControllerDriverDefinition.AutomationDriverDefinition.Properties.
                        find((eventGeneralProperty) =>
                            eventGeneralProperty.Name === eventProperty.AutomationProperty.Name.
                                replace(this.eventTagToken, automationAlias));

                    if (automationProperty != null) {

                        // Replace Template Tag with real tag
                        eventProperty.AutomationProperty = automationProperty;
                        eventProperty.Name = eventProperty.Name.replace(this.eventTagToken, automationAlias);

                        properties.push({
                            name: automationProperty.Name,
                            deviceId: automationProperty.DevicePropertyId,
                            dataType: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType[automationProperty.DataType],
                            deviceType: automationProperty.AutomationProtocolDataType.Name,
                            extendedData: {
                                isTrigger: true
                            }
                        });
                    } else {
                        this._logger.warning(`No Tag Configured for tag template ${eventProperty.Name} and resource ${resourceName}`);
                    }
                });

                await this._driverProxy.notifyRaw(this._productEventsNamingConvention, {
                    event: {
                        name: `${eventTemplate.Name}_${resourceName}`,
                        isEnabled: true,
                        properties: properties,
                    }
                });

                this._logger.info(`Registering the event ${eventTemplate.Name}_${resourceName}`);

                this._logger.debug(`Registering the event ${eventTemplate.Name}_${resourceName} with
                properties ${JSON.stringify(properties.map(prop => {
                    const rObj = {};
                    rObj[prop.name] = prop.deviceId;
                    return rObj;
                }))}`);

                this._eventsRegistered.push(`${eventTemplate.Name}_${resourceName}`);
            }

        } catch (e) {
            throw new Error(`Error registering custom events: ${(e as Error).message}`);
        }
    }

    /** Subscribes to the events.
     *  ActionType = Subscriber
     */
    private async subscribeEvents() {

        await this.unsubscribeEvents();

        const eventNames = await this.resolveEventNames();

        const splitPackage = this._driverProxy.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol.Package.split('-');
        this._productEventsNamingConvention = this._productEventsNamingConvention.replace('_DRIVER_', splitPackage[splitPackage.length - 1]);

        this._logger.debug(`Will assume protocol ${this._productEventsNamingConvention} to register`);
        for (const eventName of eventNames) {
            const fullEventName = `${this._productEventsNamingConvention}.${eventName}`;
            this._subscribedReferences.set(fullEventName, this.newEventHandler.bind(this));

            await this._driverProxy.subscribeRaw(fullEventName, this._subscribedReferences.get(fullEventName));

            this._logger.info(`Subscribed to the event '${fullEventName}'`);

        }
    }

    /** Unsubscribes from the events.
    *  ActionType = Subscriber
    */
    private async unsubscribeEvents() {

        // Usubscribe Events
        for (const subscribedReferenceValue of Array.from(this._subscribedReferences.values())) {
            await this._driverProxy.unsubscribeRaw(subscribedReferenceValue);
        }
        this._logger.info(`Unsubscribed from all events`);
    }

    /** Handles the event occurrence */
    private async newEventHandler(message: any) {
        try {

            const newExecutionContext = this._executionContext.fork({
                properties: <EquipmentEventZoneContext>{
                    originalEvent: message.event,
                    originalTimeStamp: message.timestamp,
                    originalRaw: message
                }
            });
            newExecutionContext.run(() => {

                const valuesToEmit: any[] = [];
                const eventOccurrence: EventOccurrence = message.content;

                eventOccurrence.propertyValues.forEach(prop => {

                    valuesToEmit.push({ Name: prop.propertyName, Value: prop.value });
                });

                if (valuesToEmit.length > 0) {

                    this._logger.info(`Received '${message.content.eventName}' event with data: ${JSON.stringify(valuesToEmit)}'`);

                    const resourceName = eventOccurrence.eventName.split(`${this.eventTemplateName}_`)[1];

                    this.value.emit(valuesToEmit);
                    this.eventTemplate.emit(this.eventTemplateName);
                    this.resource.emit(resourceName);
                    this.resourceType.emit(this._resourceResourceType || this._subResourceInformation.get(resourceName)[this.resourceTypePersistedAlias]);
                    this.success.emit(true);
                }
            });

        } catch (error) {
            this._logger.error(`Error processing event message: ${(error as Error).message}`);
        }
    }

    private async resolveEventNames(): Promise<Array<string>> {

        const resourceName: string = await this._dataStore.retrieve('ResourceName', '');
        this._subResourceInformation = new Map<string, SubResourcesInformation>(
            await this._dataStore.retrieve('AutomationControllerResourceAssociations_' + resourceName, []));

        const resourceNames: Array<string> = [];

        if (this._subResourceInformation !== null && this._subResourceInformation.size > 0) {
            for (const [key, value] of this._subResourceInformation.entries()) {

                resourceNames.push(`${this.eventTemplateName}_${key}`);
            }
        } else {
            this._logger.debug('No SubResource relation found, will assume resource name');

            this._resourceResourceType = await this._dataStore.retrieve('ResourceType', '');
            resourceNames.push(`${this.eventTemplateName}_${resourceName}`);
        }
        return resourceNames;
    }

    /** Called when the driver is connected */
    private handleOnConnected: any = async (args: any): Promise<void> => {
        if (args != null && !args.newConnection) {
            if (this.autoActivate) {
                await this.subscribeEvents();
            }
        }
    }
}

// Add settings here
/** CustomDynamicEvents Settings object */
export interface CustomDynamicEventsSettings extends System.TaskDefaultSettings {
    /** Information about the example setting */
    eventTemplateName: string;
    eventTagToken: string;
    eventTagAliasName: string;
    resourceTypePersistedAlias: string;
    autoActivate: boolean;
    actionType: ActionType;
}

/**
 * Execution context
 */
export interface EquipmentEventZoneContext extends Dependencies.ExecutionContextSpecificationProperties {
    originalEvent: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent,
    originalTimeStamp: moment.Moment,
    originalRaw: System.EquipmentEventOccurrence<any>
}

