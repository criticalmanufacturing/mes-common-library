using Cmf.Common.CustomActionUtilities;
using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using Cmf.Foundation.Configuration;

namespace Cmf.Community.IoTMESInteroperability.Actions.ConnectIoT
{
    public class IoTCreateNewRecipe : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            /// DEE triggered by IoT to create a new recipe
            /// Action Groups:NA
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion

            return true;

            //---End DEE Condition Code---
        }
        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            //System
            UseReference("", "System.Data");

            //Foundation
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Foundation.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects");

            //Navigo
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Foundation.BusinessOrchestration.TableManagement");

            //Custom
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities");
            UseReference("Newtonsoft.Json.dll", "Newtonsoft.Json");

            #region Service Provider

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();

            IGenericServiceOrchestration genericServiceOrchestration = serviceProvider.GetService<IGenericServiceOrchestration>();
            IEntityHelper entityHelper = serviceProvider.GetService<IEntityHelper>();

            #endregion

            IResource resource = entityFactory.Create<IResource>();
            resource.Name = Input["ResourceName"].ToString();

            string resourceRecipeName = Input["ResourceRecipeName"].ToString();
            IRecipe resourceCurrentRecipe = null;

            if (resource.ObjectExists())
            {
                resource.Load(1);
                resourceCurrentRecipe = resource.CurrentRecipe;
            }

            IRecipe recipe = entityFactory.Create<IRecipe>();
            recipe.Name = Input["RecipeName"].ToString();

            bool isToUpdateRecipe = false;
            Input["Result"] = "Failed";

            #region Validate and Set Recipe

            if (Input.ContainsKey("RecipeRevision")
                && !Input["RecipeRevision"].ToString().IsNullOrWhiteSpace()
                && recipe.ObjectExists(recipe.Name, Input["RecipeRevision"].ToString())) // CONFIGURE RECIPE WITH REVISION
            {
                recipe.Revision = Input["RecipeRevision"].ToString();
                recipe.Load();

                bool isValidResourceRecipeName = (!resourceRecipeName.IsNullOrWhiteSpace() && resourceRecipeName.Equals(recipe.ResourceRecipeName));
                bool isValidRevision = resourceCurrentRecipe?.Revision.IgnoreCaseEquals(recipe.Revision) ?? false;

                // RECIPE WITH CORRECT REVISION ALREADY IN RESOURCE
                if (isValidResourceRecipeName
                    && isValidRevision
                    && resourceCurrentRecipe.Name.IgnoreCaseEquals(recipe.Name))
                {
                    Input["Result"] = "Success";
                    return Input;
                }

                // SET RECIPE WITH REVISION ON THE RESOURCE
                else if (isValidResourceRecipeName
                    && (resourceCurrentRecipe == null || !resourceCurrentRecipe.Name.IgnoreCaseEquals(recipe.Name) || !isValidRevision))
                {
                    resource.SetResourceRecipe(recipe, true);
                    Input["Result"] = "Success";
                    return Input;
                }
                else
                {
                    isToUpdateRecipe = true;
                }

            }
            else if ((!Input.ContainsKey("RecipeRevision") || Input["RecipeRevision"].ToString().IsNullOrWhiteSpace())
                && recipe.ObjectExists())  // CONFIGURE RECIPE WITHOUT REVISION
            {
                recipe.Load();

                bool isValidResourceRecipeName = (!resourceRecipeName.IsNullOrWhiteSpace() && resourceRecipeName.Equals(recipe.ResourceRecipeName));

                // RECIPE ALREADY IN RESOURCE
                if (isValidResourceRecipeName
                    && resource.CurrentRecipe != null
                    && resource.CurrentRecipe.Name.Equals(recipe.Name))
                {
                    Input["Result"] = "Success";
                    return Input;
                }

                // SET RECIPE ON THE RESOURCE
                else if (isValidResourceRecipeName
                    && (resource.CurrentRecipe == null || !resource.CurrentRecipe.Name.Equals(recipe.Name)))
                {
                    resource.SetResourceRecipe(recipe, true);
                    Input["Result"] = "Success";
                    return Input;
                }
                else
                {
                    isToUpdateRecipe = true;
                }

            }

            #endregion

            #region Update or Create New Recipe

            if (isToUpdateRecipe)
            {
                // Create Change Set for new Recipe Version
                IChangeSet changeSet = entityHelper.CreateChangeSet();

                recipe.ResourceRecipeName = resourceRecipeName;

                if (Input.ContainsKey("RecipeRevision")
                    && !Input["RecipeRevision"].ToString().IsNullOrWhiteSpace())
                {
                    recipe.Revision = Input["RecipeRevision"].ToString();
                }

                if (Input.ContainsKey("RecipeBody") && !Input["RecipeBody"].ToString().IsNullOrEmpty())
                {
                    recipe.BodySource = RecipeBodySource.HumanEdited;
                    recipe.BodyFormat = RecipeBodyFormat.Text;

                    // Converting input info to byte[] in base 64 and add it to Body of the recipe Body
                    var bytesUTF8 = System.Text.Encoding.UTF8.GetBytes(Input["RecipeBody"].ToJsonString());
                    var stringBase64 = System.Convert.ToBase64String(bytesUTF8);

                    IRecipeBody recipeBody = entityFactory.Create<IRecipeBody>();
                    recipe.Body = recipeBody;
                    recipe.Body.Body = Convert.FromBase64String(stringBase64);
                }

                // Change set and Create Version
                recipe.ChangeSet = changeSet;

                if (!recipe.Revision.IsNullOrEmpty())
                {
                    recipe.CreateRevision();
                }
                else
                {
                    recipe.CreateVersion();
                }

                // Set version as effective
                changeSet.RequestApproval();
                recipe.Load();
                recipe.MakeEffective();

                // Checks if the Current Recipe on the Resource Details is Equals to the Recipe created and if not sets it
                if (resource.CurrentRecipe == null || !resource.CurrentRecipe.Name.Equals(recipe.Name))
                {
                    resource.SetResourceRecipe(recipe, true);
                }

                Input["Result"] = "Success";
            }
            else
            {
                // CREATE NEW RECIPE
                // Create Change Set for new Recipe Version
                IChangeSet changeSet = entityHelper.CreateChangeSet();

                // Get Config for Recipe Type and verify if it exists on Lookup Table
                string recipeType = Config.GetConfig("/IoT/IoTUtilities/DefaultRecipeType").GetConfigValue<string>();

                LookupTable lookupTable = new LookupTable();
                lookupTable.Load("RecipeType");
                bool existType = lookupTable.Values.Any(E => E.Value == recipeType);

                if (!existType)
                {
                    GeneralUtilities.ThrowLocalizedException("The type used to create the recipe {0} doesn't exist.", recipe.Name);
                }

                recipe.Type = recipeType;
                recipe.ResourceRecipeName = resourceRecipeName;

                if (Input.ContainsKey("RecipeRevision")
                    && !Input["RecipeRevision"].ToString().IsNullOrWhiteSpace())
                {
                    recipe.Revision = Input["RecipeRevision"].ToString();
                }

                if (Input.ContainsKey("RecipeBody") && !Input["RecipeBody"].ToString().IsNullOrEmpty())
                {
                    recipe.BodySource = RecipeBodySource.HumanEdited;
                    recipe.BodyFormat = RecipeBodyFormat.Text;

                    // Converting input info to byte[] in base 64 and add it to Body of the recipe Body
                    var bytesUTF8 = System.Text.Encoding.UTF8.GetBytes(Input["RecipeBody"].ToJsonString());
                    var stringBase64 = System.Convert.ToBase64String(bytesUTF8);

                    IRecipeBody recipeBody = entityFactory.Create<IRecipeBody>();
                    recipe.Body = recipeBody;
                    recipe.Body.Body = Convert.FromBase64String(stringBase64);
                }

                // Change set and Create
                recipe.ChangeSet = changeSet;

                if (recipe.ObjectExists() && !recipe.Revision.IsNullOrEmpty())
                {
                    recipe.CreateRevision();
                }
                else
                {
                    CreateObjectInput createObjectInput = new CreateObjectInput()
                    {
                        Object = recipe
                    };

                    genericServiceOrchestration.CreateObject(createObjectInput);
                }

                // Set version as effective
                changeSet.RequestApproval();
                recipe.Load();
                recipe.MakeEffective();

                // Set Recipe on the Resource
                resource.SetResourceRecipe(recipe, true);

                Input["Result"] = "Success";
            }

            #endregion

            return Input;

            //---End DEE Code---

        }
    }
}
