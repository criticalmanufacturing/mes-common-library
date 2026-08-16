import type { IoTATLScriptContextTest } from '../../types';

export function injectUtilitiesHTML(): IoTATLScriptContextTest {
    return {
        _execute: async function () {
            // PackagePacker: Start of Async Script
            const ID: string = "customCodeUtilitiesFramework"
            const UTIL_API_DTS_CONTENT: string = `
/**
 * Example task shared code library, to be used within code task
 */
export interface CustomCodeUtilitiesFramework {
    createHashCode(stringToHash: string): number;
    resolveSmartTable(framework: any, contextTableKeys: Map<string, any>,
        contextResolveValues: Map<string, any>, mappingTablePersistedName: string, configurationTable: string, onlyFirstRow: boolean = false): Promise<any>;
    resetTableMapping(framework: any, mappingTablePersistedName: string): Promise<void>
    store(framework: any, identifier: string, data: any, location: DataStoreLocation): Promise<void>;
    isNullOrUndefined(value: any): boolean;
}`;
            const UTIL_API_CLASS_NAME: string = "CustomCodeUtilitiesFramework";
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
