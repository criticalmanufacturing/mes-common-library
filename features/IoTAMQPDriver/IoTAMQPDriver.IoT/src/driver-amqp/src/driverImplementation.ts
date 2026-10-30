import { injectable, inject, Container } from "inversify";
import { CommunicationState, PropertyValuePair } from "@criticalmanufacturing/connect-iot-driver";
import { Property, PropertyValue, Command, DeviceDriverBase, CommandParameter } from "@criticalmanufacturing/connect-iot-driver";
import { TYPES as DRIVER_TYPES } from "@criticalmanufacturing/connect-iot-driver";
import { AMQPCommunicationSettings, aMQPDefaultCommunicationSettings, validateCommunicationParameters } from "./communicationSettings";
import { validateCommands, validateCommandParameters, CommandExtendedData, CommandParameterExtendedData, ParameterType } from "./extendedData/index";
import { TYPES } from "./types";
import { ExtensionHandler } from "./extensions";
import * as rhea from "rhea";
import { Connection, Sender } from "rhea";
import { sleep } from "@criticalmanufacturing/connect-iot-common/dist/src/utils";
import { createHash } from "node:crypto";
import * as os from "os";
import * as path from "node:path";
import * as fs from "node:fs";

export enum FileType {
    Certificate = "Certificate",
    PrivateKey = "PrivateKey"
}

@injectable()
export class AMQPDeviceDriver extends DeviceDriverBase {
    private _communicationSettings: AMQPCommunicationSettings;
    private _container: Container;

    @inject(TYPES.Injector)
    private _parentContainer: Container;

    @inject(DRIVER_TYPES.DriverId)
    protected _driverId: string;

    private _extensionHandler: ExtensionHandler;
    private _amqpConnection: Connection;
    private _isDisconnecting: boolean = false;

    protected _certificatesRootFolder: string;
    protected _certificateFile: string;
    protected _privateKeyFile: string;

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
        if (!this._container) {
            this._container = new Container();
            this._container.parent = this._parentContainer;

            this._extensionHandler = this._container.get<ExtensionHandler>(TYPES.ExtensionHandler);
        }

