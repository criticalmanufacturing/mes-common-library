using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Steps;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Runs the simulation until the host stops (Ctrl+C / SIGTERM): prepares the MES, starts the batch steps and the
    /// order source; on stop, no new orders are created and the lots already moving finish their current step. A dry run
    /// (--dry-run) stops after the preparation. A failure is logged, recorded in <see cref="RunOutcome"/> (non-zero exit
    /// code) and stops the host.
    /// </summary>
    public sealed class SimulationHost(
        LineDefinition line,
        LineStartup startup,
        OrderSource orders,
        BatchProcessor batches,
        LotFlow flow,
        InFlightLots inFlight,
        RunOutcome outcome,
        IHostApplicationLifetime lifetime,
        ILogger<SimulationHost> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the host finish starting before the (synchronous) MES calls of the preparation
            await Task.Yield();

            try
            {
                startup.Prepare();
                if (line.Startup.DryRun)
                {
                    lifetime.StopApplication();
                    return;
                }

                var batchLoops = batches.RunLoopsAsync(lot => flow.Start(lot, new LotRunData(), stoppingToken), stoppingToken);
                await orders.RunAsync(stoppingToken);
                await batchLoops;

                if (inFlight.Count > 0)
                {
                    logger.LogInformation($"Stopping: waiting for {inFlight.Count} lot run(s) in progress to finish");
                }
                await inFlight.WhenAllAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Stopped while preparing or waiting: not a failure
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "The simulation failed: {Message}", ex.Message);
                outcome.Fail(ex);
                lifetime.StopApplication();
            }
        }
    }
}
