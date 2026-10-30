import { System, Utilities } from "@criticalmanufacturing/connect-iot-controller-engine";

/**
 * Example task shared code library, to be used within code task
 */
export class CustomUtilitiesUtilApi {

    // EntityTypeId -> EntityTypeName
    private _systemEntityTypeCache: Map<string, string> = new Map<string, string>();

    /**
     * Get a system object from an id
     * @param id Id of the object
     * @param type Type of the object
     * @param levelsToLoad Levels to Load (defaults to 0)
     * @param typeIsTypeId Flag indicating if the type is a typeId instead of a typeName
     * @param settings Settings to use. If undefined, will use the class-wide settings
     */
    public async getObjectById(framework: any, id: string, type: string, levelsToLoad?: number, typeIsTypeId?: boolean, settings?: SystemApiUtilsSettings):
        Promise<any> {
        settings = settings || SystemApiUtilsDefaults;
        typeIsTypeId = typeIsTypeId || false;
        levelsToLoad = levelsToLoad || 0;
        let typeName = type;

        if (typeIsTypeId === true) {
            typeName = await (this.resolveSystemTypeName(framework, type, settings));
        }

        framework.logger.debug(`Getting ObjectById: Id='${id}', Type='${typeName}'`);

        const input = new (await System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects.GetObjectByIdInput)();
        input.Id = id;
        input.LevelsToLoad = levelsToLoad;
        input.Type = typeName;

        const res: System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects.GetObjectByIdOutput =
            await Utilities.ExecuteWithSystemErrorRetry(framework.logger, settings.maxRetries, settings.sleepBetweenRetries, async () => {
                return (await framework.system.call(input));
            }
            );

        framework.logger.info(`Resolved Object Name='${res.Instance.Name}' from Id='${id}'`);
        return (res.Instance);
    }

    /**
     * Get a system object from a name
     * @param name Name of the object
     * @param type Type of the object
     * @param levelsToLoad Levels to Load (defaults to 0)
     * @param typeIsTypeId Flag indicating if the type is a typeId instead of a typeName
     * @param settings Settings to use. If undefined, will use the class-wide settings
     */
    public async getObjectByName(framework: any, name: string, type: string, levelsToLoad?: number,
        typeIsTypeId?: boolean, settings?: SystemApiUtilsSettings): Promise<any> {
        settings = settings || SystemApiUtilsDefaults;
        typeIsTypeId = typeIsTypeId || false;
        levelsToLoad = levelsToLoad || 0;
        let typeName = type;

        if (typeIsTypeId === true) {
            typeName = await (this.resolveSystemTypeName(framework, type, settings));
        }

        framework.logger.debug(`Getting ObjectByName: Name='${name}', Type='${typeName}'`);
        const input = new (await System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects.GetObjectByNameInput)();
        input.Name = name;
        input.LevelsToLoad = levelsToLoad;
        input.Type = typeName;

        const res: System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects.GetObjectByNameOutput =
            await Utilities.ExecuteWithSystemErrorRetry(framework.logger, settings.maxRetries, settings.sleepBetweenRetries, async () => {
                return (await framework.system.call(input));
            }
            );

        framework.logger.info(`Resolved Object Id='${res.Instance.Id}' from Name='${name}'`);
        return (res.Instance);
    }

    /**
     * Loads all attributes from a loaded entity
     * @param entity entity that has attributes to be loaded
     * @param specificAttributes Specific attributes to load. Empty or null value means load all attributes.
     * @param settings Settings to use. If undefined, will use the class-wide settings
     */
    public async loadAttributes(framework: any, entity: any, specificAttributes?: string[], settings?: SystemApiUtilsSettings): Promise<any> {
        settings = settings || SystemApiUtilsDefaults;

        const input = new (await System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects.LoadObjectAttributesInput)();
        input.Entity = entity;

        if (specificAttributes != null && specificAttributes.length > 0) {
            input.Attributes = specificAttributes;
        }

        framework.logger.debug(`Loading Attributes for Id='${entity.Id}', Name='${entity.Name}'`);
        const res: System.LBOS.Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects.LoadObjectAttributesOutput =
            await Utilities.ExecuteWithSystemErrorRetry(framework.logger, settings.maxRetries, settings.sleepBetweenRetries, async () => {
                return (await framework.system.call(input));
            }
            );

        framework.logger.info(`Loaded Attributes for Id='${entity.Id}', Name='${entity.Name}'`);
        return (res.Entity);
    }

