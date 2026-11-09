import { injectable, inject, Container } from "inversify";
import { TYPES as COMMON_TYPES, Communication, Logger } from "@criticalmanufacturing/connect-iot-common";
import { TYPES as DRIVER_TYPES, Command, CommandParameter, Property } from "@criticalmanufacturing/connect-iot-driver";
import { Event as EquipmentEvent } from "@criticalmanufacturing/connect-iot-driver";
import { AMQPDeviceDriver } from "../driverImplementation";
import { commandExtendedDataDefaults, eventExtendedDataDefaults, propertyExtendedDataDefaults, validateCommandParameters, validateCommands, validateEventProperties, validateEvents } from "../extendedData";

@injectable()
export class ExtensionHandler {
    private _container: Container;
    private _driver: AMQPDeviceDriver;
    private _pJson = require("../../package.json");

    @inject(DRIVER_TYPES.Communication.Controller)
    private _controllerTransport: Communication.Transport;
    @inject(COMMON_TYPES.Logger)
    private _logger: Logger;

    /**
     * Initialize the Transaction Handler. It is assumed this method is called with the controller communication channel already created.
     */
    public async initialize(container: Container): Promise<void> {
        this._container = container;

        this._driver = this._container.get<AMQPDeviceDriver>(DRIVER_TYPES.Device.Driver);

        this._controllerTransport.unsubscribe(this.handleExecuteCommand);

        this._controllerTransport.subscribe("connect.iot.driver.template.executeCommand", this.handleExecuteCommand.bind(this));
    }

    /**
     * Send a command to the device
     * @param msg Message containing the command data
     */
    private handleExecuteCommand = async (msg: Communication.Message<{ command: Command; parameters: any; }> | undefined): Promise<Communication.Message<any>> => {
        if (msg != null) {

            const command: Command = msg.content.command;

            if (command == null) {
                this._logger.error(`No Command was provided!`);
                throw new Error(`No Command was provided!`);
            }

            // Prepare default values
            command.deviceId = command.deviceId != null ? command.deviceId : "";
            command.extendedData = command.extendedData != null ? command.extendedData : {};
            command.extendedData = Object.assign({}, commandExtendedDataDefaults, command.extendedData || {});
            command.parameters = command.parameters != null ? command.parameters : [];
            command.parameters.forEach((parameter: CommandParameter) => {
                parameter.extendedData = parameter.extendedData != null ? parameter.extendedData : {};
            });

            // Validate the event structure
            validateCommands(this._pJson, [command]);
            validateCommandParameters(this._pJson, [command]);

            this._logger.info(`Sending command '${command.deviceId}' to device. Command was sent by a controller task.`);
            const parameters: Map<CommandParameter, any> = new Map<CommandParameter, any>();
            if (msg.content.parameters) {
                for (const p in msg.content.parameters) {
                    if (msg.content.parameters.hasOwnProperty(p)) {
                        if (p !== "$id") {
                            const param = command.parameters.find((cp: CommandParameter) => cp.name === p);
                            if (param != null) {
                                parameters.set(param, msg.content.parameters[p]);
                            } else {
                                this._logger.error(`Parameter '${p}' is not known for the command!`);
                                throw new Error(`Parameter '${p}' is not known for the command!`);
                            }
                        }
                    }
                }
            }

            return {
                type: Communication.MESSAGES.REPLY,
                content: await this._driver.execute(command, parameters),
            };
        } else {
            throw new Error("Invalid command received!");
        }
    };
}