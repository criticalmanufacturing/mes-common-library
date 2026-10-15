using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Data Collection Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="DataCollectionHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
public class DataCollectionHelper(IEntityFactory entityFactory, ILocalizationOrchestration localizationOrchestration) : IDataCollectionHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;

    /// <inheritdoc/>
    public IDataCollectionInstanceCollection GetDataCollectionInstancesForMaterials(List<long> materialIds)
    {

        IEntityTypeProperty materiaIdProperty = _entityFactory.Create<IEntityTypeProperty>();
        materiaIdProperty.Load("Id", Navigo.Common.Constants.Material);
        long materialEntityPropertyId = materiaIdProperty.Id;

        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Description = string.Empty;
        query.EntityTypeName = "DataCollectionInstance";
        query.Name = "GetDCInstancesForMaterials";
        query.Query = new Query
            {
                Distinct = false,
                Filters = new FilterCollection
                {
                    new Filter
                    {
                        Name = "Id",
                        ObjectName = "Material",
                        ObjectAlias = "DataCollectionInstance_Material_2",
                        Operator = Cmf.Foundation.Common.FieldOperator.In,
                        Value = "@DataCollectionInstance_Material_Id",
                        LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                        FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                    }
                },
                Fields = new FieldCollection
                {
                    new Field
                    {
                        Alias = "Id",
                        ObjectName = "DataCollectionInstance",
                        ObjectAlias = "DataCollectionInstance_1",
                        IsUserAttribute = false,
                        Name = "Id",
                        Position = 0,
                        Sort = Cmf.Foundation.Common.FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "Name",
                        ObjectName = "DataCollectionInstance",
                        ObjectAlias = "DataCollectionInstance_1",
                        IsUserAttribute = false,
                        Name = "Name",
                        Position = 1,
                        Sort = Cmf.Foundation.Common.FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "CreatedOn",
                        ObjectName = "DataCollectionInstance",
                        ObjectAlias = "DataCollectionInstance_1",
                        IsUserAttribute = false,
                        Name = "CreatedOn",
                        Position = 2,
                        Sort = Cmf.Foundation.Common.FieldSort.NoSort
                    },
                    new Field
                    {
                        Alias = "MaterialId",
                        ObjectName = "Material",
                        ObjectAlias = "DataCollectionInstance_Material_2",
                        IsUserAttribute = false,
                        Name = "Id",
                        Position = 3,
                        Sort = Cmf.Foundation.Common.FieldSort.NoSort
                    }
                },
                QueryParameters = new QueryParameterCollection
                {
                    new QueryParameter
                    {
                        Name = "DataCollectionInstance_Material_Id",
                        Value = materialIds,
                        EntityTypePropertyId = materialEntityPropertyId,
                        IsOptional = false,
                        FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal
                    }
                },
                Relations = new RelationCollection
                {
                    new Relation
                    {
                        Alias = string.Empty,
                        IsRelation = false,
                        Name = string.Empty,
                        SourceEntity = "DataCollectionInstance",
                        SourceEntityAlias = "DataCollectionInstance_1",
                        SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                        SourceProperty = "MaterialId",
                        TargetEntity = "Material",
                        TargetEntityAlias = "DataCollectionInstance_Material_2",
                        TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                        TargetProperty = "Id"
                    }
                }
        };

        IDataCollectionInstanceCollection dcInstances = _entityFactory.CreateCollection<IDataCollectionInstanceCollection>();
        DataSet dataSet = query.Execute(false, query.Query.QueryParameters);
        if (!dataSet.HasData())
        {
            return dcInstances;
        }

        Dictionary<long, string> materialDCLookup = dataSet.Tables[0].AsEnumerable()
                                                                     .Select(row => new
                                                                     {
                                                                         MaterialId = row.Field<long>("MaterialId"),
                                                                         CreatedOn = row.Field<DateTime>("CreatedOn"),
                                                                         Name = row.Field<string>("Name")
                                                                     })
                                                                    .OrderByDescending(x => x.CreatedOn)
                                                                    .DistinctBy(x => x.MaterialId)
                                                                    .ToDictionary(x => x.MaterialId, x => x.Name);

        foreach(long matId in materialIds)
        {
            if (materialDCLookup.TryGetValue(matId, out string dcInstanceName))
            {
                IDataCollectionInstance dcInstanceMatch = _entityFactory.Create<IDataCollectionInstance>();
                dcInstanceMatch.Name = dcInstanceName;
                dcInstances.Add(dcInstanceMatch);
            }
        }

        if (dcInstances.Count < 1)
        {
            return dcInstances;
        }

        dcInstances.Load();

        return dcInstances;
    }
}