    /**
     * Executes a system query
     * @param queryObject The query object to execute
     * @param parameterCollection Optional parameter collection to use
     * @param settings Settings to use. If undefined, will use the class-wide settings
     */
    public async executeQuery(framework: any, queryObject: any,
        parameterCollection?: any,
        settings?: SystemApiUtilsSettings): Promise<any> {
        settings = settings || SystemApiUtilsDefaults;

        const input = new System.LBOS.Cmf.Foundation.BusinessOrchestration.QueryManagement.InputObjects.ExecuteQueryInput();
        input.QueryObject = queryObject;
        if (parameterCollection != null) {
            input.QueryParameters = parameterCollection;
        }

        framework.logger.debug(`Executing query '${queryObject.Name}' in System, over entity '${queryObject.EntityTypeName}'`);
        const res: any =
            await Utilities.ExecuteWithSystemErrorRetry(framework.logger, settings.maxRetries, settings.sleepBetweenRetries, async () => {
                const result = await framework.system.call(input);
                return (result.NgpDataSet);
            }
            );

        framework.logger.info(`Executed query, and received '${res?.T_Result?.length || 0}' results`);
        return (res);
    }

    /**
     * Update System and/or communication states of an automation Instance
     * @param instanceId Full id of the entity (expected format AutomationControllerInstance/2002010030220000001)
     * @param newState Optional new state to set
     * @param newCommunicationState Optional new communication state to set (only for Driver Instances)
     * @param settings Settings to use. If undefined, will use the class-wide settings
     */
    public async setInstanceSystemState(framework: any, instanceId: string, newState?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationSystemState,
        newCommunicationState?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommunicationState,
        settings?: SystemApiUtilsSettings): Promise<void> {
        settings = settings || SystemApiUtilsDefaults;

        const [instanceType, id] = instanceId.split("/");
        if (instanceType != null && id != null) {
            let type: System.LBOS.Cmf.Foundation.BusinessObjects.SetAutomationStateType;

            // Determine which type to set. Also, Controller Instances cannot have communication states
            switch (instanceType.toLocaleLowerCase()) {
                case "automationcontrollerinstance":
                    type = System.LBOS.Cmf.Foundation.BusinessObjects.SetAutomationStateType.AutomationControllerInstance;
                    newCommunicationState = undefined;
                    break;
                case "automationdriverinstance": type = System.LBOS.Cmf.Foundation.BusinessObjects.SetAutomationStateType.AutomationDriverInstance; break;
                default: throw new Error(`Invalid instance type '${instanceType}'. No state change will be performed.`);
            }

            let communicationLogText: string =
                newState != null ? `'${System.LBOS.Cmf.Foundation.BusinessObjects.AutomationSystemState[newState || 0]}'` : "";
            communicationLogText += newCommunicationState != null ? `
        (${System.LBOS.Cmf.Foundation.BusinessObjects.AutomationCommunicationState[newCommunicationState || 0]})` : "";

            framework.logger.debug(`Updating '${instanceId}' instance to ${communicationLogText}`);
            const input = new (await System.LBOS.Cmf.Foundation.BusinessOrchestration.ConnectIoTManagement.InputObjects.SetAutomationStateInput)();
            input.Id = id;
            input.Type = type;
            input.IgnoreLastServiceId = true;

            if (newState != null) {
                input.SystemState = newState;
            }
            if (newCommunicationState != null) {
                input.CommunicationState = newCommunicationState;
            }

            await Utilities.ExecuteWithSystemErrorRetry(framework.logger, settings.maxRetries, settings.sleepBetweenRetries, async () => {
                await framework.system.call(input);
            });

            framework.logger.info(`Updated '${instanceId}' instance to ${communicationLogText}`);
        } else {
            throw new Error(`Invalid instanceId '${instanceId}' provided. No state change will be performed.`);
        }
    }

    /**
     * Resolve the SystemType name from a SystemType Id
     * @param systemTypeId Id to resolve
     * @param settings Settings to use. If undefined, will use the class-wide settings
     */
    private async resolveSystemTypeName(framework: any, systemTypeId: string, settings?: SystemApiUtilsSettings): Promise<string> {
        settings = settings || SystemApiUtilsDefaults;
        let result = this._systemEntityTypeCache.get(systemTypeId);
        if (result == null) {
            framework.logger.info(`Resolving EntityType name from the Id '${systemTypeId}'`);
            const input = new System.LBOS.Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects.GetEntityTypeByIdInput();
            input.Id = systemTypeId;

            const res: System.LBOS.Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.OutputObjects.GetEntityTypeByIdOutput =
                await Utilities.ExecuteWithSystemErrorRetry(framework.logger, settings.maxRetries, settings.sleepBetweenRetries, async () => {
                    return (framework.system.call(input));
                }
                );

            result = res.EntityType.Name;
            framework.logger.info(`Resolved EntityType name '${result}' from the Id '${systemTypeId}'`);
            this._systemEntityTypeCache.set(systemTypeId, result);
        }

        return (result);
    }
}

/** Settings to use for the SystemApiUtils */
export interface SystemApiUtilsSettings {
    /** Maximum number of retries to get a reply from System */
    maxRetries: number;
    /** Milliseconds to wait between retries */
    sleepBetweenRetries: number;
}

/** Default SystemApiUtilsSettings values*/
export const SystemApiUtilsDefaults: SystemApiUtilsSettings = {
    maxRetries: 30,
    sleepBetweenRetries: 1000,
};

export const ID: string = "customCodeUtilitiesAPI";
