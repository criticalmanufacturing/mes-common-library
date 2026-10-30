using System;
using System.Collections.Generic;
using System.Text;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.Extensions
{
    // <summary>
    // Extensions to extend dictionary functionalities
    // </summary>
    public static class DictionaryExtensions
    {
        // <summary>
        // Try to get some value from a dictionary and return the default for the given type
        // </summary>
        // <typeparam name="Key"></typeparam>
        // <typeparam name="Value"></typeparam>
        // <typeparam name="ValueAs"></typeparam>
        // <param name="dictionary"></param>
        // <param name="key"></param>
        // <param name="valueAs"></param>
        // <returns></returns>
        public static bool TryGetValueAs<Key, Value, ValueAs>(this IDictionary<Key, Value> dictionary, Key key, out ValueAs valueAs) where ValueAs : Value
        {
            if (dictionary.TryGetValue(key, out Value value))
            {
                valueAs = value == null ? default : (ValueAs)value;
                return true;
            }

            valueAs = default;
            return false;
        }
    }
}
