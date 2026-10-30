using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cmf.Connect.IoT.Driver.MTConnect.Common.JSON
{
    /// <summary>
    /// 
    /// </summary>
    public class MTConnectJsonSerializer
    {
        private static Regex m_RegExpObj = new Regex(@"'\$type':\s*'([\w|\.]*),\s*([\w|\.\-\s]*)'".Replace("'", "\""), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static string ReplaceType(string json, string newAssembly)
        {
            MatchCollection regExpMatches = m_RegExpObj.Matches(json);
            if (regExpMatches != null && regExpMatches.Any())
            {
                var distinctGroup = regExpMatches.OfType<Match>().Select(m => m.Groups[0].Value).Distinct();

                foreach (var match in distinctGroup)
                {
                    var group = regExpMatches.First(regExpMatch => regExpMatch.Groups[0].ToString() == match).Groups;

                    if (group != null && group.Count == 3)
                    {
                        var newToken = $"\"$type\": \"{group[1]}, {newAssembly}\"";
                        json = (json.Replace(group[0].ToString(), newToken));
                    }
                }
            }

            // Else...
            return (json);
        }

        /// <summary>
        /// Serializes a CFX object into JSON format
        /// </summary>
        /// <param name="o">The object to be serialized</param>
        /// <param name="formatted">If true, the resultant JSON will be formatted for easy human interpretation (whitespace and carriage returns added)</param>
        /// <returns>A string representing the CFX object in JSON format</returns>
        public static string SerializeObject(object o, bool formatted = false)
        {
            Newtonsoft.Json.Formatting format = formatted ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None;
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(o, format, settings);

            return json;
        }



        /// <summary>
        /// Converts a string in JSON format into the CFX object that it represents
        /// </summary>
        /// <typeparam name="T">The Type of the object</typeparam>
        /// <param name="jsonData">A string in JSON format which represents the CFX object</param>
        /// <returns>An object of type T which is represented by the jsonData parameter</returns>
        public static T DeserializeObject<T>(string jsonData)
        {
            JsonSettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;

            jsonData = ReplaceType(jsonData, "Cmf.Connect.IoT.Driver.IpcCfx");
            // .Replace(", CFX", ", Cmf.Connect.IoT.Driver.IpcCfx");

            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonData, JsonSettings);
        }

        /// <summary>
        /// Converts a string in JSON format into the CFX object that it represents
        /// </summary>
        /// <param name="jsonData">A string in JSON format which represents the CFX object</param>
        /// <param name="type">The Type of the object</param>
        /// <returns>An object of type T which is represented by the jsonData parameter</returns>
        public static object DeserializeObject(string jsonData, Type type)
        {
            JsonSettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;
            jsonData = ReplaceType(jsonData, "Cmf.Connect.IoT.Driver.IpcCfx");
            // .Replace(", CFX", ", Cmf.Connect.IoT.Driver.IpcCfx");
            return Newtonsoft.Json.JsonConvert.DeserializeObject(jsonData, type, JsonSettings);
        }

        private static Newtonsoft.Json.JsonSerializerSettings jsonSettings = new Newtonsoft.Json.JsonSerializerSettings();

        /// <summary>
        /// Settings that control how serialization and deserialization of CFX objects will be done
        /// </summary>
        public static Newtonsoft.Json.JsonSerializerSettings JsonSettings
        {
            get
            {
                if (jsonSettings == null) jsonSettings = new Newtonsoft.Json.JsonSerializerSettings();
                return jsonSettings;
            }
            set
            {
                jsonSettings = value;
            }
        }
    }
}
