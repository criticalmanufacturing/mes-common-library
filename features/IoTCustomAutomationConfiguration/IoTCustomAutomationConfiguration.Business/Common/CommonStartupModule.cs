using Cmf.Foundation.BusinessObjects.LocalizationService;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Foundation.Services.HostStartup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common
{
    public class CommonStartupModule : IStartupModule
    {
        public MiddlewarePositioning MiddlewarePositioning => MiddlewarePositioning.None;

        public int ServiceRegistrationOrder => 0;

        public void Configure(IApplicationBuilder app, ConfigureMiddlewareContext context)
        {
        }

        public void ConfigureRootServices(IServiceCollection services)
        {
        }

        public void ConfigureServices(IServiceCollection services, ConfigureServicesContext context)
        {


            services.AddOptions();
        }
    }
}
