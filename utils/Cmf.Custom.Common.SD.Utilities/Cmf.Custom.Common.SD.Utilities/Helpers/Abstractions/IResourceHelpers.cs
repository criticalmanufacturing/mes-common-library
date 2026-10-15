
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;


/// <summary>
///     Employee Helpers
/// </summary>
public interface IResourceHelpers
{
    /// <summary>
    /// Retrieves a collection of resources that are currently checked in by the specified employee across all areas.
    /// </summary>
    /// <remarks>
    /// The returned collection contains only unique resources; duplicate resources across areas are excluded.
    /// </remarks>
    /// <param name="employee">
    /// The employee for whom to retrieve checked-in resources.
    /// </param>
    /// <returns>
    /// An <see cref="IResourceCollection"/> containing the unique resources checked in by the specified employee across all areas.
    /// Returns an empty collection if <paramref name="employee"/> is <see langword="null"/> or if no resources are found.
    /// </returns>
    IResourceCollection GetResourcesCheckedInByEmployee(IEmployee employee);

    /// <summary>
    /// Queries checklist instances associated with the specified resource and checklist types.
    /// </summary>
    /// <param name="resourceId">
    /// The identifier of the resource.
    /// </param>
    /// <param name="checklistTypes">
    /// The checklist types to include in the query.
    /// </param>
    /// <returns>
    /// A <see cref="DataSet"/> containing the matching checklist instances.
    /// </returns>
    DataSet GetChecklistInstanceForResource(long resourceId, List<string> checklistTypes);

    /// <summary>
    /// Attach a set of Material to a Resource
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="materials"></param>
    /// <param name="order"></param>
    public void AttachConsumablesToResource(IResource instance, IMaterialCollection materials, int order = 0);
}

