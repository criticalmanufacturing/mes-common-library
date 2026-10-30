import { ExtendedCustomProperty } from "./extendedCustomProperty";

export interface AutomationConfigurationValue {

    // The name of the given property
    Name: string,
    // The value of the given property
    Value: any,
    /** Extended information for custom behavior */
    ExtendedCustomProperties: ExtendedCustomProperty[]
}

export enum SubMaterialStateEnum {
    Queued = "Queued",
    InProcess = "InProcess",
    Processed = "Processed",
    Skipped = "Skipped"
}
