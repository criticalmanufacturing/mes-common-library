import { Command, validateConfigurations } from "@criticalmanufacturing/connect-iot-driver";

/**
 * Address - Overrides command address
 * Subject - property will be used as message subject
 * Body - will be used as full message body
 * Value - will be used to create an entry for the message body
 */
export enum ParameterType {
    Address = "Address",
    Subject = "Subject",
    Body = "Body",
    Value = "Value"
}

/** Command Parameter Extended Data allows ASCII conversion selection and definition of Command Parameter Type */
export interface CommandParameterExtendedData {
    parameterType: ParameterType;
}

/** Default extended data for the Command Parameter of this driver */
export const commandParameterExtendedDataDefaults: CommandParameterExtendedData = {
    parameterType: ParameterType.Value,
};

/**  Assign default extended data in the command parameter, based on the defaults and defined values */
export function validateCommandParameters(definition: any, commands: Command[]): void {
    for (const command of commands) {
        if (command.parameters.length > 0) {
            for (const parameter of command.parameters) {
                parameter.extendedData = Object.assign({}, commandParameterExtendedDataDefaults, parameter.extendedData || {});
                validateConfigurations(parameter.extendedData, definition.criticalManufacturing.automationProtocol.extendedData.commandParameter);
            }
        }
    }
}
