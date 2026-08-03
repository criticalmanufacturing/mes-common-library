import {
    Task,
    TaskBase,
    Dependencies,
    System,
    DI,
    TYPES,
    Container,
    Communication,
    Utilities
} from "@criticalmanufacturing/connect-iot-controller-engine";
import { AutomationConfigurationDataHandler } from "../../persistence/implementation/automationConfigurationDataHandler";
import { AutomationConfigurationData } from "../../persistence/model/automationConfigurationData";

/** Default values for settings */
export const SETTINGS_DEFAULTS: LoadAutomationConfigurationSettings = {
    message: "",
    _outputs: [],
    entityTypeName: "",
    levelsToLoad: 0,
    retries: 0,
    sleepBetweenRetries: 0
};

/**
 * @whatItDoes
 *
 * This task does something ... describe here
 *
 * @howToUse
 *
 * yada yada yada
 *
 * ### Inputs
 * * `any` : **activate** - Activate the task
 *
 * ### Outputs
 *
 * * `bool`  : ** success ** - Triggered when the the task is executed with success
 * * `Error` : ** error ** - Triggered when the task failed for some reason
 *
 * ### Settings
 * See {@see LoadAutomationConfigurationSettings}
 */
@Task.Task()
export class LoadAutomationConfigurationTask extends TaskBase implements Task.TaskInstance, LoadAutomationConfigurationSettings {

    /** Accessor helper for untyped properties and output emitters. */
    [key: string]: any;

    /** **Inputs** */
    @Task.InputProperty(Task.INPUT_ACTIVATE)
    public activate: any = undefined;

    /** **Outputs** */
    /** To output a success notification */
    @Task.OutputProperty(Task.OUTPUT_SUCCESS)
    public success: Task.Output<boolean> = new Task.Output<boolean>();
    /** To output an error notification */
    @Task.OutputProperty(Task.OUTPUT_ERROR)
    public error: Task.Output<Error> = new Task.Output<Error>();
    /** configuration */
    @Task.OutputProperty(Task.TaskValueType.Object)
    public configuration: Task.Output<any> = new Task.Output<any>();

    /** Properties Settings */
	/** Message */
    message: string;
    entityTypeName: string;
    levelsToLoad: number;
    retries: number;
    sleepBetweenRetries: number;
    _outputs: LoadAutomationConfigurationeOutputSettings[];

    private action = "CustomAutomationRetrieveConfigurationData";
    /** Settings */
    inputs: Task.TaskInput[];
    outputs: Task.TaskOutput[];

    @DI.Inject(TYPES.Dependencies.Logger)
    public _logger: Dependencies.Logger;

    @DI.Inject(TYPES.System.Driver)
    public _driverProxy: System.DriverProxy;

    @DI.Inject(TYPES.Dependencies.Injector)
    private _taskContainer: Container;

    private _configuration: Communication.DriverConfig;

    /** Callback used when instance is available. */
    private _instanceCallBack: System.InstanceProxyCallback;

    /** Instance */
    private _instance: System.LBOS.Cmf.Foundation.BusinessObjects.Entity;

    /** Instance Proxy. Responsible for receiving the correct instance. */
    @DI.Inject(TYPES.Task.InstanceProxy)
    private _instanceProxy: System.InstanceProxy;


    @DI.Inject(TYPES.System.API)
    public _systemAPI: System.SystemAPI;

    @DI.Inject(TYPES.System.Proxy)
    public _systemProxy: System.SystemProxy;

    @DI.Inject("GlobalAutomationConfigurationDataHandler")
    private _automationConfigurationDataHandler: AutomationConfigurationDataHandler;

