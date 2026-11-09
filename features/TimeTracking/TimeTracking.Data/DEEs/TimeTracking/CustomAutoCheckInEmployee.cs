using Cmf.Community.TimeTracking.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cmf.Community.TimeTracking.Actions.TimeTracking
{
    public class CustomAutoCheckInEmployee : DeeDevBase
    {
        public override bool DeeTestCondition(Dictionary<string, object> Input)
        {
            //---Start DEE Condition Code---

            #region Info

            /// <summary>
            ///     Automatically CheckIn an employee into a Resource before a TrackIn
            ///
            ///     Action Groups:
            ///     - BusinessObjects.MaterialCollection.TrackIn.Pre
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
            Cmf.Community.TimeTracking.Common.Abstractions.IUtilities utilities = serviceProvider.GetService<Cmf.Community.TimeTracking.Common.Abstractions.IUtilities>();

            if (!Input.ContainsKey(Navigo.Common.Constants.Resource) || Input[Navigo.Common.Constants.Resource] is not IResource || Input[Navigo.Common.Constants.Resource] is null)
            {
                return false;
            }

            IResource resource = Input[Navigo.Common.Constants.Resource] as IResource;
            resource.LoadAttributes(new Collection<string> { CommunityConstants.ResourceAttributeIsToCheckInAtTrackIn });

            // If IsToCheckInAtTrackIn Resource Attribute is set as true, current employee must be checked in DeeActionCode
            return resource.Attributes.ContainsKey(CommunityConstants.ResourceAttributeIsToCheckInAtTrackIn) &&
                resource.Attributes[CommunityConstants.ResourceAttributeIsToCheckInAtTrackIn] is true;

            //---End DEE Condition Code---
        }

        public override Dictionary<string, object> DeeActionCode(Dictionary<string, object> Input)
        {
            //---Start DEE Code---

            // MES-Common-Library
            UseReference("Cmf.Community.TimeTracking.Common.dll", "Cmf.Community.TimeTracking.Common");

            // Navigo
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.Abstractions");
            UseReference("Cmf.Navigo.BusinessOrchestration.dll", "Cmf.Navigo.BusinessOrchestration.LaborManagement.InputObjects");

            // Foundation
            UseReference("Cmf.Foundation.Common.dll", "Cmf.Foundation.Common.LocalizationService");

            IServiceProvider serviceProvider = (IServiceProvider)Input["ServiceProvider"];
            Cmf.Community.TimeTracking.Common.Abstractions.IUtilities utilities = serviceProvider.GetService<Cmf.Community.TimeTracking.Common.Abstractions.IUtilities>();
            ILaborOrchestration laborOrchestration = serviceProvider.GetService<ILaborOrchestration>();
            ILocalizationService localizationService = serviceProvider.GetService<ILocalizationService>();
            IUtilities foundationUtilities = serviceProvider.GetRequiredService<IUtilities>();

            IResource resource = Input[Navigo.Common.Constants.Resource] as IResource;

            IEmployee currentEmployee = utilities.GetCurrentUserEmployee();
            if (!currentEmployee.ObjectExists())
            {
                throw new CommunityException(localizationService, CommunityConstants.CustomNoEmployeeForUser, foundationUtilities.DomainUserName);
            }

            //Get All Resources where employee is checked in
            IResourceCollection resourcesCheckedIn = currentEmployee.GetCheckedInResources();

            // Only check in Employee to Input Resource, if not already checked in
            if (!resourcesCheckedIn.Any(res => res.Id == resource.Id))
            {
                ICheckInEmployeeParameters checkInParameters = new CheckInEmployeeParameters
                {
                    ResourcesCertification = new Dictionary<IResource, ICertification>
                    {
                        {resource, null }
                    }
                };
                CheckInEmployeesInput checkInInput = new()
                {
                    Employees = new Dictionary<IEmployee, ICheckInEmployeeParameters>
                    {
                        {currentEmployee, checkInParameters }
                    }
                };

                laborOrchestration.CheckInEmployees(checkInInput);
            }

            //---End DEE Code---

            return Input;
        }
    }
}
