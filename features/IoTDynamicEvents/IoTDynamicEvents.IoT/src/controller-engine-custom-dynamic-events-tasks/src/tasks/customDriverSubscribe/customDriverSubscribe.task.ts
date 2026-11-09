import { Task, System, TaskBase, Dependencies, Communication } from "@criticalmanufacturing/connect-iot-controller-engine";

/** Default values for settings */
export const SETTINGS_DEFAULTS: CustomDriverSubscribeSettings = {
    autoActivate: true,
    messageType: ""
};

/**
 * @whatItDoes
 *
 * Subscribes to raw messages from the driver communication and emits the
 * message type and content when a matching message is received. Multiple
 * message types can be provided as a comma-separated list.
 *
 * @howToUse
 *
 * The task subscribes automatically when `autoActivate` is enabled. When
 * automatic activation is disabled, trigger the task with the `activate`
 * input after configuring the message types.
 *
 * ### Inputs
 * * `any` : **activate** - Subscribe to the configured message types
 *
 * ### Outputs
 *
 * * `string` : **type** - Type of the received driver message
 * * `any` : **message** - Content of the received driver message
 * * `bool` : **success** - Triggered after the subscriptions are registered
 * * `Error` : **error** - Triggered when the subscriptions cannot be registered
 *
 * ### Settings
 * * `boolean` : **autoActivate** - Subscribe automatically when the task is initialized and when the driver reconnects
 * * `string` : **messageType** - Driver message type or comma-separated message types to subscribe to
 */
@Task.Task()
export class CustomDriverSubscribeTask extends TaskBase implements CustomDriverSubscribeSettings {

    /** Accessor helper for untyped properties and output emitters. */
    // [key: string]: any;

    /** **Inputs** */
    public autoActivate: boolean;
    public messageType: string;

    /** **Outputs** */
    public type: Task.Output<string> = new Task.Output<string>();
    public message: Task.Output<any> = new Task.Output<any>();

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    public override async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            this.activate = undefined;

            try {
                await this._driverProxy.unsubscribeRaw(this.onMessageReceivedHandler);

                for (const msgType of this.messageType.split(',')) {
                    if (msgType.trim() !== '') {
                        await this._driverProxy.subscribeRaw(msgType.trim(), this.onMessageReceivedHandler);
                        this._logger.info(`Subscribed topic '${msgType.trim()}' in driver communication`);
                    }
                }
                this.success.emit(true);
            } catch (error) {
                this.logAndEmitError(`Failed to subscribe topic '${this.messageType}' in driver communication: ${(error as Error).message}`);
            }
        }
    }

    /** Handler to emit the message content */
    private onMessageReceivedHandler: any = (message: any): void => {
        if (message && message.type && message.content) {
            const newExecutionContext = this._executionContext.fork({
                properties: <CustomDriverSubscribeZoneContext>{
                    originalMessageType: message.type,
                    originalMessageContent: message.content
                }
            });
            newExecutionContext.run(() => {
                if (this.type && (typeof (this.type) === 'object') && (this.type.hasOwnProperty('_eventEmitter'))
                    && this.message && (typeof (this.message) === 'object') && (this.message.hasOwnProperty('_eventEmitter'))) {

                    this.type.emit(message.type);
                    this.message.emit(message.content);
                }
            });
        }
    }

    /**
     * Register automatically when the driver is available if defined
     */
    async onInit(): Promise<void> {
        this.sanitizeSettings(SETTINGS_DEFAULTS);

        if (this.autoActivate) {
            this.handleOnConnected({ newConnection: true });

            this._driverProxy.on('driverConnected', this.handleOnConnected);
        }
    }

    async onDestroy(): Promise<void> {
        this._driverProxy.off('driverConnected', this.handleOnConnected);
        await this._driverProxy.unsubscribeRaw(this.onMessageReceivedHandler);
    }

    private handleOnConnected = async (args: Communication.DriverConnectedEventArgs): Promise<void> => {
        if (args != null && args.newConnection) {
            try {
                await this._driverProxy.unsubscribeRaw(this.onMessageReceivedHandler);

                for (const msgType of this.messageType.split(',')) {
                    if (msgType.trim() !== '') {
                        await this._driverProxy.subscribeRaw(msgType.trim(), this.onMessageReceivedHandler);
                        this._logger.info(`Subscribed topic '${msgType.trim()}' in driver communication`);
                    }
                }

                this.success.emit(true);
            } catch (error) {
                this.logAndEmitError(`Failed to subscribe topic '${this.messageType}' in driver communication: ${(error as Error).message}`);
            }
        }
    }
}

// Add settings here
/** CustomDriverSubscribe Settings object */
export interface CustomDriverSubscribeSettings extends System.TaskDefaultSettings {
    autoActivate: boolean;
    messageType: string;
}

/**
 * Execution context
 */
export interface CustomDriverSubscribeZoneContext extends Dependencies.ExecutionContextSpecificationProperties {
    originalMessageType: any,
    originalMessageContent: any
}