    /**
     * When one or more input values is changed this will be triggered,
     * @param changes Task changes
     */
    async onChanges(changes: Task.Changes): Promise<void> {
        if (changes["activate"]) {
            this.activate = undefined;

            //retrive id
            const systemSettings: Utilities.SystemApiUtilsSettings = {
                maxRetries: 3,
                sleepBetweenRetries: 1000,
                logger: this._logger,
            };

            this._instance = await this._systemProxy.getObjectById(this._instance.Id, this.entityTypeName, this.levelsToLoad, null, systemSettings);

            if (this.action != null) {
                const deeInputs = new Map<string, object>();

                let loadController = true;
                //retrieve instance entity
                deeInputs.set(`InstanceName`, this._instance.Name as any);
                deeInputs.set(`InstanceEntityTypeName`, this.entityTypeName as any);
                //is driver instance entity
                if (this._taskContainer.isBound(TYPES.System.Driver)) {
                    loadController = false;
                }
                deeInputs.set(`LoadControlerConfiguration`, loadController as any);

                // Immediately capture the inputs NOW and not after await loading rules and dee actions
                // because that takes time, and if we cascade dee actions and they end "almost at the same time"
                // we will execute the first DEE call with the inputs of the second DEE call

                let rule: System.LBOS.Cmf.Foundation.BusinessObjects.Rule = null as any;
                // It comes from input, it needs to be validated/ loaded
                // Load rule
                rule = await this.loadRule(this.action);
                if (rule == null) {
                    this._logger.error("No Rule found");
                    this.error.emit({
                        name: "NoRuleFound",
                        message: "Target Rule not found"
                    });
                    return;
                }

                // Validating rule
                if (rule.DEERule == null) {
                    this._logger.error("Action has no DEEAction associated... Leaving...");
                    this.error.emit({
                        name: "NoDEEActionFound",
                        message: "Target Rule has no DEEAction associated"
                    });
                    return;
                }

                // Get DeeAction
                const dee = await this.loadDeeAction(rule.DEERule);
                if (dee == null) {
                    this._logger.error("Target DEE not found...");
                    this.error.emit({
                        name: "InvalidDEEFound",
                        message: "Target DEE not found."
                    });
                    return;
                }

                let deeOutputs = new Map<string, object>();

                // Execute DEE
                const input = new System.LBOS.Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.InputObjects.ExecuteActionInput();
                input.Action = dee;
                input.Input = deeInputs;
                // Inject BaseInput values
                input.IgnoreLastServiceId = true;
                //input.OperationTarget = this.operationTarget as unknown as System.LBOS.Cmf.Foundation.BusinessObjects.EntityTypeSource;
                input.NumberOfRetries = 3;
                input.PageNumber = 0;
                input.PageSize = 0;
                input.ServiceComments = "";

                const output: any =
                    await Utilities.ExecuteWithSystemErrorRetry(this._logger, this.retries, this.sleepBetweenRetries, async () => {
                        const result = await this._systemAPI.call<System.LBOS
                            .Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.OutputObjects.ExecuteActionOutput>(input);
                        return (result);
                    }
                    );
                deeOutputs = output.Output;
                const automationConfiguration: AutomationConfigurationData = deeOutputs.get("RetrivedConfigurationData") as any;

                await this._automationConfigurationDataHandler.store(automationConfiguration);
                this.success.emit(true);

                // Deal with user outputs
                if (this._outputs != null && this._outputs.length > 0) {
                    this._outputs.forEach(_output => {
                        const configurationValue = automationConfiguration.AutomationConfigurationValues.find(o => o.Name === _output.name);
                        if (configurationValue) {
                            (this[`${_output.name}`] as Task.Output<any>).emit(configurationValue.Value);
                        }
                    });
                }
                this.configuration.emit(deeOutputs);

            } else {
                this._logger.error("No action found, nothing to do");
                this.error.emit({
                    name: "NoActionFound",
                    message: "No action found, nothing to do"
                });
                return;
            }
        } new Error("Will stop processing, but Error output will be triggered with this value");

    }

