using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    /// <summary>The guards before previous runs are terminated: the maximum, the dry run and the confirmation.</summary>
    public class PreviousRunCleanupGuardTests
    {
        private sealed class FixedConfirmation(bool answer) : ITerminationConfirmation
        {
            public List<string> Questions { get; } = [];

            public bool Confirm(string question)
            {
                Questions.Add(question);
                return answer;
            }
        }

        private readonly FakeMes _mes = new();

        private (PreviousRunCleanup Cleanup, FixedConfirmation Confirmation) Cleanup(Action<StartupOptions> startup, bool answer = true, int orders = 2)
        {
            var options = TestLineOptions();
            options.Order.ProductionOrderNameFormat = "PO SIM.{id}";
            startup(options.Startup);

            var orderNames = Enumerable.Range(1, orders).Select(i => $"PO SIM.0000000{i}").Append("PO SIM.0001").ToList();
            _mes.MasterDataFake.Handlers["FindOpenProductionOrders"] = _ => orderNames;
            _mes.MasterDataFake.Handlers["GetByName"] = args => (string)args[0]! == "Full Loss" ? new Reason { Name = "Full Loss" } : new ProductionOrder { Name = (string)args[0]! };

            var confirmation = new FixedConfirmation(answer);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ClientConfiguration:Connection:EnvironmentAddress"] = "https://mes.example" })
                .Build();
            return (new PreviousRunCleanup(new LineDefinition(Options.Create(options)), _mes, new ResourceLocks(), confirmation, configuration,
                NullLogger<PreviousRunCleanup>.Instance), confirmation);
        }

        private int Terminations => _mes.Calls.Count(c => c.EndsWith(".Terminate") || c.EndsWith(".AbortProcess"));

        [Fact]
        public void MoreOrdersThanTheMaximum_StopsBeforeChangingAnything()
        {
            var (cleanup, _) = Cleanup(s => { s.MaxTerminatedOrders = 1; s.ConfirmTerminate = false; });

            var ex = Assert.Throws<InvalidOperationException>(() => cleanup.Run());

            Assert.Contains("MaxTerminatedOrders", ex.Message);
            Assert.Equal(0, Terminations);
        }

        [Fact]
        public void ADryRun_OnlyLists()
        {
            var (cleanup, confirmation) = Cleanup(s => s.DryRun = true);

            Assert.False(cleanup.Run());
            Assert.Equal(0, Terminations);
            Assert.Empty(confirmation.Questions);
        }

        [Fact]
        public void WithoutConfirmation_NothingIsTerminated()
        {
            var (cleanup, confirmation) = Cleanup(s => s.ConfirmTerminate = true, answer: false);

            Assert.False(cleanup.Run());
            Assert.Equal(0, Terminations);
            Assert.Contains("2 production order(s) named 'PO SIM.{id}' on https://mes.example", Assert.Single(confirmation.Questions));
        }

        [Fact]
        public void Confirmed_TerminatesOnlyTheOrdersMatchingTheNamingConvention()
        {
            var (cleanup, _) = Cleanup(s => s.ConfirmTerminate = true, answer: true);

            Assert.True(cleanup.Run());
            // "PO SIM.0001" doesn't match "PO SIM.{id}" (8 hex digits): only the two simulator orders
            Assert.Equal(2, _mes.Calls.Count(c => c == "MasterDataGateway.Terminate"));
        }

        [Fact]
        public void Yes_SkipsTheQuestion()
        {
            var (cleanup, confirmation) = Cleanup(s => s.ConfirmTerminate = false);

            Assert.True(cleanup.Run());
            Assert.Empty(confirmation.Questions);
        }
    }
}
