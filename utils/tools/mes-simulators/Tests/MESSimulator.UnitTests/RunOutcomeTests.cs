using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MESSimulator.Mes;
using MESSimulator.Pipeline;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class RunOutcomeTests
    {
        private sealed class FakeLifetime : IHostApplicationLifetime
        {
            public bool Stopped { get; private set; }
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;
            public void StopApplication() => Stopped = true;
        }

        [Fact]
        public async Task AFailedPreparation_IsRecordedAndStopsTheHost()
        {
            var mes = new FakeMes();
            mes.LaborFake.Handlers["GetCurrentUser"] = _ => throw new InvalidOperationException("MES unreachable");
            mes.MasterDataFake.Handlers["GetConfig"] = _ => throw new InvalidOperationException("MES unreachable");
            var lifetime = new FakeLifetime();

            using var services = new ServiceCollection()
                .AddLogging()
                .AddMESSimulator(new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("simulationConfiguration.semi.json", optional: false)
                    .Build())
                .AddSingleton<IMesGateway>(mes)
                .AddSingleton<IHostApplicationLifetime>(lifetime)
                .AddSingleton<SimulationHost>()
                .BuildServiceProvider();

            var host = services.GetRequiredService<SimulationHost>();
            await host.StartAsync(CancellationToken.None);
            await host.ExecuteTask!;

            var outcome = services.GetRequiredService<RunOutcome>();
            Assert.True(outcome.Failed);
            Assert.Contains("MES unreachable", outcome.Failure!.Message);
            Assert.True(lifetime.Stopped);
        }

        [Fact]
        public void OnlyTheFirstFailureIsKept()
        {
            var outcome = new RunOutcome();
            outcome.Fail(new Exception("first"));
            outcome.Fail(new Exception("second"));

            Assert.Equal("first", outcome.Failure!.Message);
        }
    }
}