    private instanceCallBack: System.InstanceProxyCallback = async (availableInstance: System.InstanceProxyModel) => {
        // Keep the instance received from controller
        this._instance = availableInstance.instance;
        this._instanceWasLoaded = false;
        if (this._instance != null && this._instance["$type"] != null) {
            this.entityTypeName = this.entityTypeName || this._instance["$type"];
        }
    };

    /** Right after settings are loaded, create the needed dynamic outputs. */
    async onBeforeInit(): Promise<void> {
        if (this._outputs) {
            for (const output of this._outputs) {
                this[`${output.name}`] = new Task.Output<any>();
            }
        }
    }

    /** Initialize this task, register any event handler, etc */
    async onInit(): Promise<void> {
        this.driverName = "";
        // Calculate the value based on the existence (or not) of a driver proxyin the task container
        if (this._taskContainer.isBound(TYPES.System.Driver)) {
            const driverProxy = this._taskContainer.get<System.DriverProxy>(TYPES.System.Driver);
            this.driverName = (driverProxy.automationControllerDriverDefinition as any).DisplayName;
        }
        if (this._instanceProxy != null) {
            this._instanceCallBack = this._instanceProxy.subscribe(this.driverName, this.instanceCallBack);
        }
    }

    /** Cleanup internal data, unregister any event handler, etc */
    async onDestroy(): Promise<void> {
        if (this._instanceProxy != null && this._instanceCallBack != null) {
            this._instanceProxy.unsubscribe(this._driverName, this._instanceCallBack);
            this._instanceCallBack = null as any;
        }
    }

    /**
     * Load a target rule
     * @param ruleName Rule's Name
     */
    private async loadRule(ruleName: string): Promise<System.LBOS.Cmf.Foundation.BusinessObjects.Rule> {
        const systemSettings: Utilities.SystemApiUtilsSettings = {
            maxRetries: this.retries,
            sleepBetweenRetries: this.sleepBetweenRetries,
            logger: this._logger,
        };

        return (await this._systemProxy.getObjectByName(ruleName, "Rule", 0, false, systemSettings));
    }

    /**
     * Load a DeeAction from its name
     * @param actionName Name of the DeeAction
     */
    private async loadDeeAction(actionName: string): Promise<System.LBOS.Cmf.Foundation.Common.DynamicExecutionEngine.Action> {
        // Get DeeAction
        const input = new System.LBOS.Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.InputObjects.GetActionByNameInput();
        input.Name = actionName;
        const output: any =
            await Utilities.ExecuteWithSystemErrorRetry(this._logger, this.retries, this.sleepBetweenRetries, async () => {
                const result = await this._systemAPI.call<System.LBOS
                    .Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.OutputObjects.GetActionByNameOutput>(input);
                return (result);
            }
            );

        return output.Action;
    }
}

// Add settings here
/** LoadAutomationConfiguration Settings object */
export interface LoadAutomationConfigurationSettings extends System.TaskDefaultSettings {
	/** Message */
	message: string;
    _outputs: LoadAutomationConfigurationeOutputSettings[];
    /** EntityType Name */
    entityTypeName: string,
    /** LevelsToLoad to be used in loading */
    levelsToLoad: number,
    /** Number of retries until a good answer is received from System */
    retries: number;
    /** Number of milliseconds to wait between retries */
    sleepBetweenRetries: number;
}

/** Output Driver format */
export interface LoadAutomationConfigurationeOutputSettings {
    /** Name of the parameter */
    name: string;
    /** Label to use on GUI */
    label?: string;
    /** Default value for that input */
    defaultValue: any;
    /** ValueType. */
    valueType?: Task.TaskComplexValueType;
    /** DataType */
    dataType?: string;
    /** Automation Reference Type */
    referenceType?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationReferenceType;
    /** When is an Enum, these are the values to show */
    enumValues?: { value: string }[];
    /** Description of the input */
    description?: string;
    /** Description of the input */
    automationDataType?: System.LBOS.Cmf.Foundation.BusinessObjects.AutomationDataType;
    /** Complex Automation Data Type Value (Culture) */
    complexValue?: any;
}
