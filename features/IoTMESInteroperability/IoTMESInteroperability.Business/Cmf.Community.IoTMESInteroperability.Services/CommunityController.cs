using Cmf.Community.IoTMESInteroperability;
using Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects;
using Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects;
using Cmf.Foundation.Common;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Cmf.Community.IoTMESInteroperability.Services
{
    /// <summary>
    /// Community Services
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class CommunityController : ControllerBase
    {
        private const string OBJECT_TYPE_NAME = "Cmf.Community.IoTMESInteroperability.Services.CommunityManagement";

        private readonly ICommunityOrchestration _ioTUtilitiesOrchestration;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommunityController"/> class.
        /// </summary>
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public CommunityController(ICommunityOrchestration ioTUtilitiesOrchestration) : base()
        {
            this._ioTUtilitiesOrchestration = ioTUtilitiesOrchestration;
        }

        #region IoTMaterialOperation
        /// <summary
        /// IoTMaterialOperation
        /// </summary
        /// <param name="input">IoTMaterialOperation Input Object</param>
        /// <returns>IoTMaterialOperation Output Object</returns>
        /// <exception cref="CmfBaseException">If any unexpected error occurs.</exception>
        [HttpPost()]
        public IoTMaterialOperationOutput IoTMaterialOperation(IoTMaterialOperationInput input)
        {
            Cmf.Foundation.Common.Utilities.StartMethod(
                    OBJECT_TYPE_NAME,
                    "IoTMaterialOperation",
                    new KeyValuePair<string, object>("IoTMaterialOperationInput", input));

            IoTMaterialOperationOutput output = new IoTMaterialOperationOutput();

            try
            {
                output = _ioTUtilitiesOrchestration.IoTMaterialOperation(input);

                Cmf.Foundation.Common.Utilities.EndMethod(
                    -1,
                    -1,
                    new KeyValuePair<string, object>("IoTMaterialOperationInput", input),
                    new KeyValuePair<string, object>("IoTMaterialOperationOutput", output));
            }
            catch (CmfBaseException)
            {
                throw;
            }
            catch (Exception excep)
            {
                throw new CmfBaseException(excep.Message, excep);
            }

            return output;
        }
        #endregion IoTMaterialOperation

        /// <summary>
        ///  GetMaterialRecipe
        /// </summary>
        /// <param name="input"> GetMaterialRecipeInput </param>
        /// <returns> GetMaterialRecipeOutput </returns>
        /// <exception cref="CmfBaseException">If any unexpected error occurs.</exception>
        [HttpPost()]
        public GetMaterialRecipeOutput GetMaterialRecipe(GetMaterialRecipeInput input)
        {
            Cmf.Foundation.Common.Utilities.StartMethod(
                    OBJECT_TYPE_NAME,
                    "GetMaterialRecipe",
                    new KeyValuePair<string, object>("GetMaterialRecipeInput", input));

            GetMaterialRecipeOutput output = new GetMaterialRecipeOutput();

            try
            {
                output = _ioTUtilitiesOrchestration.GetMaterialRecipe(input);

                Cmf.Foundation.Common.Utilities.EndMethod(
                    -1,
                    -1,
                    new KeyValuePair<string, object>("GetMaterialRecipeInput", input),
                    new KeyValuePair<string, object>("GetMaterialRecipeOutput", output));
            }
            catch (CmfBaseException)
            {
                throw;
            }
            catch (Exception excep)
            {
                throw new CmfBaseException(excep.Message, excep);
            }

            return output;
        }
    }
}