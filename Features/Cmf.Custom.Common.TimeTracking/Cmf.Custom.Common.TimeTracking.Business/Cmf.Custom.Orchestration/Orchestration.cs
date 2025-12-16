
using Cmf.Custom.Common.TimeTracking.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Cmf.Custom.Common.TimeTracking.Orchestration
{
    /// <summary>
    ///     Orchestration
    /// </summary>
    public class Orchestration : IOrchestration
    {
        private const string OBJECT_TYPE_NAME = "Cmf.Custom.Common.TimeTracking.Orchestration.ManagementOrchestration";
        private readonly IUtilities _utilities;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Orchestration"/> class.
        /// </summary>
        [ActivatorUtilitiesConstructor]
        public Orchestration(IUtilities utilities)
        {
            _utilities = utilities;
        }
    }
}
