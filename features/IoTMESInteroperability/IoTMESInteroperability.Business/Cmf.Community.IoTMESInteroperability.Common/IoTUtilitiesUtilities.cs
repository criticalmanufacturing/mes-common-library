using Cmf.Common.CustomActionUtilities;
using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.QueryObject.Enums;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.Base;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Cmf.Community.IoTMESInteroperability.Utilities
{
    public class IoTUtilities
    {
        #region Generic

        /// <summary>
        /// Create ChangeSet
        /// </summary>
        private IChangeSet CreateChangeSet()
        {
            IEntityFactory entityFactory = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>();

            IChangeSet changeSet = entityFactory.Create<IChangeSet>();
            changeSet.Name = string.Format("ChangeSet_{0}", Guid.NewGuid().ToString().Replace("-", string.Empty));
            changeSet.MakeEffectiveOnApproval = true;
            changeSet.Description = "Generated automaticaly";
            changeSet.Type = "General";

            changeSet.Create();

            return changeSet;

        }

        /// <summary>
        /// Determines whether [is feature enabled] [the specified configuration path].
        /// </summary>
        /// <param name="configPath">The configuration path.</param>
        /// <param name="throwError">if set to <c>true</c> [throw error].</param>
        /// <returns>
        ///   <c>true</c> if [is feature enabled] [the specified configuration path]; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsFeatureEnabled(string configPath, bool throwError = true)
        {
            bool isFeatureEnabled = GeneralUtilities.GetConfigValue<bool>(configPath);
            if (!isFeatureEnabled && throwError)
            {
                GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.FeatureIsNotEnabled, configPath);
            }

            return isFeatureEnabled;
        }

        /// <summary>
        /// Resolves the RecipeContext Smart Table to return the IRecipe to use when searching for a RequiredPartNumber Parameter for a Material
        /// </summary>
        /// <param name="material">The material for which we want to resolve the Recipe</param>
        /// <returns></returns>
        public static IRecipe GetRecipeFromRecipeContext(IMaterial material)
        {
            // Check if there is a Recipe applicable to the target material
            IRecipe recipe = null;

            //  Prepare resolving row
            INgpDataRow valuesToResolve = ApplicationContext.CurrentServiceProvider.GetService<INgpDataRow>();

            #region Prepare values for resolve
            material.Load(2);

            valuesToResolve.Add("Material", material.Name);

            if (material.Step != null)
            {
                valuesToResolve.Add("Step", material.Step.Name);
                valuesToResolve.Add("Service", material.Step.ResolveServiceContexts(material).Name);
            }

            if (!String.IsNullOrWhiteSpace(material.LogicalFlowPath))
            {
                valuesToResolve.Add("LogicalFlowPath", material.LogicalFlowPath);
            }

            if (material.Product != null)
            {
                valuesToResolve.Add("Product", material.Product.Name);
            }

            if (material.Product?.ProductGroup != null)
            {
                valuesToResolve.Add("ProductGroup", material.Product.ProductGroup.Name);
            }

            if (material.Flow != null)
            {
                valuesToResolve.Add("Flow", material.Flow.Name);
            }

            if (!String.IsNullOrWhiteSpace(material.Type))
            {
                valuesToResolve.Add("MaterialType", material.Type);
            }

            // Resource, Resource Type, Model and Running Mode are to be ignored because when we resolve this, the Material will never be tracked in

            #endregion Prepare values for resolve

            SmartTable recipeContext = new SmartTable();
            recipeContext.Load(Cmf.Navigo.Common.Constants.RecipeContext);
            INgpDataSet ngpDataSet = recipeContext.Resolve(valuesToResolve, true);

            // check if a Recipe is defined to be applied
            DataSet dsResult = NgpDataSet.ToDataSet(ngpDataSet);
            if (dsResult != null && dsResult.Tables.Count > 0 && dsResult.Tables[0].Rows.Count > 0)
            {
                string recipeName = Convert.ToString(dsResult.Tables[0].Rows[0]["Recipe"]);
                if (!String.IsNullOrWhiteSpace(recipeName))
                {
                    recipe = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>().Create<IRecipe>();
                    recipe.Load(recipeName);
                }
            }

            return recipe;
        }


        /// <summary>
        /// Tries to retrieve a given item from the Input dictionary, based on the item name and passed type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Input"></param>
        /// <param name="inputEntryName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static T GetInputItem<T>(Dictionary<string, object> Input, string inputEntryName, T defaultValue = default(T))
        {
            // define return value
            T returnObject = defaultValue;

            // try to extract the element
            object oExtractedItem;
            if (Input.TryGetValue(inputEntryName, out oExtractedItem) && oExtractedItem is T)
            {
                returnObject = (T)oExtractedItem;
            }

            return returnObject;
        }

        /// <summary>
        /// Get the Materials that has been tracked out of the previous sub resource
        /// Only for SubResources
        /// <param name="resource"/>
        /// </summary>
        public static IMaterialCollection GetMaterialsFromPreviousSubResource(IResource resource)
        {
            IEntityFactory entityFactory = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>();

            Dictionary<string, string> previousSubResourceFlow = GetPreviousProcessSubResourceAndFlow(resource);
            string resourceName = previousSubResourceFlow.FirstOrDefault().Key;
            IResource previousSubResource = entityFactory.Create<IResource>();
            previousSubResource.Load(resourceName);

            string flowName = previousSubResourceFlow.FirstOrDefault().Value;
            IFlow flow = entityFactory.Create<IFlow>();
            flow.Load(flowName);

            if (previousSubResource == null)
            {
                throw new Exception("Resource " + resource.Name + " has no previous process SubResource");
            }

            IMaterialCollection materials = entityFactory.CreateCollection<IMaterialCollection>();

            IQueryObject query = new QueryObject();
            query.EntityTypeName = "Material";
            query.Name = "GetMaterialsFromPreviousSubResource";
            query.Query = new Query();
            query.Query.Distinct = false;
            query.Query.Filters = new FilterCollection()
            {
                new Filter()
                {
                     Name = "UniversalState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsNotEqualTo,
                     Value = (int) UniversalState.Terminated,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                     Name = "SystemState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsEqualTo,
                     Value = (int)MaterialSystemState.Queued,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                     Name = "LastProcessedResourceId",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsEqualTo,
                     Value = previousSubResource.Id,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType =FilterType.Normal,
                },
                new Filter()
                {
                     Name = "SystemState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_ParentMaterial_2",
                     Operator = FieldOperator.IsEqualTo,
                     Value = (int) MaterialSystemState.InProcess,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                    Name = "FlowId",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    Operator = FieldOperator.IsEqualTo,
                    Value = flow.DefinitionId,
                    LogicalOperator = LogicalOperator.AND,
                    FilterType = FilterType.Normal,
                },
            };
            query.Query.Fields = new FieldCollection()
            {
                new Field()
                {
                    Alias = "ModifiedOn",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "ModifiedOn",
                    Position = 0,
                    Sort = FieldSort.Ascending
                },
                new Field()
                {
                    Alias = "Id",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 1,
                    Sort = FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "Name",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 2,
                    Sort = FieldSort.NoSort
                }
            };
            query.Query.Relations = new RelationCollection()
            {
                new Relation()
                {
                    Alias = "",
                    IsRelation = false,
                    Name = "",
                    SourceEntity = "Material",
                    SourceEntityAlias = "Material_1",
                    SourceJoinType = JoinType.InnerJoin,
                    SourceProperty = "ParentMaterialId",
                    TargetEntity = "Material",
                    TargetEntityAlias = "Material_ParentMaterial_2",
                    TargetJoinType = JoinType.InnerJoin,
                    TargetProperty = "Id"
                }
            };

            DataSet dataSet = query.Execute(false, new QueryParameterCollection());

            if (dataSet.HasData())
            {
                foreach (DataRow row in dataSet.Tables[0].Rows)
                {
                    string materialName = row["Name"].ToString();
                    IMaterial material = entityFactory.Create<IMaterial>();
                    material.Name = materialName;

                    materials.Add(material);
                }
            }

            return materials;
        }

        /// <summary>
        /// Get the previous SubResourceId and Flow
        /// Only for SubResources
        /// <param name="resource"/>
        /// </summary>
        public static Dictionary<string, string> GetPreviousProcessSubResourceAndFlow(IResource resource)
        {
            IEntityFactory entityFactory = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>();

            // Get the Parent Resource
            IResource parentResource = GetParentResource(resource);

            // If there is a Parent Resource
            if (parentResource != null)
            {
                // Assuming that the resource only have one associated service
                parentResource.LoadRelations(Cmf.Navigo.Common.Constants.ResourceService);
                IService resourceService = parentResource.ResourceServices.FirstOrDefault().TargetEntity;
                resourceService.LoadServiceContexts(null, new OperationAttributeCollection());
                DataSet resourceServiceDataSet = NgpDataSet.ToDataSet(resourceService.ServiceContexts);

                // Get Step Name
                string stepName = resourceServiceDataSet.Tables[0].Rows[0]["Step"].ToString();
                IStep resourceStep = entityFactory.Create<IStep>();
                resourceStep.Load(stepName);

                // Resolve StepInLineFlow
                NgpDataRow ngpDataRow = new NgpDataRow();
                ngpDataRow.Add("Step", resourceStep);
                ngpDataRow.Add("Resource", parentResource);
                ngpDataRow.Add("ResourceType", parentResource.Type);
                ngpDataRow.Add("Model", parentResource.Model);
                DataSet stepLineFlowContextDataSet = NgpDataSet.ToDataSet(resourceStep.ResolveStepLineFlowContexts(ngpDataRow));

                IFlow flow = entityFactory.Create<IFlow>();
                if (stepLineFlowContextDataSet.Tables.Count > 0 && stepLineFlowContextDataSet.Tables[0].Rows.Count > 0)
                {
                    // Assuming that only exists one row
                    string inLineFlowName = stepLineFlowContextDataSet.Tables[0].Rows[0]["LineFlow"].ToString();
                    flow.Load(inLineFlowName);
                }
                else
                {
                    // Get materials that are able to be tracked in at the current resource
                    IMaterialCollection materialsToTrackIn = GetSubResourceMaterials(resource);

                    IMaterial firstMaterial = materialsToTrackIn.FirstOrDefault();
                    firstMaterial.Load();

                    // Assuming all materials have the same flow
                    // Resolve the flow using the first material
                    flow = firstMaterial.Flow;
                }
                flow.LoadRelations("FlowStep");

                IResource previousSubResource = null;

                if (flow.ObjectExists())
                {
                    // Foreach step of the flow (in order)
                    // Resources of the step of the flow which have the same parent resource as the parent resource of current resource
                    foreach (IFlowItem flowStep in flow.FlowItems.Where(fi => fi.Step != null).OrderBy(fi => fi.Position))
                    {
                        IResourceCollection serviceResources = entityFactory.CreateCollection<IResourceCollection>();

                        flowStep.Step.GetResourcesForStep().ToList().ForEach(resourceForStep =>
                        {
                            if (resourceForStep.ParentResources.ToList().
                                                            Any(resourceForStepParentResource =>
                                                                    resourceForStepParentResource.Id == parentResource.Id))
                            {
                                serviceResources.Add(resourceForStep);
                            }
                        });

                        // I am now at the current step, so the previous iteration was the previous sub resource
                        if (serviceResources.Any(serviceResource => serviceResource.Id == resource.Id))
                        {
                            Dictionary<string, string> resourceFlow = new Dictionary<string, string>();
                            resourceFlow.Add(previousSubResource.Name, flow.Name);
                            return resourceFlow;
                        }

                        // I assume that the first resource that services the last step is the previous sub resource
                        previousSubResource = serviceResources.FirstOrDefault();
                    }
                }

            }


            return null;
        }

        /// <summary>
        /// Get SubResource Materials
        /// Only for SubResources
        /// <param name="resource"/>
        /// </summary>
        private static IMaterialCollection GetSubResourceMaterials(IResource resource)
        {

            IEntityFactory entityFactory = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>();

            IMaterialCollection materials = entityFactory.CreateCollection<IMaterialCollection>();

            IQueryObject query = new QueryObject();
            query.EntityTypeName = "Material";
            query.Name = "GetSubResourceMaterialsForResourceView";
            query.Query = new Query();
            query.Query.Distinct = false;
            query.Query.Filters = new FilterCollection()
            {
                new Filter()
                {
                     Name = "UniversalState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsNotEqualTo,
                     Value = (int) UniversalState.Terminated,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                     Name = "SystemState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsEqualTo,
                     Value = (int)MaterialSystemState.Queued,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                     ObjectName = "Service",
                     ObjectAlias = "Material_RequiredService_2",
                     Value = null,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType =FilterType.AlwaysTrue,
                },
                new Filter()
                {
                     ObjectName = "Resource",
                     ObjectAlias = "Material_RequiredService_ResourceService_SourceEntity_4",
                     Value = null,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType =FilterType.AlwaysTrue,
                },
                new Filter()
                {
                     Name = "SourceEntityId",
                     ObjectName = "ResourceService",
                     ObjectAlias = "Material_RequiredService_ResourceService_3",
                     Operator = FieldOperator.IsEqualTo,
                     Value = resource.Id,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType =FilterType.Normal,
                },
                new Filter()
                {
                     Name = "SystemState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_ParentMaterial_2",
                     Operator = FieldOperator.IsEqualTo,
                     Value = (int) MaterialSystemState.InProcess,
                     LogicalOperator = LogicalOperator.Nothing,
                     FilterType = FilterType.Normal,
                }
            };
            query.Query.Fields = new FieldCollection()
            {
                new Field()
                {
                    Alias = "ModifiedOn",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "ModifiedOn",
                    Position = 0,
                    Sort = FieldSort.Ascending
                },
                new Field()
                {
                    Alias = "Id",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 1,
                    Sort = FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "Name",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 2,
                    Sort = FieldSort.NoSort
                }
            };
            query.Query.Relations = new RelationCollection()
            {
                new Relation()
                {
                    Alias = "",
                    IsRelation = false,
                    Name = "",
                    SourceEntity = "Material",
                    SourceEntityAlias = "Material_1",
                    SourceJoinType = JoinType.InnerJoin,
                    SourceProperty = "RequiredServiceId",
                    TargetEntity = "Service",
                    TargetEntityAlias = "Material_RequiredService_2",
                    TargetJoinType = JoinType.InnerJoin,
                    TargetProperty = "Id"
                },
                new Relation()
                {
                    Alias = "Material_RequiredService_ResourceService_3",
                    IsRelation = true,
                    Name = "ResourceService",
                    SourceEntity = "Resource",
                    SourceEntityAlias = "Material_RequiredService_ResourceService_SourceEntity_4",
                    SourceJoinType = JoinType.InnerJoin,
                    SourceProperty = "Id",
                    TargetEntity = "Service",
                    TargetEntityAlias = "Material_RequiredService_2",
                    TargetJoinType = JoinType.InnerJoin,
                    TargetProperty = "Id"
                },
                new Relation()
                {
                    Alias = "",
                    IsRelation = false,
                    Name = "",
                    SourceEntity = "Material",
                    SourceEntityAlias = "Material_1",
                    SourceJoinType = JoinType.InnerJoin,
                    SourceProperty = "ParentMaterialId",
                    TargetEntity = "Material",
                    TargetEntityAlias = "Material_ParentMaterial_2",
                    TargetJoinType = JoinType.InnerJoin,
                    TargetProperty = "Id"
                }
            };

            DataSet dataSet = query.Execute(false, new QueryParameterCollection());

            if (dataSet.HasData())
            {
                foreach (DataRow row in dataSet.Tables[0].Rows)
                {
                    string materialName = row["Name"].ToString();
                    IMaterial material = entityFactory.Create<IMaterial>();
                    material.Name = materialName;

                    materials.Add(material);
                }
            }

            return materials;
        }

        /// <summary>
        /// Get the previous SubResource by SubResource Order
        /// Only for SubResources
        /// <param name="resource"/>
        /// </summary>
        public static IResource GetPreviousProcessSubResourceByOrder(IResource resource)
        {
            IResource parentResource = GetParentResource(resource);
            IResource previousSubResource = null;

            parentResource?.LoadRelations(Navigo.Common.Constants.SubResource);

            if (parentResource != null
                && parentResource.SubResourcesCount > 0
                && parentResource.RelationCollection[Navigo.Common.Constants.SubResource].Any())
            {
                int resourceOrder = parentResource.RelationCollection[Navigo.Common.Constants.SubResource]
                    .Cast<ISubResource>()
                    .FirstOrDefault(x => x.TargetEntity.Id == resource.Id)
                    .Order;

                int previousResourceOrder = resourceOrder - 1;

                if (previousResourceOrder > 0)
                {
                    previousSubResource = parentResource.RelationCollection[Navigo.Common.Constants.SubResource]
                                            .Cast<ISubResource>()
                                            .LastOrDefault(x => x.Order <= previousResourceOrder &&
                                            x.TargetEntity.ProcessingType == ProcessingType.Process).TargetEntity;
                }
            }

            return previousSubResource;
        }

        /// <summary>
        /// Get All In Process Materials for inline resource
        /// <param name="resource"/>
        /// </summary>
        public static IMaterialCollection GetAllInlineMaterialsInProcess(IResource resource)
        {
            IEntityFactory entityFactory = ApplicationContext.CurrentServiceProvider.GetService<IEntityFactory>();

            IMaterialCollection materials = entityFactory.CreateCollection<IMaterialCollection>();

            IQueryObject query = new QueryObject();
            query.EntityTypeName = "Material";
            query.Name = "GetAllInlineMaterialsInProcess";
            query.Query = new Query();
            query.Query.Distinct = false;
            query.Query.Filters = new FilterCollection()
            {
                new Filter()
                {
                     Name = "UniversalState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsNotEqualTo,
                     Value = (int) UniversalState.Terminated,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                     Name = "SystemState",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsEqualTo,
                     Value = (int)MaterialSystemState.InProcess,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType = FilterType.Normal,
                },
                new Filter()
                {
                     Name = "LastProcessedResourceId",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsEqualTo,
                     Value = resource.Id,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType =FilterType.Normal,
                },
                new Filter()
                {
                     Name = "IsInLineStep",
                     ObjectName = "Material",
                     ObjectAlias = "Material_1",
                     Operator = FieldOperator.IsEqualTo,
                     Value = true,
                     LogicalOperator = LogicalOperator.AND,
                     FilterType =FilterType.Normal,
                }
            };
            query.Query.Fields = new FieldCollection()
            {
                new Field()
                {
                    Alias = "TrackInDate",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "TrackInDate",
                    Position = 0,
                    Sort = FieldSort.Ascending
                },
                new Field()
                {
                    Alias = "Id",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 1,
                    Sort = FieldSort.NoSort
                },
                new Field()
                {
                    Alias = "Name",
                    ObjectName = "Material",
                    ObjectAlias = "Material_1",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 2,
                    Sort = FieldSort.NoSort
                }
            };

            DataSet dataSet = query.Execute(false, new QueryParameterCollection());

            if (dataSet.HasData())
            {
                foreach (DataRow row in dataSet.Tables[0].Rows)
                {
                    string materialName = row["Name"].ToString();
                    IMaterial material = entityFactory.Create<IMaterial>();
                    material.Name = materialName;

                    materials.Add(material);
                }
            }

            return materials;
        }

        /// <summary>
        /// Get the Parent Resource
        /// <param name="resource"></param>
        /// <returns></returns>
        public static IResource GetParentResource(IResource resource)
        {
            IResource parentResource = null;

            if (resource.ParentResourcesCount == 1)
            {
                resource.GetAscendentResources(1);
                if (resource.RelationCollection[Navigo.Common.Constants.SubResource].Any())
                {
                    parentResource = resource.RelationCollection[Navigo.Common.Constants.SubResource]
                        .Select(x => x.SourceEntity)
                        .Cast<IResource>()
                        .FirstOrDefault();
                }
            }

            return parentResource;
        }

        #endregion Generic

        #region Queries

        /// <summary>
		/// Validates if folder exists
		/// <param name="folderName"/>
		/// </summary>
		public static bool FolderExists(string folderName)
        {
            if (folderName.IsNullOrEmpty())
            {
                throw new Exception("Folder Name is required");
            }

            IQueryObject query = new QueryObject();
            query.EntityTypeName = "Folder";
            query.Name = "GetFolderByName";
            query.Query = new Query();
            query.Query.Distinct = false;
            query.Query.Filters = new FilterCollection()
            {
                new Filter()
                {
                    Name = "Name",
                    ObjectName = "Folder",
                    ObjectAlias = "Folder_1",
                    Operator = FieldOperator.Contains,
                    Value = folderName,
                    LogicalOperator = LogicalOperator.Nothing,
                    FilterType = FilterType.Normal,
                }
            };
            query.Query.Fields = new FieldCollection()
            {
               new Field()
               {
                   Alias = "Id",
                   ObjectName = "Folder",
                   ObjectAlias = "Folder_1",
                   IsUserAttribute = false,
                   Name = "Id",
                   Position = 0,
                   Sort = FieldSort.NoSort
               },
               new Field()
               {
                   Alias = "Name",
                   ObjectName = "Folder",
                   ObjectAlias = "Folder_1",
                   IsUserAttribute = false,
                   Name = "Name",
                   Position = 1,
                   Sort = FieldSort.NoSort
               }
            };
            query.Query.Relations = new RelationCollection();

            DataSet dataSet = query.Execute(false, new QueryParameterCollection());

            return dataSet.HasData();
        }

        #endregion
    }
}