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
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="BOMHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="genericServiceOrchestration">
///     The generic service orchestration.
/// </param>
public class BOMHelper(IEntityFactory entityFactory) : IBOMHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;

    /// <inheritdoc/>
    public IBOMCollection GetBOMAssociatedWithProducts(IList<string> productNames)
    {
        IBOMCollection bomCollection = _entityFactory.CreateCollection<IBOMCollection>();

        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Description = "";
        query.EntityTypeName = "BOMProduct";
        query.Name = "GetBOMAssociatedWithProducts";
        query.Query = new Query();
        query.Query.Distinct = false;
        query.Query.Filters = new FilterCollection() {
                new Filter()
                {
                    Name = "Name",
                    ObjectName = "Product",
                    ObjectAlias = "BOMProduct_TargetEntity_2",
                    Operator = Cmf.Foundation.Common.FieldOperator.In,
                    Value = productNames,
                    LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                },
                new Filter()
                {
                    Name = "UniversalState",
                    ObjectName = "BOM",
                    ObjectAlias = "BOMProduct_SourceEntity_2",
                    Operator = Cmf.Foundation.Common.FieldOperator.IsNotEqualTo,
                    Value = Cmf.Foundation.Common.Base.UniversalState.Terminated,
                    LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                },
                new Filter()
                {
                    Name = "IsDefaultRevision",
                    ObjectName = "BOM",
                    ObjectAlias = "BOMProduct_SourceEntity_2",
                    Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                    Value = true,
                    LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                }
            };
        query.Query.Fields = new FieldCollection() {
                new Field()
                {
                    Alias = "Id",
                    ObjectName = "BOMProduct",
                    ObjectAlias = "BOMProduct_1",
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 0,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "Name",
                    ObjectName = "BOMProduct",
                    ObjectAlias = "BOMProduct_1",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 1,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "SourceEntityName",
                    ObjectName = "BOM",
                    ObjectAlias = "BOMProduct_SourceEntity_2",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 3,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "__cmf_html_SourceEntity_Id",
                    ObjectName = "BOM",
                    ObjectAlias = "BOMProduct_SourceEntity_2",
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 2,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "__cmf_html_SourceEntity_Name",
                    ObjectName = "BOM",
                    ObjectAlias = "BOMProduct_SourceEntity_2",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 5,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "__cmf_html_SourceEntity_Revision",
                    ObjectName = "BOM",
                    ObjectAlias = "BOMProduct_SourceEntity_2",
                    IsUserAttribute = false,
                    Name = "Revision",
                    Position = 6,
                    Sort = Cmf.Foundation.Common.FieldSort.NoSort
                }
            };
        query.Query.Relations = new RelationCollection() {
                new Relation()
                {
                    Alias = "",
                    IsRelation = false,
                    Name = "",
                    SourceEntity = "BOMProduct",
                    SourceEntityAlias = "BOMProduct_1",
                    SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                    SourceProperty = "TargetEntityId",
                    TargetEntity = "Product",
                    TargetEntityAlias = "BOMProduct_TargetEntity_2",
                    TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                    TargetProperty = "DefinitionId"
                },
                new Relation()
                {
                    Alias = "",
                    IsRelation = false,
                    Name = "",
                    SourceEntity = "BOMProduct",
                    SourceEntityAlias = "BOMProduct_1",
                    SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                    SourceProperty = "SourceEntityId",
                    TargetEntity = "BOM",
                    TargetEntityAlias = "BOMProduct_SourceEntity_2",
                    TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                    TargetProperty = "Id"
                }
            };


        DataSet ds = query.Execute(false, query.Query.QueryParameters);

        if (ds.HasData())
        {
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                IBOM bOM = _entityFactory.Create<IBOM>();
                bOM.Name = row["SourceEntityName"] as string;

                if (bomCollection.FirstOrDefault(b => b.Name == bOM.Name) == null)
                {
                    bomCollection.Add(bOM);
                }
            }
        }

        if (bomCollection.Count > 0)
        {
            bomCollection.Load();
        }

        return bomCollection;

    }

    /// <inheritdoc/>
    public IBOM GetBOMByHash(string hash)
    {
        IBOM bom = _entityFactory.Create<IBOM>();
        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Description = string.Empty;
        query.EntityTypeName = "BOM";
        query.Name = "GetBomByHash";
        query.Query = new Query();
        query.Query.Distinct = false;
        query.Query.Filters = new FilterCollection {
            new Filter
            {
                Name = "UniversalState",
                ObjectName = "BOM",
                ObjectAlias = "BOM_1",
                Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                Value = Cmf.Foundation.Common.Base.UniversalState.Effective,
                LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
            },
            new Filter
            {
                Name = "Hash",
                ObjectName = "BOM",
                ObjectAlias = "BOM_1",
                Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                Value = "1",
                LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
            }
        };
        query.Query.Fields = new FieldCollection {
            new Field
            {
                Alias = "Id",
                ObjectName = "BOM",
                ObjectAlias = "BOM_1",
                IsUserAttribute = false,
                Name = "Id",
                Position = 0,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "DefinitionId",
                ObjectName = "BOM",
                ObjectAlias = "BOM_1",
                IsUserAttribute = false,
                Name = "DefinitionId",
                Position = 1,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "Revision",
                ObjectName = "BOM",
                ObjectAlias = "BOM_1",
                IsUserAttribute = false,
                Name = "Revision",
                Position = 2,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "Name",
                ObjectName = "BOM",
                ObjectAlias = "BOM_1",
                IsUserAttribute = false,
                Name = "Name",
                Position = 3,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            }
        };
        query.Query.Relations = new RelationCollection();
        query.Query.Top = 1;
        DataSet result = query.Execute(false, new QueryParameterCollection());
        if (result.HasData())
        {
            bom.Name = result.Tables[0].Rows[0]["Name"] as string;
            bom.Load();
        }
        return bom;
    }
}