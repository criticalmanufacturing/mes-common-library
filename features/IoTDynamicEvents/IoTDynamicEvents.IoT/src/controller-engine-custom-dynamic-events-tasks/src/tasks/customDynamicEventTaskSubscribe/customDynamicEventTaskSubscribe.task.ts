import { Task, System, TaskBase, Utilities, Dependencies } from "@criticalmanufacturing/connect-iot-controller-engine";
import { EventOccurrence, SubResourcesInformation } from "../../types/types";

/** Default values for settings */
export const SETTINGS_DEFAULTS: CustomDynamicEventTaskSubscribeSettings = {
    eventTemplateName: '',
    resourceNamePersistedAlias: 'ResourceName',
    resourceTypePersistedAlias: 'ResourceType',
    subResourceRelationPersistedAlias: 'AutomationControllerResourceAssociations_',
    autoActivate: false
};


/**
 * @whatItDoes
 *
 * Subscribes to custom automation events from the driver communication and
 * emits the event values together with the resource and event template when
 * a matching event is received. Events are resolved for the configured
 * resource and its related sub-resources.
 *
 * @howToUse
 *
 * Configure the event template and persisted data aliases before triggering
 * the task with `activate`. When `autoActivate` is enabled, subscriptions are
 * also refreshed after a driver connection is restored.
 *
 * ### Inputs
 * * `any` : **activate** - Subscribe to the configured custom automation events
 *
 * ### Outputs
 *
 * * `string` : **resource** - Resource name associated with the received event
 * * `string` : **resourceType** - Resource type associated with the received event
 * * `string` : **eventTemplate** - Configured event template name
 * * `any` : **value** - Received event property values as name/value pairs
 * * `bool` : **success** - Triggered after subscriptions are registered or an event is processed
 * * `Error` : **error** - Triggered when the task cannot complete its operation
 *
 * ### Settings
 * * `string` : **eventTemplateName** - Name of the custom event template to subscribe to
 * * `string` : **resourceNamePersistedAlias** - Persisted alias containing the resource name
 * * `string` : **resourceTypePersistedAlias** - Persisted alias containing the resource type
 * * `string` : **subResourceRelationPersistedAlias** - Persisted alias prefix used to retrieve sub-resource relations
 * * `boolean` : **autoActivate** - Refresh subscriptions after a driver reconnection
 */
@Task.Task()
export class CustomDynamicEventTaskSubscribeTask extends TaskBase implements CustomDynamicEventTaskSubscribeSettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Outputs** */
    public resource: Task.Output<string> = new Task.Output<string>();
    public resourceType: Task.Output<string> = new Task.Output<string>();
    public eventTemplate: Task.Output<string> = new Task.Output<string>();
    public value: Task.Output<any> = new Task.Output<any>();

    /** To keep the reference to the subscribed callback*/
    private _subscribedReferences: Map<string, any> = new Map<string, any>();
    private _subResourceInformation: any = {};
    private _resourceResourceType: string = '';
    private _productEventsNamingConvention: string = 'connect.iot.driver._DRIVER_.registerEvent';

    /** Properties Settings */
    /** Information about the example setting */
    eventTemplateName: string = '';
    resourceNamePersistedAlias: string = 'ResourceName';
    resourceTypePersistedAlias: string = 'ResourceType';
    subResourceRelationPersistedAlias: string = 'AutomationControllerResourceAssociations_';
    autoActivate: boolean = false;

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {

        if (changes['activate']) {
            this.activate = undefined;
            try {

                await this.subscribeEvents();
                this.success.emit(true);
            } catch (e) {
                throw new Error(`Something went wrong performing the Subscribe action`);
            }
        }
    }

    /** Initialize this task, register any event handler, etc */
    public override async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);
        try {
            this.autoActivate = Utilities.convertValueToType(this.autoActivate, Task.TaskValueType.Boolean, true);
            if (this._driverProxy != null) {
                this._driverProxy.on('driverConnected', this.handleOnConnected);
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

        const resourceName: string = await this._dataStore.retrieve(this.resourceNamePersistedAlias, '');
        this._subResourceInformation = new Map<string, SubResourcesInformation>(
            await this._dataStore.retrieve(this.subResourceRelationPersistedAlias + resourceName, []));

        const resourceNames: Array<string> = [];

        if (this._subResourceInformation !== null && this._subResourceInformation.size > 0) {
            for (const [key, value] of this._subResourceInformation.entries()) {

                resourceNames.push(`${this.eventTemplateName}_${key}`);
            }
        } else {
            this._logger.debug('No SubResource relation found, will assume resource name');

            this._resourceResourceType = await this._dataStore.retrieve(this.resourceTypePersistedAlias, '');
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
/** CustomDynamicEventTaskSubscribe Settings object */
export interface CustomDynamicEventTaskSubscribeSettings extends System.TaskDefaultSettings {
    /** Information about the example setting */
    eventTemplateName: string;
    resourceNamePersistedAlias: string;
    resourceTypePersistedAlias: string;
    subResourceRelationPersistedAlias: string;
    autoActivate: boolean;
}

/**
 * Execution context
 */
export interface EquipmentEventZoneContext extends Dependencies.ExecutionContextSpecificationProperties {
    originalEvent: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationEvent,
    originalTimeStamp: moment.Moment,
    originalRaw: System.EquipmentEventOccurrence<any>
}
