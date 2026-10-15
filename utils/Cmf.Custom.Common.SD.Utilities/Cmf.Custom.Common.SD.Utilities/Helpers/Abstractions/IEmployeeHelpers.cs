using Cmf.Navigo.BusinessObjects.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

/// <summary>
///     Employee Helpers
/// </summary>
public interface IEmployeeHelpers
{
    /// <summary>
    ///     If the current logged in user has an MES employee, return its employeeNumber
    /// </summary>
    /// <returns>
    ///     An employee number
    /// </returns>
    public string GetLoggedInUserEmployeeNumber();

    /// <summary>
    ///     If the current logged in user has an MES employee, return its employeeName
    /// </summary>
    /// <returns>
    ///     An employee name
    /// </returns>
    public string GetLoggedInUserEmployeeName();

    /// <summary>
    ///     If the current logged in user has an MES employee, return the employee
    /// </summary>
    /// <returns>
    ///     The current user's employee if it exists. Null if it doesn't.
    /// </returns>
    public IEmployee? GetLoggedInUserEmployee();

    /// <summary>
    ///     Gets Employee with a specific EmployeeNumbers
    /// </summary>
    /// <param name="numbers">
    ///     EmployeeNumbers to filter by
    /// </param>
    /// <returns>
    ///     A list of Employees
    /// </returns>
    public IEmployeeCollection GetEmployeesByNumber(string[] numbers, Cmf.Navigo.BusinessObjects.ClockedState clockedState);

    /// <summary>
    /// Get Resource Employees
    /// </summary>
    /// <param name="resource">The resource</param>
    public IEmployeeCollection GetResourceEmployees(IResource resource);
}
