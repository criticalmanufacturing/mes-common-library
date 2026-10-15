
using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.QueryObject.Enums;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ProductHelper"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="genericServiceOrchestration">
///     The generic service orchestration.
/// </param>
public class ProductHelper(IEntityFactory entityFactory, IGenericServiceOrchestration genericServiceOrchestration) : IProductHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IGenericServiceOrchestration _genericServiceOrchestration = genericServiceOrchestration;

    /// <inheritdoc/>
    public IProductCharacteristicCollection GetProductCharacteristics(IProduct product)
    {
        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Name = "GetProductCharacteristics";
        query.Description = "Get Product Characteristics";
        query.EntityTypeName = "ProductCharacteristic";
        query.Query = new Query
        {
            Distinct = false,
            Filters = new FilterCollection
                {
                    new Filter
                    {
                        Name = "Id",
                        ObjectName = "Product",
                        ObjectAlias = "ProductCharacteristic_Product_2",
                        Operator = FieldOperator.IsEqualTo,
                        Value = product.Id,
                        LogicalOperator = LogicalOperator.Nothing,
                        FilterType = FilterType.Normal,
                    }
                },
            Fields = new FieldCollection {
                    new Field
                    {
                        Alias = "Id",
                        ObjectName = "ProductCharacteristic",
                        ObjectAlias = "ProductCharacteristic_1",
                        IsUserAttribute = false,
                        Name = "Id",
                        Position = 0,
                        Sort = FieldSort.NoSort
                    }
                },
            Relations = new RelationCollection
                {
                    new Relation
                    {
                        Alias = string.Empty,
                        IsRelation = false,
                        Name = string.Empty,
                        SourceEntity = "ProductCharacteristic",
                        SourceEntityAlias = "ProductCharacteristic_1",
                        SourceJoinType = JoinType.InnerJoin,
                        SourceProperty = "ProductId",
                        TargetEntity = "Product",
                        TargetEntityAlias = "ProductCharacteristic_Product_2",
                        TargetJoinType = JoinType.InnerJoin,
                        TargetProperty = "DefinitionId"
                    }
                }
        };
        DataSet outputDataSet = query.Execute(false, new QueryParameterCollection());

        IProductCharacteristicCollection productCharacteristics = _entityFactory.CreateCollection<IProductCharacteristicCollection>();

        if (outputDataSet.HasData())
        {
            GetObjectsByFilterInput getObjectsByFilterInput = new GetObjectsByFilterInput
            {
                Type = _entityFactory.Create<IProductCharacteristic>(),
                Filter = new FilterCollection()
            };

            foreach (DataRow outputRow in outputDataSet.Tables[0].Rows)
            {
                getObjectsByFilterInput.Filter.Add(
                    new Filter
                    {
                        Name = "Id",
                        Operator = FieldOperator.IsEqualTo,
                        Value = outputRow["Id"],
                        LogicalOperator = LogicalOperator.OR
                    }
                );
            }

            GetObjectsByFilterOutput getObjectsByFilterOutput = _genericServiceOrchestration.GetObjectsByFilter(getObjectsByFilterInput);

            if (getObjectsByFilterOutput?.Instance != null && getObjectsByFilterOutput?.Instance?.Count > 0)
            {
                productCharacteristics.AddRange(getObjectsByFilterOutput.Instance.Cast<IProductCharacteristic>());
            }
        }

        return productCharacteristics;
    }

    /// <inheritdoc/>
    public DataSet GetBomProducts(Dictionary<string, string> productTofind, string bomName, FilterCollection filterCollection)
    {
        List<IBOMProduct> bomProducts = new List<IBOMProduct>();
        IEntityTypeProperty materiaIdProperty = _entityFactory.Create<IEntityTypeProperty>();
        materiaIdProperty.Load("Id", Navigo.Common.Constants.BOM);
        long materialEntityPropertyId = materiaIdProperty.Id;

        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Description = string.Empty;
        query.EntityTypeName = "BOMProduct";
        query.Name = "CustomGetBomProducts";
        query.Query = new Query()
        {
            Distinct = true,
            Filters = filterCollection,
            Fields = new FieldCollection
            {
            new Field
            {
                Alias = "Id",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = false,
                Name = "Id",
                Position = 0,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "Name",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = false,
                Name = "Name",
                Position = 1,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "AssembleMode",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = false,
                Name = "Location",
                Position = 4,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort,
                DisplayStyleName = "AssembleMode"
            },
            new Field
            {
                Alias = "IsReference",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = false,
                Name = "IsReference",
                Position = 7,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "Quantity",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = false,
                Name = "Quantity",
                Position = 8,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "Units",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = false,
                Name = "Units",
                Position = 9,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "PSA",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = true,
                Name = "PSA",
                Position = 10,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "StorageBin",
                ObjectName = "BOMProduct",
                ObjectAlias = "BOMProduct_1",
                IsUserAttribute = true,
                Name = "StorageBin",
                Position = 11,
                Sort = Cmf.Foundation.Common.FieldSort.Ascending
            },
            new Field
            {
                Alias = "Description",
                ObjectName = "Product",
                ObjectAlias = "BOMProduct_TargetEntity_2",
                IsUserAttribute = false,
                Name = "Description",
                Position = 3,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort,
                DisplayStyleName = "Description"
            },
            new Field
            {
                Alias = "__cmf_html_Product_Id",
                ObjectName = "Product",
                ObjectAlias = "BOMProduct_TargetEntity_2",
                IsUserAttribute = false,
                Name = "Id",
                Position = 2,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort,
                DisplayStyleName = "__cmf_html_Product_Id"
            },
            new Field
            {
                Alias = "__cmf_html_Product_Name",
                ObjectName = "Product",
                ObjectAlias = "BOMProduct_TargetEntity_2",
                IsUserAttribute = false,
                Name = "Name",
                Position = 13,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "__cmf_html_Product_Revision",
                ObjectName = "Product",
                ObjectAlias = "BOMProduct_TargetEntity_2",
                IsUserAttribute = false,
                Name = "Revision",
                Position = 14,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "__cmf_html_SourceStep_Id",
                ObjectName = "Step",
                ObjectAlias = "BOMProduct_Step_2",
                IsUserAttribute = false,
                Name = "Id",
                Position = 5,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort,
                DisplayStyleName = "__cmf_html_SourceStep_Id"
            },
            new Field
            {
                Alias = "__cmf_html_SourceStep_Name",
                ObjectName = "Step",
                ObjectAlias = "BOMProduct_Step_2",
                IsUserAttribute = false,
                Name = "Name",
                Position = 15,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "__cmf_html_AssemblyStep_Id",
                ObjectName = "Step",
                ObjectAlias = "BOMProduct_AssemblyStep_2",
                IsUserAttribute = false,
                Name = "Id",
                Position = 6,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            },
            new Field
            {
                Alias = "__cmf_html_AssemblyStep_Name",
                ObjectName = "Step",
                ObjectAlias = "BOMProduct_AssemblyStep_2",
                IsUserAttribute = false,
                Name = "Name",
                Position = 16,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            }

        },
            Relations = new RelationCollection
        {
            new Relation
            {
                Alias = string.Empty,
                IsRelation = false,
                Name = string.Empty,
                SourceEntity = "BOMProduct",
                SourceEntityAlias = "BOMProduct_1",
                SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                SourceProperty = "SourceEntityId",
                TargetEntity = "BOM",
                TargetEntityAlias = "BOMProduct_SourceEntity_2",
                TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                TargetProperty = "Id"
            },
            new Relation
            {
                Alias = string.Empty,
                IsRelation = false,
                Name = string.Empty,
                SourceEntity = "BOMProduct",
                SourceEntityAlias = "BOMProduct_1",
                SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                SourceProperty = "TargetEntityId",
                TargetEntity = "Product",
                TargetEntityAlias = "BOMProduct_TargetEntity_2",
                TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                TargetProperty = "DefinitionId"
            },
            new Relation
            {
                Alias = string.Empty,
                IsRelation = false,
                Name = string.Empty,
                SourceEntity = "BOMProduct",
                SourceEntityAlias = "BOMProduct_1",
                SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.LeftJoin,
                SourceProperty = "StepId",
                TargetEntity = "Step",
                TargetEntityAlias = "BOMProduct_Step_2",
                TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                TargetProperty = "Id"
            },
            new Relation
            {
                Alias = string.Empty,
                IsRelation = false,
                Name = string.Empty,
                SourceEntity = "BOMProduct",
                SourceEntityAlias = "BOMProduct_1",
                SourceJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                SourceProperty = "AssemblyStepId",
                TargetEntity = "Step",
                TargetEntityAlias = "BOMProduct_AssemblyStep_2",
                TargetJoinType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.JoinType.InnerJoin,
                TargetProperty = "Id"
            }

            },
            FieldsDisplayStyles = new FieldDisplayStyleCollection {
            new FieldDisplayStyle
            {
                Name = "AssembleMode",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular",
                    TextAlignment = 0
                }
            },
            new FieldDisplayStyle
            {
                Name = "__conditional_AssembleMode",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular"
                }
            },
            new FieldDisplayStyle
            {
                Name = "Description",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular",
                    TextAlignment = 0
                }
            },
            new FieldDisplayStyle
            {
                Name = "__conditional_Description",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular"
                }
            },
            new FieldDisplayStyle
            {
                Name = "__cmf_html_Product_Id",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular",
                    TextAlignment = 0
                }
            },
            new FieldDisplayStyle
            {
                Name = "__conditional___cmf_html_Product_Id",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular"
                }
            },
            new FieldDisplayStyle
            {
                Name = "__cmf_html_SourceStep_Id",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular",
                    TextAlignment = 0
                }
            },
            new FieldDisplayStyle
            {
                Name = "__conditional___cmf_html_SourceStep_Id",
                IsConditional = false,
                FontAndText = new FieldDisplayStyleFontAndText {
                    FontFamily = "open-sans-regular"
                }
            }
        },
            QueryParameters = new QueryParameterCollection {
                new QueryParameter
                {
                    Name = "BOM_Name",
                    Value = string.Empty,
                    EntityTypePropertyId = materialEntityPropertyId,
                    IsOptional = false,
                    FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal
                }
        }
        };

        DataSet result = query.Execute(false, query.Query.QueryParameters);

        if (result.HasData())
        {
            result.Tables[0].Columns.Add("SFG", typeof(object));
            result.Tables[0].Columns.Add("Product", typeof(object));
            result.Tables[0].Columns.Add("SourceStep", typeof(object));
            result.Tables[0].Columns.Add("AssemblyStep", typeof(object));


            foreach (DataRow row in result.Tables[0].Rows)
            {
                //get the assembly mode step field
                string assemblyModeName = row.Field<string>("__cmf_html_AssemblyStep_Name");

                if (assemblyModeName != null)
                {
                    IStep assemblyModeStep = _entityFactory.Create<IStep>();
                    assemblyModeStep.Name = assemblyModeName;
                    assemblyModeStep.Load();
                    row["AssemblyStep"] = assemblyModeStep;
                }

                //get the source step field
                string sourceStepName = row.Field<string>("__cmf_html_SourceStep_Name");

                if (sourceStepName != null)
                {
                    IStep sourceStep = _entityFactory.Create<IStep>();
                    sourceStep.Name = sourceStepName;
                    sourceStep.Load();
                    row["SourceStep"] = sourceStep;
                }

                //get product name and load it
                string productName = row.Field<string>("__cmf_html_Product_Name");
                IProduct product = _entityFactory.Create<IProduct>();
                if (productName != null)
                {
                    product.Name = productName;
                    product.Load();
                }

                row["Product"] = product;

                if (productTofind.ContainsKey(product.Name))
                {
                    string poName = productTofind.GetValueOrDefault(product.Name);
                    IMaterial material = _entityFactory.Create<IMaterial>();
                    material.Name = poName;
                    material.Load();
                    row["SFG"] = material;
                }
            }
        }

        return result;
    }
}