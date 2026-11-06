using Cmf.Foundation.Services.HostStartup;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;

namespace Cmf.Custom.Common.SD.Utilities;

public class UtilitiesStartupModule : IStartupModule
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
        services.AddTransient<IAttributeHelper, AttributeHelper>();
        services.AddTransient<IDataCollectionHelpers, DataCollectionHelper>();
        services.AddTransient<IDEEHelper, DEEHelper>();
        services.AddTransient<IEntityHelper, EntityHelper>();
        services.AddTransient<IERPHelper, ERPHelper>();
        services.AddTransient<IFlowHelpers, FlowHelpers>();
        services.AddTransient<IFlowSearchHelper, FlowSearchHelper>();
        services.AddTransient<IGenericHelper, GenericHelper>();
        services.AddTransient<IMaterialHelpers, MaterialHelper>();
        services.AddTransient<IResourceHelper, ResourceHelper>();
        services.AddTransient<ITableHelper, TableHelper>();
        services.AddTransient<IServiceContextUtilities, ServiceContextUtilities>();
        services.AddTransient<IAreaHelpers, AreaHelpers>();
    }
}
