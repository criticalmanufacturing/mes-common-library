import { Command, getConfigurationNode, validateConfigurationEnum, validateConfigurations } from "@criticalmanufacturing/connect-iot-driver";

/**
 * Command Type
 */
export enum MTConnectCommandType {
    Probe = "Probe",
    Current = "Current",
    Sample = "Sample",
    Assets = "Assets",
    Asset = "Asset",
}

/** Extended Command data specific for this driver */
export interface CommandExtendedData {
    // Command type.
    mtConnectCommandType: MTConnectCommandType;
}

/** Default extended data for the commands of this driver */
export const commandExtendedDataDefaults: CommandExtendedData = {
    mtConnectCommandType: MTConnectCommandType.Probe
};

/** Assign extended data in the commands, based on the defaults and defined values */
export function validateCommands(definition: any, commands: Command[]): void {
    const commandType = getConfigurationNode(definition.criticalManufacturing.automationProtocol.extendedData.command, "mtConnectCommandType");
    for (const command of commands) {
        command.extendedData = Object.assign({}, commandExtendedDataDefaults, command.extendedData || {});
        validateConfigurations(command.extendedData, definition.criticalManufacturing.automationProtocol.extendedData.command);
        validateConfigurationEnum(commandType, MTConnectCommandType, (command.extendedData as CommandExtendedData).mtConnectCommandType);
    }
}
