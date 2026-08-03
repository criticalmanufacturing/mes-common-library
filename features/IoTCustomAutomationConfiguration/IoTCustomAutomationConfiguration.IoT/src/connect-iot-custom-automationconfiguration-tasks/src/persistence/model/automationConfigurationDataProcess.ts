import { AutomationConfigurationData } from "./automationConfigurationData";

export interface AutomationConfigurationDataProcess {

    /**
     *
     */
    store(automationConfigurationData: AutomationConfigurationData): Promise<void>;

    /**
     *
     */
    getConfigurationByDriverName(driverName: string);

    /**
     *
     */
    getControllerConfiguration(id: string);

    /**
     * Removes an Material object from the persistence
     * @param id Material Id
     */
    deleteConfiguration (id: string);

    /**
     * Loads all the existing Materials to memory
     */
    InitializePersistedData();

}
