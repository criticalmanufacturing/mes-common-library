using Xunit;
using Microsoft.Extensions.Configuration;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// The configuration sources and their precedence: files &lt; MESSIM_ environment variables &lt; command line.
    /// </summary>
    public sealed class ConfigurationTests : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("semisim-config").FullName;
        private readonly List<string> _variables = [];

        public void Dispose()
        {
            foreach (var name in _variables)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            Directory.Delete(_directory, recursive: true);
        }

        private void SetVariable(string name, string value)
        {
            _variables.Add(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        private string Write(string name, string json)
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, json);
            return path;
        }

        private IConfigurationRoot Build(string? lineFile = null, Dictionary<string, string?>? overrides = null) =>
            new ConfigurationBuilder().AddSimulatorSources(_directory, lineFile, overrides ?? []).Build();

        [Fact]
        public void AnEnvironmentVariableOverridesTheFiles()
        {
            Write("appsettings.json", """{ "Mes": { "RetryMaxAttempts": 5 } }""");
            var line = Write("simulationConfiguration.semi.json", """{ "Line": { "Order": { "MaxOrders": 0 } } }""");
            SetVariable("MESSIM_Mes__RetryMaxAttempts", "9");
            SetVariable("MESSIM_Line__Order__MaxOrders", "20");

            var configuration = Build(line);

            Assert.Equal("9", configuration["Mes:RetryMaxAttempts"]);
            Assert.Equal("20", configuration["Line:Order:MaxOrders"]);
        }

        [Fact]
        public void TheCommandLineOverridesTheEnvironment()
        {
            Write("appsettings.json", "{}");
            var line = Write("simulationConfiguration.semi.json", "{}");
            SetVariable("MESSIM_Simulation__Speed", "50");

            var configuration = Build(line, overrides: new() { ["Simulation:Speed"] = "3" });

            Assert.Equal("3", configuration["Simulation:Speed"]);
        }

        [Fact]
        public void VariablesWithoutThePrefixAreIgnored()
        {
            Write("appsettings.json", """{ "Simulation": { "Speed": 100 } }""");
            var line = Write("simulationConfiguration.semi.json", "{}");
            SetVariable("Simulation__Speed", "1");

            Assert.Equal("100", Build(line)["Simulation:Speed"]);
        }

        [Fact]
        public void AppSettingsCanBeMissingWhenTheEnvironmentSuppliesTheValues()
        {
            var line = Write("simulationConfiguration.semi.json", "{}");
            SetVariable("MESSIM_ClientConfiguration__Connection__EnvironmentAddress", "https://mes.example");

            Assert.Equal("https://mes.example", Build(line)["ClientConfiguration:Connection:EnvironmentAddress"]);
        }

        [Fact]
        public void WithoutALineFile_ItFailsListingTheLineFilesItFinds()
        {
            Write("appsettings.json", "{}");
            Write("simulationConfiguration.semi.json", "{}");
            Write("simulationConfiguration.industrial.json", "{}");

            var ex = Assert.Throws<InvalidOperationException>(() => Build());

            Assert.Contains("--line", ex.Message);
            Assert.Contains("simulationConfiguration.industrial.json, simulationConfiguration.semi.json", ex.Message);
        }

        [Fact]
        public void ARelativeLineFileIsAlsoFoundNextToTheExecutable()
        {
            Write("appsettings.json", "{}");
            Write("simulationConfiguration.relative-test.json", """{ "Line": { "Order": { "Product": "Relative" } } }""");

            // Not in the current directory: found in the base directory (where the build copies the line files)
            Assert.Equal("Relative", Build(lineFile: "simulationConfiguration.relative-test.json")["Line:Order:Product"]);
        }

        [Fact]
        public void TheLineFileVariableIsUsedButTheArgumentWins()
        {
            Write("appsettings.json", "{}");
            var fromVariable = Write("variable.json", """{ "Line": { "Order": { "Product": "FromVariable" } } }""");
            var fromArgument = Write("argument.json", """{ "Line": { "Order": { "Product": "FromArgument" } } }""");
            SetVariable(SimulatorConfiguration.LineFileVariable, fromVariable);

            Assert.Equal("FromVariable", Build()["Line:Order:Product"]);
            Assert.Equal("FromArgument", Build(lineFile: fromArgument)["Line:Order:Product"]);
        }

        [Fact]
        public void AMissingLineFileFailsWithItsPath()
        {
            Write("appsettings.json", "{}");
            var missing = Path.Combine(_directory, "nope.json");

            var ex = Assert.Throws<FileNotFoundException>(() => Build(lineFile: missing));

            Assert.Contains("nope.json", ex.Message);
        }
    }
}
