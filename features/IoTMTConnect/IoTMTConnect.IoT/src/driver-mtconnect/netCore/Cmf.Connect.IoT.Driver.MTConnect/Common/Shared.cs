using Cmf.Connect.IoT.Driver.MTConnect.Common;
using Cmf.Connect.IoT.Driver.MTConnect.DriverObjects;

namespace Cmf.Connect.IoT.Driver.MTConnect.Common
{
    /// <summary>
    /// Static/Shared Instances
    /// </summary>
    public static class Shared
    {
        public static CommunicationSettings Settings = new CommunicationSettings();
        public static IoTLogger Log;
        public static EventDispatcher EventDispatcher = new EventDispatcher();
    }
}