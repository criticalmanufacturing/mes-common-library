using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using MESSimulator.Pipeline;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    /// <summary>A batch that fails: cancelled, its lots requeued (a few times), then the fallback; a failed move-next.</summary>
    public class BatchProcessorTests
    {
        private readonly List<string> _calls = [];
        private readonly FakeMes _mes;
        private readonly FakeTracker _tracker;
        private readonly BatchQueues _queues;
        private readonly BatchProcessor _processor;

        public BatchProcessorTests()
        {
            _mes = new FakeMes(_calls);
            _tracker = new FakeTracker(_calls);
            _tracker.Route.AddRange(["PRE_CURE", "COAT"]);
            var line = TestLine();
            _queues = new BatchQueues(line);
            _processor = new BatchProcessor(line, _queues, _mes, _tracker, new ResourceCatalog(line, new FakeStepResources(), new FirstValueRandom()),
                new ResourceLocks(), new SilentOperatorCheckIn(), FastClock(), new FirstValueRandom(), new FakeFailedLots(_calls),
                NullLogger<BatchProcessor>.Instance);

            _mes.BatchesFake.Handlers["Create"] = _ => new BatchCollection { new Batch { Name = "BATCH-1" } };
            _mes.BatchesFake.Handlers["Release"] = args => args[0];
            _mes.MasterDataFake.Handlers["GetByName"] = args => new Batch { Name = (string)args[0]!, SystemState = BatchSystemState.InProcess };
        }

        private static List<Material> Lots() => [Lot("Lot.A", atStep: "PRE_CURE", quantity: 40), Lot("Lot.B", atStep: "PRE_CURE", quantity: 40)];

        [Fact]
        public async Task AFailedTrackIn_CancelsTheBatch_AndRequeuesItsLots()
        {
            _mes.BatchesFake.Handlers["TrackIn"] = _ => throw new InvalidOperationException("track-in refused");

            await Assert.ThrowsAsync<InvalidOperationException>(() => _processor.ProcessAsync("PRE_CURE", Lots(), requeueLeftovers: true));

            Assert.Contains("BatchGateway.Abort", _calls);
            Assert.Contains("BatchGateway.Cancel", _calls);
            Assert.Equal((2, 80), _queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task ALotInFailedBatchesThreeTimes_GoesToTheFallback()
        {
            _mes.BatchesFake.Handlers["TrackIn"] = _ => throw new InvalidOperationException("track-in refused");
            var lots = Lots();

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var batchLots = lots;
                await Assert.ThrowsAsync<InvalidOperationException>(() => _processor.ProcessAsync("PRE_CURE", batchLots, requeueLeftovers: true));
                lots = _queues.TakeAll("PRE_CURE");
                if (attempt < 3)
                {
                    Assert.Equal(2, lots.Count);
                }
            }

            Assert.Empty(lots);
            Assert.Equal(["failed Lot.A at PRE_CURE", "failed Lot.B at PRE_CURE"], _calls.Where(c => c.StartsWith("failed")).Order());
        }

        [Fact]
        public async Task ALotLeftInProcessByAFailedBatch_GoesToTheFallback()
        {
            var lots = Lots();
            lots[0].SystemState = MaterialSystemState.InProcess;
            _mes.BatchesFake.Handlers["TrackOut"] = _ => throw new InvalidOperationException("track-out failed");

            await Assert.ThrowsAsync<InvalidOperationException>(() => _processor.ProcessAsync("PRE_CURE", lots, requeueLeftovers: true));

            Assert.Equal(["failed Lot.A at PRE_CURE"], _calls.Where(c => c.StartsWith("failed")));
            Assert.Equal((1, 40), _queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task AFailedReload_PutsTheLotsBack()
        {
            _mes.BatchesFake.Handlers["Create"] = _ => throw new InvalidOperationException("never reached");
            var failing = new ThrowingReloadTracker(_tracker);
            var line = TestLine();
            var processor = new BatchProcessor(line, _queues, _mes, failing, new ResourceCatalog(line, new FakeStepResources(), new FirstValueRandom()),
                new ResourceLocks(), new SilentOperatorCheckIn(), FastClock(), new FirstValueRandom(), new FakeFailedLots(_calls),
                NullLogger<BatchProcessor>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync("PRE_CURE", Lots(), requeueLeftovers: true));

            Assert.Equal((2, 80), _queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task AFailedMoveNext_StillHandsTheProcessedLotsOn()
        {
            var lots = Lots();
            _mes.BatchesFake.Handlers["TrackIn"] = args => args[0];
            _mes.BatchesFake.Handlers["TrackOut"] = _ => lots.Select(l => { l.SystemState = MaterialSystemState.Processed; return l; }).ToList();
            _tracker.FailMoveNextTimesFor["Lot.A"] = 1;

            var processed = await _processor.ProcessAsync("PRE_CURE", lots, requeueLeftovers: true);

            Assert.Equal(["Lot.A", "Lot.B"], processed.Select(l => l.Name));
            Assert.All(processed, l => Assert.Equal(MaterialSystemState.Processed, l.SystemState));
        }

        /// <summary>A tracker whose reload of several lots fails (the MES is unreachable).</summary>
        private sealed class ThrowingReloadTracker(FakeTracker inner) : IMaterialTracker
        {
            public Material Reload(Material material) => inner.Reload(material);
            public MaterialCollection Reload(IEnumerable<Material> materials) => throw new InvalidOperationException("MES unreachable");
            public Task<Material> DispatchAndTrackInAsync(Material lot, string resourceName) => inner.DispatchAndTrackInAsync(lot, resourceName);
            public Task<Material> TrackOutAndMoveNextAsync(Material lot) => inner.TrackOutAndMoveNextAsync(lot);
            public string? ProcessingResourceName(Material lot) => inner.ProcessingResourceName(lot);
            public Task<Material> TrackOutAsync(Material lot) => inner.TrackOutAsync(lot);
            public Task<MaterialCollection> TrackInSubMaterialsAsync(MaterialCollection subMaterials, string resourceName) => inner.TrackInSubMaterialsAsync(subMaterials, resourceName);
            public Task TrackOutSubMaterialsAsync(MaterialCollection subMaterials, string resourceName) => inner.TrackOutSubMaterialsAsync(subMaterials, resourceName);
            public Task<Material> SplitAndTrackOutAsync(Material lot, IReadOnlyList<Material> subMaterials, bool isLastSplit) => inner.SplitAndTrackOutAsync(lot, subMaterials, isLastSplit);
            public Task<Material> SplitQuantityAndTrackOutAsync(Material lot, decimal quantity, bool isLastSplit) => inner.SplitQuantityAndTrackOutAsync(lot, quantity, isLastSplit);
            public List<Material>? MoveNext(IReadOnlyList<Material> lots) => inner.MoveNext(lots);
            public List<Material>? SkipAndMoveNext(IReadOnlyList<Material> lots) => inner.SkipAndMoveNext(lots);
        }
    }
}
