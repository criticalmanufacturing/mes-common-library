using System;
using System.Reflection;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class ReflectionExtensions
{
    /// <summary>Gets a non-indexed property value, returning null when it cannot be read.</summary>
    /// <param name="prop">The property information.</param>
    /// <param name="objInstance">The object instance.</param>
    /// <returns>The property value, or null if unavailable.</returns>
    public static object TryGetPropertyValue(this PropertyInfo prop, object objInstance)
    {
        object propValue = null;
        try
        {
            if (prop.GetIndexParameters().Length == 0)
            {
                propValue = prop.GetValue(objInstance, null);
            }
        }
        catch (Exception) { }
        return propValue;
    }
}
