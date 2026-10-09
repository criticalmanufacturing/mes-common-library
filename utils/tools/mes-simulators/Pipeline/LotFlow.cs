using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Steps;
using MESSimulator.Steps.Actions;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Moves a lot along the line the way the MES routes it: after every step the lot is wherever the MES moved it,
    /// and that step's definition in simulationConfiguration.json says what to do next:
    /// a batch step → the lot joins that step's batch queue;
    /// before any other step, chaos may send the lot to rework (and scrap some wafers);
    /// a pass-through step → the lot is moved to the next step, or shipped there when it is in another facility
    /// (and stays at the step at the end of its flow); at the end of the line (Wafer Shipping FE) its production order
    /// is closed once complete and the lot is shipped without being received;
    /// a step without resources, or not in simulationConfiguration.json → the lot stops there;
    /// a conditional step whose condition says no → the lot is moved to the next step without being processed;
    /// any other step → <see cref="StepExecutor"/> runs it. When a step splits the lot, each new lot continues on its own.
    /// A lot that fails and can't be run again goes to <see cref="IFailedLotHandler"/> (by default: terminated, so it
    /// doesn't hold its resource).
    /// </summary>
    public sealed class LotFlow(
        LineDefinition line,
        StepExecutor executor,
        IMaterialTracker tracker,
        ResourceCatalog resources,
        BatchQueues batchQueues,
        InFlightLots inFlight,
        IEnumerable<IStepCondition> conditions,
        IShipper shipper,
        IProductionOrderCompletion completion,
        IReworkChaos chaos,
        SimulationClock clock,
        IFailedLotHandler failedLots,
        ILogger<LotFlow> logger)
    {
        // A lot that fails while still queued at its step is run again, after 10, 20, 40 simulated minutes; one left in
        // process (it failed after its track-in) has its step finished (resumed) up to twice. Only then it goes to the
        // failed-lot fallback
        private const int maxRetriesWhileQueued = 3;
        private const int maxResumes = 2;
        private const int retryWaitSeconds = 600;

        private readonly Dictionary<string, IStepCondition> _conditions = conditions.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Runs the lot in the background (tracked, so shutdown waits for it). The task ends when the lot leaves the line,
        /// stops, or joins a batch queue.
        /// </summary>
        public Task Start(Material lot, LotRunData data, CancellationToken cancellationToken = default)
        {
            var run = RunSafeAsync(lot, data, cancellationToken);
            inFlight.Track(run);
            return run;
        }

        // Where the lot really is (the copy passed in can be steps behind)
        private string? CurrentStepOf(Material lot)
        {
            try
            {
                return LineDefinition.CurrentStepName(tracker.Reload(lot));
            }
            catch
            {
                return LineDefinition.CurrentStepName(lot);
            }
        }

        private async Task RunSafeAsync(Material lot, LotRunData data, CancellationToken cancellationToken)
        {
            int queuedRetries = 0, resumes = 0;
            StepDefinition? resume = null;
            while (true)
            {
                Exception failure;
                try
                {
                    if (resume != null)
                    {
                        var lots = await executor.ResumeAsync(resume, lot, data);
                        resume = null;
                        if (lots.Count != 1)
                        {
                            await RunChildrenAsync(lots, data, cancellationToken);
                            return;
                        }
                        lot = lots[0];
                    }
                    await RunAsync(lot, data, cancellationToken);
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation($"Lot '{lot.Name}' stopped waiting at {CurrentStepOf(lot) ?? "?"}: the simulator is stopping");
                    return;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                resume = null;
                var current = ReloadOrNull(lot);
                var stepName = current == null ? CurrentStepOf(lot) : LineDefinition.CurrentStepName(current);
                int waitSeconds;
                if (current?.SystemState == MaterialSystemState.Queued && queuedRetries < maxRetriesWhileQueued)
                {
                    // Still queued at its step: the step failed before the track-in, nothing was done to the lot, so
                    // running it again is safe
                    waitSeconds = retryWaitSeconds << queuedRetries++;
                    logger.LogWarning($"Lot '{lot.Name}' failed at {stepName ?? "?"} while still queued there ({failure.Message}): retrying in {waitSeconds / 60} simulated min ({queuedRetries}/{maxRetriesWhileQueued})");
                }
                else if (current?.SystemState == MaterialSystemState.InProcess && resumes < maxResumes
                         && stepName != null && !line.IsBatchStep(stepName) && line.FindStep(stepName) is { PassThrough: false } step)
                {
                    // Left in process (it failed after the track-in, e.g. at the track-out): finish the step rather than
                    // leave it holding its resource
                    resume = step;
                    waitSeconds = retryWaitSeconds << resumes++;
                    logger.LogWarning($"Lot '{lot.Name}' failed at {stepName} while in process there ({failure.Message}): resuming the step in {waitSeconds / 60} simulated min ({resumes}/{maxResumes})");
                }
                else
                {
                    logger.LogError($"Lot '{lot.Name}' stopped at {stepName ?? "?"}: {failure.Message}");
                    await failedLots.HandleAsync(current ?? lot, stepName, failure);
                    return;
                }

                try
                {
                    await Task.Delay(clock.PollInterval(waitSeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation($"Lot '{lot.Name}' left at {stepName}: the simulator is stopping");
                    return;
                }
                lot = current!;
            }
        }

        // Split: each new lot continues on its own, and one failing does not stop the others
        private Task RunChildrenAsync(IEnumerable<Material> lots, LotRunData data, CancellationToken cancellationToken) =>
            Task.WhenAll(lots.Select(child => RunSafeAsync(child, data.ForChild(), cancellationToken)));

        private Material? ReloadOrNull(Material lot)
        {
            try
            {
                return tracker.Reload(lot);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Runs the lot until it reaches a batch step or leaves the configured line.
        /// </summary>
        public async Task RunAsync(Material lot, LotRunData data, CancellationToken cancellationToken = default)
        {
            string? previousStep = null;
            while (true)
            {
                lot = tracker.Reload(lot);
                var stepName = LineDefinition.CurrentStepName(lot);

                // The MES did not move the lot on (e.g. end of its flow): stop instead of processing the step again
                if (stepName != null && string.Equals(stepName, previousStep, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning($"Lot '{lot.Name}' is still at {stepName} after it was processed: leaving it there");
                    return;
                }
                previousStep = stepName;

                if (stepName == null || line.FindStep(stepName) is not { } step)
                {
                    logger.LogInformation($"Lot '{lot.Name}' reached '{stepName}', which is not in the line file: leaving it there");
                    return;
                }

                // Already processed at its step (a split child or a batch lot whose move-next failed, a resumed lot): move
                // it on instead of running the step again
                if (!step.PassThrough && lot.SystemState == MaterialSystemState.Processed)
                {
                    var next = tracker.MoveNext([lot]);
                    if (next == null)
                    {
                        logger.LogInformation($"Lot '{lot.Name}' reached the end of its flow at {stepName}");
                        return;
                    }
                    logger.LogInformation($"Lot '{lot.Name}' was processed at {stepName}: moved it on");
                    lot = next.Single();
                    continue;
                }

                // Chaos: the lot may be sent to rework from here (and lose wafers); it then follows its rework flow
                if (!step.PassThrough)
                {
                    var current = chaos.MaybeRework(lot, stepName, data);
                    if (current == null)
                    {
                        // The order's last wafers may have been the ones scrapped: it can be completed now
                        completion.CloseIfComplete(lot, stepName);
                        return;
                    }
                    if (!ReferenceEquals(current, lot))
                    {
                        lot = current;
                        previousStep = null;
                        continue;
                    }
                }

                if (line.IsBatchStep(stepName))
                {
                    int waiting = batchQueues.Enqueue(stepName, lot);
                    logger.LogInformation($"Lot '{lot.Name}' queued for {stepName} ({waiting} waiting)");
                    return;
                }

                if (step.PassThrough && step.ClosesProductionOrder)
                {
                    completion.CloseIfComplete(lot, stepName);
                }

                if (step.PassThrough && step.Ship)
                {
                    lot = shipper.Ship(lot, step.ShipTo, step.Receive);
                    if (!step.Receive)
                    {
                        logger.LogInformation($"Lot '{lot.Name}' shipped from {stepName}{(step.ShipTo == null ? "" : $" to {step.ShipTo}")}: end of the line");
                        return;
                    }
                    logger.LogInformation($"Lot '{lot.Name}' shipped from {stepName} to {LineDefinition.CurrentStepName(lot)}");
                    continue;
                }

                if (step.PassThrough)
                {
                    var moved = tracker.MoveNext([lot]);
                    if (moved == null)
                    {
                        logger.LogInformation($"Lot '{lot.Name}' reached the end of its flow at {stepName}");
                        return;
                    }
                    logger.LogInformation($"Lot '{lot.Name}' passed through {stepName}");
                    lot = moved.Single();
                    continue;
                }

                if (!resources.HasResources(stepName))
                {
                    logger.LogWarning($"Lot '{lot.Name}' reached '{stepName}', which has no resources (in the line file or in the MES): leaving it there");
                    return;
                }

                if (step.Condition != null && !ConditionAllows(step, lot))
                {
                    logger.LogInformation($"Lot '{lot.Name}' skips {stepName}: condition '{step.Condition}' not met, moving it to the next step");
                    // A queued lot can't be moved next: skip the step's process first
                    lot = tracker.SkipAndMoveNext([lot])?.Single()
                        ?? throw new InvalidOperationException($"No next step to skip '{lot.Name}' past {stepName}");
                    continue;
                }

                var lots = await executor.ExecuteAsync(step, lot, data, cancellationToken);
                if (lots.Count == 1)
                {
                    lot = lots[0];
                    continue;
                }

                await RunChildrenAsync(lots, data, cancellationToken);
                return;
            }
        }

        private bool ConditionAllows(StepDefinition step, Material lot) =>
            _conditions.TryGetValue(step.Condition!, out var condition)
                ? condition.ShouldRun(lot)
                : throw new InvalidOperationException($"Step '{step.Name}' uses unknown condition '{step.Condition}'");
    }
}
