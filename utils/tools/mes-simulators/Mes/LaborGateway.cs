using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.SecurityManagement.InputObjects;
using Cmf.Foundation.Security;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Users, employees, clock-in and resource check-in/check-out.
    /// </summary>
    public interface ILaborGateway
    {
        /// <summary>The user the simulator runs as (the personal access token's user).</summary>
        User GetCurrentUser();

        /// <summary>The employee associated with the user account, or null when there is none.</summary>
        Employee? FindEmployeeByUserAccount(string userAccount);

        Employee CreateEmployee(Employee employee);
        void ClockIn(Employee employee);

        /// <summary>Employees currently checked in on the resource.</summary>
        List<Employee> GetCheckedInEmployees(Resource resource);

        void CheckIn(Employee employee, Resource resource);
        void CheckOut(Employee employee, Resource resource);
    }

    public sealed class LaborGateway(IMesCall mes) : ILaborGateway
    {
        public User GetCurrentUser() =>
            mes.Run("GetCurrentUser", () => new GetCurrentUserInput().GetCurrentUserSync()).User;

        public Employee? FindEmployeeByUserAccount(string userAccount)
        {
            try
            {
                return mes.Run("GetEmployeeByUserAccount", () => new GetEmployeeByUserAccountInput()
                {
                    UserAccount = userAccount
                }.GetEmployeeByUserAccountSync(), userAccount).Employee;
            }
            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("no Employee", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        public Employee CreateEmployee(Employee employee) =>
            mes.Run("CreateObject", () => new CreateObjectInput()
            {
                Object = employee
            }.CreateObjectSync(), $"Employee '{employee.Name}'").Object as Employee
            ?? throw new InvalidOperationException($"Employee '{employee.Name}' was not created");

        public void ClockIn(Employee employee) =>
            mes.Run("ClockInEmployees", () => new ClockInEmployeesInput()
            {
                Employees = [employee]
            }.ClockInEmployeesSync(), employee.Name);

        public List<Employee> GetCheckedInEmployees(Resource resource)
        {
            var loaded = mes.Run("GetCheckedInEmployeesForResources", () => new GetCheckedInEmployeesForResourcesInput()
            {
                Resources = [resource]
            }.GetCheckedInEmployeesForResourcesSync(), resource.Name).Resources.FirstOrDefault();

            return loaded?.RelationCollection != null && loaded.RelationCollection.ContainsKey("ResourceEmployee")
                ? loaded.RelationCollection["ResourceEmployee"].Cast<ResourceEmployee>().Select(re => re.TargetEntity).ToList()
                : [];
        }

        // Same call as the other simulators: check in on the resource without a specific certification
        public void CheckIn(Employee employee, Resource resource) =>
            mes.Run("ManageResourceEmployees(CheckIn)", () => new ManageResourceEmployeesInput()
            {
                ResourceEmployeesToCheckIn = new Dictionary<Employee, CheckInEmployeeParameters>()
                {
                    {
                        employee,
                        new CheckInEmployeeParameters()
                        {
                            ResourcesCertification = new Dictionary<Resource, Certification>() { { resource, null! } }
                        }
                    }
                }
            }.ManageResourceEmployeesSync(), $"{employee.Name} @ {resource.Name}");

        public void CheckOut(Employee employee, Resource resource) =>
            mes.Run("ManageResourceEmployees(CheckOut)", () => new ManageResourceEmployeesInput()
            {
                ResourceEmployeesToCheckOut = new Dictionary<Resource, EmployeeCollection>() { { resource, [employee] } }
            }.ManageResourceEmployeesSync(), $"{employee.Name} @ {resource.Name}");
    }
}
