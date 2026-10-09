using Microsoft.Extensions.Options;
using MESSimulator.Steps;

namespace MESSimulator.Line
{
    /// <summary>
    /// Checks simulationConfiguration.json at startup so a typo fails fast with a clear message instead of in the middle of a run.
    /// </summary>
    public sealed class LineOptionsValidator(IEnumerable<StepActionDescriptor> actions, IEnumerable<StepConditionDescriptor> conditions) : IValidateOptions<LineOptions>
    {
        private readonly Dictionary<string, StepActionDescriptor> _actions = actions.ToDictionary(a => a.Key, StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _conditions = conditions.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        public ValidateOptionsResult Validate(string? name, LineOptions options)
        {
            var errors = new List<string>();
            var order = options.Order;

            if (ProductionOrderNaming.Validate(order.ProductionOrderNameFormat) is { } namingError) errors.Add(namingError);
            if (order.ProductionOrderPrefix != null)
                errors.Add($"Line:Order:ProductionOrderPrefix was replaced by ProductionOrderNameFormat: use \"{order.ProductionOrderPrefix}.{ProductionOrderNaming.IdToken}\" for the same names.");
            if (options.FailedLots.Terminate && string.IsNullOrWhiteSpace(options.FailedLots.TerminateReason))
                errors.Add("Line:FailedLots:TerminateReason is required when FailedLots:Terminate is on.");
            ValidateProducts(order, errors);
            if (options.Startup.TerminatePreviousRuns && string.IsNullOrWhiteSpace(options.Startup.TerminateReason))
                errors.Add("Line:Startup:TerminateReason is required when TerminatePreviousRuns is on.");
            if (options.Startup.MaxTerminatedOrders < 1) errors.Add("Line:Startup:MaxTerminatedOrders must be >= 1.");
            if (order.LaunchIntervalSeconds <= 0) errors.Add("Line:Order:LaunchIntervalSeconds must be > 0.");
            if (order.MaxOrders < 0) errors.Add("Line:Order:MaxOrders must be >= 0 (0: no limit).");
            if (order.MaxLotsInFlight < 0) errors.Add("Line:Order:MaxLotsInFlight must be >= 0 (0: no limit).");

            if (options.Startup.CheckInOperator && string.IsNullOrWhiteSpace(options.Startup.Operator.Calendar))
                errors.Add("Line:Startup:Operator:Calendar is required when CheckInOperator is on.");

            var chaos = options.Chaos;
            foreach (var (flow, probability) in chaos.ReworkFlows)
            {
                if (probability is < 0 or > 1) errors.Add($"Line:Chaos:ReworkFlows:{flow} must be between 0 and 1.");
            }
            if (chaos.ScrapProbability is < 0 or > 1) errors.Add("Line:Chaos:ScrapProbability must be between 0 and 1.");
            if (chaos.MaxScrapFraction is <= 0 or > 1) errors.Add("Line:Chaos:MaxScrapFraction must be > 0 and <= 1.");
            if (chaos.MaxReworksPerLot < 0) errors.Add("Line:Chaos:MaxReworksPerLot must be >= 0.");
            if (chaos.ScrapProbability > 0 && chaos.ScrapReasons.Count == 0) errors.Add("Line:Chaos:ScrapReasons is required when ScrapProbability > 0.");

            if (options.Steps.Count == 0) errors.Add("Line:Steps is empty.");

            ValidateConsumableRules("Line:Consumables:Default", options.Consumables.Default, isDefault: true, errors);
            foreach (var (product, rules) in options.Consumables.Products)
            {
                ValidateConsumableRules($"Line:Consumables:Products:{product}", options.Consumables.For(product), isDefault: false, errors);
            }

            foreach (var (stepName, step) in options.Steps)
            {
                string prefix = $"Line:Steps:{stepName}";

                if (step.MinSeconds < 0 || step.MaxSeconds < step.MinSeconds)
                    errors.Add($"{prefix}: time range [{step.MinSeconds}, {step.MaxSeconds}] is invalid.");
                if (step.MinSecondsPerWafer < 0 || step.MaxSecondsPerWafer < step.MinSecondsPerWafer)
                    errors.Add($"{prefix}: per-wafer time range [{step.MinSecondsPerWafer}, {step.MaxSecondsPerWafer}] is invalid.");
                if (step.LineStepMinSeconds < 0 || step.LineStepMaxSeconds < step.LineStepMinSeconds)
                    errors.Add($"{prefix}: line step time range [{step.LineStepMinSeconds}, {step.LineStepMaxSeconds}] is invalid.");

                foreach (var actionKey in step.Actions)
                {
                    if (!_actions.ContainsKey(actionKey))
                        errors.Add($"{prefix}: unknown action '{actionKey}'. Known actions: {string.Join(", ", _actions.Keys)}.");
                }

                if (step.Actions.Count(key => _actions.TryGetValue(key, out var a) && a.Hook == StepHook.TrackOut) > 1)
                    errors.Add($"{prefix}: more than one track-out action.");

                if (!string.IsNullOrWhiteSpace(step.Condition) && !_conditions.Contains(step.Condition))
                    errors.Add($"{prefix}: unknown condition '{step.Condition}'. Known conditions: {string.Join(", ", _conditions)}.");

                if (step.PassThrough && (step.Resources.Count > 0 || step.Actions.Count > 0 || step.Condition != null || step.SingleMaterial || step.Batch != null))
                    errors.Add($"{prefix}: a pass-through step cannot have resources, actions, a condition, SingleMaterial or a batch.");

                if (step.Ship && !step.PassThrough)
                    errors.Add($"{prefix}: Ship is for pass-through steps (a processing step ships with the 'ship' action).");
                if (!step.Receive && !step.Ship)
                    errors.Add($"{prefix}: Receive=false only applies to a step that ships.");
                if (step.ClosesProductionOrder && !step.PassThrough)
                    errors.Add($"{prefix}: ClosesProductionOrder is for pass-through steps.");

                if (step.Batch != null)
                {
                    if (step.Batch.MinQuantity < 1 || step.Batch.MaxQuantity < step.Batch.MinQuantity)
                        errors.Add($"{prefix}: batch quantity range [{step.Batch.MinQuantity}, {step.Batch.MaxQuantity}] is invalid.");
                    if (step.Batch.CheckIntervalSeconds <= 0)
                        errors.Add($"{prefix}: batch CheckIntervalSeconds must be > 0.");
                    if (step.Actions.Count > 0 || step.SingleMaterial || step.Condition != null)
                        errors.Add($"{prefix}: a batch step cannot have actions, a condition or SingleMaterial.");
                }
            }

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }

        /// <summary>The products of the order (Line:Order:Products, or the single Line:Order:Product) with their resolved settings.</summary>
        private static void ValidateProducts(OrderOptions order, List<string> errors)
        {
            if (order.Products.Count == 0 && string.IsNullOrWhiteSpace(order.Product))
                errors.Add("Line:Order:Product is required (or list the products in Line:Order:Products).");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var profiles = order.Profiles();
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                string prefix = order.Products.Count > 0 ? $"Line:Order:Products:{i}" : "Line:Order";

                if (order.Products.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(p.Product)) errors.Add($"{prefix}:Product is required.");
                    else if (!names.Add(p.Product)) errors.Add($"{prefix}:Product '{p.Product}' is listed twice.");
                    if (p.Weight < 0) errors.Add($"{prefix}:Weight must be >= 0 (0: never launched).");
                }
                if (string.IsNullOrWhiteSpace(p.Facility)) errors.Add($"{prefix}:Facility is required (here or in Line:Order).");
                if (string.IsNullOrWhiteSpace(p.LotFlowPath)) errors.Add($"{prefix}:LotFlowPath is required (here or in Line:Order).");
                if (!p.DiscreteProduct && string.IsNullOrWhiteSpace(p.WaferFlowPath)) errors.Add($"{prefix}:WaferFlowPath is required for a wafer-based product (here or in Line:Order).");
                if (string.IsNullOrWhiteSpace(p.StartFlowPath)) errors.Add($"{prefix}:StartFlowPath is required (here or in Line:Order).");
                if (p.MinLotQuantity < 1 || p.MaxLotQuantity < p.MinLotQuantity)
                    errors.Add($"{prefix}: lot quantity range [{p.MinLotQuantity}, {p.MaxLotQuantity}] is invalid.");
                if (!p.DiscreteProduct && p.DiesPerWafer < 1) errors.Add($"{prefix}:DiesPerWafer must be >= 1.");
                if (p.Requires.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(p.RequiresAtStep)) errors.Add($"{prefix}:RequiresAtStep is required with Requires.");
                    foreach (var requirement in p.Requires)
                    {
                        if (string.IsNullOrWhiteSpace(requirement.Product)) errors.Add($"{prefix}:Requires: Product is required.");
                        if (requirement.QuantityPerUnit <= 0) errors.Add($"{prefix}:Requires:{requirement.Product}: QuantityPerUnit must be > 0.");
                        if (requirement.Replenish != null)
                        {
                            if (string.IsNullOrWhiteSpace(requirement.Replenish.FlowPath)) errors.Add($"{prefix}:Requires:{requirement.Product}:Replenish:FlowPath is required.");
                            if (requirement.Replenish.Quantity < requirement.QuantityPerUnit * p.MaxLotQuantity)
                                errors.Add($"{prefix}:Requires:{requirement.Product}:Replenish:Quantity must cover the largest order ({requirement.QuantityPerUnit * p.MaxLotQuantity:0.##}).");
                        }
                    }
                }
            }

            if (profiles.Count > 0 && profiles.All(p => p.Weight == 0))
                errors.Add("Line:Order:Products: at least one product needs a Weight above 0, or no order is ever launched.");
        }

        private static void ValidateConsumableRules(string prefix, ConsumableRules rules, bool isDefault, List<string> errors)
        {
            if (rules.LowQuantity is not >= 0)
                errors.Add($"{prefix}: LowQuantity must be >= 0{(isDefault ? " and set" : "")}.");
            if (rules.ReplacementQuantity is not > 0 || rules.ReplacementQuantity <= rules.LowQuantity)
                errors.Add($"{prefix}: ReplacementQuantity must be > LowQuantity.");
            if (string.IsNullOrWhiteSpace(rules.TerminateReason))
                errors.Add($"{prefix}: TerminateReason is required.");
        }
    }
}
