
using Cmf.Community.TimeTracking;
using Cmf.Community.TimeTracking.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Cmf.Community.TimeTracking.Orchestration
{
    public class CommunityOrchestration : ICommunityOrchestration
    {
        private const string OBJECT_TYPE_NAME = "Cmf.Community.TimeTracking.Orchestration.CommunityManagementOrchestration";
        private readonly IUtilities _utilities;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CommunityOrchestration"/> class.
        /// </summary>
        [ActivatorUtilitiesConstructor]
        public CommunityOrchestration(IUtilities utilities)
        {
            _utilities = utilities;
        }
    }
}
