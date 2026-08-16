import type { IoTATLScriptContextTest } from '../../types';

export function injectUtilitiesHTML(): IoTATLScriptContextTest {
    return {
        _execute: async function () {
            // PackagePacker: Start of Async Script
            const ID: string = "customCodeUtilitiesAPI"
            const UTIL_API_DTS_CONTENT: string = `
/**
 * Example task shared code library, to be used within code task
 */
export interface customUtilitiesAPI {
    /** Retrieves a system object by ID, optionally loading additional levels. */
    getObjectById(framework: any, id: string, type: string, levelsToLoad?: number, typeIsTypeId?: boolean, settings?: SystemApiUtilsSettings): Promise<any>;
    /** Retrieves a system object by name, optionally loading additional levels. */
    getObjectByName(framework: any, name: string, type: string, levelsToLoad?: number, typeIsTypeId?: boolean, settings?: SystemApiUtilsSettings): Promise<any>;
    /** Loads all attributes or the selected attributes for an entity. */
    loadAttributes(framework: any, entity: any, specificAttributes?: string[], settings?: SystemApiUtilsSettings): Promise<any>;
    /** Executes a system query with an optional parameter collection. */
    executeQuery(framework: any, queryObject: any, parameterCollection?: any, settings?: SystemApiUtilsSettings): Promise<any>;
    /** Updates the system and optional communication state of an automation instance. */
    setInstanceSystemState(framework: any, instanceId: string, newState?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationSystemState,
        newCommunicationState?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommunicationState, settings?: SystemApiUtilsSettings): Promise<void>;
}`;
            const UTIL_API_CLASS_NAME: string = "customUtilitiesAPI";
            this.service?.container.library.addFields(
                { name: ID, type: UTIL_API_CLASS_NAME }
            );
            this.service?.container.library.addDefinitions(
                UTIL_API_DTS_CONTENT
            );

            // PackagePacker: End of Async Script
        },
    };
}
