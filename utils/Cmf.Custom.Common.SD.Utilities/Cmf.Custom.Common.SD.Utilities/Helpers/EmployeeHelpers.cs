
using Cmf.Common.CustomActionUtilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Security.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using System.Data;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

/// <summary>
///     Employee Helpers
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="EmployeeHelpers"/> class.
/// </remarks>
/// <param name="entityFactory">
///     The entity factory.
/// </param>
/// <param name="utilities">
///     The utilities.
/// </param>
public class EmployeeHelpers(IEntityFactory entityFactory, IUtilities utilities) : IEmployeeHelpers
{
    private readonly IEntityFactory _entityFactory = entityFactory;
    private readonly IUtilities _utilities = utilities;

    /// <inheritdoc/>
    public IEmployeeCollection GetResourceEmployees(IResource resource)
    {
        IEmployeeCollection employees = _entityFactory.CreateCollection<IEmployeeCollection>();
        resource.LoadRelations(Navigo.Common.Constants.ResourceEmployee);
        if (resource.RelationCollection != null && resource.RelationCollection.ContainsKey(Navigo.Common.Constants.ResourceEmployee) && resource.RelationCollection[Navigo.Common.Constants.ResourceEmployee].Count > 0)
        {
            foreach (ResourceEmployee item in resource.RelationCollection[Navigo.Common.Constants.ResourceEmployee])
            {
                employees.Add(item.TargetEntity);
            }
        }
        if (employees.Count > 0)
        {
            employees.Load();
        }
        return employees;
    }

    /// <inheritdoc/>
    public string GetLoggedInUserEmployeeNumber()
    {
        return GetLoggedInUserEmployee() is IEmployee employee ? employee.EmployeeNumber : string.Empty;
    }

    /// <inheritdoc/>
    public string GetLoggedInUserEmployeeName()
    {
        return GetLoggedInUserEmployee() is IEmployee employee ? employee.Name : string.Empty;
    }

    /// <inheritdoc/>
    public IEmployee? GetLoggedInUserEmployee()
    {
        string userAccount = _utilities.DomainUserName;
        IUser user = _entityFactory.Create<IUser>();
        user.Load(userAccount);

        IEmployee employee = _entityFactory.Create<IEmployee>();
        employee.LoadByUserAccount(userAccount);

        return employee.ObjectExists() ? employee : null;
    }

    /// <inheritdoc/>
    public IEmployeeCollection GetEmployeesByNumber(string[] numbers, Cmf.Navigo.BusinessObjects.ClockedState clockedState)
    {
        IEmployeeCollection employees = _entityFactory.CreateCollection<IEmployeeCollection>();

        FilterCollection filters = new FilterCollection();
        foreach (string number in numbers)
        {
            filters.Add(new Filter
            {
                Name = "EmployeeNumber",
                ObjectName = "Employee",
                ObjectAlias = "Employee_1",
                Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                Value = number,
                LogicalOperator = Cmf.Foundation.Common.LogicalOperator.OR,
                FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
            });
        }
        if (filters.Count > 0)
        {
            filters[filters.Count - 1].LogicalOperator = Cmf.Foundation.Common.LogicalOperator.Nothing;
            IQueryObject query = _entityFactory.Create<IQueryObject>();
            query.Description = string.Empty;
            query.EntityTypeName = "Employee";
            query.Name = "GetEmployeesByNumber";
            query.Query = new Query
            {
                Distinct = true,
                Filters = new FilterCollection {
                        new Filter
                        {
                            Name = "ClockedState",
                            ObjectName = "Employee",
                            ObjectAlias = "Employee_1",
                            Operator = Cmf.Foundation.Common.FieldOperator.IsEqualTo,
                            Value = clockedState,
                            LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.Normal,
                        },
                        new Filter
                        {
                            LogicalOperator = Cmf.Foundation.Common.LogicalOperator.AND,
                            FilterType = Cmf.Foundation.BusinessObjects.QueryObject.Enums.FilterType.AlwaysTrue,
                            InnerFilters = filters
                        }
                    },
                Relations = new RelationCollection(),
                Fields = new FieldCollection {
                        new Field
                        {
                            Alias = "Id",
                            ObjectName = "Employee",
                            ObjectAlias = "Employee_1",
                            IsUserAttribute = false,
                            Name = "Id",
                            Position = 0,
                            Sort = Cmf.Foundation.Common.FieldSort.NoSort
                        },
                        new Field
                        {
                            Alias = "Name",
                            ObjectName = "Employee",
                            ObjectAlias = "Employee_1",
                            IsUserAttribute = false,
                            Name = "Name",
                            Position = 1,
                            Sort = Cmf.Foundation.Common.FieldSort.NoSort
                        }
                    }
            };

            DataSet result = query.Execute(false, new QueryParameterCollection());

            if (result.HasData())
            {
                foreach (DataRow row in result.Tables[0].Rows)
                {
                    IEmployee employee = _entityFactory.Create<IEmployee>();
                    employee.Name = row["Name"] as string;
                    employees.Add(employee);
                }
            }
            employees.Load();
        }

        return employees;
    }
}
