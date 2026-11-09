using System.Runtime.Serialization;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures
{
    // <summary>
    // Support class that represents an extended custom property
    // </summary>
    [DataContract]
    public class ExtendedCustomProperty
    {
        // <summary>
        // The name of the given property
        // </summary>
        [DataMember]
        public string Name { get; set; }

        // <summary>
        // The value of the given property
        // </summary>
        [DataMember]
        public object Value { get; set; }
    }
}
