using Cmf.Connect.IoT.Driver.MTConnect.Extensions;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Cmf.Connect.IoT.Driver.MTConnect.DriverObjects
{
    /// <summary>
    /// Representation of received command
    /// </summary>
    public class CommandReceived
    {
        /// <summary>Id of the command received</summary>
        public string CommandId { get; private set; }

        /// <summary>Type of command to execute</summary>
        public CommandType CommandType { get; private set; } = CommandType.Probe;

        /// <summary>List of properties and values of the event</summary>
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>Device</summary>
        public string Device { get; private set; }

        /// <summary>Asset</summary>
        public string Asset { get; private set; }

        /// <summary>The sequence number to retrieve the current data for</param></summary>
        public long At { get; private set; } = 0;

        /// <summary>The XPath expression specifying the components and/or data items to include</summary>
        public string Path { get; private set; }

        public long From { get; private set; }
        public long To { get; private set; }
        public int Count { get; private set; }

        public CommandReceived(IDictionary<string, object> fromJsIoT)
        {
            var input = fromJsIoT;

            this.CommandId = input.Get<string>("commandId", "");
            Enum.TryParse<CommandType>(input.Get<string>("commandType", ""), out CommandType res);
            this.CommandType = res;
            var commandParameters = input.Get<object[]>("parameters", null);

            if (commandParameters != null)
            {
                foreach (ExpandoObject commandParameter in commandParameters)
                {
                    var name = commandParameter.Get<string>("name", "");
                    var value = (commandParameter as IDictionary<string, object>)["value"];

                    Parameters[name] = value;

                    // Remove Parameters that are not part of the message body
                    if (name.Equals("device", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.Device = value.ToString();
                    }
                    else if (name.Equals("assetId", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.Asset = value.ToString();
                    }
                    else if (name.Equals("at", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.At = long.Parse(value.ToString());
                    }
                    else if (name.Equals("path", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.Path = value.ToString();
                    }
                    else if (name.Equals("from", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.From = long.Parse(value.ToString());
                    }
                    else if (name.Equals("to", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.To = long.Parse(value.ToString());
                    }
                    else if (name.Equals("count", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value.ToString()))
                    {
                        this.Count = int.Parse(value.ToString());
                    }
                }
            }
        }
    }
}
