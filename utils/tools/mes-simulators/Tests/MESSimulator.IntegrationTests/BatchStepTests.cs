using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MESSimulator.Mes;
using MESSimulator.Pipeline;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace MESSimulator.IntegrationTests
{
    /// <summary>
    /// Integration tests against the MES configured in appsettings.json (or the MESSIM_ variables), with the line in
    /// MESSIM_LINE_FILE (default: simulationConfiguration.semi.json). They change the MES, so they only run when
    /// MESSIM_INTEGRATION=1 (see <see cref="MesTheoryAttribute"/>). The lots must already be queued at the batch step
    /// (e.g. produced by a previous simulator run); set MESSIM_LOTS="Lot.A,Lot.B,..." to choose them.
    /// </summary>
    [Trait("Category", "Integration")]
    public class BatchStepTests
    {
        // High speed shrinks the simulated process delay to about a second
        private const decimal speed = 100m;

        private readonly ITestOutputHelper _output;
        private readonly ServiceProvider _services;

        public BatchStepTests(ITestOutputHelper output)
        {
            _output = output;
            _services = TestServices.Build(speed);
        }

        [MesTheory]
        [InlineData("PRE_CURE")]
        [InlineData("CureWafers")]
        public async Task ProcessAsync_RunsTheQueuedLotsAsABatch(string stepName)
        {
            var lots = TestServices.LoadLots(_services, TestServices.LotNamesFromEnvironment());

            var processed = await _services.GetRequiredService<BatchProcessor>().ProcessAsync(stepName, lots);

            foreach (var material in processed)
            {
                _output.WriteLine($"{material.Name} -> {material.FlowPath}");
            }

            Assert.NotEmpty(processed);
            Assert.All(processed, m => Assert.Contains(m.Name, lots.Select(l => l.Name)));
        }
    }

    /// <summary>
    /// A theory that only runs against a MES on purpose: skipped unless MESSIM_INTEGRATION=1 and MESSIM_LOTS are set, so
    /// a plain "dotnet test" of the solution never reaches (or changes) a MES.
    /// </summary>
    public sealed class MesTheoryAttribute : TheoryAttribute
    {
        public MesTheoryAttribute()
        {
            if (Environment.GetEnvironmentVariable("MESSIM_INTEGRATION") != "1")
            {
                Skip = "Integration test against a live MES: set MESSIM_INTEGRATION=1 (and MESSIM_LOTS) to run it";
            }
            else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MESSIM_LOTS")))
            {
                Skip = "Set MESSIM_LOTS to the lots queued at the batch step, e.g. MESSIM_LOTS=Lot.A,Lot.B";
            }
        }
    }

    /// <summary>
    /// Builds the simulator's services from the same sources as the console host (appsettings.json, the line file,
    /// the MESSIM_ variables).
    /// </summary>
    internal static class TestServices
    {
        public static ServiceProvider Build(decimal speed)
        {
            var lineFile = SimulatorConfiguration.ResolveLineFile(null)
                ?? Path.Combine(AppContext.BaseDirectory, "simulationConfiguration.semi.json");
            var configuration = new ConfigurationBuilder()
                .AddSimulatorSources(AppContext.BaseDirectory, lineFile,
                    new Dictionary<string, string?> { ["Simulation:Speed"] = speed.ToString(System.Globalization.CultureInfo.InvariantCulture) })
                .Build();

            return new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                .AddLogging()
                .AddMESSimulator(configuration)
                .BuildServiceProvider();
        }

        public static IReadOnlyList<string> LotNamesFromEnvironment()
        {
            var lotNames = Environment.GetEnvironmentVariable("MESSIM_LOTS")?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return lotNames is { Length: > 0 }
                ? lotNames
                : throw new InvalidOperationException("Set MESSIM_LOTS to the lots queued at the batch step, e.g. MESSIM_LOTS=Lot.A,Lot.B");
        }

        public static MaterialCollection LoadLots(IServiceProvider services, IEnumerable<string> lotNames)
        {
            var materials = services.GetRequiredService<IMaterialGateway>();
            var lots = new MaterialCollection();
            foreach (var lotName in lotNames)
            {
                lots.Add(materials.GetByName(lotName) ?? throw new InvalidOperationException($"Lot '{lotName}' not found"));
            }
            return lots;
        }
    }
}
