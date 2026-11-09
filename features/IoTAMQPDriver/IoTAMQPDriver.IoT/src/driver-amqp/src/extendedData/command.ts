import { Command, validateConfigurations } from "@criticalmanufacturing/connect-iot-driver";

/**
 * NotDurable - No durability (default) - Nothing persists if connection drops
 * Durable - Configuration persists - Queue config survives, but unsettled messages may be lost
 * FullDurability - Full durability  - Queue AND unsettled messages survive connection drops
 */
export enum Durable {
    NotDurable = 0,
    Durable = 1,
    FullDurability = 2
}

/**
 * SessionEnd - Delete when AMQP session ends (default) - Cleaned up when session closes
 * LinkDetach - Delete when link detaches - Cleaned up when sender.close() called
 * ConnectionClose - Delete when connection closes  - Cleaned up when connection drops
 * Never - Never auto-delete (manual cleanup required)  - Persists until manually deleted
 */
export enum ExpirationPolicy {
    SessionEnd = "session-end",
    LinkDetach = "link-detach",
    ConnectionClose = "connection-close",
    Never = "never"
}

/**
 * Queue - Direct routing - one-to-one routing based on exact key match
 * Topic - Topic routing - pattern-based routing with wildcards
 * Fanout - Fanout routing - broadcast to all bound queues
 * Headers - Headers-based routing (message headers instead of routing key)
 */
export enum Capabilities {
    Queue = "Queue",
    Topic = "Topic",
    Fanout = "Fanout",
    Headers = "Headers"
}

/** Extended Command data specific for this driver */
export interface CommandExtendedData {
    address: string;
    durable: Durable | undefined;
    expirationPolicy: ExpirationPolicy | undefined;
    dynamic: boolean;
    dynamicNodeProperties: any | undefined;
    capabilities: Capabilities;
    extraCapabilities: string[] | undefined;
    timeout: number | undefined;
}

/** Default extended data for the commands of this driver */
export const commandExtendedDataDefaults: CommandExtendedData = {
    address: "",
    durable: undefined,
    expirationPolicy: undefined,
    dynamic: false,
    dynamicNodeProperties: null,
    capabilities: Capabilities.Queue,
    extraCapabilities: undefined,
    timeout: undefined
};

/** Assign extended data in the commands, based on the defaults and defined values */
export function validateCommands(definition: any, commands: Command[]): void {
    for (const command of commands) {
        command.extendedData = Object.fromEntries(Object.entries(command.extendedData).map(([key, value]) => {
            return [key, value === "" ? undefined : value];
        }));
        command.extendedData = Object.assign({}, commandExtendedDataDefaults, command.extendedData || {});

        let defaultsCmdExtendedData = definition.criticalManufacturing.automationProtocol.extendedData.command;

        if (typeof command.extendedData.durable === "number") {
            command.extendedData.durable = Durable[command.extendedData.durable];
        }

        command.extendedData = setDefaults(command.extendedData, defaultsCmdExtendedData, "expirationPolicy");
        command.extendedData = setDefaults(command.extendedData, defaultsCmdExtendedData, "durable");

        validateConfigurations(command.extendedData, definition.criticalManufacturing.automationProtocol.extendedData.command);
    }

    function setDefaults(cmdExtendedData: any, defaultCmdExtendedData: any, value: string) {
        if (cmdExtendedData[value] === undefined) {
            cmdExtendedData[value] = defaultCmdExtendedData.find((cmd: { name: string; }) => cmd.name === value).defaultValue;
        }
        return cmdExtendedData;
    }
}
