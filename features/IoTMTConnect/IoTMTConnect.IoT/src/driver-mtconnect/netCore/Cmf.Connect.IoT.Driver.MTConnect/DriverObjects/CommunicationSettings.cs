using Cmf.Connect.IoT.Driver.MTConnect.Extensions;
using System.Collections.Generic;
using System.Text;

namespace Cmf.Connect.IoT.Driver.MTConnect.DriverObjects
{
    public class CommunicationSettings
    {
        /// <summary>Protocol used to communicate with the endpoints</summary>
        public string NetCoreSdkVersion { get; set; } = "";

        /// <summary>IP address of the MTConnect Agent</summary>
        public string Address { get; set; } = "";

        /// <summary>Port of the MTConnect Agent</summary>
        public int Port { get; set; } = 5000;

        /// <summary>Device</summary>
        public string Device { get; set; } = null;

        /// <summary>Time to Connect</summary>
        public float ConnectingTimeout { get; set; } = 30;

        /// <summary>
        /// Load the settings from json format to the internal structure
        /// </summary>
        /// <param name="settings">List of settings</param>
        public void Load(IDictionary<string, object> settings)
        {
            NetCoreSdkVersion = settings.Get("netCoreSdkVersion", NetCoreSdkVersion);
            Address = settings.Get("address", Address);
            Port = settings.Get("port", Port);
            Device = settings.Get("device", Device);
            ConnectingTimeout = settings.Get("connectingTimeout", ConnectingTimeout);
        }

        /// <summary>
        /// Dump the configuration settings to log for future validation
        /// </summary>
        /// <returns></returns>
        public string Dump()
        {
            StringBuilder cfgDump = new StringBuilder();
            cfgDump.AppendLine();
            cfgDump.AppendLine($"NetCoreSdkVersion: '{NetCoreSdkVersion}'");
            cfgDump.AppendLine($"Address: '{Address}'");
            cfgDump.AppendLine($"Port: '{Port}'");
            cfgDump.AppendLine($"Device: '{Device}'");

            return (cfgDump.ToString());
        }
    }
}
