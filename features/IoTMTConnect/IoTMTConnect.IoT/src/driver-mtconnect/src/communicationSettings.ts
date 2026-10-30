import { validateConfigurations } from "@criticalmanufacturing/connect-iot-driver";

/** Driver Communication Settings. Will be available in the Equipment Setup Task */
export interface MTConnectCommunicationSettings {
    // Add driver specific settings here
    /** Set the .net core SDK version to use, when multiple are installed in the system where the driver is running. Leave empty to ignore this setting. */
    netCoreSdkVersion: string;

    // MTConnect Address
    address: string;
    // MTConnect Address Port
    port: number;
    // MTConnect Device
    device: string;

    // Common/driver WS settings
    heartbeatInterval: number;
    setupTimeout: number;
    intervalBeforeReconnect: number;
    connectingTimeout: number;
}

/** Default Communication Settings */
export const mTConnectDefaultCommunicationSettings: MTConnectCommunicationSettings = {
    // Add driver specific default settings here
    /** Set the .net core SDK version to use, when multiple are installed in the system where the driver is running. Leave empty to ignore this setting. */
    netCoreSdkVersion: "",

    // Default MTConnect Address
    address: "localhost",
    // Default MTConnect Address Port
    port: 5000,
    // Default MTConnect Device
    device: "",

    // Common/driver WS settings
    heartbeatInterval: 30000,
    setupTimeout: 10000,
    intervalBeforeReconnect: 5000,
    connectingTimeout: 30000,
};

/** Validate communication parameters enum values */
export function validateCommunicationParameters(definition: any, configs: any): void {
    validateConfigurations(configs, definition.criticalManufacturing.automationProtocol.parameters);
}
