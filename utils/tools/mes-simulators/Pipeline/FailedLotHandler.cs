using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Steps;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// What happens to a lot the simulator gives up on (<see cref="LotFlow"/> can't run it again).
    /// </summary>
    public interface IFailedLotHandler
    {
        /// <summary>Called once per lot, after the lot flow gave up on it. Never throws.</summary>
        Task HandleAsync(Material lot, string? stepName, Exception failure);
    }

    /// <summary>
    /// The fallback for extreme failures (Line:FailedLots): a lot left where it failed can hold its resource for good (a
    /// lot in process keeps the resource's BOM, setup and slots, so lots of other products are refused there), so it is
    /// aborted and terminated instead. Only lots of the simulator's own production orders; see
    /// <see cref="PreviousRunCleanup.TerminateFailedLot"/>.
    /// </summary>
    public sealed class FailedLotHandler(
        LineDefinition line,
        PreviousRunCleanup cleanup,
        IResourceOccupancyPolicy occupancy,
        SetUpLots setUpLots,
        ILogger<FailedLotHandler> logger) : IFailedLotHandler
    {
        public Task HandleAsync(Material lot, string? stepName, Exception failure)
        {
            // Whatever happens to it, the simulator no longer drives it: lots waiting for its resource stop waiting for it
            occupancy.Done(lot.Name);
            setUpLots.Release(lot.Name);

            if (!line.FailedLots.Terminate)
            {
                logger.LogWarning($"Lot '{lot.Name}' is left at {stepName ?? "?"} (Line:FailedLots:Terminate is off)");
                return Task.CompletedTask;
            }

            // The MES calls are synchronous: keep them off the caller's thread
            return Task.Run(() =>
            {
                try
                {
                    if (cleanup.TerminateFailedLot(lot.Name))
                    {
                        logger.LogWarning($"Lot '{lot.Name}' terminated at {stepName ?? "?"} so it does not hold its resource (Line:FailedLots)");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Lot '{lot.Name}' could not be terminated at {stepName ?? "?"}: {ex.Message}. Terminate it by hand, or with --terminateonstart on the next run");
                }
            });
        }
    }
}
