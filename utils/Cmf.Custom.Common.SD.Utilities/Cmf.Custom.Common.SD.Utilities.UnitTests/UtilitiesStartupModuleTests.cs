using Cmf.Custom.Common.SD.Utilities;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests;

public class UtilitiesStartupModuleTests
{
    [Theory]
    [InlineData(typeof(IAttributeHelper), typeof(AttributeHelper))]
    [InlineData(typeof(IDataCollectionHelpers), typeof(DataCollectionHelper))]
    [InlineData(typeof(IDEEHelper), typeof(DEEHelper))]
    [InlineData(typeof(IEntityHelper), typeof(EntityHelper))]
    [InlineData(typeof(IERPHelper), typeof(ERPHelper))]
    [InlineData(typeof(IFlowHelpers), typeof(FlowHelpers))]
    [InlineData(typeof(IFlowSearchHelper), typeof(FlowSearchHelper))]
    [InlineData(typeof(IGenericHelper), typeof(GenericHelper))]
    [InlineData(typeof(IMaterialHelpers), typeof(MaterialHelper))]
    [InlineData(typeof(IResourceHelper), typeof(ResourceHelper))]
    [InlineData(typeof(ITableHelper), typeof(TableHelper))]
    [InlineData(typeof(IServiceContextUtilities), typeof(ServiceContextUtilities))]
    [InlineData(typeof(IAreaHelpers), typeof(AreaHelpers))]
    public void ConfigureServices_RegistersMigratedServicesAsTransient(Type serviceType, Type implementationType)
    {
        var services = new ServiceCollection();
        var module = new UtilitiesStartupModule();

        module.ConfigureServices(services, null!);

        var descriptor = services.SingleOrDefault(service =>
            service.ServiceType == serviceType && service.ImplementationType == implementationType);

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }
}
