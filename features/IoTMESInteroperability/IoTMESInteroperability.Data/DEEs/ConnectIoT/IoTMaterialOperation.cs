using System;
using System.Collections.Generic;
using System.Linq;
using Cmf.Navigo.BusinessObjects;
using Cmf.Common.CustomActionUtilities;
using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Community.IoTMESInteroperability.Common;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Community.IoTMESInteroperability;
using Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects;
using Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects;
using Cmf.Community.IoTMESInteroperability.Utilities;


namespace Cmf.Community.IoTMESInteroperability.Actions
{
    public class IoTMaterialOperation : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---


            #region Info

            /// <summary>
            /// Summary text
            ///     DEE To be Invoked by IoT to Perform an Operation
            /// Action Groups: 
            /// Depends On:
            /// Is Dependency For:
            /// Exceptions:
            /// </summary>

            #endregion Info

            string contextParameter_Material = "IoTMaterialOperation_Material";
            string contextParameter_Materials = "IoTMaterialOperation_Materials";
            string contextParameter_Resource = "IoTMaterialOperation_Resource";
            string contextParameter_Operation = "IoTMaterialOperation_Operation";
            bool isToExecute = false;

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IDEEHelper deeHelper = serviceProvider.GetService<IDEEHelper>();

            if (Input.Keys.Contains("Operation"))
            {
                IResource resource = Input["Resource"] as Resource;
                string operation = Input["Operation"] as string;

                if (string.IsNullOrEmpty(operation))
                {

                    GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidMaterialOperation);
                }

                Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation materialOperation =
                    (Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation)Enum.Parse(typeof(Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation), operation);

                IMaterialCollection materials = Input.Keys.Contains("Materials") ? Input["Materials"] as MaterialCollection : null;
                IMaterial material = Input.Keys.Contains("Material") ? Input["Material"] as Material : null;

                resource.Load();

                if (materials == null && material == null && resource != null
                    && IoTUtilities.IsFeatureEnabled(IoTUtilitiesConfigs.AllowTrackInWithoutMaterialId, throwError: false))
                {
                    switch (materialOperation)
                    {
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.TrackIn:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackIn:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackIns:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexDispatchAndTrackIn:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexDispatchAndTrackIns:

                            materials = IoTUtilities.GetMaterialsFromPreviousSubResource(resource);
                            break;

                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.TrackOut:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOut:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOuts:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOutAndMoveNext:
                        case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOutsAndMoveNext:

                            materials = IoTUtilities.GetAllInlineMaterialsInProcess(resource);
                            break;

                        default:
                            break;
                    }

                    if (!materials.Any())
                    {
                        GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidNoMaterialFound);
                    }
                    material = materials[0];
                }

                if (materials == null && material == null)
                {
                    GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidNoMaterialFound);
                }

                switch (materialOperation)
                {
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.TrackIn:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackIn:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackIns:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexDispatchAndTrackIn:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexDispatchAndTrackIns:

                        if (resource == null)
                        {
                            GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidResource);
                        }

                        break;

                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.TrackOut:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOut:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOuts:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOutAndMoveNext:
                    case Cmf.Community.IoTMESInteroperability.Common.Enums.IoTMaterialOperation.ComplexTrackOutsAndMoveNext:
                        break;
                    default:
                        GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidMaterialOperation);
                        break;
                }

                deeHelper.SetContextParameter(contextParameter_Materials, materials);
                deeHelper.SetContextParameter(contextParameter_Material, material);
                deeHelper.SetContextParameter(contextParameter_Resource, resource);
                deeHelper.SetContextParameter(contextParameter_Operation, materialOperation);

                isToExecute = true;
            }
            else
            {
                GeneralUtilities.ThrowLocalizedException(IoTUtilitiesMessages.InvalidInputs);
            }

            return isToExecute;

            //---End DEE Condition Code---

        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.Abstractions");
            UseReference("Cmf.Navigo.Common.dll", "Cmf.Navigo.Common");
            UseReference("Cmf.Navigo.BusinessObjects.dll", "Cmf.Navigo.BusinessObjects");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Common");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability");
            UseReference("Cmf.Community.IoTMESInteroperability.Orchestration.dll", "Cmf.Community.IoTMESInteroperability.Orchestration");
            UseReference("Cmf.Community.IoTMESInteroperability.Orchestration.dll", "Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects");
            UseReference("Cmf.Community.IoTMESInteroperability.Orchestration.dll", "Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects");
            UseReference("Cmf.Community.IoTMESInteroperability.Common.dll", "Cmf.Community.IoTMESInteroperability.Orchestration.Abstractions");


            #region Service Provider

            // Get services provider information
            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            IDEEHelper deeHelper = serviceProvider.GetService<IDEEHelper>();
            #endregion

            #region Utils

            ICommunityOrchestration _utilitiesOrchestration = serviceProvider.GetService<ICommunityOrchestration>();
            #endregion


            string contextParameter_Material = "IoTMaterialOperation_Material";
            string contextParameter_Materials = "IoTMaterialOperation_Materials";
            string contextParameter_Resource = "IoTMaterialOperation_Resource";

            IMaterial material = deeHelper.GetContextParameter<IMaterial>(contextParameter_Material);
            IMaterialCollection materials = deeHelper.GetContextParameter<IMaterialCollection>(contextParameter_Materials);
            IResource resource = deeHelper.GetContextParameter<IResource>(contextParameter_Resource);


            IoTMaterialOperationInput input = new IoTMaterialOperationInput();

            if (Input.Keys.Contains("Operation"))
            {
                input.Operation = Input["Operation"] as string;
            }

            if (resource != null)
            {
                input.Resource = resource as Resource;
            }
            if (material != null)
            {
                input.Material = material as Material;
            }
            if (materials != null)
            {
                input.Materials = materials as MaterialCollection;
            }

            IoTMaterialOperationOutput output = _utilitiesOrchestration.IoTMaterialOperation(input);


            string materialNameOutput = "";
            if (output != null && output.MaterialName != null)
            {
                materialNameOutput = output.MaterialName;
            }

            Dictionary<String, Object> outputDEE = new Dictionary<String, Object>();
            outputDEE.Add("MaterialNameOutput", materialNameOutput);
            return (outputDEE);


            //---End DEE Code---
        }
    }
}
