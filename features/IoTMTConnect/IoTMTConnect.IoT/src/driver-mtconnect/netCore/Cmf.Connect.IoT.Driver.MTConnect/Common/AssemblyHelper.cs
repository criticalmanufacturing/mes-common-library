using MTConnect.Formatters;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Cmf.Connect.IoT.Driver.MTConnect.Common
{
    public static class AssemblyHelper
    {
        public static void InitializeMTConnectFormatterAssemblies()
        {
            // Get assemblies that are already loaded (including Costura-embedded ones)
            var mtConnectAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.Contains("MTConnect"));

            if (mtConnectAssembly == null)
            {
                throw new Exception("MTConnect assembly still not found after loading");
            }

            var mtConnectAssembliesType = Type.GetType("MTConnect.Formatters.ResponseDocumentFormatter, MTConnect.NET-Common");
            if (mtConnectAssembliesType == null)
            {
                throw new Exception("Could not find MTConnect.Assemblies type");
            }

            // Use reflection to get the private _formatters field
            var formattersField = mtConnectAssembliesType.GetField("_formatters",
                BindingFlags.NonPublic | BindingFlags.Static);

            var existingFormatters = (ConcurrentDictionary<string, IResponseDocumentFormatter>)formattersField.GetValue(null);
            existingFormatters.TryAdd("xml", (IResponseDocumentFormatter)Activator.CreateInstance(Type.GetType("MTConnect.Formatters.Xml.XmlResponseDocumentFormatter, MTConnect.NET-XML")));
        }
    }
}
