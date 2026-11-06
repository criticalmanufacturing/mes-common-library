using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class DataCollectionExtensions
{
    /// <summary>Gets the matching parameter value from a data collection instance.</summary>
    /// <param name="dci">The data collection instance.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="readingNumber">An optional reading number.</param>
    /// <param name="sampleName">An optional sample name.</param>
    /// <returns>The last matching value, or null if no point matches.</returns>
    public static object GetParameterValue(this IDataCollectionInstance dci, string parameterName, int? readingNumber = null, string sampleName = null)
    {
        object value = null;
        foreach (IDataCollectionPoint point in dci.DataCollectionPoints)
        {
            if (point.Value != null
                && point.TargetEntity.Name == parameterName
                && (readingNumber == null || point.ReadingNumber == readingNumber)
                && (string.IsNullOrEmpty(sampleName) || point.SampleId == sampleName))
            {
                value = point.Value;
            }
        }
        return value;
    }

    /// <summary>Checks whether a data collection instance violates the supplied limits.</summary>
    /// <param name="dci">The data collection instance.</param>
    /// <param name="dcLimits">The limits to check.</param>
    /// <param name="parameterToCheck">An optional parameter name to restrict the check.</param>
    /// <param name="includeWarningLimits">Whether warning limits count as violations.</param>
    /// <returns>True if an error limit, or an included warning limit, is violated.</returns>
    public static bool IsDCLimitsViolated(this IDataCollectionInstance dci, IDataCollectionLimitSet dcLimits, string parameterToCheck = null, bool includeWarningLimits = false)
    {
        bool violated = false;
        if (dci != null && dcLimits != null && dci.DataCollectionPoints != null && dci.DataCollectionPoints.Count > 0)
        {
            dcLimits.LoadRelations(Cmf.Navigo.Common.Constants.DataCollectionParameterLimit);
            if (dcLimits.DataCollectionParameterLimits != null && dcLimits.DataCollectionParameterLimits.Count > 0)
            {
                foreach (IDataCollectionPoint point in dci.DataCollectionPoints)
                {
                    if (string.IsNullOrEmpty(parameterToCheck) || point.TargetEntity.Name == parameterToCheck)
                    {
                        foreach (IDataCollectionParameterLimit limit in dcLimits.DataCollectionParameterLimits)
                        {
                            if (point.TargetEntity.Id != limit.TargetEntity.Id)
                            {
                                continue;
                            }
                            decimal readingValue = point.TargetEntity.DataType switch
                            {
                                ParameterDataType.Decimal or ParameterDataType.Long => Convert.ToDecimal(point.Value),
                                ParameterDataType.DateTime => Convert.ToDecimal(Convert.ToDateTime(point.Value).Ticks),
                                ParameterDataType.Duration => Convert.ToDecimal(((TimeSpan)point.Value).Ticks),
                                _ => 0
                            };
                            decimal? lowerError = limit.LowerErrorLimit;
                            decimal? upperError = limit.UpperErrorLimit;
                            decimal? lowerWarning = limit.LowerWarningLimit;
                            decimal? upperWarning = limit.UpperWarningLimit;
                            if (limit.LimitType == DataCollectionParameterLimitType.Relative)
                            {
                                decimal quantity = dci.MaterialPrimaryQuantity ?? 0;
                                if (lowerError != null) lowerError = quantity * lowerError / 100;
                                if (upperError != null) upperError = quantity * upperError / 100;
                                if (lowerWarning != null) lowerWarning = quantity * lowerWarning / 100;
                                if (upperWarning != null) upperWarning = quantity * upperWarning / 100;
                            }
                            if (lowerError != null && lowerError > readingValue) violated = true;
                            if (upperError != null && upperError < readingValue) violated = true;
                            if (lowerWarning != null && lowerWarning > readingValue) violated = includeWarningLimits;
                            if (upperWarning != null && upperWarning < readingValue) violated = includeWarningLimits;
                        }
                    }
                }
            }
        }
        return violated;
    }
}
