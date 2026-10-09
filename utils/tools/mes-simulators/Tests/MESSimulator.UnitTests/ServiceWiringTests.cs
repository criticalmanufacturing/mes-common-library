using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using Xunit;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// Builds the real service graph from the shipped simulationConfiguration.json (no MES calls are made) to catch wiring mistakes such
    /// as circular dependencies before a live run.
    /// </summary>
    public class ServiceWiringTests
    {
        private static ServiceProvider BuildServices(string lineFile = "simulationConfiguration.semi.json") =>
            new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddLogging()
                .AddMESSimulator(new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(lineFile, optional: false)
                    .Build())
                .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        [Fact]
        public void EveryPipelineServiceResolves()
        {
            using var services = BuildServices();

            Assert.NotNull(services.GetRequiredService<LineStartup>());
            Assert.NotNull(services.GetRequiredService<OrderSource>());
            Assert.NotNull(services.GetRequiredService<LotFlow>());
            Assert.NotNull(services.GetRequiredService<BatchProcessor>());
        }

        [Fact]
        public void TheShippedLineIsValid()
        {
            using var services = BuildServices();

            var line = services.GetRequiredService<IOptions<LineOptions>>().Value;

            Assert.NotEmpty(line.Steps);
        }

        [Fact]
        public void EveryActionMatchesItsRegisteredDescriptor()
        {
            using var services = BuildServices();

            var descriptors = services.GetServices<StepActionDescriptor>().ToDictionary(d => d.Key);
            var actions = services.GetServices<IStepAction>().ToList();

            Assert.Equal(descriptors.Count, actions.Count);
            Assert.All(actions, action => Assert.Equal(descriptors[action.Key].Hook, action.Hook));
        }

        [Fact]
        public void TheIndustrialLineIsValidAndDiscrete()
        {
            using var services = BuildServices("simulationConfiguration.industrial.json");

            var line = services.GetRequiredService<IOptions<LineOptions>>().Value;
            var profiles = line.Order.Profiles();

            // The finished good waits for its semi-finished goods; the line launches those (weight 0) only when it is short
            var finished = Assert.Single(profiles, p => p.Requires.Count > 0);
            Assert.Equal("Rooftop Unit - HVAC RTU", finished.Product);
            Assert.Equal("Kanban Final Assembly", finished.RequiresAtStep);
            Assert.True(finished.Weight > 0);
            Assert.All(finished.Requires.Where(r => !r.Product.StartsWith("Roof") && !r.Product.StartsWith("Cover Fan")),
                r => Assert.Contains(profiles, p => p.Product == r.Product && p.Requires.Count == 0 && p.Weight == 0));
            Assert.All(profiles, p => Assert.True(p.DiscreteProduct));
            Assert.All(profiles, p => Assert.Empty(p.WaferFlowPath));
            Assert.NotEmpty(line.Steps);
        }
    }
}
