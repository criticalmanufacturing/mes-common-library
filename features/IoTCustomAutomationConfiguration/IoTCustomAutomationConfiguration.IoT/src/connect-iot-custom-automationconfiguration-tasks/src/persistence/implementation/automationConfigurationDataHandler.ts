import { Dependencies, DI, System, TYPES } from "@criticalmanufacturing/connect-iot-controller-engine";

import {
    injectable
} from "inversify";

import { Priority, PriorityQueue } from "./priorityQueue";
import * as moment from "moment";
import { AutomationConfigurationData } from "../model/automationConfigurationData";
import { AutomationConfigurationDataProcess } from "../model/automationConfigurationDataProcess";

@injectable()
export class AutomationConfigurationDataHandler implements AutomationConfigurationDataProcess {

    private _AutomationConfigurationData: AutomationConfigurationData[];
    private _AutomationConfigurationDataNames: string[];
    private priorityQueue = new PriorityQueue("AutomationConfigurationDataHandler");


    @DI.Inject(TYPES.Dependencies.Logger)
    private _logger: Dependencies.Logger;

    @DI.Inject(TYPES.System.PersistedDataStore)
    private _dataStore: System.DataStore;

    /**
     * Method to create the Material persistence file
     * when a MO material is tracked in
     * State is saved as SETUP
     * @param materialData Material data
     */
    public async store(
        automationConfigurationData: AutomationConfigurationData,
        priority: number = Priority.Low): Promise<void> {

        return new Promise<void>((resolve, reject) => {
            const callback = async () => {
                try {
                    if (this._AutomationConfigurationData === undefined) {
                        await this.InitializePersistedData();
                    }

                    if (automationConfigurationData) {
                        this._logger.info(`Storing data for Automation Configuration Data ${automationConfigurationData.AutomationConfigurationName}`);
                        automationConfigurationData.LastUpdate = moment().utc().valueOf().toString();
                        this._AutomationConfigurationData.push(automationConfigurationData);
                        const identifier = `Configuration_${automationConfigurationData.AutomationConfigurationName}`;

                        await this._dataStore.store(identifier, automationConfigurationData, System.DataStoreLocation.Persistent);

                        if (!this._AutomationConfigurationDataNames.find(o => o === identifier)) {
                            this._AutomationConfigurationDataNames.push(identifier);
                            await this._dataStore.store(`AutomationConfigurationDataOnPersistence`, this._AutomationConfigurationDataNames, System.DataStoreLocation.Persistent);
                        }

                        // tslint:disable-next-line:max-line-length
                        this._logger.info(`Stored data for Configuration '${automationConfigurationData.AutomationConfigurationName}' and content '${JSON.stringify(automationConfigurationData)}'`);
                    }
                } catch (error) {
                    reject(error);
                }

                resolve();
            };

            this.priorityQueue.enqueue(callback, priority);
        });
    }

    public async getConfigurationByDriverName(id: string) {
        if (!id) {
           return;
        }

        if (this._AutomationConfigurationData === undefined) {
            await this.InitializePersistedData();
        }

        const result = this._AutomationConfigurationData.filter(o =>
            o.AutomationConfigurationDriverFriendlyName &&
            o.AutomationConfigurationRelatedEntityName === id);

        return result;

    }

    public async getControllerConfiguration(id: string) {
        if (!id) {
            return;
         }

        if (this._AutomationConfigurationData === undefined) {
            await this.InitializePersistedData();
        }

        const result = this._AutomationConfigurationData.filter(o =>
            o.AutomationConfigurationRelatedEntityName === id);

        return result;

    }

    public async deleteConfiguration(id: string,
        priority: number = Priority.Medium): Promise<void> {
        return new Promise<void>((resolve, reject) => {
            const callback = async () => {
                try {
                    if (this._AutomationConfigurationData === undefined) {
                        await this.InitializePersistedData();
                    }

                    const automationConfigurationData = this._AutomationConfigurationData.find(o =>
                        o.AutomationConfigurationRelatedEntityName === id);

                    if (automationConfigurationData) {
                        this._AutomationConfigurationData.splice(this._AutomationConfigurationData.indexOf(automationConfigurationData), 1);

                        const identifier = `Configuration_${automationConfigurationData.AutomationConfigurationName}`;
                        await this._dataStore.store(identifier, undefined, System.DataStoreLocation.Persistent);

                        const materialNameIndex = this._AutomationConfigurationDataNames.findIndex(o => o === identifier);

                        if (materialNameIndex !== -1) {
                            this._AutomationConfigurationDataNames.splice(materialNameIndex, 1);
                            await this._dataStore.store(`AutomationConfigurationDataOnPersistence`, this._AutomationConfigurationDataNames, System.DataStoreLocation.Persistent);
                        }

                        this._logger.info(`Configuration with id '${id}' was deleted.`);
                    } else {
                        this._logger.warning(`Configuration with id '${id}' does not exist. Could not delete it.`);
                    }
                } catch (error) {
                    this._logger.error(`Error: ${(error as Error).message}`);
                    reject(error);
                }

                resolve();
            };

            this.priorityQueue.enqueue(callback, priority);
        });
    }

    /**
     * Loads all the existing Materials to memory
     */
    public async InitializePersistedData(priority: number = Priority.High): Promise<void> {
        return new Promise<void>((resolve, reject) => {
            const callback: any = async () => {
                try {
                    this._AutomationConfigurationData = [];
                    this._AutomationConfigurationDataNames = await this._dataStore.retrieve("AutomationConfigurationDataOnPersistence", []);

                    for (const identifier of this._AutomationConfigurationDataNames) {
                        const material = await this._dataStore.retrieve(identifier, undefined);

                        if (material) {
                            this._AutomationConfigurationData.push(material);
                        }
                    }
                } catch (error) {
                    this._logger.error(`Error: ${(error as Error).message}`);
                    reject(error);
                }

                resolve();
            };

            this.priorityQueue.enqueue(callback, priority);
        });
    }
}

export const ACD = "automationConfigurationDataHandler";
