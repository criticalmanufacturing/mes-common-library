import { AutomationConfigurationValue } from "./automationConfigurationValue";
import { ExtendedCustomProperty } from "./extendedCustomProperty";


export interface AutomationConfigurationData {

    // Automation Configuration Name
    AutomationConfigurationName : string,
    // Automation Configuration Related Entity Name
    AutomationConfigurationRelatedEntityName: string,
    // Automation Configuration Driver Friendly Name
    AutomationConfigurationDriverFriendlyName: string,
    AutomationConfigurationValues: AutomationConfigurationValue[],
    // Nested Automation Configuration Data
    NestedAutomationConfigurationData: AutomationConfigurationData,
    /** Extended information for custom behavior */
    ExtendedCustomProperties: ExtendedCustomProperty[]
    /** Last Time the Material was updated */
    LastUpdate: string,

}

export enum MaterialStateEnum {
    Setup = "Setup",
    InProcess = "InProcess",
    Complete = "Complete",
    Aborting = "Aborting",
    Aborted = "Aborted"
}
