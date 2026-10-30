using System.Runtime.Serialization;

namespace Cmf.Community.IoTMESInteroperability.Common.Enums
{
    /// <summary>
    /// IoTMaterialOperation
    /// </summary>
    [DataContract(Namespace = "", Name = "IoTMaterialOperation")]
    public enum IoTMaterialOperation
    {
        /// <summary>
        /// TrackIn
        /// </summary>
        [EnumMember]
        TrackIn = 0,

        /// <summary>
        /// ComplexTrackIn
        /// </summary>
        [EnumMember]
        ComplexTrackIn = 1,

        /// <summary>
        /// ComplexTrackIn
        /// </summary>
        [EnumMember]
        ComplexTrackIns = 2,

        /// <summary>
        /// TrackOut
        /// </summary>
        [EnumMember]
        TrackOut = 3,

        /// <summary>
        /// ComplexTrackOut
        /// </summary>
        [EnumMember]
        ComplexTrackOut = 4,

        /// <summary>
        /// ComplexTrackOut
        /// </summary>
        [EnumMember]
        ComplexTrackOuts = 5,

        /// <summary>
        /// ComplexDispatchAndTrackIn
        /// </summary>
        [EnumMember]
        ComplexDispatchAndTrackIn = 6,

        /// <summary>
        /// ComplexDispatchAndTrackIns
        /// </summary>
        [EnumMember]
        ComplexDispatchAndTrackIns = 7,

        /// <summary>
        /// ComplexTrackOutAndMoveNext
        /// </summary>
        [EnumMember]
        ComplexTrackOutAndMoveNext = 8,

        /// <summary>
        /// ComplexTrackOutsAndMoveNext
        /// </summary>
        [EnumMember]
        ComplexTrackOutsAndMoveNext = 9,
    }
}
