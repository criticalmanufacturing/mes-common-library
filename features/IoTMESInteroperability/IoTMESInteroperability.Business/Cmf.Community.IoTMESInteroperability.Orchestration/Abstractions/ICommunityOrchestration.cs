using Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects;
using Cmf.Community.IoTMESInteroperability.Orchestration.OutputObjects;

namespace Cmf.Community.IoTMESInteroperability;

public interface ICommunityOrchestration
{
    #region IoTMaterialOperation
    /// <summary
    /// IoTMaterialOperation
    /// </summary
    /// <param name="input">IoTMaterialOperation Input Object</param>
    /// <returns>IoTMaterialOperation Output Object</returns>
    /// <exception cref="CmfBaseException">If any unexpected error occurs.</exception>
    IoTMaterialOperationOutput IoTMaterialOperation(IoTMaterialOperationInput input);

    #endregion IoTMaterialOperation

    #region GetMaterialRecipe
    /// <summary>
    /// GetMaterialRecipe
    /// </summary
    /// <param name="input"> GetMaterialRecipeInput Object </param>
    /// <returns> GetMaterialRecipeOutput Object </returns>
    /// <exception cref="CmfBaseException">If any unexpected error occurs.</exception>
    GetMaterialRecipeOutput GetMaterialRecipe(GetMaterialRecipeInput input);

    #endregion GetMaterialRecipe
}