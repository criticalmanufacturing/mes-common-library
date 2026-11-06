using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.GenericTables;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Foundation.BusinessOrchestration.ChangeSetManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

public class TableHelper : ITableHelper
{
    private readonly IEntityFactory _entityFactory;
    private readonly IChangeSetOrchestration _changeSetOrchestration;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TableHelper(IEntityFactory entityFactory, IChangeSetOrchestration changeSetOrchestration)
    {
        _entityFactory = entityFactory;
        _changeSetOrchestration = changeSetOrchestration;
    }

    public DataSet GetDataFromGenericTable(string genericTableName, IFilterCollection filters)
    {
        DataSet data = null;
        IGenericTable table = CreateGenericTable();
        table.Load(genericTableName);
        table.LoadData(filters);
        if (table.HasData)
        {
            data = NgpDataSet.ToDataSet(table.Data);
        }
        return data;
    }

    public ILookupTable GetLookupTable(string tableName)
    {
        ILookupTable table = CreateLookupTableInstance();
        table.Load(tableName);
        return table;
    }

    public bool AddValueToLookupTable(string lookupTableName, string value, string description = "")
    {
        ILookupTable lookupTable = GetLookupTable(lookupTableName);
        if (lookupTable != null && lookupTable.Id > 0)
        {
            return lookupTable.AddValue(value, description);
        }
        return false;
    }


    public ILookupTableValueCollection GetLookupTableValues(string lookupTableName, params string[] valueNames)
    {
        ILookupTableValueCollection values = new LookupTableValueCollection();
        ILookupTable table = GetLookupTable(lookupTableName);
        if (table != null && table.Id > 0)
        {
            values = table.GetLookupTableValues(valueNames);
        }
        return values;
    }


    public List<string> GetLookupTableValues(string tableName)
    {
        ILookupTable table = CreateLookupTableInstance();
        table.Load(tableName);
        return table.Values.Select(value => value.Value).ToList();
    }

    public DataSet ResolveSmartTable(string tableName, Dictionary<string, string> nameAndValues, bool onlyFirstRow)
    {
        DataSet data = new();
        ISmartTable table = CreateSmartTable();
        table.Load(tableName);
        INgpDataRow values = CreateNgpDataRow();
        foreach (var item in nameAndValues)
        {
            values.Add(item.Key, item.Value);
        }
        INgpDataSet result = table.Resolve(values, onlyFirstRow);
        if (result != null)
        {
            data = NgpDataSet.ToDataSet(result);
        }
        return data;
    }

    public DataSet GetDataFromSmartTable(string smartTableName, IFilterCollection filters)
    {
        DataSet data = null;
        ISmartTable table = CreateSmartTable();
        table.Load(smartTableName);
        table.LoadData(filters);
        if (table.HasData)
        {
            data = NgpDataSet.ToDataSet(table.Data);
        }
        return data;
    }

    protected virtual IGenericTable CreateGenericTable() => new GenericTable();

    protected virtual ILookupTable CreateLookupTableInstance() => new LookupTable();

    protected virtual ISmartTable CreateSmartTable() => new SmartTable();

    protected virtual INgpDataRow CreateNgpDataRow() => new NgpDataRow();

    public ISmartTable AddOrRemoveDataOnSmartTable(string smartTableName, INgpDataSet smartTableValues, bool isDelete, string changeSetType = "General", bool isToMakeEffectiveOnApproval = true)
    {
        IChangeSet changeSet = null;
        ISmartTable smartTable = CreateSmartTable();
        smartTable.Load(smartTableName);
        if (smartTable.HasChangeControl)
        {
            INameGenerator nameGenerator = _entityFactory.Create<INameGenerator>();
            changeSet = _entityFactory.Create<IChangeSet>();
            changeSet.Name = nameGenerator.GenerateName(Guid.NewGuid().ToString());
            changeSet.Type = changeSetType;
            changeSet.MakeEffectiveOnApproval = isToMakeEffectiveOnApproval;
            changeSet.Create();
        }

        if (isDelete)
        {
            smartTable.RemoveRows(smartTableValues);
        }
        else
        {
            smartTable.InsertOrUpdateRows(smartTableValues);
        }

        if (smartTable.HasChangeControl)
        {
            _changeSetOrchestration.RequestChangeSetApproval(new RequestChangeSetApprovalInput { ChangeSet = changeSet });
        }
        return smartTable;
    }
}
