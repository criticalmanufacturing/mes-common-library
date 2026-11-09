using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Community.IoTMESInteroperability;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Community.IoTMESInteroperability.Common.Enums;
using Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects;
using Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects;
using Cmf.Community.IoTMESInteroperability.Utilities;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cmf.Community.IoTMESInteroperability.Orchestration
{
    public class CommunityOrchestration : ICommunityOrchestration
    {
        private const string OBJECT_TYPE_NAME = "Cmf.Community.IoTMESInteroperability.Orchestration.CommunityManagementOrchestration";

        // Entity Factory
        private readonly IEntityFactory _entityFactory;

        // Utilities
        private readonly IUtilities _utilities;
        private readonly ILocalizationService _localizationService;

        // Orchestrations
        private readonly IGenericServiceOrchestration _genericServiceOrchestration;
        private readonly IMaterialOrchestration _materialOrchestration;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommunityOrchestration"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public CommunityOrchestration(
            IEntityFactory entityFactory,
            IUtilities utilities,
            ILocalizationService localizationService,
            IGenericServiceOrchestration genericServiceOrchestration,
            IMaterialOrchestration materialOrchestration
            ) : base()
        {
            // Entity Factory
            _entityFactory = entityFactory;

            // Utilities
            _utilities = utilities;
            _localizationService = localizationService;

            // Orchestrations
            _genericServiceOrchestration = genericServiceOrchestration;
            _materialOrchestration = materialOrchestration;

        }

        #region IoTMaterialOperation
        /// <summary
        /// IoTMaterialOperation
        /// </summary
        /// <param name="input">IoTMaterialOperation Input Object</param>
        /// <returns>IoTMaterialOperation Output Object</returns>
        /// <exception cref="CmfBaseException">If any unexpected error occurs.</exception>
        public IoTMaterialOperationOutput IoTMaterialOperation(IoTMaterialOperationInput input)
        {
            _utilities.StartMethod(
                OBJECT_TYPE_NAME,
                "IoTMaterialOperation",
                new KeyValuePair<string, object>("IoTMaterialOperationInput", input));

            IoTMaterialOperationOutput output = new IoTMaterialOperationOutput();

            try
            {
                #region Validate Input

                // _utilities.ValidateNullInput(input);

                // Service Comments
                ApplicationContext.CallContext.AddServiceComments(input.ServiceComments);

                #endregion

                string operation = input.Operation;
                Resource resource = input.Resource;
                Material material = input.Material;
                MaterialCollection materials = input.Materials;

                // Do the actual loads on Service only (not on DEE)
                if (materials != null)
                {
                    materials.Load();
                }
                if (material != null)
                {
                    material.Load();
                }


                #region Implementation (from DEE )

                IoTMaterialOperation materialOperation = Enum.Parse<IoTMaterialOperation>(operation);

                string materialNameOutput = "";

                switch (materialOperation)
                {
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.TrackIn:

                        if (material.SystemState.Equals(MaterialSystemState.Dispatched)
                            || (material.IsInLineStep ?? false && material.SystemState.Equals(MaterialSystemState.Queued)))
                        {
                            _materialOrchestration.TrackInMaterial(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.TrackInMaterialInput()
                                {
                                    Resource = resource,
                                    Material = material
                                });
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialNotDispQueuedAtResource, material.Name, resource.Name, materialOperation.ToString());
                        }
                        materialNameOutput = material.Name;
                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackIn:

                        if (material.SystemState.Equals(MaterialSystemState.Dispatched)
                            || (material.IsInLineStep ?? false && material.SystemState.Equals(MaterialSystemState.Queued)))
                        {
                            _materialOrchestration.ComplexTrackInMaterial(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexTrackInMaterialInput()
                                {
                                    Resource = resource,
                                    Material = material
                                });
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialNotDispQueuedAtResource, material.Name, resource.Name, materialOperation.ToString());
                        }
                        materialNameOutput = material.Name;

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexDispatchAndTrackIn:

                        if (material.SystemState.Equals(MaterialSystemState.Queued))
                        {
                            _materialOrchestration.ComplexDispatchAndTrackInMaterial(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexDispatchAndTrackInMaterialInput()
                                {
                                    Resource = resource,
                                    Material = material
                                });
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialNotDispQueuedAtResource, material.Name, resource.Name, materialOperation.ToString());
                        }
                        materialNameOutput = material.Name;

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexDispatchAndTrackIns:

                        Dictionary<IMaterial, IDispatchMaterialParameters> materialsToDispatchAndTrackIn = new Dictionary<IMaterial, IDispatchMaterialParameters>();
                        IDispatchMaterialParameters dispatchMaterialParameters = new DispatchMaterialParameters()
                        {
                            Resource = resource
                        };

                        foreach (IMaterial materialValue in materials)
                        {
                            if (materialValue.SystemState.Equals(MaterialSystemState.Queued))
                            {
                                materialsToDispatchAndTrackIn.Add(materialValue, dispatchMaterialParameters);
                                materialNameOutput = materialValue.Name;

                            }
                        }

                        if (materialsToDispatchAndTrackIn.Any())
                        {
                            _materialOrchestration.ComplexDispatchAndTrackInMaterials(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexDispatchAndTrackInMaterialsInput()
                                {
                                    Materials = materialsToDispatchAndTrackIn
                                });

                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTNoMaterialsDispQueuedAtResource, resource.Name, materialOperation.ToString());
                        }

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackIns:

                        IMaterialCollection materialsToTrackIn = _entityFactory.CreateCollection<IMaterialCollection>();

                        foreach (IMaterial materialValue in materials)
                        {
                            if (materialValue.SystemState.Equals(MaterialSystemState.Dispatched)
                                || (materialValue.IsInLineStep ?? false && materialValue.SystemState.Equals(MaterialSystemState.Queued)))
                            {
                                materialsToTrackIn.Add(materialValue);
                                materialNameOutput = materialValue.Name;
                            }
                        }

                        if (materialsToTrackIn.Any())
                        {
                            _materialOrchestration.ComplexTrackInMaterials(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexTrackInMaterialsInput()
                                {
                                    Resource = resource,
                                    Materials = materialsToTrackIn
                                });
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTNoMaterialsDispQueuedAtResource, resource.Name, materialOperation.ToString());
                        }
                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.TrackOut:

                        if (material.SystemState.Equals(MaterialSystemState.InProcess))
                        {
                            //Validate TrackOut with current resource
                            if (resource != null && material.LastProcessedResource != null
                                && !material.LastProcessedResource.Name.IgnoreCaseEquals(resource.Name))
                            {
                                GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialInvalidMaterialOperationOnResource, material.Name, resource.Name, materialOperation.ToString());
                            }

                            _materialOrchestration.TrackOutMaterial(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.TrackOutMaterialInput()
                                {
                                    Material = material
                                });
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialNotInProcess, material.Name, materialOperation.ToString());
                        }
                        materialNameOutput = material.Name;

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOut:

                        if (material.SystemState.Equals(MaterialSystemState.InProcess))
                        {
                            //Validate TrackOut with current resource
                            if (resource != null && material.LastProcessedResource != null
                                && !material.LastProcessedResource.Name.IgnoreCaseEquals(resource.Name))
                            {
                                GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialInvalidMaterialOperationOnResource, material.Name, resource.Name, materialOperation.ToString());
                            }

                            _materialOrchestration.ComplexTrackOutMaterial(
                            new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexTrackOutMaterialInput()
                            {
                                Material = material
                            });
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialNotInProcess, material.Name, materialOperation.ToString());
                        }
                        materialNameOutput = material.Name;

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOuts:

                        Dictionary<IMaterial, ComplexTrackOutParameters> trackoutMaterials = new Dictionary<IMaterial, ComplexTrackOutParameters>();

                        foreach (IMaterial materialAux in materials)
                        {
                            if (materialAux.SystemState.Equals(MaterialSystemState.InProcess))
                            {
                                //Validate TrackOut with current resource
                                if (resource != null && materialAux.LastProcessedResource != null
                                    && !materialAux.LastProcessedResource.Name.IgnoreCaseEquals(resource.Name))
                                {
                                    GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialInvalidMaterialOperationOnResource, material.Name, resource.Name, materialOperation.ToString());
                                    //continue;
                                }

                                trackoutMaterials.Add(materialAux, new ComplexTrackOutParameters());
                                materialNameOutput = materialAux.Name;
                            }
                        }

                        if (trackoutMaterials.Any())
                        {
                            _materialOrchestration.ComplexTrackOutMaterials(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexTrackOutMaterialsInput()
                                {
                                    Materials = trackoutMaterials
                                }
                            );
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTNoMaterialsInProcess, materialOperation.ToString());
                        }

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOutAndMoveNext:

                        if (material.SystemState.Equals(MaterialSystemState.InProcess))
                        {
                            //Validate TrackOut with current resource
                            if (resource != null && material.LastProcessedResource != null
                                && !material.LastProcessedResource.Name.IgnoreCaseEquals(resource.Name))
                            {
                                GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialInvalidMaterialOperationOnResource, material.Name, resource.Name, materialOperation.ToString());
                                //break;
                            }

                            _materialOrchestration.ComplexTrackOutAndMoveMaterialToNextStep(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexTrackOutAndMoveMaterialToNextStepInput()
                                {
                                    Material = material
                                }
                            );
                            materialNameOutput = material.Name;
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialNotInProcess, material.Name, materialOperation.ToString());

                        }

                        break;
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOutsAndMoveNext:

                        Dictionary<IMaterial, ComplexTrackOutAndMoveNextParameters> moveNextMaterials = new Dictionary<IMaterial, ComplexTrackOutAndMoveNextParameters>();

                        foreach (IMaterial materialAux in materials)
                        {
                            if (materialAux.SystemState.Equals(MaterialSystemState.InProcess))
                            {
                                //Validate TrackOut with current resource
                                if (resource != null && materialAux.LastProcessedResource != null
                                    && !materialAux.LastProcessedResource.Name.IgnoreCaseEquals(resource.Name))
                                {
                                    GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTMaterialInvalidMaterialOperationOnResource, material.Name, resource.Name, materialOperation.ToString());
                                    //break;
                                }

                                moveNextMaterials.Add(materialAux, new ComplexTrackOutAndMoveNextParameters());
                                materialNameOutput = materialAux.Name;
                            }
                        }

                        if (moveNextMaterials.Any())
                        {
                            _materialOrchestration.ComplexTrackOutAndMoveMaterialsToNextStep(
                                new Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects.ComplexTrackOutAndMoveMaterialsToNextStepInput()
                                {
                                    Materials = moveNextMaterials
                                }
                            );
                        }
                        else
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.IoTNoMaterialsInProcess, materialOperation.ToString());
                        }

                        break;

                    default:
                        GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidMaterialOperation);
                        break;
                }

                #endregion Implementation (from DEE )



                _utilities.EndMethod(
               -1,
               -1,
               new KeyValuePair<string, object>("IoTMaterialOperationInput", input),
                new KeyValuePair<string, object>("IoTMaterialOperationOutput", output));

            }
            catch (CmfBaseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CmfBaseException(ex.Message, ex);
            }

            return output;

        }
        #endregion IoTMaterialOperation

        #region GetMaterialRecipe
        /// <summary
        /// GetMaterialRecipe
        /// </summary
        /// <param name="input">GetMaterialRecipeInput Input Object</param>
        /// <returns>GetMaterialRecipeOutput Output Object</returns>
        /// <exception cref="CmfBaseException">If any unexpected error occurs.</exception>
        public GetMaterialRecipeOutput GetMaterialRecipe(GetMaterialRecipeInput input)
        {

            _utilities.StartMethod(
                OBJECT_TYPE_NAME,
                "GetMaterialRecipe",
                new KeyValuePair<string, object>("GetMaterialRecipeInput", input));

            GetMaterialRecipeOutput output = new GetMaterialRecipeOutput();
            output.FeedbackMessages = new Collection<FeedbackMessage>();

            try
            {
                #region Missing Parameters

                // Check if all inputs are available
                _utilities.ValidateNullInput(input);
                #endregion

                // Load material
                IMaterial material = _entityFactory.Create<IMaterial>();
                material.Name = input.MaterialName;
                material.Load();
                string prodName = material.Product.Name;

                //Output
                string recipeName = "";
                string resourceRecipeName = "";

                // query SmartTable RecipeContext
                IRecipe recipe = IoTUtilities.GetRecipeFromRecipeContext(material);
                if (recipe != null)
                {
                    recipeName = recipe.Name;
                    resourceRecipeName = recipe.ResourceRecipeName;
                }

                // Return input data for convenience\logging
                output.RecipeName = recipeName;
                output.MaterialName = input.MaterialName;
                output.ResourceName = input.ResourceName;
                output.ResourceRecipeName = resourceRecipeName;
                output.ProductName = prodName;

                _utilities.EndMethod(
                   -1,
                   -1,
                   new KeyValuePair<string, object>("GetMaterialRecipeInput", input),
                   new KeyValuePair<string, object>("GetMaterialRecipeOutput", output));
            }
            catch (CmfBaseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CmfBaseException(ex.Message, ex);
            }
            return output;
        }
        #endregion GetMaterialRecipe
    }
}
