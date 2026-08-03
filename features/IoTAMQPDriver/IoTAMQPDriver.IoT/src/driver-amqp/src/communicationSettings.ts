import { validateConfigurations } from "@criticalmanufacturing/connect-iot-driver";

/** Driver Communication Settings. Will be available in the Equipment Setup Task */
export interface AMQPCommunicationSettings {
    // Add driver specific settings here
    address: string,
    port: number,

    // Common/driver WS settings
    heartbeatInterval: number;
    setupTimeout: number;
    intervalBeforeReconnect: number;
    connectingTimeout: number;

    /** ********** Security ********** */
    username: string;
    password: string;
    /** Certificates root folder */
    certificatesRootFolder: string;
    /** Certificate to use for communication */
    certificate: string;
    /** Certificate's private key */
    privateKey: string;
}

/** Default Communication Settings */
export const aMQPDefaultCommunicationSettings: AMQPCommunicationSettings = {
    // Add driver specific default settings here
    address: "127.0.0.1",
    port: 5671,

    // Common/driver WS settings
    heartbeatInterval: 30000,
    setupTimeout: 10000,
    intervalBeforeReconnect: 5000,
    connectingTimeout: 30000,

    /** ********** Security ********** */
    username: "",
    password: "",
    /** EndPoint certificates root folder for amqps*/
    certificate: "",
    /** EndPoint certificate to use for communication for amqps*/
    certificatesRootFolder: "${tmp}/ConnectIoT/AMQP/Certificates/${id}",
    /** EndPoint certificate's private key for amqps*/
    privateKey: "",
};

/** Validate communication parameters enum values */
export function validateCommunicationParameters(definition: any, configs: any): void {
    validateConfigurations(configs, definition.criticalManufacturing.automationProtocol.parameters);
}
