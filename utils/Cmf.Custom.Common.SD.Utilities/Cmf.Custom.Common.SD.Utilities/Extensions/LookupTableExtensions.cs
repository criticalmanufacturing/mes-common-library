using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using System;
using System.Linq;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class LookupTableExtensions
{
    /// <summary>Adds a lookup table value when no case-insensitive match already exists.</summary>
    /// <param name="lookupTable">The lookup table to update.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="description">The value description.</param>
    /// <returns>True if a value was added; otherwise, false.</returns>
    public static bool AddValue(this ILookupTable lookupTable, string value, string description = "")
    {
        if (lookupTable.Values != null
            && !lookupTable.Values.Any(existing => string.Equals(existing.Value, value, StringComparison.CurrentCultureIgnoreCase)))
        {
            lookupTable.AddLookupTableValues(new LookupTableValueCollection
            {
                new LookupTableValue { Value = value, Description = description }
            });
            return true;
        }
        return false;
    }

    /// <summary>Gets lookup table values whose names match the requested values.</summary>
    /// <param name="table">The lookup table.</param>
    /// <param name="valueNames">The values to select.</param>
    /// <returns>A collection of matching lookup table values.</returns>
    public static ILookupTableValueCollection GetLookupTableValues(this ILookupTable table, params string[] valueNames)
    {
        ILookupTableValueCollection output = new LookupTableValueCollection();
        output.AddRange(table.Values.Where(value => valueNames.Contains(value.Value, StringComparer.InvariantCultureIgnoreCase)));
        return output;
    }
}
