import { Task, System, TaskBase, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";
import { SubResourcesInformation } from "../../types/types";

/** Default values for settings */
export const SETTINGS_DEFAULTS: CustomDynamicEventTaskRegisterSettings = {
    eventTemplateName: '',
    eventTagToken: '',
    eventTagAliasName: '',
    resourceNamePersistedAlias: 'ResourceName',
    resourceTypePersistedAlias: 'ResourceType',
    subResourceRelationPersistedAlias: 'AutomationControllerResourceAssociations_',
    autoActivate: true
};

/**
 * @whatItDoes
 *
 * Registers custom automation events in the driver communication based on an
 * event template. The task resolves resource and sub-resource information,
 * replaces the configured event tag token, and registers the resulting event
 * properties with the driver.
 *
 * @howToUse
 *
 * Configure the event template and tag settings before triggering the task
 * with `activate`. When `autoActivate` is enabled, registrations are also
 * refreshed after a driver connection is restored.
 *
 * ### Inputs
 * * `any` : **activate** - Register the configured custom automation events
 *
 * ### Outputs
 *
 * * `bool` : **success** - Triggered after the custom events are registered
 * * `Error` : **error** - Triggered when the custom events cannot be registered
 *
 * ### Settings
 * * `string` : **eventTemplateName** - Name of the event template to register
 * * `string` : **eventTagToken** - Token in the event template that identifies the tag placeholder
 * * `string` : **eventTagAliasName** - Persisted alias containing the tag value
 * * `string` : **resourceNamePersistedAlias** - Persisted alias containing the resource name
 * * `string` : **resourceTypePersistedAlias** - Persisted alias containing the resource type
 * * `string` : **subResourceRelationPersistedAlias** - Persisted alias prefix used to retrieve sub-resource relations
 * * `boolean` : **autoActivate** - Refresh registrations after a driver reconnection
 */
@Task.Task()
export class CustomDynamicEventTaskRegisterTask extends TaskBase implements CustomDynamicEventTaskRegisterSettings {

    private _eventsRegistered: Array<string> = new Array<string>();
    private _subResourceInformation: any = {};
    private _productEventsNamingConvention: string = 'connect.iot.driver._DRIVER_.registerEvent';

    /** Properties Settings */
    eventTemplateName: string = '';
    eventTagToken: string = '';
    eventTagAliasName: string = '';
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
                await this.registerEvents();
                this.success.emit(true);
            } catch (e) {
                this.logAndEmitError(`Failed to register events in driver communication: ${(e as Error).message}`);
            }
        }
    }

    /** Initialize this task, register any event handler, etc */
    async onInit(): Promise<void> {
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

    /** Registers the events with the related properties.
     *  ActionType = Register
     */
    private async registerEvents() {
        try {

            const resourceName: string = await this._dataStore.retrieve(this.resourceNamePersistedAlias, '');

            if (resourceName === null || resourceName === '') {
                throw new Error('Unknown Parent Resource');
            }

            this._subResourceInformation = new Map<string, SubResourcesInformation>(
                await this._dataStore.retrieve(this.subResourceRelationPersistedAlias + resourceName, []));

            // Retrieve Event Template
            this['_templateEventName'] = this.eventTemplateName;

            // #region Retrieve Templates

            const eventTemplate = this._driverProxy.automationControllerDriverDefinition.
                AutomationDriverDefinition.Events.find((event) => event.Name === this.eventTemplateName);

            const eventPropertiesTemplate = this._driverProxy.automationControllerDriverDefinition.
                AutomationDriverDefinition.EventProperties.filter((eventProperty) => eventProperty.AutomationEvent.Name === this.eventTemplateName);

            const generalProperties = this._driverProxy.automationControllerDriverDefinition.AutomationDriverDefinition.Properties;

            const splitPackage = this._driverProxy.automationControllerDriverDefinition.AutomationDriverDefinition.AutomationProtocol.Package.split('-');
            this._productEventsNamingConvention = this._productEventsNamingConvention.replace('_DRIVER_', splitPackage[splitPackage.length - 1]);

            this._logger.debug(`Will assume protocol ${this._productEventsNamingConvention} to register`);

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
                this['_resourceResourceType'] = await this._dataStore.retrieve('ResourceType', '');

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

    /** Called when the driver is connected */
    private handleOnConnected: any = async (args: any): Promise<void> => {
        if (args != null && !args.newConnection) {
            if (this.autoActivate) {
                await this.registerEvents();
            }
        }
    }
}

// Add settings here
/** CustomDynamicEventTaskRegister Settings object */
export interface CustomDynamicEventTaskRegisterSettings extends System.TaskDefaultSettings {
    eventTemplateName: string;
    eventTagToken: string;
    eventTagAliasName: string;
    resourceNamePersistedAlias: string;
    resourceTypePersistedAlias: string;
    subResourceRelationPersistedAlias: string;
    autoActivate: boolean;
}