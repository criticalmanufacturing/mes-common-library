using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Cmf.Custom.SemiconductorTemplate.Common
{
    /// <summary>
    /// Class containing utilities to handle common DEE Action operations
    /// </summary>
    public class DEEActionUtilities
    {
        /// <summary>
        /// Checks if current action group (present in Input dicionary) is valid based on list of given action groups
        /// </summary>
        /// <param name="Input">Dictionary where in theory action group is defined</param>
        /// <param name="ValidActionGroups">List of valid action groups</param>
        /// <param name="defaultBehavior">default behavior of method. Defaults to false but can be changed by invoker</param>
        /// <returns></returns>
        public static bool IsActionGroupValid(Dictionary<string, object> Input, Collection<string> ValidActionGroups, bool defaultBehavior = false)
        {
            // by default action group is not valid unless explicitly defined otherwise
            bool isValid = defaultBehavior;

            // proceed if Action group name can be extracted from Input
            string actionGroupName = GetActionGroup(Input);
            if (!String.IsNullOrWhiteSpace(actionGroupName) && ValidActionGroups != null && ValidActionGroups.Any())
            {
                isValid = ValidActionGroups.Any(E => String.Equals(E, actionGroupName, StringComparison.InvariantCultureIgnoreCase));
            }

            return isValid;
        }

        /// <summary>
        /// Retrieves the action group
        /// </summary>
        /// <param name="Input"></param>
        /// <returns></returns>
        public static string GetActionGroup(Dictionary<string, object> Input)
        {
            string returnValue = GetInputItem<string>(Input, "ActionGroupName", String.Empty);

            return returnValue;
        }

        /// <summary>
        /// Tries to retrieve a given item from the Input dictionary, based on the item name and passed type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Input"></param>
        /// <param name="inputEntryName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static T GetInputItem<T>(Dictionary<string, object> Input, string inputEntryName, T defaultValue = default(T))
        {
            // define return value
            T returnObject = defaultValue;

            // try to extract the element
            object oExtractedItem = null;
            if (Input.TryGetValue(inputEntryName, out oExtractedItem) && oExtractedItem is T)
            {
                returnObject = (T)oExtractedItem;
            }

            return returnObject;
        }
    }
}
