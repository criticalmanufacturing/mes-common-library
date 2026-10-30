using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.DataStructures
{
    // <summary>
    // Support class that represents an extended custom property
    // </summary>
    [DataContract]
    public class AutomationConfigurationValue
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

        // <summary>
        // Extended Information for Custom Behavior
        // </summary>
        [DataMember]
        public List<ExtendedCustomProperty> ExtendedCustomProperties { get; set; }
    }
}
