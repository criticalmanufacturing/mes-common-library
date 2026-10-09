using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class OrderSourceReservationTests
    {
        [Fact]
        public async Task AnOrderThatFailsAfterReservingStock_FreesIt()
        {
            var options = TestLineOptions();
            options.Order.MaxOrders = 1;
            options.Order.LaunchIntervalSeconds = 1;
            options.Order.Products =
            [
                new ProductOptions { Product = "Rooftop Unit", Requires = [new() { Product = "VFD-10HP", QuantityPerUnit = 1 }], RequiresAtStep = "Kanban" }
            ];
            var line = new LineDefinition(Options.Create(options));

            var mes = new FakeMes();
            mes.MasterDataFake.Handlers["GetByName"] = args => new Product { Id = 1, Name = (string)args[0]! };
            mes.MaterialsFake.Handlers["StockAt"] = _ => 1000m;
            // The order itself can't be created (e.g. the MES refuses it)
            mes.MasterDataFake.Handlers["Create"] = _ => throw new InvalidOperationException("order refused");

            var tracker = new FakeTracker([]);
            var (flow, queues) = Flow(tracker);
            var source = new OrderSource(line, mes, tracker, flow, new InFlightLots(), queues, FastClock(), new FirstValueRandom(),
                NullLogger<OrderSource>.Instance);

            await source.RunAsync(CancellationToken.None);

            Assert.Contains("MaterialGateway.StockAt", mes.Calls);
            Assert.Equal(0, source.Reserved("VFD-10HP"));
        }
    }
}
