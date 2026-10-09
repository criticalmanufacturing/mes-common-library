using System.Collections.Concurrent;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Mes;
using MESSimulator.Steps;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Batch steps (e.g. PRE_CURE, CureWafers): a loop per step checks its queue and, once the waiting lots add up to
    /// the batch minimum, runs one batch (create, release, track in, track out) and moves its lots next.
    /// A batch that fails is cancelled (aborted first when in process); its lots go back to the queue, up to
    /// <see cref="maxBatchFailuresPerLot"/> times per lot, and a lot that can't (no longer queued, or failed too often) goes to
    /// the failed-lot fallback. Lots processed but not moved on are still handed on (their flow moves them on).
    /// </summary>
    public sealed class BatchProcessor(
        LineDefinition line,
        BatchQueues queues,
        IMesGateway mes,
        IMaterialTracker tracker,
        ResourceCatalog resources,
        ResourceLocks locks,
        IOperatorCheckIn operatorCheckIn,
        SimulationClock clock,
        IRandomSource random,
        IFailedLotHandler failedLots,
        ILogger<BatchProcessor> logger)
    {
        private const int maxBatchFailuresPerLot = 3;

        // Batch runs each lot was part of that failed
        private readonly ConcurrentDictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Runs one consumer loop per batch step until <paramref name="token"/> is cancelled; every processed lot is
        /// handed to <paramref name="onProcessed"/>.
        /// </summary>
        public Task RunLoopsAsync(Action<Material> onProcessed, CancellationToken token) =>
            Task.WhenAll(line.Batches.Keys.Select(stepName => RunLoopAsync(stepName, onProcessed, token)));

        private async Task RunLoopAsync(string stepName, Action<Material> onProcessed, CancellationToken token)
        {
            var batch = line.Batches[stepName];
            var interval = clock.PollInterval(batch.CheckIntervalSeconds);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Under-provisioned: wait until the queued lots add up to the batch minimum
                var (waitingLots, waitingQuantity) = queues.Peek(stepName);
                if (waitingQuantity < batch.MinQuantity)
                {
                    logger.LogDebug($"{stepName} has {waitingLots} lot(s) waiting ({waitingQuantity}), need at least {batch.MinQuantity}");
                    continue;
                }

                try
                {
                    foreach (var lot in await ProcessAsync(stepName, queues.TakeAll(stepName), requeueLeftovers: true))
                    {
                        onProcessed(lot);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"{stepName} batch run failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Builds one batch from <paramref name="lots"/> (oldest first, within the step's min/max) and runs it on a random
        /// resource of the step: process time, batch lifecycle, move next. Lots left out go back to the step's queue when
        /// <paramref name="requeueLeftovers"/> is set. Returns the processed lots (empty when no batch could be made).
        /// </summary>
        public async Task<List<Material>> ProcessAsync(string stepName, IEnumerable<Material> lots, bool requeueLeftovers = false)
        {
            var batch = line.Batches.TryGetValue(stepName, out var rules)
                ? rules
                : throw new InvalidOperationException($"'{stepName}' is not a batch step in the line file");

            var taken = lots.ToList();
            BatchPlan? plan = null;
            string resourceName;
            try
            {
                // Lots changed since they were queued, reload them before building the batch
                plan = BatchPlanner.Plan(tracker.Reload(taken), batch.MinQuantity, batch.MaxQuantity);

                if (requeueLeftovers && plan.Leftovers.Count > 0)
                {
                    queues.EnqueueRange(stepName, plan.Leftovers);
                }

                if (!plan.HasBatch)
                {
                    logger.LogDebug($"{stepName}: {plan.Leftovers.Sum(BatchPlanner.LotQuantity)} under the minimum {batch.MinQuantity}, waiting for more lots");
                    return [];
                }

                resourceName = resources.PickResource(stepName);
                logger.LogDebug($"Running {stepName} batch of {plan.BatchLots.Count} lot(s) ({plan.Quantity}) on '{resourceName}'" +
                    (plan.Leftovers.Count > 0 ? $", {plan.Leftovers.Count} lot(s) ({plan.Leftovers.Sum(BatchPlanner.LotQuantity)}) wait for the next batch" : ""));

                var step = line.FindStep(stepName)!;
                await Task.Delay(clock.RandomDuration(random, step.MinSeconds, step.MaxSeconds));
            }
            catch when (requeueLeftovers)
            {
                // Nothing was done to the lots yet: they wait for the next attempt instead of being lost
                // (the leftovers, when already back in the queue, are not added twice)
                queues.EnqueueRange(stepName, plan == null ? taken : plan.BatchLots);
                throw;
            }

            List<Material> processed;
            string? batchName = null;
            try
            {
                // Two batch steps can share a resource (e.g. the Koyo ovens): one batch at a time on it
                await using (await operatorCheckIn.CheckInAsync(resourceName))
                using (await locks.AcquireAsync(resourceName))
                {
                    processed = RunBatchLifecycle(BuildBatch(plan.BatchLots, resourceName), created => batchName = created);
                }
            }
            catch (Exception ex) when (requeueLeftovers)
            {
                await RecoverFailedBatchAsync(stepName, batchName, plan.BatchLots, ex);
                throw;
            }
            logger.LogInformation($"Processed {processed.Count} lot(s) in {stepName} on '{resourceName}'");
            foreach (var lot in plan.BatchLots)
            {
                _failures.TryRemove(lot.Name, out _);
            }

            if (processed.Count == 0)
            {
                return processed;
            }
            try
            {
                return tracker.MoveNext(processed) ?? processed;
            }
            catch (Exception ex)
            {
                // Processed at the batch step: handed on anyway, their flow moves a processed lot on first
                logger.LogWarning($"Moving the {stepName} batch lots on failed ({ex.Message}): their flow moves them on");
                return processed;
            }
        }

        /// <summary>
        /// A batch that failed after its lots were taken: the batch is cancelled (aborted first when in process), so it
        /// doesn't hold its lots; the lots still queued go back to the queue, unless they failed too often; any other lot
        /// (left in process...) goes to the failed-lot fallback.
        /// </summary>
        private async Task RecoverFailedBatchAsync(string stepName, string? batchName, IReadOnlyList<Material> lots, Exception failure)
        {
            if (batchName != null)
            {
                try
                {
                    if (mes.AbortAndCancel(batchName))
                    {
                        logger.LogWarning($"{stepName}: cancelled batch '{batchName}' after it failed ({failure.Message})");
                    }
                }
                catch (Exception cancel)
                {
                    logger.LogError($"{stepName}: cancelling batch '{batchName}' failed: {cancel.Message}");
                }
            }

            MaterialCollection current;
            try
            {
                current = tracker.Reload(lots);
            }
            catch (Exception reload)
            {
                logger.LogError($"{stepName}: reloading the lots of the failed batch failed ({reload.Message}): they go back to the queue");
                queues.EnqueueRange(stepName, lots);
                return;
            }

            var requeue = new List<Material>();
            foreach (var lot in current)
            {
                int failures = _failures.AddOrUpdate(lot.Name, 1, (_, n) => n + 1);
                if (lot.SystemState == MaterialSystemState.Queued && failures < maxBatchFailuresPerLot)
                {
                    requeue.Add(lot);
                    continue;
                }

                _failures.TryRemove(lot.Name, out _);
                logger.LogError($"Lot '{lot.Name}' gives up on {stepName} ({lot.SystemState}, {failures} failed batch run(s)): {failure.Message}");
                await failedLots.HandleAsync(lot, stepName, failure);
            }
            queues.EnqueueRange(stepName, requeue);
        }

        private Batch BuildBatch(IReadOnlyList<Material> lots, string resourceName)
        {
            var batchMaterials = new BatchMaterialCollection();
            for (int i = 0; i < lots.Count; i++)
            {
                batchMaterials.Add(new BatchMaterial
                {
                    Material = lots[i],
                    Step = lots[i].Step,
                    Quantity = BatchPlanner.LotQuantity(lots[i]),
                    IsMainMaterial = i == 0
                });
            }

            return new Batch
            {
                BatchMaterials = batchMaterials,
                Resource = mes.Resources.GetByName(resourceName),
                Step = batchMaterials[0].Step
            };
        }

        private List<Material> RunBatchLifecycle(Batch batch, Action<string> created)
        {
            var batches = new BatchCollection { batch };

            logger.LogDebug("Creating Batches");
            batches = mes.Batches.Create(batches);
            created(batches[0].Name);
            logger.LogInformation("Created Batches");

            // A failed release (e.g. under the MES minimum batch size), track-in or track-out cancels the batch (see
            // RecoverFailedBatchAsync), or its lots stay in it and no later batch can take them
            logger.LogDebug("Releasing Batches");
            batches = mes.Batches.Release(batches);
            logger.LogInformation("Released Batches");

            logger.LogDebug("Tracking In Batches");
            batches = mes.Batches.TrackIn(batches);
            logger.LogInformation("Tracked In Batches");

            logger.LogDebug("Tracking Out Batches");
            var materials = mes.Batches.TrackOut(batches);
            logger.LogInformation("Tracked Out Batches");

            return materials;
        }
    }
}
