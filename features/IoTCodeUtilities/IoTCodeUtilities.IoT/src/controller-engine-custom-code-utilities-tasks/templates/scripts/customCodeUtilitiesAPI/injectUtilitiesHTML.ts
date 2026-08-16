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
    getObjectById(framework: any, id: string, type: string, levelsToLoad?: number, typeIsTypeId?: boolean, settings?: SystemApiUtilsSettings): Promise<any>;
    getObjectByName(framework: any, name: string, type: string, levelsToLoad?: number, typeIsTypeId?: boolean, settings?: SystemApiUtilsSettings): Promise<any>;
    loadAttributes(framework: any, entity: any, specificAttributes?: string[], settings?: SystemApiUtilsSettings): Promise<any>;
    executeQuery(framework: any, queryObject: any, parameterCollection?: any, settings?: SystemApiUtilsSettings): Promise<any>;
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