        await this._extensionHandler.initialize(this._container);
        // Initialize the specific driver
        // ...
    }

    /**
     * Notification regarding the communication parameters being available.
     * Validate the integrity of the values
     * Note: Called by the driverBase
     * @param communication Communication settings object
     */
    public async setCommunicationConfiguration(communication: any): Promise<void> {
        this._communicationSettings = Object.assign({}, aMQPDefaultCommunicationSettings, communication);

        // eslint-disable-next-line
        const pJson = require("../package.json");
        validateCommunicationParameters(pJson, this._communicationSettings);

        // Prepare the extended data
        validateCommands(pJson, this.configuration.commands);
        validateCommandParameters(pJson, this.configuration.commands);

        this.manageCertificates();
    }

    /**
     * Connect to the equipment.
     * Note: Called by the driverBase
     */
    public async connectToDevice(): Promise<void> {
        this.setCommunicationState(CommunicationState.Connecting);

        try {
            const connectionConfig: any = {
                host: this._communicationSettings.address,
                port: this._communicationSettings.port
            };

            // Add TLS if certificates are provided
            if (this._communicationSettings.certificate) {
                connectionConfig.transport = "tls";
                connectionConfig.key = fs.readFileSync(this._privateKeyFile);
                connectionConfig.cert = fs.readFileSync(this._certificateFile);
            }

            // Always set a username so rhea enables the SASL layer (required by most brokers,
            // even for anonymous access); default to "anonymous" when none is configured.
            connectionConfig.username = this._communicationSettings.username || "anonymous";
            if (this._communicationSettings.username && this._communicationSettings.password) {
                connectionConfig.password = this._communicationSettings.password;
            }

            this._amqpConnection = rhea.connect(connectionConfig);

            this._amqpConnection.on("connection_error", async (context) => {
                if (!this._isDisconnecting) {
                    this._isDisconnecting = true;
                    this.logger.error(`There was a connection error '${context?.error?.message}', will disconnect.`);
                    this.setCommunicationState(CommunicationState.ConnectingFailed);
                    this.disconnect();
                }
            });

            this._amqpConnection.on("disconnected", async (context) => {
                if (!this._isDisconnecting) {
                    this._isDisconnecting = true;
                    this.logger.error(`Disconnected from Broker '${context?.error?.message ?? JSON.stringify(context?.error?.errors ?? "")}', will force disconnect.`);
                    this.setCommunicationState(CommunicationState.Disconnected);
                    this.disconnect();
                }
            });

            // Provide an empty object as a fallback to avoid errors
            const { $id, ...cleanCommunicationSettings } = this._communicationSettings as any || {};
            this.logger.info(`Using the following configurations: ${JSON.stringify(cleanCommunicationSettings, undefined, " ")}`);

            await AMQPDeviceDriver.waitFor(this.connectingTimeout, `Connection was never opened`, () => (this._amqpConnection?.is_open() ?? false) && !this._isDisconnecting);

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
            this._amqpConnection.close();
        } catch (error) {
            this.logger.error(`Failed to disconnect from device: ${error.message}`);
        }
        this._isDisconnecting = false;
        this.setCommunicationState(CommunicationState.Disconnected);
    }

    /**
     * Notification that the setup process was a success
     * Note: Called by the driverBase
     */
    public async setupCompleted(): Promise<void> {
        // Since the setup was a success, set the state to Communicating
        await this.setCommunicationState(CommunicationState.Communicating);
    }

    /**
     * Request the equipment for values of the properties
     * Note: Called by the driverBase
     * @param properties List of properties to get values
     */
    public async getValues(properties: Property[]): Promise<PropertyValue[]> {
        throw new Error("Get Values should not be called directly");
    }

    /**
     * Set the value of properties in the equipment.
     * Note: Called by the driverBase
     * @param propertiesAndValues List of properties and new values
     */
    public async setValues(propertiesAndValues: PropertyValuePair[]): Promise<boolean> {
        throw new Error("Set Values should not be called directly");
    }

    /**
     * Send a command to the equipment. Depending on some settings, different messages can be sent.
     * Note: Called by the driverBase
     * @param command Command to send
     * @param parameters List of parameters to use
     */
    public async execute(command: Command, parameters: Map<CommandParameter, any>): Promise<any> {

        if (!this._amqpConnection || !this._amqpConnection.is_open()) {
            this.logger.warning("Driver is not ready, will ignore execute");
            return;
        }

        const { parameterAddress, commandOptions, body, subject }: { parameterAddress: string; commandOptions: CommandExtendedData; body: Record<string, any>; subject: string | undefined; } = this.parseCommandParameters(command, parameters);

        const uniqueIdentifier = createHash("sha256").update((command.deviceId + "_" + parameterAddress)).digest("hex");
        let sender = this._amqpConnection.find_sender((s: Sender) => s.name === uniqueIdentifier);

        if (!sender) {
            sender = this.registerNewSender(sender, commandOptions, uniqueIdentifier, parameterAddress, command);
        }

        if (sender?.sendable() ?? false) {
            await AMQPDeviceDriver.waitFor(10,
                `Sender never became sendable '${command.deviceId}' address '${parameterAddress}' '${uniqueIdentifier}'`,
                () => sender?.sendable() ?? false);
        }

        sender.send({
            body: JSON.stringify(body),
            content_type: "application/json",
            content_encoding: "utf-8",
            subject,
            message_id: Date.now().toString(),
            creation_time: new Date()
        });

        return (true);
    }

    private parseCommandParameters(command: Command, parameters: Map<CommandParameter, any>) {
        const commandOptions = command.extendedData as CommandExtendedData;
        let body: Record<string, any> = {};
        let subject: string | undefined = undefined;
        let parameterAddress = commandOptions.address;
        for (const [key, value] of parameters) {
            switch ((key.extendedData as CommandParameterExtendedData).parameterType) {
                case ParameterType.Subject:
                    subject = this.convertValueToDevice(value, key.dataType, key.deviceType);
                    break;
                case ParameterType.Body:
                    body = { ...body, ...this.convertValueToDevice(value, key.dataType, key.deviceType) };
                    break;
                case ParameterType.Value:
                    body[key.name] = this.convertValueToDevice(value, key.dataType, key.deviceType);
                    break;
                case ParameterType.Address:
                    parameterAddress = this.convertValueToDevice(value, key.dataType, key.deviceType);
                    break;
            }
        }
        return { parameterAddress, commandOptions, body, subject };
    }

    private registerNewSender(sender: rhea.Sender | undefined, commandOptions: CommandExtendedData, uniqueIdentifier: string, parameterAddress: string, command: Command) {
        const capabilities: string[] = [commandOptions.capabilities, ...commandOptions.extraCapabilities ?? []];

        sender = this._amqpConnection.open_sender({
            name: uniqueIdentifier,
            target: {
                address: parameterAddress,
                durable: commandOptions.durable?.valueOf(),
                expiry_policy: commandOptions.expirationPolicy,
                dynamic: commandOptions.dynamic,
                dynamic_node_properties: commandOptions.dynamicNodeProperties,
                capabilities: capabilities ?? [],
                timeout: commandOptions.timeout,
            }
        });

        sender.on("sender_open", () => {
            this.logger.debug(`Sender opened: command '${command.deviceId}' address '${parameterAddress}' '${uniqueIdentifier}'`);
        });

        sender.on("rejected", (context) => {
            this.logger.warning(`Sender rejected: command '${command.deviceId}' address '${parameterAddress}' '${uniqueIdentifier}'`);
        });

        sender.on("sendable", () => {
            this.logger.debug(`Sender sendable: command '${command.deviceId}' address '${parameterAddress}' '${uniqueIdentifier}'`);

        });

        sender.on("sender_error", (context) => {
            this.logger.error(`Sender error: command '${command.deviceId}' address '${parameterAddress}' '${uniqueIdentifier}' - '${JSON.stringify(context?.sender?.error ?? "")}'`);
        });

        return sender;
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

    protected get driverInstanceId() {
        return this._driverId.replace("/", "_");
    }

    protected createDir(dirPath: string): void {
        if (!fs.existsSync(dirPath)) {
            fs.mkdirSync(dirPath, { recursive: true });
        }
    }

    protected copyFileOrContents(destinationFile: string, fileOrContents: string, type: FileType): void {
        this.logger.debug(`Preparing ${type}`);
        if (fs.existsSync(fileOrContents)) {
            this.logger.info(`Copying ${type} from '${fileOrContents}'`);
            fs.copyFileSync(fileOrContents, destinationFile);
            this.logger.debug(`Copied ${type} file to ${destinationFile}`);
        } else if (fileOrContents.includes("-----BEGIN") && fileOrContents.includes("-----END")) {
            this.logger.info(`Copying ${type} from '<certificate string>'`);
            fs.writeFileSync(destinationFile, fileOrContents);
            this.logger.debug(`Copied ${type} content to ${destinationFile}`);
        } else {
            const errorMessage = `${type} is neither a valid file nor a valid content`;
            this.logger.error(errorMessage);
            throw new Error(errorMessage);
        }
    }

    private manageCertificates(): void {

        // Certificates
        this.logger.debug("Managing certificates");
        this._certificatesRootFolder = this._communicationSettings.certificatesRootFolder;

        if (this._communicationSettings.certificate !== "") {

            this._certificatesRootFolder = path.resolve(this._certificatesRootFolder
                ?.replace(/\$\{tmp\}/gi, os.tmpdir()) // replace token ${tmp}
                .replace(/\$\{temp\}/gi, os.tmpdir()) // replace token ${temp}
                .replace(/\$\{id\}/gi, this.driverInstanceId) // replace token ${id}
            );
            this._communicationSettings.certificatesRootFolder = this._certificatesRootFolder;
            const certsPath = `${this._certificatesRootFolder}/own/certs`;
            const privatePath = `${this._certificatesRootFolder}/own/private`;

            this.createDir(certsPath);
            this.createDir(privatePath);

            this.logger.debug(`Prepared Certificates folder: ${this._certificatesRootFolder}`);

            // if either certificate or privateKey are provided, copy them. Else use the generated ones.
            if (this._communicationSettings.certificate || this._communicationSettings.privateKey) {

                this._certificateFile = `${certsPath}/certificate.pem`;
                this._privateKeyFile = `${privatePath}/private_key.pem`;

                this.copyFileOrContents(this._certificateFile,
                    this._communicationSettings.certificate
                        .replace("-----BEGIN CERTIFICATE-----", "-----BEGIN CERTIFICATE-----\n")
                        .replace("-----END CERTIFICATE-----", "\n-----END CERTIFICATE-----")
                        .replace(/(.{64})/g, "$1\n"), // Add newlines every 64 chars
                    FileType.Certificate);
                this.copyFileOrContents(this._privateKeyFile,
                    this._communicationSettings.privateKey
                        .replace("-----BEGIN PRIVATE KEY-----", "-----BEGIN PRIVATE KEY-----\n")
                        .replace("-----END PRIVATE KEY-----", "\n-----END PRIVATE KEY-----")
                        .replace(/(.{64})/g, "$1\n"), // Add newlines every 64 chars,
                    FileType.PrivateKey);
            } else {
                this.logger.debug(`No certificate/public key defined.`);
            }
        }
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

        switch (toType) {
            case "Object":
                if (typeof raw === "string") {
                    try {
                        raw = JSON.parse(raw);
                    } catch {
                        throw new Error(`Failed parsing value '${raw}' from '${fromType}' to '${toType}`);
                    }
                }

                return AMQPDeviceDriver.cleanObject(raw, "$id");
            default:
                break;
        }
        return (raw);
    }

    private static async waitFor(timeout: number, errorMessage: string, callback: any): Promise<void> {
        while (true) {
            if (callback()) { return; }

            if (timeout <= 0) { throw Error(errorMessage); }

            timeout -= 100;
            await sleep(100);
        }
    }

    /**
     * Recursively traverses an object and deletes all properties matching a specified key.
     *
     * @param obj - The object to be cleaned. It can contain nested objects.
     * @param key - The property key to remove from the object and all its nested levels.
     * @returns The same object reference, with all matching keys removed.
     *
     * @example
     * const obj = { name: "Bob", $id: "001", nested: { $id: "002" } };
     * cleanObject(obj, "$id");
     * // Result: { name: "Bob", nested: {} }
     */
    private static cleanObject(obj: any, key: string): any {
        for (const i in obj) {
            if (!obj.hasOwnProperty(i)) {
                continue;
            } else if (typeof obj[i] === "object") {
                this.cleanObject(obj[i], key);
            } else if (key && i === key) {
                delete obj[key];
            }
        }
        return obj;
    }
}
