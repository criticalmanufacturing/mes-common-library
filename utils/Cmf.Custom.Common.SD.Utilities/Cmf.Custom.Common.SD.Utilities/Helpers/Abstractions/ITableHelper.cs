using Cmf.Foundation.BusinessObjects.Abstractions;
using System.Collections.Generic;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
/// This class provides helper methods for table operations.
/// </summary>
public interface ITableHelper
{
    /// <summary>
    /// Inserts or removes values in an existing smart table.
    /// </summary>
    /// <param name="smartTableName">The name of the smart table.</param>
    /// <param name="smartTableValues">The values to update.</param>
    /// <param name="isDelete">Whether to remove values.</param>
    /// <param name="changeSetType">The change set type to use if the smart table has change control.</param>
    /// <param name="isToMakeEffectiveOnApproval">Whether the change set makes the smart table effective on approval when it has change control.</param>
    /// <returns>The updated smart table.</returns>
    ISmartTable AddOrRemoveDataOnSmartTable(string smartTableName, INgpDataSet smartTableValues, bool isDelete, string changeSetType = "General", bool isToMakeEffectiveOnApproval = true);


    /// <summary>
    /// Adds a new value to a lookup table specified by name.
    /// </summary>
    /// <param name="lookupTableName">The name of the lookup table.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="description">The description of the value. The default value is an empty string.</param>
    /// <returns><see langword="true"/> if the value is added; otherwise, <see langword="false"/>.</returns>
    bool AddValueToLookupTable(string lookupTableName, string value, string description = "");

    /// <summary>
    /// Gets data from a generic table using the specified filters.
    /// </summary>
    /// <param name="genericTableName">The name of the generic table to load.</param>
    /// <param name="filters">The filters to use when loading data. If <see langword="null"/>, all data is loaded.</param>
    /// <returns>The dataset after applying the filters.</returns>
    DataSet GetDataFromGenericTable(string genericTableName, IFilterCollection filters);

    /// <summary>
    /// Gets data from a smart table using the specified filters.
    /// </summary>
    /// <param name="smartTableName">The name of the smart table to load.</param>
    /// <param name="filters">The filters to use when loading data. If <see langword="null"/>, all data is loaded.</param>
    /// <returns>The dataset after applying the filters.</returns>
    DataSet GetDataFromSmartTable(string smartTableName, IFilterCollection filters);

    /// <summary>
    /// Gets the lookup table with the specified name.
    /// </summary>
    /// <param name="tableName">The name of the lookup table to load.</param>
    /// <returns>The requested lookup table, or <see langword="null"/> if it does not exist or cannot be loaded.</returns>
    ILookupTable GetLookupTable(string tableName);


    /// <summary>
    /// Gets the values from the lookup table with the specified name.
    /// </summary>
    /// <param name="tableName">The name of the lookup table.</param>
    /// <returns>A list containing the values in the lookup table.</returns>
    List<string> GetLookupTableValues(string tableName);

    /// <summary>
    /// Gets lookup table values whose names match the specified names.
    /// </summary>
    /// <param name="lookupTableName">The name of the lookup table.</param>
    /// <param name="valueNames">The names of the values to retrieve.</param>
    /// <returns>The requested lookup table values if found; otherwise, <see langword="null"/>.</returns>
    ILookupTableValueCollection GetLookupTableValues(string lookupTableName, params string[] valueNames);

    /// <summary>
    /// Creates a dataset from a smart table using the specified parameter values.
    /// </summary>
    /// <param name="tableName">The name of the smart table.</param>
    /// <param name="nameAndValues">A mapping of parameter names to values.</param>
    /// <param name="onlyFirstRow">Whether to retrieve only the first row from the table.</param>
    /// <returns>The dataset created from the smart table.</returns>
    DataSet ResolveSmartTable(string tableName, Dictionary<string, string> nameAndValues, bool onlyFirstRow);
}
