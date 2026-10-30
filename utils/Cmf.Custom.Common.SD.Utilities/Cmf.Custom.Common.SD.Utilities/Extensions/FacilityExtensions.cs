using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessObjects.QueryObject.Enums;
using Cmf.Foundation.Common;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class FacilityExtensions
{

    /// <summary>
    ///     Given a value for the Facility Attribute Plant, return the timezone for the facility first available Area
    /// </summary>
    /// <param name="plant">
    ///     The facility plant attribute
    /// </param>
    /// <returns>
    ///  The timezone in string format
    /// </returns>
    /// <exception cref="CoreException"></exception>
    public static string GetFacilityTimeZone(string plant)
    {
        QueryObject query = new QueryObject
        {
            Description = string.Empty,
            EntityTypeName = "Area",
            Name = "GetFacilityTimeZone",
            Query = new Query
            {
                Distinct = false,
                Top = 1,
                Filters = new FilterCollection {
                new Filter
                {
                    Name = "Plant",
                    ObjectName = "Facility",
                    ObjectAlias = "Area_Facility_2",
                    Operator = FieldOperator.IsEqualTo,
                    Value = plant,
                    LogicalOperator = LogicalOperator.Nothing,
                    FilterType = FilterType.Normal,
                }
                },
                Fields = new FieldCollection {
                new Field
                {
                    Alias = "Id",
                    ObjectName = "Area",
                    ObjectAlias = "Area_1",
                    IsUserAttribute = false,
                    Name = "Id",
                    Position = 0,
                    Sort = FieldSort.NoSort
                },
                new Field
                {
                    Alias = "Name",
                    ObjectName = "Area",
                    ObjectAlias = "Area_1",
                    IsUserAttribute = false,
                    Name = "Name",
                    Position = 1,
                    Sort = FieldSort.NoSort
                },
                new Field
                {
                    Alias = "CalendarTimeZone",
                    ObjectName = "Calendar",
                    ObjectAlias = "Area_Calendar_2",
                    IsUserAttribute = false,
                    Name = "TimeZone",
                    Position = 2,
                    Sort = FieldSort.NoSort
                }
                },
                Relations = new RelationCollection {
                new Relation
                {
                    Alias = string.Empty,
                    IsRelation = false,
                    Name = string.Empty,
                    SourceEntity = "Area",
                    SourceEntityAlias = "Area_1",
                    SourceJoinType = JoinType.InnerJoin,
                    SourceProperty = "FacilityId",
                    TargetEntity = "Facility",
                    TargetEntityAlias = "Area_Facility_2",
                    TargetJoinType = JoinType.InnerJoin,
                    TargetProperty = "Id"
                },
                new Relation
                {
                    Alias = string.Empty,
                    IsRelation = false,
                    Name = string.Empty,
                    SourceEntity = "Area",
                    SourceEntityAlias = "Area_1",
                    SourceJoinType = JoinType.InnerJoin,
                    SourceProperty = "CalendarId",
                    TargetEntity = "Calendar",
                    TargetEntityAlias = "Area_Calendar_2",
                    TargetJoinType = JoinType.InnerJoin,
                    TargetProperty = "Id"
                }
                }
            }
        };

        DataSet result = query.Execute(false, new QueryParameterCollection());

        return result.HasData() ? result.Tables[0].Rows[0]["CalendarTimeZone"] as string : string.Empty;
    }

    /// <summary>
    /// Get all the Facilities associatied to a given site
    /// </summary>
    /// <param name="siteName">
    ///     The site name
    /// </param>
    /// <returns>List of facility names</returns>
    public static List<string> GetFacilitiesFromSite(string siteName)
    {
        QueryObject query = new QueryObject();
        query.Description = string.Empty;
        query.EntityTypeName = "Facility";
        query.Name = "GetFacilitiesFromSite";
        query.Query = new Query();
        query.Query.Distinct = false;
        query.Query.Filters = new FilterCollection
        {
            new Filter
            {
                Name = "Name",
                ObjectName = "Site",
                ObjectAlias = "Facility_Site_2",
                Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                Value = siteName,
                LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing,
                FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
            }
        };
        query.Query.Fields = new FieldCollection
        {
            new Field
            {
                Alias = "Name",
                ObjectName = "Facility",
                ObjectAlias = "Facility_1",
                IsUserAttribute = false,
                Name = "Name",
                Position = 1,
                Sort = Cmf.Foundation.Common.FieldSort.NoSort
            }
        };
        query.Query.Relations = new RelationCollection();

        DataSet result = query.Execute(false, new QueryParameterCollection());

        List<string> facilityNames = new();
        if (result.HasData())
        {
            foreach (DataRow row in result.Tables[0].Rows)
            {
                if (row["Name"] is string name && !string.IsNullOrEmpty(name))
                {
                    facilityNames.Add(name);
                }
            }
        }

        return facilityNames;
    }
}