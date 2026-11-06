using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
/// Provides helpers for managing resources.
/// </summary>
public interface IResourceHelper
{
    /// <summary>
    /// Attaches the specified materials as consumables to a resource.
    /// </summary>
    /// <param name="instance">The resource to which the consumables are attached.</param>
    /// <param name="materials">The materials to attach as consumables.</param>
    /// <param name="order">The order assigned to each attached consumable.</param>
    public void AttachConsumablesToResource(IResource instance, IMaterialCollection materials, int order = 0);

    /// <summary>
    /// Changes the state of the resource.
    /// </summary>
    /// <param name="resource">The resource to change state for.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="reason">The reason for the state change.</param>
    public void ChangeState(IResource resource, string newState, string reason = null);
}