using System.Text.RegularExpressions;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Mes;
using MESSimulator.Steps;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Cascade-terminates what earlier runs left behind, like the SMT/OIB simulators' --terminateonstart: every
    /// material of the simulator's production orders (lots, split lots, wafers), then the orders themselves. Only
    /// orders whose name matches Line:Order:ProductionOrderNameFormat (see <see cref="ProductionOrderNaming"/>) are
    /// touched: the MES master data can have orders with the same prefix (e.g. "PO SC.0001"). Consumables the simulator
    /// created are kept. Also terminates single lots the simulator gave up on (<see cref="TerminateFailedLot"/>).
    /// </summary>
    public sealed class PreviousRunCleanup(
        LineDefinition line,
        IMesGateway mes,
        ResourceLocks locks,
        ITerminationConfirmation confirmation,
        IConfiguration configuration,
        ILogger<PreviousRunCleanup> logger)
    {
        // How many material names a dry run lists
        private const int listedMaterials = 50;

        // Same chunk size as the other simulators
        private const int chunkSize = 100;

        // Failed lots are terminated from the lot flows, which run in parallel; the reason cache below is not thread-safe
        private readonly object _failedLotsLock = new();
        private Reason? _failedLotReason;

        /// <summary>Whether the production order was created by the simulator (its name follows the naming convention).</summary>
        public static bool IsSimulatorOrder(string? orderName, string nameFormat) => ProductionOrderNaming.Matches(orderName, nameFormat);

        /// <summary>
        /// Terminates what earlier runs left behind, after the guards: more orders than Line:Startup:MaxTerminatedOrders
        /// stops with an error, a dry run (--dry-run) only lists them, and Line:Startup:ConfirmTerminate asks first.
        /// Returns whether anything was terminated.
        /// </summary>
        public bool Run()
        {
            string format = line.Order.ProductionOrderNameFormat;
            string prefix = ProductionOrderNaming.QueryPrefix(format);
            string host = configuration["ClientConfiguration:Connection:EnvironmentAddress"] ?? "the MES";

            var orderNames = mes.MasterData.FindOpenProductionOrders(prefix).Where(o => IsSimulatorOrder(o, format)).ToList();
            var materialNames = OpenMaterials(format);
            string summary = $"{materialNames.Count} material(s) of {orderNames.Count} production order(s) named '{format}' on {host}";

            if (orderNames.Count == 0 && materialNames.Count == 0)
            {
                logger.LogInformation($"No previous runs to terminate (no open production order named '{format}' on {host})");
                return false;
            }
            if (orderNames.Count > line.Startup.MaxTerminatedOrders)
            {
                throw new InvalidOperationException($"Refusing to terminate previous runs: {summary}, more than Line:Startup:MaxTerminatedOrders " +
                    $"({line.Startup.MaxTerminatedOrders}). Check that '{format}' only matches the simulator's orders, then raise the limit.");
            }
            if (line.Startup.DryRun)
            {
                logger.LogWarning($"Dry run: would terminate {summary}. Orders: {string.Join(", ", orderNames)}. Materials: " +
                    string.Join(", ", materialNames.Take(listedMaterials)) + (materialNames.Count > listedMaterials ? $", ... ({materialNames.Count - listedMaterials} more)" : ""));
                return false;
            }
            if (line.Startup.ConfirmTerminate && !confirmation.Confirm($"Terminate {summary}?"))
            {
                logger.LogWarning("Terminating previous runs was not confirmed: they are left as they are");
                return false;
            }

            var reason = mes.MasterData.GetByName<Reason>(line.Startup.TerminateReason)
                ?? throw new InvalidOperationException($"Reason '{line.Startup.TerminateReason}' (Line:Startup:TerminateReason) not found");
            logger.LogInformation($"Terminating previous runs: {summary}");

            if (materialNames.Count > 0)
            {
                var materials = Load(materialNames);

                // A material in process cannot be terminated: abort it first
                AbortInProcess(materials);

                // Parents first: terminating a lot may take its wafers along; whatever is left goes next
                TerminateChunks(Load(materials.Where(m => m.ParentMaterial == null).Select(m => m.Name)), reason);
                TerminateChunks(Load(OpenMaterials(format)), reason);

                // Anything still in process (an abort that failed, a lot that started again meanwhile): once more
                var remaining = Load(OpenMaterials(format));
                if (remaining.Any(m => m.SystemState == MaterialSystemState.InProcess))
                {
                    logger.LogWarning($"{remaining.Count(m => m.SystemState == MaterialSystemState.InProcess)} material(s) still in process after the first pass: aborting them again");
                    AbortInProcess(remaining);
                    TerminateChunks(Load(OpenMaterials(format)), reason);
                }

                int left = OpenMaterials(format).Count;
                if (left > 0)
                {
                    logger.LogWarning($"{left} material(s) of the simulator's production orders could not be terminated");
                }
            }

            foreach (var orderName in orderNames)
            {
                try
                {
                    var order = mes.MasterData.GetByName<ProductionOrder>(orderName);
                    if (order != null)
                    {
                        mes.MasterData.Terminate(order);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Could not terminate production order '{orderName}': {ex.Message}");
                }
            }
            logger.LogInformation($"Terminated previous runs ({orderNames.Count} production order(s))");
            return true;
        }

        /// <summary>
        /// The materials to abort: those in process that no in-process material holds (aborting a lot also aborts its
        /// wafers). A wafer in process whose lot is not in process (a lot that already moved on) is aborted on its own.
        /// </summary>
        public static List<Material> AbortTargets(IReadOnlyCollection<Material> materials)
        {
            var inProcess = materials.Where(m => m.SystemState == MaterialSystemState.InProcess).ToList();
            var names = inProcess.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return inProcess.Where(m => m.ParentMaterial == null || !names.Contains(m.ParentMaterial.Name)).ToList();
        }

        private void AbortInProcess(IReadOnlyCollection<Material> materials)
        {
            var targets = AbortTargets(materials);
            logger.LogInformation($"Aborting {targets.Count} material(s) in process");
            foreach (var material in targets)
            {
                try
                {
                    // The lot flows track in and out under the same per-resource lock
                    using var _ = material.LastProcessedResource?.Name is { Length: > 0 } resourceName ? locks.Acquire(resourceName) : null;
                    mes.Materials.AbortProcess([material]);
                    logger.LogDebug($"Aborted '{material.Name}' at {material.FlowPath}");
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Could not abort '{material.Name}': {ex.Message}");
                }
            }
        }

        /// <summary>The blocking batches an MES termination error names: "The Objects are BATCH-26-31 (Batch)."</summary>
        public static List<string> BlockingBatches(string message) =>
            Regex.Matches(message, @"([^\s,]+) \(Batch\)").Select(m => m.Groups[1].Value).Distinct().ToList();

        /// <summary>
        /// Terminates one material, repairing what the MES says is in the way (up to twice): a loss reason the step does not
        /// accept (another one the step accepts, or, when the step has none, the lot moves to the start step first), or a
        /// batch that still holds the lot (the batch is cancelled).
        /// </summary>
        private void TerminateOne(string materialName, Reason reason)
        {
            for (int attempt = 0; ; attempt++)
            {
                var material = mes.Materials.GetByName(materialName)!;
                try
                {
                    mes.Materials.Terminate(material, reason);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt >= 2 || !Repair(material, ex.Message, ref reason))
                    {
                        throw;
                    }
                }
            }
        }

        private bool Repair(Material material, string message, ref Reason reason)
        {
            if (message.Contains("must have Loss Reasons defined", StringComparison.OrdinalIgnoreCase) && material.Step != null)
            {
                var accepted = TerminateReasonsOf(material.Step);
                if (accepted.Count > 0)
                {
                    reason = accepted.FirstOrDefault(r => r.Name.Equals(line.Startup.TerminateReason, StringComparison.OrdinalIgnoreCase)) ?? accepted[0];
                    logger.LogDebug($"'{material.Name}' is at '{material.Step.Name}', which terminates with '{reason.Name}'");
                    return true;
                }

                // A step without terminate reasons (e.g. PREPARATION WPREP): from the start step on, the lot can be terminated
                var profiles = line.Order.Profiles();
                var start = (profiles.FirstOrDefault(p => p.Product == material.Product?.Name) ?? profiles[0]).StartFlowPath;
                mes.Materials.MoveToNextStep(material, start);
                logger.LogDebug($"'{material.Name}' moved from '{material.Step.Name}' to '{start}' to be terminated");
                return true;
            }

            var batches = BlockingBatches(message);
            foreach (var name in batches)
            {
                if (mes.AbortAndCancel(name))
                {
                    logger.LogDebug($"Cancelled batch '{name}' that held simulator lots");
                }
            }
            return batches.Count > 0;
        }

        private readonly Dictionary<long, List<Reason>> _terminateReasons = [];

        /// <summary>The reasons a step can terminate a material with (its StepReason relations applicable to terminate).</summary>
        private List<Reason> TerminateReasonsOf(Step step)
        {
            if (!_terminateReasons.TryGetValue(step.Id, out var reasons))
            {
                var loaded = mes.MasterData.LoadRelations(mes.MasterData.GetById<Step>(step.Id)!, "StepReason")!;
                reasons = loaded.RelationCollection.ContainsKey("StepReason")
                    ? loaded.RelationCollection["StepReason"].Cast<StepReason>()
                        .Where(r => r.ApplicableToTerminate == true)
                        .Select(r => mes.MasterData.GetById<Reason>(r.TargetEntity.Id)!).ToList()
                    : [];
                _terminateReasons[step.Id] = reasons;
            }
            return reasons;
        }

        private List<string> OpenMaterials(string format) =>
            mes.Materials.FindOpenByProductionOrder(ProductionOrderNaming.QueryPrefix(format))
                .Where(m => IsSimulatorOrder(m.ProductionOrder, format)).Select(m => m.Material).ToList();

        /// <summary>
        /// The fallback for a lot the simulator gave up on (Line:FailedLots): aborts it (and its wafers) when in process,
        /// so it stops holding its resource, and terminates it; a batch that still holds it is cancelled. Only a lot of
        /// one of the simulator's own production orders (matching the naming convention) is touched. Returns whether
        /// the lot was terminated; throws when the MES refuses.
        /// </summary>
        public bool TerminateFailedLot(string lotName)
        {
            lock (_failedLotsLock)
            {
                string format = line.Order.ProductionOrderNameFormat;
                var open = mes.Materials.FindOpenByProductionOrder(ProductionOrderNaming.QueryPrefix(format));
                var owner = open.FirstOrDefault(m => string.Equals(m.Material, lotName, StringComparison.OrdinalIgnoreCase));
                if (owner.Material == null)
                {
                    logger.LogWarning($"Lot '{lotName}' is not an open material of a production order named '{format}': not terminating it");
                    return false;
                }
                if (!IsSimulatorOrder(owner.ProductionOrder, format))
                {
                    logger.LogWarning($"Lot '{lotName}' belongs to production order '{owner.ProductionOrder}', which is not named '{format}': not terminating it");
                    return false;
                }

                _failedLotReason ??= mes.MasterData.GetByName<Reason>(line.FailedLots.TerminateReason)
                    ?? throw new InvalidOperationException($"Reason '{line.FailedLots.TerminateReason}' (Line:FailedLots:TerminateReason) not found");

                // The lot and its wafers (not the other lots of the same order, e.g. its split siblings)
                var materials = Load(open.Where(m => m.ProductionOrder == owner.ProductionOrder).Select(m => m.Material))
                    .Where(m => string.Equals(m.Name, lotName, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(m.ParentMaterial?.Name, lotName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                AbortInProcess(materials);

                TerminateOne(lotName, _failedLotReason);

                // Terminating the lot usually takes its wafers along; whatever is left goes too
                var wafers = materials.Where(m => m.ParentMaterial != null).Select(m => m.Name).ToList();
                if (wafers.Count > 0)
                {
                    TerminateChunks(Load(wafers), _failedLotReason);
                }
                return true;
            }
        }

        private List<Material> Load(IEnumerable<string> names) =>
            names.Chunk(chunkSize).SelectMany(chunk => mes.Materials.GetByNames(chunk)).ToList();

        private void TerminateChunks(List<Material> materials, Reason reason)
        {
            foreach (var chunk in materials.Where(m => m.UniversalState != Cmf.Foundation.Common.Base.UniversalState.Terminated).Chunk(chunkSize))
            {
                var collection = new MaterialCollection();
                collection.AddRange(chunk);
                try
                {
                    mes.Materials.Terminate(collection, reason);
                    logger.LogDebug($"Terminated {chunk.Length} material(s)");
                }
                catch (Exception ex)
                {
                    // One bad material fails the whole call: retry one by one so the rest still goes
                    logger.LogDebug($"Terminating {chunk.Length} material(s) together failed ({ex.Message}); retrying one by one");
                    foreach (var material in chunk)
                    {
                        try
                        {
                            TerminateOne(material.Name, reason);
                        }
                        catch (Exception single)
                        {
                            logger.LogDebug($"Could not terminate '{material.Name}': {single.Message}");
                        }
                    }
                }
            }
        }
    }
}
