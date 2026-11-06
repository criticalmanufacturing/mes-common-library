using Cmf.Foundation.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions
{
    /// <summary>
    /// This is a utility class for handling entities.
    /// </summary>
    public interface IEntityHelper
    {
        /// <summary>
        /// This method creates a ChangeSet and then returns it.
        /// </summary>
        /// <param name="isEffectiveOnApproval">A boolean that defines if the created ChangeSet should be made effective on approval. Is true by default.</param>
        /// <returns>The ChangeSet object after its creation.</returns>
        IChangeSet CreateChangeSet(bool isEffectiveOnApproval = true);

        /// <summary>
        /// This method creates a specific structure for a ChangeSet.
        /// </summary>
        /// <returns>The ChangeSet object after its creation.</returns>
        IChangeSet CreateChangeSetStructure();

        /// <summary>
        /// This method creates Entity Type versions, assigning a new ChangeSet for a collection to them.
        /// </summary>
        /// <param name="entities">The entities to be created.</param>
        /// <param name="lookUpChangeSetType">the type of change set to use (currently unused)</param>
        /// <param name="requestApproval">A boolean that defines whether or not to request approval for this entity type. Is true by default (currently unused).</param>
        /// <param name="isEffectiveOnApproval">A boolean that defines if the created ChangeSet should be made effective on approval. Is true by default.</param>
        void CreateNewEntitiesVersion(IEntityVersionCollection<IEntityVersion> entities, string lookUpChangeSetType, bool requestApproval = true, bool isEffectiveOnApproval = true);

        /// <summary>
        /// This method creates an Entity Type version, assigning a new ChangeSet to it.
        /// </summary>
        /// <param name="entity">The entity to be created.</param>
        /// <param name="requestApproval">A boolean that defines whether or not to request approval for this entity type. Is true by default.</param>
        /// <param name="isEffectiveOnApproval">A boolean that defines if the created ChangeSet should be made effective on approval. Is true by default.</param>
        void CreateNewEntityVersion(IEntityVersion entity, bool requestApproval = true, bool isEffectiveOnApproval = true);








    }
}
