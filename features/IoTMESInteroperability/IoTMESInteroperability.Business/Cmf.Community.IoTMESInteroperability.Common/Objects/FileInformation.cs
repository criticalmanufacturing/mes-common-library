using Cmf.Foundation.BusinessObjects;
using System.Runtime.Serialization;

namespace Cmf.Community.IoTMESInteroperability.Common.Objects
{
    /// <summary>
    ///
    /// </summary>
    [DataContract(Name = "FileInformation")]
    public class FileInformation
    {
        /// <summary>
        /// Gets or sets the file location
        /// </summary>
        /// <value>
        /// The File Location
        /// </value>
        [DataMember(Name = "FileLocation", Order = 0)]
        public string FileLocation { get; set; }

        /// <summary>
        /// Gets or sets the File Name
        /// </summary>
        /// <value>
        /// The filename
        /// </value>
        [DataMember(Name = "FileName", Order = 1)]
        public string FileName { get; set; }
    }
}
