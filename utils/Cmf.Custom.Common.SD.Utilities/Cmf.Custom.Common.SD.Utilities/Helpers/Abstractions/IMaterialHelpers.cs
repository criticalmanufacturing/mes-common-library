using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Custom.Common.SD.Utilities.DataStructures;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Material Helpers
/// </summary>
public interface IMaterialHelpers
{
    /// <summary>
    ///     Gets the checklist instances associated with the specified material.
    /// </summary>
    /// <param name="materialId">
    ///     The material identifier.
    /// </param>
    /// <returns>
    ///     A <see cref="DataSet"/> containing the checklist instances.
    /// </returns>
    public DataSet GetChecklistInstanceForMaterial(long materialId);

    /// <summary>
    ///     Gets all active characteristics of the specified <see cref="IMaterial"/>.
    /// </summary>
    /// <param name="material">
    ///     The material.
    /// </param>
    /// <returns>
    ///     An <see cref="IMaterialCharacteristicCollection"/> containing the material characteristics.
    /// </returns>
    public IMaterialCharacteristicCollection GetMaterialCharacteristics(IMaterial material);

    /// <summary>
    /// This method moves a Material to a certain Step through the MoveMaterialToStep orchestration.
    /// </summary>
    /// <param name="material">The Material to move.</param>
    /// <param name="flowPath">The new FlowPath location for the material.</param>
    /// <param name="serviceOutput">Service output used to append the MoveNext output feedback messages. The default value is null.</param>
    /// <param name="serviceInput">Input parameters from the calling service, used to append the service comment. The default value is null.</param>
    /// <param name="comment">The comment to append to the service.</param>
    public void MoveMaterialToStep(IMaterial material, string flowPath, BaseOutput serviceOutput = null, BaseInput serviceInput = null, string comment = null);
    /// <summary>
    /// This method simulates a Material rename by splitting its complete quantity to a new lot.
    /// </summary>
    /// <param name="material">The Material object to rename.</param>
    /// <param name="newName">The new material name.</param>
    /// <param name="serviceOutput">Output parameters from the calling service used to append feedback messages.</param>
    /// <param name="serviceInput">Input parameters from the calling service used to append the service comment.</param>
    /// <param name="comment">The comment to append to the service.</param>
    /// <param name="feedbackMessage">The message to return to the parent service call.</param>
    /// <returns>The new renamed Material.</returns>
    public IMaterial RenameMaterial(IMaterial material, string newName, BaseOutput serviceOutput = null, BaseInput serviceInput = null, string comment = null, string feedbackMessage = null);
    /// <summary>
    /// This method gets the lots in a container as a material collection.
    /// </summary>
    /// <param name="container">The source container where the lots are searched.</param>
    /// <returns>A material collection with the lots in the container.</returns>
    public IMaterialCollection GetLotsInContainer(IContainer container);
    /// <summary>
    /// This method creates lots (as a "Lot" structure) from a material collection.
    /// </summary>
    /// <param name="materials">The material collection.</param>
    /// <returns>A material collection with the assembled structure.</returns>
    public IMaterialCollection GetLotsInMaterialStructure(IMaterialCollection materials);
    /// <summary>
    /// This method creates lots (as a "Lot" structure) from a single material.
    /// </summary>
    /// <param name="material">The single material.</param>
    /// <returns>A material collection with the assembled structure.</returns>
    public IMaterialCollection GetLotsInMaterialStructure(IMaterial material);
    /// <summary>
    /// This method gets materials in the specified form from a material collection and its material structures.
    /// </summary>
    /// <param name="materials">The source material collection.</param>
    /// <param name="form">The material form name.</param>
    /// <returns>A material collection containing the matching materials.</returns>
    public IMaterialCollection GetMaterialsInMaterialStructure(IMaterialCollection materials, string form);
    /// <summary>
    /// This method gets a material in the specified form from its material structure.
    /// </summary>
    /// <param name="material">The single material.</param>
    /// <param name="form">The material form name.</param>
    /// <returns>A material collection containing the matching material.</returns>
    public IMaterialCollection GetMaterialsInMaterialStructure(IMaterial material, string form);
    /// <summary>
    /// This method retrieves attached consumables from the resource and its descendants.
    /// </summary>
    /// <param name="mainResource">The main resource where the consumables are searched.</param>
    /// <param name="consumableFeedSearchDepth">The maximum depth for searching descendants from the main resource. The default value is 1.</param>
    /// <returns>A collection of attached consumables.</returns>
    public AttachedConsumableCollection GetAttachedConsumables(IResource mainResource, short consumableFeedSearchDepth = 1);
    /// <summary>
    /// This method gets the last flow for a material.
    /// </summary>
    /// <param name="material">The material to inspect.</param>
    /// <returns>The last flow for the provided material.</returns>
    public IFlow GetStepParentFlow(IMaterial material);
    /// <summary>
    /// This method resolves all BOM Products for a material.
    /// </summary>
    /// <param name="material">The material whose BOM Products are resolved.</param>
    /// <returns>The BOM Product collection for the material.</returns>
    public IBOMProductCollection ResolveBOMProducts(IMaterial material);

}