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
    public class AutomationConfigurationData
    {
        // <summary>
        // Automation Configuration Name
        // </summary>
        [DataMember]
        public string AutomationConfigurationName { get; set; }

        // <summary>
        // Automation Configuration Related Entity Name
        // </summary>
        [DataMember]
        public string AutomationConfigurationRelatedEntityName { get; set; }

        // <summary>
        // Automation Configuration Driver Friendly Name
        // </summary>
        [DataMember]
        public string AutomationConfigurationDriverFriendlyName { get; set; }

        // <summary>
        // Automation Configuration Values
        // </summary>
        [DataMember]
        public List<AutomationConfigurationValue> AutomationConfigurationValues { get; set; }

        // <summary>
        // Nested Automation Configuration Data
        // </summary>
        [DataMember]
        public List<AutomationConfigurationData> NestedAutomationConfigurationData { get; set; }

        // <summary>
        // Extended Information for Custom Behavior
        // </summary>
        [DataMember]
        public List<ExtendedCustomProperty> ExtendedCustomProperties { get; set; }
    }
}
