using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Pipeline;
using MESSimulator.Steps;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// Stands in for the MES tracking calls. Records every call in <see cref="Calls"/> (shared with the fake
    /// actions, so tests can assert order) and, when a <see cref="Route"/> is given, moves lots along it on
    /// track-out / move-next by rewriting their flow path, like the MES does.
    /// </summary>
    internal sealed class FakeTracker(List<string> calls) : IMaterialTracker
    {
        public List<string> Calls { get; } = calls;

        /// <summary>Step names in flow order.</summary>
        public List<string> Route { get; } = [];

        /// <summary>Lots whose track-in throws.</summary>
        public HashSet<string> FailTrackInFor { get; } = [];

        /// <summary>The track-in of the lot fails with an unexpected error this many times, then works.</summary>
        public Dictionary<string, int> FailTrackInTimesFor { get; } = new();

        /// <summary>Called at the start of every track-in with the lot and the resource, to observe the state around it.</summary>
        public Action<Material, string>? OnTrackIn { get; set; }

        /// <summary>The track-in of the lot finds the resource full this many times before it succeeds.</summary>
        public Dictionary<string, int> FullFor { get; } = new();

        /// <summary>The track-in of the lot is refused this many times because lots with another BOM are in process.</summary>
        public Dictionary<string, int> OtherBomFor { get; } = new();

        /// <summary>The track-in of the lot finds its feeder product gone (another lot swapped the feeder) this many times.</summary>
        public Dictionary<string, int> FeederSwappedFor { get; } = new();

        /// <summary>The track-in of the lot is refused this many times because lots of another product are in process.</summary>
        public Dictionary<string, int> ProductMixFor { get; } = new();

        /// <summary>The track-in of the lot is refused this many times because the operator is not checked in.</summary>
        public Dictionary<string, int> NotCheckedInFor { get; } = new();

        /// <summary>Resources that refuse every lot because lots with another BOM are in process (never tracked out).</summary>
        public HashSet<string> OtherBomAt { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The track-out (and move-next) of the lot fails this many times (the lot stays in process).</summary>
        public Dictionary<string, int> FailTrackOutTimesFor { get; } = new();

        /// <summary>The track-out of the lot fails this many times because the operator is not checked in.</summary>
        public Dictionary<string, int> NotCheckedInAtTrackOutFor { get; } = new();

        /// <summary>The move-next of the lot fails this many times.</summary>
        public Dictionary<string, int> FailMoveNextTimesFor { get; } = new();

        // Where each lot was tracked in (the resource it is in process on)
        private readonly Dictionary<string, string> _trackedInOn = new();

        public string? ProcessingResourceName(Material lot)
        {
            lock (_trackedInOn)
            {
                return _trackedInOn.GetValueOrDefault(lot.Name);
            }
        }

        private static bool Consume(Dictionary<string, int> failures, string lotName)
        {
            lock (failures)
            {
                if (failures.TryGetValue(lotName, out int left) && left > 0)
                {
                    failures[lotName] = left - 1;
                    return true;
                }
                return false;
            }
        }

        public static string FlowPathAt(string stepName) => $"FLOW:A:1/SUB:A:1/{stepName}:1";

        public void MoveToNextStep(Material lot)
        {
            var current = LineDefinition.CurrentStepName(lot);
            int index = Route.FindIndex(s => s == current);
            if (index >= 0 && index + 1 < Route.Count)
            {
                lot.FlowPath = FlowPathAt(Route[index + 1]);
            }
        }

        public Material Reload(Material material) => material;

        public MaterialCollection Reload(IEnumerable<Material> materials)
        {
            var collection = new MaterialCollection();
            collection.AddRange(materials);
            return collection;
        }

        public Task<Material> DispatchAndTrackInAsync(Material lot, string resourceName)
        {
            Record($"trackIn {lot.Name} @ {resourceName}");
            OnTrackIn?.Invoke(lot, resourceName);
            if (FailTrackInFor.Contains(lot.Name))
            {
                throw new InvalidOperationException($"track-in failed for {lot.Name}");
            }
            lock (FullFor)
            {
                if (FailTrackInTimesFor.TryGetValue(lot.Name, out int failures) && failures > 0)
                {
                    FailTrackInTimesFor[lot.Name] = failures - 1;
                    throw new InvalidOperationException($"an MES error nobody expected for {lot.Name}");
                }
                if (FullFor.TryGetValue(lot.Name, out int full) && full > 0)
                {
                    FullFor[lot.Name] = full - 1;
                    throw new InvalidOperationException($"{resourceName} reached the maximum number of concurrent materials in process");
                }
                if (ProductMixFor.TryGetValue(lot.Name, out int mix) && mix > 0)
                {
                    ProductMixFor[lot.Name] = mix - 1;
                    throw new InvalidOperationException($"The Resource {resourceName} does not allow Product mixes.");
                }
                if (FeederSwappedFor.TryGetValue(lot.Name, out int swapped) && swapped > 0)
                {
                    FeederSwappedFor[lot.Name] = swapped - 1;
                    throw new InvalidOperationException($"There was no Material of Product AZ Spray found in the Consumable Feeds of Resource {resourceName}");
                }
                if (NotCheckedInFor.TryGetValue(lot.Name, out int notCheckedIn) && notCheckedIn > 0)
                {
                    NotCheckedInFor[lot.Name] = notCheckedIn - 1;
                    throw new InvalidOperationException("Employee 'admin' is not checked in.");
                }
                if (OtherBomAt.Contains(resourceName) || OtherBomFor.TryGetValue(lot.Name, out int otherBom) && otherBom > 0)
                {
                    if (!OtherBomAt.Contains(resourceName))
                    {
                        OtherBomFor[lot.Name] = OtherBomFor[lot.Name] - 1;
                    }
                    throw new InvalidOperationException(
                        $"It's not possible to track-in the Material to Resource {resourceName} because the required Material BOM does not match the current BOM at the Resource and the Resource is configured to only accept Materials in-process with the same BOM.");
                }
            }
            lock (_trackedInOn)
            {
                _trackedInOn[lot.Name] = resourceName;
            }
            lot.SystemState = MaterialSystemState.InProcess;
            return Task.FromResult(lot);
        }

        public Task<Material> TrackOutAndMoveNextAsync(Material lot)
        {
            if (Consume(NotCheckedInAtTrackOutFor, lot.Name))
            {
                Record($"trackOut failed {lot.Name}");
                throw new InvalidOperationException("Employee 'admin' is not checked in.");
            }
            if (Consume(FailTrackOutTimesFor, lot.Name))
            {
                Record($"trackOut failed {lot.Name}");
                throw new InvalidOperationException($"track-out failed for {lot.Name}");
            }
            Record($"trackOut {lot.Name}");
            lot.SystemState = MaterialSystemState.Queued;
            MoveToNextStep(lot);
            return Task.FromResult(lot);
        }

        public Task<Material> TrackOutAsync(Material lot)
        {
            Record($"trackOut (no move) {lot.Name}");
            lot.SystemState = MaterialSystemState.Processed;
            return Task.FromResult(lot);
        }

        public Task<MaterialCollection> TrackInSubMaterialsAsync(MaterialCollection subMaterials, string resourceName) => Task.FromResult(subMaterials);

        public Task TrackOutSubMaterialsAsync(MaterialCollection subMaterials, string resourceName) => Task.CompletedTask;

        /// <summary>The split numbers (1-based, counted over the whole test) that fail once.</summary>
        public HashSet<int> FailSplitNumbers { get; } = [];

        private int _splits;

        /// <summary>
        /// Splits the wafers off into a new lot "{lot}.{n}" (processed at the step), removing them from the parent's
        /// sub-materials; the parent terminates with its last wafers.
        /// </summary>
        public Task<Material> SplitAndTrackOutAsync(Material lot, IReadOnlyList<Material> subMaterials, bool isLastSplit)
        {
            int number = Interlocked.Increment(ref _splits);
            if (FailSplitNumbers.Remove(number))
            {
                Record($"split failed {lot.Name}");
                throw new InvalidOperationException($"split {number} failed for {lot.Name}");
            }
            var child = new Material { Name = $"{lot.Name}.{number:00}", FlowPath = lot.FlowPath, SystemState = MaterialSystemState.Processed };
            foreach (var wafer in subMaterials)
            {
                lot.SubMaterials?.Remove(wafer);
            }
            if (lot.SubMaterials == null || lot.SubMaterials.Count == 0)
            {
                lot.UniversalState = Cmf.Foundation.Common.Base.UniversalState.Terminated;
            }
            Record($"split {child.Name} ({subMaterials.Count} wafer(s))");
            return Task.FromResult(child);
        }
        public Task<Material> SplitQuantityAndTrackOutAsync(Material lot, decimal quantity, bool isLastSplit) => Task.FromResult(lot);

        public List<Material>? SkipAndMoveNext(IReadOnlyList<Material> lots)
        {
            foreach (var lot in lots)
            {
                Record($"skipProcess {lot.Name}");
            }
            return MoveNext(lots);
        }

        public List<Material>? MoveNext(IReadOnlyList<Material> lots)
        {
            // Like the MES: no next step at the end of the route
            if (Route.Count > 0 && Route.FindIndex(s => s == LineDefinition.CurrentStepName(lots[0])) == Route.Count - 1)
            {
                Record($"moveNext {lots[0].Name}: end of flow");
                return null;
            }

            foreach (var lot in lots)
            {
                if (Consume(FailMoveNextTimesFor, lot.Name))
                {
                    Record($"moveNext failed {lot.Name}");
                    throw new InvalidOperationException($"move-next failed for {lot.Name}");
                }
            }
            foreach (var lot in lots)
            {
                Record($"moveNext {lot.Name}");
                lot.SystemState = MaterialSystemState.Queued;
                MoveToNextStep(lot);
            }
            return lots.ToList();
        }

        private void Record(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }
        }
    }

    /// <summary>The MES's resources per step (none unless set), counting the lookups.</summary>
    internal sealed class FakeStepResources : IStepResourceSource
    {
        public Dictionary<string, List<string>> ByStep { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Lookups { get; private set; }

        public IReadOnlyList<string> ResourcesFor(string stepName)
        {
            Lookups++;
            return ByStep.GetValueOrDefault(stepName) ?? [];
        }
    }

    /// <summary>Ships a lot to the next step of the tracker's route.</summary>
    internal sealed class FakeShipper(FakeTracker tracker) : MESSimulator.Steps.Actions.IShipper
    {
        public Material Ship(Material lot, string? destination = null, bool receive = true)
        {
            lock (tracker.Calls)
            {
                tracker.Calls.Add(receive ? $"ship {lot.Name}" : $"ship {lot.Name} (not received)");
            }
            if (receive)
            {
                tracker.MoveToNextStep(lot);
            }
            return lot;
        }
    }

    /// <summary>
    /// No chaos unless told: <see cref="ReworkAt"/> sends a lot queued at that step to the step named in the value
    /// (its rework flow), <see cref="ScrapAllAt"/> scraps all its wafers there.
    /// </summary>
    internal sealed class FakeChaos(FakeTracker tracker) : IReworkChaos
    {
        public Dictionary<string, string> ReworkAt { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ScrapAllAt { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Material? MaybeRework(Material lot, string stepName, LotRunData data)
        {
            if (ScrapAllAt.Contains(stepName))
            {
                lock (tracker.Calls)
                {
                    tracker.Calls.Add($"scrap all of {lot.Name} at {stepName}");
                }
                return null;
            }

            if (data.Reworks == 0 && ReworkAt.TryGetValue(stepName, out var reworkStep))
            {
                data.Reworks++;
                lock (tracker.Calls)
                {
                    tracker.Calls.Add($"rework {lot.Name} from {stepName} to {reworkStep}");
                }
                return new Material { Name = lot.Name, FlowPath = FakeTracker.FlowPathAt(reworkStep), PrimaryQuantity = lot.PrimaryQuantity, SubMaterialsPrimaryQuantity = lot.SubMaterialsPrimaryQuantity };
            }

            return lot;
        }
    }

    /// <summary>Records the production order checks.</summary>
    internal sealed class FakeCompletion(List<string> calls) : IProductionOrderCompletion
    {
        public void CloseIfComplete(Material lot, string stepName)
        {
            lock (calls)
            {
                calls.Add($"close order of {lot.Name} at {stepName}");
            }
        }
    }

    /// <summary>Records the lots the flow gave up on.</summary>
    internal sealed class FakeFailedLots(List<string> calls) : IFailedLotHandler
    {
        public Task HandleAsync(Material lot, string? stepName, Exception failure)
        {
            lock (calls)
            {
                calls.Add($"failed {lot.Name} at {stepName}");
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>Every resource can run every lot, except those listed in <see cref="Ineligible"/>.</summary>
    internal sealed class FakeEligibility : IResourceEligibility
    {
        public HashSet<string> Ineligible { get; } = [];

        public bool CanRun(Material lot, string resourceName) => !Ineligible.Contains(resourceName);
    }

    internal sealed class FakeAction(string key, StepHook hook, List<string> calls, Action<StepContext>? behaviour = null) : IStepAction
    {
        public string Key => key;

        public StepHook Hook => hook;

        public Task ExecuteAsync(StepContext context)
        {
            lock (calls)
            {
                calls.Add($"{key} {context.Lot.Name}");
            }
            behaviour?.Invoke(context);
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeCondition(string key, Func<Material, bool> shouldRun) : IStepCondition
    {
        public string Key => key;

        public bool ShouldRun(Material lot) => shouldRun(lot);
    }

    internal sealed class FakeOccupancy(List<string> calls) : IResourceOccupancyPolicy
    {
        public Task<IDisposable> AcquireFreeResourceAsync(string resourceName, Material lot)
        {
            lock (calls)
            {
                calls.Add($"acquire {resourceName}");
            }
            return Task.FromResult<IDisposable>(new Lease(calls, resourceName));
        }

        public void Done(string lotName)
        {
            lock (calls)
            {
                calls.Add($"done {lotName}");
            }
        }

        private sealed class Lease(List<string> calls, string resourceName) : IDisposable
        {
            public void Dispose()
            {
                lock (calls)
                {
                    calls.Add($"release {resourceName}");
                }
            }
        }
    }

    /// <summary>
    /// Records operator check-in and check-out per step in the shared call list.
    /// </summary>
    internal sealed class FakeOperatorCheckIn(List<string> calls) : IOperatorCheckIn
    {
        public void EnsureOperator() { }

        public Task<IAsyncDisposable> CheckInAsync(string resourceName)
        {
            lock (calls)
            {
                calls.Add($"checkIn operator @ {resourceName}");
            }
            return Task.FromResult<IAsyncDisposable>(new Lease(calls, resourceName));
        }

        public Task CheckInAgainAsync(string resourceName)
        {
            lock (calls)
            {
                calls.Add($"checkIn again operator @ {resourceName}");
            }
            return Task.CompletedTask;
        }

        private sealed class Lease(List<string> calls, string resourceName) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                lock (calls)
                {
                    calls.Add($"checkOut operator @ {resourceName}");
                }
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Operator check-in that does nothing (tests that do not look at it).
    /// </summary>
    internal sealed class SilentOperatorCheckIn : IOperatorCheckIn
    {
        public void EnsureOperator() { }

        public Task<IAsyncDisposable> CheckInAsync(string resourceName) => Task.FromResult<IAsyncDisposable>(new Nothing());

        public Task CheckInAgainAsync(string resourceName) => Task.CompletedTask;

        private sealed class Nothing : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Deterministic "random": always the lowest value of the range.
    /// </summary>
    internal sealed class FirstValueRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>Always the largest value: as many split groups as possible, in order.</summary>
    internal sealed class LastValueRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
    }

    internal static class StepTestFactory
    {
        /// <summary>
        /// A small line: COAT, DEVELOPER, Expose (single material), ASHING PRE, INSP CD (conditional), AOI (no
        /// resources) and the PRE_CURE batch step.
        /// </summary>
        public static LineOptions TestLineOptions(string? inspCdCondition = null) => new()
        {
            Startup = new StartupOptions { Operator = new OperatorOptions { Calendar = "KommSemi Calendar" } },
            Order = new OrderOptions
            {
                Product = "2EDN7524F",
                Facility = "Production FE SC",
                LotFlowPath = "FLOW:A:1/SUB:A:1/PREPARATION:1",
                WaferFlowPath = "KANBAN:A:1/KANBAN:1",
                StartFlowPath = "FLOW:A:1/SUB:A:1/COAT:1"
            },
            Steps = new(StringComparer.OrdinalIgnoreCase)
            {
                ["COAT"] = new() { Resources = ["SUSS Coat-001", "SUSS Coat-002"] },
                ["DEVELOPER"] = new() { Resources = ["SUSS Devs-001"] },
                ["Expose"] = new() { Resources = ["Rudolph Steppers-001"], SingleMaterial = true },
                ["ASHING PRE"] = new() { Resources = ["Mattson-001"], Actions = ["split"] },
                ["INSP CD"] = new() { Resources = ["VISTEC-001"], Condition = inspCdCondition },
                ["AOI"] = new() { Resources = [] },
                ["PRE_CURE"] = new() { Resources = ["Koyo VF-5900A"], Batch = new BatchOptions { MinQuantity = 70, MaxQuantity = 100 } },
                ["RwCOAT"] = new() { Resources = ["SUSS Coat-002"] },
                ["Wafer Shipping FE"] = new() { PassThrough = true, ClosesProductionOrder = true, Ship = true, Receive = false },
                ["Wafer Reception BE"] = new() { PassThrough = true },
                ["Wafer PACKING"] = new() { PassThrough = true },
            }
        };

        public static LineDefinition TestLine(string? inspCdCondition = null) =>
            new(Options.Create(TestLineOptions(inspCdCondition)));

        public static SimulationClock FastClock() =>
            new(Options.Create(new SimulationOptions { Speed = 1_000_000m, MinPollInterval = TimeSpan.Zero }));

        public static StepExecutor Executor(FakeTracker tracker, IEnumerable<IStepAction>? actions = null,
            IResourceOccupancyPolicy? occupancy = null, LineDefinition? line = null, IOperatorCheckIn? operatorCheckIn = null,
            IResourceEligibility? eligibility = null, SetUpLots? setUpLots = null)
        {
            var random = new FirstValueRandom();
            return new StepExecutor(
                tracker,
                occupancy ?? new FakeOccupancy(tracker.Calls),
                operatorCheckIn ?? new SilentOperatorCheckIn(),
                new ResourceCatalog(line ?? TestLine(), new FakeStepResources(), random),
                eligibility ?? new FakeEligibility(),
                FastClock(),
                random,
                actions ?? [],
                NullLogger<StepExecutor>.Instance,
                setUpLots);
        }

        public static (LotFlow Flow, BatchQueues Queues) Flow(FakeTracker tracker, IEnumerable<IStepAction>? actions = null,
            IEnumerable<IStepCondition>? conditions = null, string? inspCdCondition = null, IReworkChaos? chaos = null,
            IFailedLotHandler? failedLots = null)
        {
            var line = TestLine(inspCdCondition);
            var queues = new BatchQueues(line);
            var flow = new LotFlow(
                line,
                Executor(tracker, actions, line: line),
                tracker,
                new ResourceCatalog(line, new FakeStepResources(), new FirstValueRandom()),
                queues,
                new InFlightLots(),
                conditions ?? [],
                new FakeShipper(tracker),
                new FakeCompletion(tracker.Calls),
                chaos ?? new FakeChaos(tracker),
                FastClock(),
                failedLots ?? new FakeFailedLots(tracker.Calls),
                NullLogger<LotFlow>.Instance);
            return (flow, queues);
        }

        public static Material Lot(string name, string? atStep = null, decimal quantity = 0) => new()
        {
            Name = name,
            FlowPath = atStep == null ? null : FakeTracker.FlowPathAt(atStep),
            PrimaryQuantity = quantity
        };
    }
}
