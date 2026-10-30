using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="AreaHelpers"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
public class AreaHelpers(IEntityFactory entityFactory) : IAreaHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;

    /// <inheritdoc/>
    public IAreaCollection GetAreas()
    {
        IQueryObject query = _entityFactory.Create<IQueryObject>();
        query.Name = "GetAreas";
        query.Description = "Get All Areas";
        query.EntityTypeName = "Area";
        query.Query = new Query
        {
            Distinct = false,
            Filters = new FilterCollection(),
            Fields = new FieldCollection() {
                        new Field()
                        {
                            Alias = "Name",
                            ObjectName = "Area",
                            ObjectAlias = "Area_1",
                            IsUserAttribute = false,
                            Name = "Name",
                            Position = 0,
                            Sort = FieldSort.NoSort
                        }
                    },
            Relations = new RelationCollection()
        };
        DataSet queryOutput = query.Execute(false, new QueryParameterCollection());

        IAreaCollection areas = _entityFactory.CreateCollection<IAreaCollection>();

        if (queryOutput.HasData())
        {
            foreach (DataRow outputRow in queryOutput.Tables[0].Rows)
            {
                IArea area = _entityFactory.Create<IArea>();
                area.Name = outputRow["Name"].ToString();
                areas.Add(area);
            }
        }

        areas.Load();

        return areas;
    }
}