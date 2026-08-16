import { DataStoreLocation, System } from "@criticalmanufacturing/connect-iot-controller-engine";

/**
 * Example task shared code library, to be used within code task
 */
export class CustomCodeUtilitiesFramework {

    private _currentMappings: Map<number, any[]>;

    public get mappings(): Map<number, any[]> { return (this._currentMappings); }

    /**
     * Given a String, creates a unique number or hash
     */
    public createHashCode(stringToHash: string): number {

        let hashOfString = 0;
        const l = stringToHash.length;
        if (l > 0) {
            let i = 0;
            while (i < l) {
                hashOfString = (hashOfString << 5) - hashOfString + stringToHash.charCodeAt(i++) | 0;
            }
        }
        return hashOfString;
    }

    /**
     * Resolves the smart table to retrieve parameters and positions
     * @param configurationTable Name of the Smart Table in MES
     * @param resourceName Name of the resource
     * @param resourceType Name of the resource type
     * @param productName Name of the product
     * @param materialName Name of the material
     */
    public async resolveSmartTable(framework: any, contextTableKeys: Map<string, any>,
        contextResolveValues: Map<string, any>,
        mappingTablePersistedName: string,
        configurationTable: string,
        onlyFirstRow: boolean = false,
        skipCache: boolean = false): Promise<any> {

        let stringKeys = "";

        const keys: System.LBOS.Cmf.Foundation.BusinessObjects.NgpDataRow = contextTableKeys;

        for (const [key, value] of contextTableKeys) {
            stringKeys = stringKeys + key + value.toString();
        }

        framework.logger.debug(`Resolve Table -> ${configurationTable}: ${JSON.stringify(Array.from(contextTableKeys.entries()))}`);

        const hashCode = this.createHashCode(stringKeys);

        // Sanity check
        if (this._currentMappings === null || typeof (this._currentMappings) === "undefined") {

            await this.refreshMapping(framework, mappingTablePersistedName);
        }

        if (!skipCache && (this._currentMappings != null && this._currentMappings.size > 0
            && this._currentMappings.has(hashCode))) {

            framework.logger.debug(`Will use cached mappings`);
            const mappings = this._currentMappings.get(hashCode);
            let resultMapping: any;

            if (mappings !== null && mappings.length > 0) {
                const mappingResults = mappings.map((mapping) => Object.values(mapping)[0]);
                if (contextResolveValues != null) {
                    resultMapping = mappingResults.find((result) =>
                        Array.from(contextResolveValues.entries()).every(([key, value]) => result[key] === value));
                } else {
                    resultMapping = mappingResults;
                }

                if (resultMapping == null) {
                    throw new Error(`No Mapping is defined in the MES for these configurations`);
                }

                return resultMapping;
            } else {
                throw new Error(`Mapping hash Key has returned empty values hash="${hashCode}"`);
            }
        } else {

            // First time, the mappings have not been created
            if (this._currentMappings === null || typeof (this._currentMappings) === "undefined") {

                framework.logger.debug(`First time, current mapping will be instantiated`);
                this._currentMappings = new Map<number, any[]>()
            }

            framework.logger.debug(`Will retrieve mappings from the MES`);

            // Need to Resolve the MES SmartTable
            const input: System.LBOS.Cmf.Foundation.BusinessOrchestration.TableManagement.InputObjects.ResolveSmartTableInput =
                new System.LBOS.Cmf.Foundation.BusinessOrchestration.TableManagement.InputObjects.ResolveSmartTableInput();

            const smartTable = new System.LBOS.Cmf.Foundation.BusinessObjects.SmartTables.SmartTable();

            smartTable.Name = configurationTable;
            input.SmartTable = smartTable;
            input.Values = keys;
            input.OnlyFirstRow = onlyFirstRow;

            const output = await framework.system.call(input);

            if (output != null && output.Result != null) {

                const results: any[] = <any[]>(output.Result[`T_ST_${configurationTable}`]);

                if (results != null && results.length > 0) {
                    const newMapping: any[] = [];
                    let resultMapping: any;

                    for (const result of results) {
                        newMapping.push({ result: result });
                    }

                    resultMapping = contextResolveValues != null
                        ? results.find((result) => Array.from(contextResolveValues.entries()).every(([key, value]) => result[key] === value))
                        : results;

                    this._currentMappings.set(hashCode, newMapping);

                    await this.store(framework, mappingTablePersistedName, this._currentMappings, System.DataStoreLocation.Persistent);

                    if (resultMapping == null) {
                        framework.logger.warning(`No Mapping is defined in the MES for these configurations`);
                    }

                    return resultMapping;
                } else {

                    framework.logger.warning(`Smart Table Resolve "${configurationTable}" has returned no values ="${JSON.stringify(keys)}"`);
                    return;
                }
            }
        }
    }

    /**
     * Cleans all the stored and cached data
     */
    public async resetTableMapping(framework: any, mappingTablePersistedName: string): Promise<void> {

        // clean cache
        this._currentMappings = new Map<number, any[]>()

        // clean persistency
        await this.store(framework, mappingTablePersistedName, this._currentMappings, System.DataStoreLocation.Persistent);
    }

    /**
     * Stores data in the persistency. Needed due to the fact the persistency does not accept Map objects
     * Converts Map to an Array
     */
    public async store(framework: any, identifier: string, data: any, location: DataStoreLocation): Promise<void> {

        if (data instanceof Map) {
            const obj = {};
            data.forEach((v, k) => { obj[k] = v });
            data = obj;
        }

        await framework.dataStore.store(identifier, data, location);

    }

    public isNullOrUndefined(value: any): boolean {
        if (value === null || typeof (value) === "undefined") {
            return true
        }
        return false;
    }

    private extractResults(mappings: Array<any>, contextResolveValues: Map<string, string>): Array<any> {

        const resultMapping: Array<any> = new Array<any>();
        if (mappings !== null && mappings.length > 0) {

            // Iterate over key mappings
            for (const mapping of mappings) {
                const mappingValues = Object.values(mapping);

                // Match mappings with resolve context to extract a result
                if (contextResolveValues != null && mappingValues != null && mappingValues[0] != null) {
                    let isToAdd = false;
                    for (const [key, value] of contextResolveValues) {
                        isToAdd = mappingValues[0][key] === value ? true : false;
                    }
                    if (isToAdd) {
                        resultMapping.push(mappingValues[0]);
                    }
                }
            }

            if (resultMapping == null || resultMapping.length <= 0) {
                throw new Error(`No Mapping is defined in the MES for these configurations`);
            }

            return resultMapping;
        }

        return resultMapping;
    }

    /**
     * Retrieves data in the persistency. Needed due to the fact the persistency does not accept Map objects
     * Converts Array to a Map
     */
    private async retrieveMapping(framework: any, identifier: string): Promise<Map<number, any[]>> {

        const newMapping = new Map<number, any[]>()
        const retrieve = await framework.dataStore.retrieve(identifier, undefined);

        if (retrieve !== null && typeof (retrieve) === "object") {
            Object.keys(retrieve).forEach(k => { newMapping.set(parseInt(k), retrieve[k]) });

            return newMapping;
        } else {

            return null;
        }
    }

    /**
     * Retrieves the persisted data for the mapping table
     */
    private async refreshMapping(framework, mappingTablePersistedName: string): Promise<void> {

        const storedMappings = await this.retrieveMapping(framework, mappingTablePersistedName);

        if (storedMappings != null) {

            framework.logger.info("Loaded persisted Data Collection Mappings for this controller");
            this._currentMappings = storedMappings;
        }
    }
}

export const ID: string = "customCodeUtilitiesFramework";
