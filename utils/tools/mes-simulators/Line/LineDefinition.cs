using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Options;
using MESSimulator.Steps;

namespace MESSimulator.Line
{
    /// <summary>
    /// The configured line, read once at startup: step definitions and batch rules by MES step name.
    /// </summary>
    public sealed class LineDefinition
    {
        private readonly Dictionary<string, StepDefinition> _steps = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BatchOptions> _batches = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _resources = new(StringComparer.OrdinalIgnoreCase);

        public LineDefinition(IOptions<LineOptions> options)
        {
            Order = options.Value.Order;
            Startup = options.Value.Startup;
            FailedLots = options.Value.FailedLots;
            Consumables = options.Value.Consumables;
            Chaos = options.Value.Chaos;

            foreach (var (name, step) in options.Value.Steps)
            {
                _steps[name] = new StepDefinition
                {
                    Name = name,
                    MinSeconds = step.MinSeconds,
                    MaxSeconds = step.MaxSeconds,
                    MinSecondsPerWafer = step.MinSecondsPerWafer,
                    MaxSecondsPerWafer = step.MaxSecondsPerWafer,
                    LineStepMinSeconds = step.LineStepMinSeconds,
                    LineStepMaxSeconds = step.LineStepMaxSeconds,
                    Actions = step.Actions,
                    Condition = string.IsNullOrWhiteSpace(step.Condition) ? null : step.Condition,
                    SingleMaterial = step.SingleMaterial,
                    PassThrough = step.PassThrough,
                    Ship = step.Ship,
                    ShipTo = string.IsNullOrWhiteSpace(step.ShipTo) ? null : step.ShipTo,
                    Receive = step.Receive,
                    ClosesProductionOrder = step.ClosesProductionOrder,
                    DataCollection = step.DataCollection,
                    FeederProducts = step.FeederProducts
                };
                _resources[name] = step.Resources;
                if (step.Batch != null)
                {
                    _batches[name] = step.Batch;
                }
            }
        }

        public OrderOptions Order { get; }

        public StartupOptions Startup { get; }

        public FailedLotsOptions FailedLots { get; }

        public ConsumablesOptions Consumables { get; }


        public ChaosOptions Chaos { get; }

        public IReadOnlyCollection<StepDefinition> Steps => _steps.Values;

        /// <summary>Batch steps by name.</summary>
        public IReadOnlyDictionary<string, BatchOptions> Batches => _batches;

        public IReadOnlyDictionary<string, IReadOnlyList<string>> ResourcesByStep => _resources;

        public StepDefinition? FindStep(string stepName) => _steps.GetValueOrDefault(stepName);

        public bool IsBatchStep(string stepName) => _batches.ContainsKey(stepName);

        /// <summary>
        /// The step the lot is at, from its flow path: "Y31_CSP_3L:A:1/PHOTO FUSE:A:3/ASHING PRE:1" is "ASHING PRE".
        /// Falls back to the step's name when there is no flow path.
        /// </summary>
        public static string? CurrentStepName(Material lot)
        {
            if (string.IsNullOrWhiteSpace(lot.FlowPath))
            {
                return lot.Step?.Name;
            }

            var lastSegment = lot.FlowPath[(lot.FlowPath.LastIndexOf('/') + 1)..];
            int correlation = lastSegment.IndexOf(':');
            return (correlation >= 0 ? lastSegment[..correlation] : lastSegment).Trim();
        }
    }
}
