namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common
{
    /// <summary>
    /// Support class that represents the constants to be used on the business layer
    /// </summary>
    public class CustomAutomationConfigurationConstants
    {

        #region GenericTables

        /// <summary>
        /// Custom Reclaim Container Type table name
        /// </summary>
        public static string GenericTableCustomReclaimContainerType = "CustomReclaimContainerType";

        /// <summary>
        /// Custom Reclaim Container Type table SourceContainerType Property 
        /// </summary>
        public static string GenericTableCustomReclaimContainerTypeSourceContainerTypeProperty = "SourceContainerType";

        /// <summary>
        /// CustomReclaimContainerType table ReclaimContainerType Property 
        /// </summary>
        public static string GenericTableCustomReclaimContainerTypeReclaimContainerTypeProperty = "ReclaimContainerType";

        #endregion

        #region LookupTables

        /// <summary>
        /// Custom Sorter Logistical Process
        /// </summary>
        public static string LookupTableCustomMaterialMovementLogisticalProcess = "CustomMaterialMovementLogisticalProcess";

        /// <summary>
        /// Custom Sorter Logistical Process
        /// </summary>
        public static string LookupTableCustomMaterialMovementMESAction = "CustomMaterialMovementMESAction";

        /// <summary>
        /// Custom Sorter Logistical Process for MapCarrier
        /// </summary>
        public static string LookupTableCustomSorterLogisticalProcessMapCarrier = "MapCarrier";

        /// <summary>
        /// Custom Sorter Logistical Process for TransferWafers
        /// </summary>
        public static string LookupTableCustomSorterLogisticalProcessTransferWafers = "TransferWafers";

        /// <summary>
        /// Custom Sorter Logistical Process for Compose
        /// </summary>
        public static string LookupTableCustomSorterLogisticalProcessCompose = "Compose";
        public static string LookupTableCustomSorterAdhocProcessValueAbort = "Abort";

        /// <summary>
        /// Possible types for Container
        /// </summary>
        public static string LookupTableContainerType = "ContainerType";

        /// <summary>
        /// Material Form Lot
        /// </summary>
        public static string MaterialFormLot = "Lot";
        #endregion

        #region Attributes

        /// <summary>
        /// Material Attribute Current ControlJobID
        /// </summary>
        public static string CurrentControlJobID = "CurrentControlJobID";
        /// <summary>
        /// Material Attribute Current ProcessJobID
        /// </summary>
        public static string CurrentProcessJobID = "CurrentProcessJobID";

        /// <summary>
        /// Container Attribute Lot
        /// </summary>
        public static string ContainerAttributeLot = "Lot";

        /// <summary>
        /// Resource Requires Material Movement
        /// </summary>
        public static string ResourceAttributeRequiresMaterialMovement = "RequiresMaterialMovement";

        /// <summary>
        /// Resource Attribute Automation Module Name
        /// </summary>
        public static string ResourceAttributeAutomationModuleName = "AutomationModuleName";

        /// <summary>
        /// Resource Is Load Port In Use
        /// </summary>
        public static string ResourceAttributeIsLoadPortInUse = "IsLoadPortInUse";

        /// <summary>
        /// Resource Allow Download Recipe At TrackIn
        /// </summary>
        public static string ResourceAttributeAllowDownloadRecipeAtTrackIn = "AllowDownloadRecipeAtTrackIn";


        /// <summary>
        /// Container Attribute Map Container Needed for sorter
        /// </summary>
        public static string ContainerAttributeMapContainerNeeded = "MapContainerNeeded";

        /// <summary>
        /// Container Attribute Product
        /// </summary>
        public static string ContainerAttributeProduct = "Product";

        /// <summary>
        /// Product Attribute IsTestWaferMeasurementStep
        /// </summary>
        public static string ProductAttributeCanCreateInventory = "CanCreateInventory";

        /// <summary>
        /// Step Attribute IsLotStart
        /// </summary>
        public static string StepAttributeIsLotStart = "IsLotStart";

        /// <summary>
        /// Contairn Attribute Cassette Not Present
        /// </summary>
        public static string ContainerAttributeCassetteNotPresent = "CassetteNotPresent";

        /// <summary>
        /// Protocol Instance Attribute Hold Reason on Abort
        /// </summary>
        public static string ProtocolInstanceAttributeHoldReasonOnAbort = "HoldReasonOnAbort";

        #endregion

        #region SmartTables

        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContext = "CustomMaterialMovementContext";

        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnService = "Service";
        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnResource = "Resource";
        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnMaterialType = "MaterialType";
        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnMaterial = "Material";
        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnContainerType = "ContainerType";
        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnContainer = "Container";
        /// <summary>
        /// SmartTable SmartTableCustomMaterialMovementContext Name
        /// </summary>
        public static string SmartTableCustomMaterialMovementContextColumnMaterialMovement = "MaterialMovement";

        /// <summary>
        /// SmartTable CustomSorterJobDefinitionContext Name
        /// </summary>
        public static string CustomSorterJobDefinitionContextName = "CustomSorterJobDefinitionContext";

        /// <summary>
        /// SmartTable Step Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnStep = "Step";

        /// <summary>
        /// SmartTable Product Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnProduct = "Product";

        /// <summary>
        /// SmartTable ProductGroup Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnProductGroup = "ProductGroup";

        /// <summary>
        /// SmartTable Flow Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnFlow = "Flow";

        /// <summary>
        /// SmartTable Material Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnMaterial = "Material";

        /// <summary>
        /// SmartTable MaterialType Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnMaterialType = "MaterialType";

        /// <summary>
        /// SmartTable MaterialType Property
        /// </summary>
        public static string CustomSorterJobDefinitionContextColumnCustomSorterJobDefinition = "CustomSorterJobDefinition";

        // <summary>
        // Smart Table Custom Automation Job Id Logic
        // </summary>
        public static string CustomSmartTableCustomAutomationJobIdLogic = "CustomAutomationJobIdLogic";

        // <summary>
        // Smart Table Custom Automation Job Id Logic Column Process Job Id
        // </summary>
        public static string CustomSmartTableCustomAutomationJobIdLogicProcessJobIdColumn = "ProcessJobId";

        // <summary>
        // Smart Table Custom Automation Job Id Logic Column Control Job Id
        // </summary>
        public static string CustomSmartTableCustomAutomationJobIdLogicControlJobIdColumn = "ControlJobId";

        // <summary>
        // Custom Error Handling Smart Table
        // </summary>
        public static string CustomErrorHandlingSmartTable = "CustomErrorHandling";

        // <summary>
        // Smart Table Custom Error Handling Column Custom Notification
        // </summary>
        public static string CustomErrorHandlingSmartTableNotification = "Notification";

        // <summary>
        // Smart Table Custom Error Handling Column Custom Protocol
        // </summary>
        public static string CustomErrorHandlingSmartTableProtocol = "Protocol";

        // <summary>
        // Smart Table Custom Error Handling Column Custom Rule
        // </summary>
        public static string CustomErrorHandlingSmartTableRule = "Rule";

        // <summary>
        // Custom Alarm Management Smart Table
        // </summary>
        public static string CustomAlarmManagementSmartTable = "CustomAlarmManagement";

        // <summary>
        // Smart Table Custom Alarm Management Column Custom Store Data Collection
        // </summary>
        public static string CustomAlarmManagmentTableStoreDataCollection = "StoreDataCollection";

        // <summary>
        // Smart Table Custom Alarm Management Column Custom Error Type
        // </summary>
        public static string CustomAlarmManagementSmartTableErrorType = "ErrorType";

        // <summary>
        // Smart Table Custom Alarm Management Column Custom Include Materials In Process
        // </summary>
        public static string CustomAlarmManagementSmartTableIncludeMaterialsInProcess = "IncludeMaterialsInProcess";

        // <summary>
        // Smart Table Custom Alarm Management Column Custom Context
        // </summary>
        public static string CustomAlarmManagementSmartTableContext = "Context";

        // <summary>
        // Smart Table Custom Alarm Management Column Custom Automation Module Name
        // </summary>
        public static string CustomAlarmManagementSmartTableAutomationModuleName = "AutomationModuleName";
        #endregion

        #region Name Generators

        /// <summary>
        /// Split Lot Name Generator
        /// </summary>
        public const string CustomGenerateSplitLotNames = "CustomGenerateSplitLotNames";

        /// <summary>
        /// Production lot Name Generator
        /// </summary>
        public const string CustomGenerateProductionLotNames = "CustomProductionLotNameGenerator";

        #endregion

        #region Entity Types

        public static string CustomEntityTypeCustomAutomationConfiguration = "CustomAutomationConfiguration";
        public static string CustomEntityTypeCustomAutomationConfigurationDescription = "Custom Entity to store Automation Connection Configuration";

        public static string CustomEntityTypeCustomAutomationConfigurationValue = "CustomAutomationConfigurationValue";
        public static string CustomEntityTypeCustomAutomationConfigurationValueDescription = "Custom Entity Relation between Parameter and CustomAutomationConfiguration to store Automation Connection Configuration Value";

        public static string CustomEntityTypeCustomAutomationConfigurationStructure = "CustomAutomationConfigurationStructure";
        public static string CustomEntityTypeCustomAutomationConfigurationStructureDescription = "Configuration Structure, connecting all related configurations";

        public static string CustomEntityTypeCustomAutomationConfigurationEntity = "CustomAutomationConfigurationEntity";
        public static string CustomEntityTypeCustomAutomationConfigurationEntityDescription = "Entity Relation to store Automation Connection Configuration Entity Relation";

        #endregion

        #region Configuration
        public static string CustomEntityToExcludeFromConfigurationRelationConfiguration = "/Cmf/Custom/Automation/AutomationConfigurationAutomation/EntityToExcludeFromConfigurationRelation";
        #endregion
    }
}
