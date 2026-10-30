import { Event as EquipmentEvent, getConfigurationNode, validateConfigurationEnum, validateConfigurations } from "@criticalmanufacturing/connect-iot-driver";

/**
 * Event Type
 */
export enum MTConnectEventType {
    Probe = "Probe",
    Current = "Current",
    Sample = "Sample",
    Assets = "Assets",
}

/** Extended Event data specific for this driver */
export interface EventExtendedData {
    mtConnectEventType: MTConnectEventType;
}

/** Default extended data for the events of this driver */
export const eventExtendedDataDefaults: EventExtendedData = {
    mtConnectEventType: MTConnectEventType.Probe
};

/** Assign extended data in the events, based on the defaults and defined values */
export function validateEvents(definition: any, events: EquipmentEvent[]): void {
    const mtConnectEventType = getConfigurationNode(definition.criticalManufacturing.automationProtocol.extendedData.event, "mtConnectEventType");
    for (const event of events) {
        event.extendedData = Object.assign({}, eventExtendedDataDefaults, event.extendedData || {});
        validateConfigurations(event.extendedData, definition.criticalManufacturing.automationProtocol.extendedData.event);
        validateConfigurationEnum(mtConnectEventType, MTConnectEventType, (event.extendedData as EventExtendedData).mtConnectEventType);
    }
}
