using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.QueryManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.TableManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.ConfigurationManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Configuration;
using DeeAction = Cmf.Foundation.Common.DynamicExecutionEngine.Action;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.FacilityManagement.FlowManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.OrderManagement.InputObjects;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Generic object access (get, create, clone, relations), queries and configuration (steps, smart tables).
    /// </summary>
    public interface IMasterDataGateway
    {
        T? GetByName<T>(string name) where T : class;
        T? GetById<T>(long id, int? levelsToLoad = null) where T : class;
        T? Create<T>(T entity) where T : class;
        T? LoadRelations<T>(T entity, params string[] relationNames) where T : class;
        NgpDataSet ExecuteQuery(QueryObject query);
        NgpDataSet ExecuteQueryByName(string queryName, QueryParameterCollection parameters);
        void UpdateStep(Step step);
        Config GetConfig(string path);
        Config UpdateConfig(Config config);
        DeeAction GetDeeAction(string name);
        DeeAction DisableDeeAction(DeeAction action);
        SmartTable GetSmartTable(string name, bool loadData);
        void RemoveSmartTableRows(SmartTable smartTable, NgpDataSet rows);

        /// <summary>Names of the production orders not terminated whose name starts with <paramref name="namePrefix"/>.</summary>
        List<string> FindOpenProductionOrders(string namePrefix);
        void Terminate<T>(T entity) where T : class;
        void CloseProductionOrder(ProductionOrder order);

        /// <summary>Releases a created order; the MES then tracks its progress (InProgress, Completed).</summary>
        void ReleaseProductionOrder(ProductionOrder order);

        /// <summary>Changes the order's quantity (an order with nothing in progress that reaches it becomes Completed).</summary>
        void ChangeProductionOrderQuantity(ProductionOrder order, decimal quantity);
    }

    public sealed class MasterDataGateway(IMesCall mes) : IMasterDataGateway
    {
        public T? GetByName<T>(string name) where T : class =>
            mes.Run("GetObjectByName", () => new GetObjectByNameInput()
            {
                Name = name,
                Type = typeof(T),
                IgnoreLastServiceId = true
            }.GetObjectByNameSync(), $"{typeof(T).Name} '{name}'").Instance as T;

        public T? GetById<T>(long id, int? levelsToLoad = null) where T : class
        {
            var input = new GetObjectByIdInput()
            {
                Id = id,
                Type = typeof(T),
                IgnoreLastServiceId = true
            };
            if (levelsToLoad != null)
            {
                input.LevelsToLoad = levelsToLoad.Value;
            }
            return mes.Run("GetObjectById", () => input.GetObjectByIdSync(), $"{typeof(T).Name} {id}").Instance as T;
        }

        public T? Create<T>(T entity) where T : class =>
            mes.Run("CreateObject", () => new CreateObjectInput()
            {
                Object = entity
            }.CreateObjectSync(), typeof(T).Name).Object as T;

        public T? LoadRelations<T>(T entity, params string[] relationNames) where T : class =>
            mes.Run("LoadObjectRelations", () => new LoadObjectRelationsInput()
            {
                LevelsToLoad = 1,
                Object = entity,
                RelationNames = [.. relationNames]
            }.LoadObjectRelationsSync(), string.Join(",", relationNames)).Object as T;

        public List<string> FindOpenProductionOrders(string namePrefix)
        {
            var query = new QueryObject
            {
                Description = "",
                EntityTypeName = "ProductionOrder",
                Name = "SimulatorProductionOrders",
                Query = new Query
                {
                    Distinct = false,
                    Filters =
                    [
                        new Filter()
                        {
                            Name = "Name",
                            ObjectName = "ProductionOrder",
                            ObjectAlias = "ProductionOrder_1",
                            Operator = FieldOperator.StartsWith,
                            Value = namePrefix,
                            LogicalOperator = LogicalOperator.AND,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        },
                        new Filter()
                        {
                            Name = "UniversalState",
                            ObjectName = "ProductionOrder",
                            ObjectAlias = "ProductionOrder_1",
                            Operator = FieldOperator.IsNotEqualTo,
                            Value = Cmf.Foundation.Common.Base.UniversalState.Terminated,
                            LogicalOperator = LogicalOperator.Nothing,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        }
                    ],
                    Fields =
                    [
                        new Field() { Alias = "Id", ObjectName = "ProductionOrder", ObjectAlias = "ProductionOrder_1", IsUserAttribute = false, Name = "Id", Position = 0, Sort = FieldSort.NoSort },
                        new Field() { Alias = "Name", ObjectName = "ProductionOrder", ObjectAlias = "ProductionOrder_1", IsUserAttribute = false, Name = "Name", Position = 1, Sort = FieldSort.NoSort }
                    ],
                    Relations = []
                }
            };

            var dataSet = Utilities.ToDataSet(ExecuteQuery(query));
            return dataSet.Tables.Count > 0
                ? dataSet.Tables[0].Rows.Cast<System.Data.DataRow>().Select(row => (string)row["Name"]).ToList()
                : [];
        }

        public void ReleaseProductionOrder(ProductionOrder order) =>
            mes.Run("ReleaseProductionOrders", () => new ReleaseProductionOrdersInput()
            {
                ProductionOrders = [order]
            }.ReleaseProductionOrdersSync(), order.Name);

        public void ChangeProductionOrderQuantity(ProductionOrder order, decimal quantity) =>
            mes.Run("ChangeProductionOrdersProductAndQuantity", () => new ChangeProductionOrdersProductAndQuantityInput()
            {
                ProductionOrders = [order],
                NewProductsAndQuantities = new() { [order.Id] = new ProductionOrderProductAndQuantityChange() { NewQuantity = quantity } },
                IgnoreLastServiceId = true
            }.ChangeProductionOrdersProductAndQuantitySync(), $"{order.Name} to {quantity:0.##}");

        public void CloseProductionOrder(ProductionOrder order) =>
            mes.Run("CloseProductionOrders", () => new CloseProductionOrdersInput()
            {
                ProductionOrders = [order],
                TerminateProductionOrders = false
            }.CloseProductionOrdersSync(), order.Name);

        public void Terminate<T>(T entity) where T : class =>
            mes.Run("TerminateObject", () => new TerminateObjectInput()
            {
                Object = entity
            }.TerminateObjectSync(), typeof(T).Name);

        public NgpDataSet ExecuteQuery(QueryObject query) =>
            mes.Run("ExecuteQuery", () => new ExecuteQueryInput()
            {
                QueryObject = query
            }.ExecuteQuerySync(), query.Name).NgpDataSet;

        public NgpDataSet ExecuteQueryByName(string queryName, QueryParameterCollection parameters) =>
            mes.Run("ExecuteQueryByName", () => new ExecuteQueryByNameInput()
            {
                Name = queryName,
                QueryParameters = parameters
            }.ExecuteQueryByNameSync(), queryName).NgpDataSet;

        public void UpdateStep(Step step) =>
            mes.Run("FullUpdateStep", () => new FullUpdateStepInput()
            {
                Step = step
            }.FullUpdateStepSync(), step.Name);

        public Config GetConfig(string path) =>
            mes.Run("GetConfigByPath", () => new GetConfigByPathInput()
            {
                Path = path
            }.GetConfigByPathSync(), path).Config;

        public Config UpdateConfig(Config config) =>
            mes.Run("UpdateConfig", () => new UpdateConfigInput()
            {
                Config = config
            }.UpdateConfigSync(), config.Path).Config;

        public DeeAction GetDeeAction(string name) =>
            mes.Run("GetActionByName", () => new GetActionByNameInput()
            {
                Name = name
            }.GetActionByNameSync(), name).Action;

        public DeeAction DisableDeeAction(DeeAction action) =>
            mes.Run("DisableAction", () => new DisableActionInput()
            {
                Action = action
            }.DisableActionSync(), action.Name).Action;

        public SmartTable GetSmartTable(string name, bool loadData) =>
            mes.Run("GetSmartTableByName", () => new GetSmartTableByNameInput()
            {
                SmartTableName = name,
                LoadData = loadData
            }.GetSmartTableByNameSync(), name).SmartTable;

        public void RemoveSmartTableRows(SmartTable smartTable, NgpDataSet rows) =>
            mes.Run("FullUpdateSmartTableData", () => new FullUpdateSmartTableDataInput()
            {
                SmartTable = smartTable,
                RowsToRemove = rows
            }.FullUpdateSmartTableDataSync(), smartTable.Name);
    }
}
