namespace Cmf.Custom.Common.SD.Utilities.DataStructures
{
    /// <summary>
    /// This class provides context by which some DEE functionalities work on.
    /// </summary>
    public class DeeContext
    {
        /// <summary>
        /// Method that triggered the DEE rule.
        /// Can be empty if method could not be resolved.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Indicator whether the DEE rule was executed as a Pre or Post action.
        /// Can be Unknown if trigger point could not be resolved.
        /// </summary>
        public DeeTriggerPoint TriggerPoint { get; set; }
    }

    /// <summary>
    /// An enumeration of all usable values for a DEE Triger Point.
    /// </summary>
    public enum DeeTriggerPoint
    {
        /// <summary>
        /// Unknown value, e.g. when called at Operation level, in a non-standard orchestration or when the TriggerPoint is irrelevant.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// Pre action value.
        /// </summary>
        Pre = 1,
        /// <summary>
        /// Post action value.
        /// </summary>
        Post = 2
    }
}
