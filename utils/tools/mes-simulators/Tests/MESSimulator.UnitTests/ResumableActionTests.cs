using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using MESSimulator.Steps;
using MESSimulator.Steps.Actions;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    /// <summary>Actions that run again after a failure half-way continue where they stopped.</summary>
    public class ResumableActionTests
    {
        private readonly List<string> _calls = [];

        private static Material LotWithWafers(string name, int wafers)
        {
            var lot = Lot(name, atStep: "ASHING PRE");
            lot.SubMaterials = new MaterialCollection();
            for (int i = 1; i <= wafers; i++)
            {
                lot.SubMaterials.Add(new Material { Name = $"{name}-W{i}" });
            }
            return lot;
        }

        [Fact]
        public async Task SplitTrackOut_RunAgainAfterAFailedSplit_KeepsTheLotsAlreadySplitOff()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["ASHING PRE", "COAT"]);
            tracker.FailSplitNumbers.Add(3);
            var mes = new FakeMes(_calls);
            mes.MaterialsFake.Handlers["LoadChildren"] = args => args[0];
            var action = new SplitTrackOutAction(mes, tracker, new LastValueRandom(), NullLogger<SplitTrackOutAction>.Instance);

            var lot = LotWithWafers("Lot.1", 4);
            var data = new LotRunData();
            var context = new StepContext(TestLine().FindStep("ASHING PRE")!, lot, "Gasonics-001", data);

            // 4 wafers, 4 groups: the third split fails, after two lots were split off
            await Assert.ThrowsAsync<InvalidOperationException>(() => action.ExecuteAsync(context));
            Assert.Equal(2, data.SplitChildren.Count);

            // Run again (as when the step is resumed): only the two wafers left are split, and all four lots go on
            await action.ExecuteAsync(new StepContext(context.Step, lot, "Gasonics-001", data));

            Assert.Equal(4, _calls.Count(c => c.StartsWith("split Lot.1.")));
            Assert.Equal(4, _calls.Count(c => c.StartsWith("moveNext Lot.1.")));
            Assert.Empty(data.SplitChildren);
        }

        [Fact]
        public async Task SplitTrackOut_AChildThatCantBeMovedOn_IsStillHandedOn()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["ASHING PRE", "COAT"]);
            tracker.FailMoveNextTimesFor["Lot.1.02"] = 2; // together, then on its own
            var mes = new FakeMes(_calls);
            mes.MaterialsFake.Handlers["LoadChildren"] = args => args[0];
            var action = new SplitTrackOutAction(mes, tracker, new LastValueRandom(), NullLogger<SplitTrackOutAction>.Instance);
            var context = new StepContext(TestLine().FindStep("ASHING PRE")!, LotWithWafers("Lot.1", 3), "Gasonics-001", new LotRunData());

            await action.ExecuteAsync(context);

            Assert.Equal(["Lot.1.01", "Lot.1.02", "Lot.1.03"], context.OutputLots.Select(l => l.Name));
            // Moved one by one after the failure; the one that failed stays processed at the step for its flow to move on
            Assert.Equal(MaterialSystemState.Processed, context.OutputLots[1].SystemState);
        }

        [Fact]
        public async Task PalletPack_WithoutAMaximumQuantity_FailsWithAClearMessage()
        {
            var mes = new FakeMes(_calls);
            mes.MaterialsFake.Handlers["GetMultiLevelPackingInformation"] = _ =>
                new List<MultiLevelPackingInformation> { new() { PackageProduct = new Product { Name = "Pallet" }, MaximumQuantity = null } };
            var action = new PalletPackAction(mes, new FakeTracker(_calls), NullLogger<PalletPackAction>.Instance);
            var context = new StepContext(new StepDefinition { Name = "RTU FINAL PACKAGING" }, Lot("Lot.1"), "Packer-001", new LotRunData());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => action.ExecuteAsync(context));

            Assert.Contains("no maximum quantity", ex.Message);
        }

        [Fact]
        public async Task PalletPack_LeavesTheBoxesAlreadyOnAPallet()
        {
            var mes = new FakeMes(_calls);
            var pallet = new Product { Name = "Pallet" };
            mes.MaterialsFake.Handlers["GetMultiLevelPackingInformation"] = _ =>
                new List<MultiLevelPackingInformation> { new() { PackageProduct = pallet, MaximumQuantity = 10 } };
            mes.MaterialsFake.Handlers["GetMaterialPackages"] = _ => new List<Package>
            {
                new() { Name = "Pallet-1", Product = pallet },
                new() { Name = "Box-1", Product = new Product { Name = "Box" }, ParentPackage = new Package { Name = "Pallet-1" } },
            };
            var action = new PalletPackAction(mes, new FakeTracker(_calls), NullLogger<PalletPackAction>.Instance);
            var context = new StepContext(new StepDefinition { Name = "RTU FINAL PACKAGING" }, Lot("Lot.1"), "Packer-001", new LotRunData());

            await action.ExecuteAsync(context);

            Assert.DoesNotContain("MaterialGateway.CreatePackages", _calls);
        }
    }
}
