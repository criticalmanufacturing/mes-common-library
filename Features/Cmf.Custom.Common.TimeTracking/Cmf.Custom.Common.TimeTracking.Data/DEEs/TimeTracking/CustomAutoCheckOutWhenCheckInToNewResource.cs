using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cmf.Custom.Actions.TimeTracking
{
    public class CustomAutoCheckOutWhenCheckInToNewResource : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            ///     Automatically CheckOut an employee out of a Resource when he is Checked-In into an Exclusive Resource
            ///
            ///     Action Groups:
            ///     - LaborManagement.LaborManagementOrchestration.CheckInEmployees.Pre
            ///     - LaborManagement.LaborManagementOrchestration.SpecialCheckInEmployees.Pre
            ///
            ///     Depends On:
            ///     - N/A
            ///
            ///     Is Dependency For:
            ///     - N/A
            ///
            ///     Exceptions:
            ///     - N/A
            /// </summary>

            #endregion Info

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetService<IEntityFactory>();
            IDEEHelper deeHelper = serviceProvider.GetService<IDEEHelper>();

            IEmployee employee = entityFactory.Create<IEmployee>();
            IResource resource = entityFactory.Create<IResource>();

            if (Input.TryGetValue(nameof(SpecialCheckInEmployeesInput), out object specialCheckInInput) && specialCheckInInput is SpecialCheckInEmployeesInput)
            {
                employee = (specialCheckInInput as SpecialCheckInEmployeesInput).Employees?.FirstOrDefault().Key;
                resource = (specialCheckInInput as SpecialCheckInEmployeesInput).Employees?.FirstOrDefault().Value.ResourcesCertification.FirstOrDefault().Key;
            }
            else if (Input.TryGetValue(nameof(CheckInEmployeesInput), out object checkInInput) && checkInInput is CheckInEmployeesInput)
            {
                employee = (checkInInput as CheckInEmployeesInput).Employees?.FirstOrDefault().Key;
                resource = (checkInInput as CheckInEmployeesInput).Employees?.FirstOrDefault().Value.ResourcesCertification.FirstOrDefault().Key;
            }
            if (!employee.ObjectExists())
                return false;

            resource.LoadAttributes(new Collection<string> { "IsEmployeeExclusive" });
            if (!resource.Attributes.ContainsKey("IsEmployeeExclusive") || !(bool)resource.Attributes["IsEmployeeExclusive"])
                return false;

            deeHelper.SetContextParameter("CustomAutoCheckOutEmployee_Employee", employee);
            return true;

            //---End DEE Condition Code---
        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // Navigo
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects");

            //Foundation
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.LocalizationService");

            //Common
            UseReference("Cmf.Common.CustomActionUtilities.dll", "Cmf.Common.CustomActionUtilities.Abstractions");

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            IEntityFactory entityFactory = serviceProvider.GetRequiredService<IEntityFactory>();
            IDEEHelper deeHelper = serviceProvider.GetService<IDEEHelper>();
            ILaborOrchestration laborOrchestration = serviceProvider.GetService<ILaborOrchestration>();

            IEmployee employee = deeHelper.GetContextParameter("CustomAutoCheckOutEmployee_Employee") as IEmployee;
            employee.LoadRelations("ResourceEmployee");

            if (employee.RelationCollection.Any() && employee.RelationCollection.ContainsKey(nameof(ResourceEmployee)))
            {
                IEmployeeCollection employees = entityFactory.CreateCollection<IEmployeeCollection>();
                employees.Add(employee);
                employees.Load();
                Dictionary<IResource, IEmployeeCollection> resourceEmployeesToCheckOut = new Dictionary<IResource, IEmployeeCollection>();

                foreach (IResourceEmployee relation in employee.RelationCollection[nameof(ResourceEmployee)])
                {
                    resourceEmployeesToCheckOut.Add(relation.SourceEntity, employees);
                }
                ManageResourceEmployeesInput manageResourceEmployeesInput = new ManageResourceEmployeesInput()
                {
                    ResourceEmployeesToCheckIn = new Dictionary<IEmployee, ICheckInEmployeeParameters>(),
                    ResourceEmployeesToCheckOut = resourceEmployeesToCheckOut
                };

                laborOrchestration.ManageResourceEmployees(manageResourceEmployeesInput);
            }
            //---End DEE Code---

            return Input;
        }
    }
}