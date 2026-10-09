using Cmf.Navigo.BusinessObjects;

namespace MESSimulator.Steps
{
    /// <summary>
    /// One MES step of a route: where it runs, how long it takes and which behaviours it adds to the
    /// standard dispatch/track-in → process → track-out/move-next sequence.
    /// </summary>
    public sealed record StepDefinition
    {
        /// <summary>MES step name; also the key of the step's resources in <see cref="ResourceCatalog"/>.</summary>
        public required string Name { get; init; }

        /// <summary>Simulated per-lot process time range in seconds (scaled by speed).</summary>
        public int MinSeconds { get; init; } = 20;
        public int MaxSeconds { get; init; } = 60;

        /// <summary>Simulated per-wafer process time range in seconds, added for each wafer of the lot.</summary>
        public int MinSecondsPerWafer { get; init; }
        public int MaxSecondsPerWafer { get; init; }

        /// <summary>Simulated time range of each line flow step (lineFlow action).</summary>
        public int LineStepMinSeconds { get; init; } = 5;
        public int LineStepMaxSeconds { get; init; } = 15;

        /// <summary>Keys of the <see cref="IStepAction"/>s to run at this step, in order.</summary>
        public IReadOnlyList<string> Actions { get; init; } = [];

        /// <summary>Key of an <see cref="IStepCondition"/>; when it says no, the lot skips the step.</summary>
        public string? Condition { get; init; }

        /// <summary>The resource holds one material at a time: wait for it (then abort leftovers) before tracking in.</summary>
        public bool SingleMaterial { get; init; }

        /// <summary>Pass-through step: the lot is moved next without being processed.</summary>
        public bool PassThrough { get; init; }

        /// <summary>Pass-through step whose next step is in another facility: the lot is shipped and received there.</summary>
        public bool Ship { get; init; }

        /// <summary>Facility to ship to when the lot's facility has several shipping facilities.</summary>
        public string? ShipTo { get; init; }

        /// <summary>Receive the shipped lot at the next step; false ends the line (the lot is left in transit).</summary>
        public bool Receive { get; init; } = true;

        /// <summary>Close the lot's production order here once it reached its goal.</summary>
        public bool ClosesProductionOrder { get; init; }

        /// <summary>BOM products the feeders action prepares; empty: all of the step's.</summary>
        public IReadOnlyList<string> FeederProducts { get; init; } = [];

        /// <summary>Readings posted by the dataCollection action.</summary>
        public Line.DataCollectionOptions? DataCollection { get; init; }
    }

    /// <summary>
    /// When a step action runs within the step.
    /// </summary>
    public enum StepHook
    {
        /// <summary>After the queue time, before the dispatch and track-in.</summary>
        BeforeTrackIn,

        /// <summary>Right after the track-in, before the process time.</summary>
        AfterTrackIn,

        /// <summary>Instead of the standard track-out and move-next (e.g. split and track-out).</summary>
        TrackOut
    }

    /// <summary>
    /// Data a lot carries through its route (e.g. the wafers it is composed of).
    /// </summary>
    public sealed class LotRunData
    {
        public MaterialCollection? Wafers { get; init; }

        /// <summary>How many times the lot was sent to rework in this run.</summary>
        public int Reworks { get; set; }

        /// <summary>
        /// The lots a split track-out already split off this lot, kept across a retry of the step (see
        /// <see cref="StepExecutor.ResumeAsync"/>), so a split that failed half-way continues without losing them.
        /// </summary>
        public List<Material> SplitChildren { get; } = [];

        /// <summary>The run data of a lot split off this one: the reworks carry over (the lot already had them).</summary>
        public LotRunData ForChild() => new() { Reworks = Reworks };
    }

    /// <summary>
    /// State of one lot at one step, shared by the step's actions.
    /// </summary>
    public sealed class StepContext(StepDefinition step, Material lot, string resourceName, LotRunData data)
    {
        public StepDefinition Step { get; } = step;

        /// <summary>The lot as last returned by the MES; actions replace it when they change it.</summary>
        public Material Lot { get; set; } = lot;

        public string ResourceName { get; } = resourceName;

        public LotRunData Data { get; } = data;

        /// <summary>Lots leaving the step when a <see cref="StepHook.TrackOut"/> action fans out (split); empty means <see cref="Lot"/>.</summary>
        public List<Material> OutputLots { get; } = [];
    }

    /// <summary>
    /// A behaviour a step can add, referenced by <see cref="Key"/> from <see cref="StepDefinition.Actions"/>.
    /// </summary>
    public interface IStepAction
    {
        string Key { get; }
        StepHook Hook { get; }
        Task ExecuteAsync(StepContext context);

        /// <summary>
        /// Whether running it again on a lot it already (partly) ran on is safe: it continues from the lot's state. Only
        /// such AfterTrackIn actions run again when a lot left in process is resumed (<see cref="StepExecutor.ResumeAsync"/>).
        /// </summary>
        bool Resumable => false;
    }

    /// <summary>
    /// What a registered step action is, without creating it: lets simulationConfiguration.json be validated before the actions (which
    /// themselves depend on the line) are built.
    /// </summary>
    public sealed record StepActionDescriptor(string Key, StepHook Hook);

    /// <summary>
    /// A registered step condition's key, for simulationConfiguration.json validation.
    /// </summary>
    public sealed record StepConditionDescriptor(string Key);

    /// <summary>
    /// Decides whether a lot runs a conditional step, referenced by <see cref="Key"/> from <see cref="StepDefinition.Condition"/>.
    /// </summary>
    public interface IStepCondition
    {
        string Key { get; }
        bool ShouldRun(Material lot);
    }
}
