using Xunit;

namespace MESSimulator.UnitTests
{
    public class ArgumentTests
    {
        private static Dictionary<string, string?> Parse(params string[] args)
        {
            Assert.True(Program.TryParseArguments(args, out var overrides, out _, out var error), error);
            return overrides;
        }

        [Fact]
        public void TerminateOnStart_AloneMeansTrue()
        {
            Assert.Equal("true", Parse("--terminateonstart", "--speed", "10")["Line:Startup:TerminatePreviousRuns"]);
        }

        [Theory]
        [InlineData("true", "true")]
        [InlineData("false", "false")]
        [InlineData("False", "false")]
        public void TerminateOnStart_TakesAnOptionalValue(string value, string expected)
        {
            Assert.Equal(expected, Parse("--terminateOnStart", value, "--speed", "10")["Line:Startup:TerminatePreviousRuns"]);
        }

        [Fact]
        public void Yes_TurnsTheConfirmationOff_AndDryRunIsOn()
        {
            var overrides = Parse("--yes", "--dry-run");

            Assert.Equal("false", overrides["Line:Startup:ConfirmTerminate"]);
            Assert.Equal("true", overrides["Line:Startup:DryRun"]);
        }

        [Fact]
        public void TheLineFileIsReturned()
        {
            Assert.True(Program.TryParseArguments(["-l", "simulationConfiguration.semi.json"], out _, out var lineFile, out _));
            Assert.Equal("simulationConfiguration.semi.json", lineFile);
        }

        [Theory]
        [InlineData("--bogus")]
        [InlineData("--speed", "0")]
        [InlineData("--line")]
        public void BadArguments_AreRefusedWithAReason(params string[] args)
        {
            Assert.False(Program.TryParseArguments(args, out _, out _, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }
    }
}
