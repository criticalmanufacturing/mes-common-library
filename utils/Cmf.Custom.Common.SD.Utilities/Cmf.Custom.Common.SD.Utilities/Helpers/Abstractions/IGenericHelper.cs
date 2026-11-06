using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Configuration.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions
{
    /// <summary>
    /// Provides generic configuration, localization, entity, and messaging utilities.
    /// </summary>
    public interface IGenericHelper
    {
        /// <summary>
        /// This method adds a collection of attachments to a provided entity.
        /// </summary>
        /// <param name="entityInstance">The instance of the entity where the attachments will be added.</param>
        /// <param name="attachments">The collection of attachments to add.</param>
        /// <param name="tempFileLocation">A temporary file location where internal attachments are processed before adding them to the entity.</param>
        void AddAttachments(IEntityInstance entityInstance, IEntityDocumentationCollection attachments, string tempFileLocation);


        /// <summary>
        /// This method retrieves the current logged employee.
        /// </summary>
        /// <returns>The employee who is currently logged in.</returns>
        IEmployee GetCurrentEmployee();
        /// <summary>
        /// This method retrieves a localized message formatted with the supplied parameters.
        /// </summary>
        /// <param name="localizedMessageKey">The localized message key.</param>
        /// <param name="parameters">The values to format into the localized message.</param>
        /// <returns>The localized message.</returns>
        string GetLocalizedMessage(string localizedMessageKey, params object[] parameters);
        /// <summary>
        /// Returns loaded objects matching the specified IDs.
        /// </summary>
        /// <param name="ids">The IDs of the objects to retrieve.</param>
        /// <param name="type">An object of the type to retrieve.</param>
        /// <returns>The matching objects, or an empty collection if no objects are found.</returns>
        Collection<object> GetObjectsByIds(List<long> ids, object type);
        /// <summary>
        /// Returns loaded effective versionable objects matching the specified definition IDs.
        /// </summary>
        /// <param name="ids">The definition IDs of the objects to retrieve.</param>
        /// <param name="type">An object of the versionable type to retrieve.</param>
        /// <returns>The matching objects, or an empty collection if no objects are found.</returns>
        Collection<object> GetVersionableObjectsByIds(List<long> ids, object type);


        /// <summary>
        /// Loads the specified IDs into an entity collection, clearing the collection first.
        /// </summary>
        /// <typeparam name="T">The entity type in the collection.</typeparam>
        /// <param name="instances">The collection to clear and populate with loaded entities.</param>
        /// <param name="idsToLoad">The IDs of the entities to load.</param>
        void LoadByIDs<T>(IEntityCollection<T> instances, Collection<long> idsToLoad) where T : IEntity;
        /// <summary>
        /// Loads the specified IDs into a core base collection, clearing the collection first.
        /// </summary>
        /// <typeparam name="T">The core base type in the collection.</typeparam>
        /// <param name="instances">The collection to clear and populate with loaded objects.</param>
        /// <param name="idsToLoad">The IDs of the objects to load.</param>
        void LoadByIDs<T>(ICoreBaseCollection<T> instances, List<long> idsToLoad) where T : ICoreBase;

        /// <summary>
        /// This method checks if the config is enabled given its path.
        /// </summary>
        /// <param name="configPath">A string representing the path's configuration.</param>
        /// <returns>True if the config is enabled, false otherwise.</returns>
        bool IsConfigEnabled(string configPath);

        /// <summary>
        /// This method throws a special exception message based on the localized messages feature.
        /// </summary>
        /// <param name="key">The localized message key.</param>
        /// <param name="parameters">The localized message parameters.</param>
        /// <exception cref="Cmf.Foundation.Common.CmfBaseException">Throws a base exception with the localized message.</exception>
        void ThrowLocalizedException(string key, params string[] parameters);
    }
}
