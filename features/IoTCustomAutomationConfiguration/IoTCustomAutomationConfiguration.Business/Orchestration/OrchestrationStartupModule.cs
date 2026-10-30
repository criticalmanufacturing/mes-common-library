using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Cmf.Foundation.Services.HostStartup;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Foundation.BusinessObjects.LocalizationService;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.GenericTables;
using IServiceCollection = Microsoft.Extensions.DependencyInjection.IServiceCollection;
using Cmf.Navigo.BusinessOrchestration.SpcManagement;
using Cmf.Foundation.Common;
using System.Linq;
using System.Reflection;
using System.IO;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common;
using Cmf.Community.IoTCustomAutomationConfiguration.Actions.Common.BusinessObjects.Abstractions;

namespace Cmf.Community.IoTCustomAutomationConfiguration.Actions.Orchestration;

public class OrchestrationStartupModule : IStartupModule
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
        services.AddTransient<ICustomAutomationConfigurationOrchestration, CustomAutomationConfigurationOrchestration>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddTransient<IBOM, BOM>();
        services.AddTransient<IResourceCollection, ResourceCollection>();
        services.AddTransient<IMaterial, Material>();
        services.AddTransient<IMaterialCollection, MaterialCollection>();
        services.AddTransient<IGenericTable, GenericTable>();


        var utils = typeof(Utilities).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
        string tenant = utils.FirstOrDefault(k => k.Name.Contains("ClientTenantName")).GetValue(null)?.ToString();
        string typeName = $"Cmf.Custom.{tenant}.BusinessObjects.{CustomAutomationConfigurationConstants.CustomEntityTypeCustomAutomationConfiguration}";
        string collectionTypeName = $"{typeName}Collection";
        string assemblyPath = $"{typeName}.dll";
        if (File.Exists(assemblyPath))
        {
            var customEntityAssembly = Assembly.LoadFrom(assemblyPath);
            var customEntityType = customEntityAssembly.GetType(typeName);
            //var customInterface = customEntityAssembly.GetType($"Cmf.Custom.{tenant}.BusinessObjects.Abstractions.ICustomMaterialMovement");
            services.AddTransient(typeof(ICustomAutomationConfiguration), customEntityType);
            var customCollectionEntityType = customEntityAssembly.GetType(collectionTypeName);
            services.AddTransient(typeof(ICustomAutomationConfigurationCollection), customCollectionEntityType);
        }
        services.AddOptions();
    }


}
