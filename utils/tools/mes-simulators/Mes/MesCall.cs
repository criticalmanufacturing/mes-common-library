using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Single entry point for every MES (LightBusinessObjects) call: retries transient errors, bounds how many calls run
    /// at once (Mes:MaxConcurrentCalls) and measures the call from start to end, retries included.
    /// </summary>
    public interface IMesCall
    {
        /// <param name="operation">MES service name, e.g. "ComplexDispatchAndTrackInMaterials".</param>
        /// <param name="call">The LightBusinessObjects call. A retry runs it again as it is: a call that sends an entity
        /// it loaded beforehand sends the same (stale) version, so only retry calls that are safe to repeat.</param>
        /// <param name="subject">What the call acts on (lot, resource...), for the logs.</param>
        /// <param name="retry">false for calls that must not be repeated (e.g. BatchExecute: some of its inputs may have
        /// been applied before the error).</param>
        T Run<T>(string operation, Func<T> call, string? subject = null, bool retry = true);
    }

    public sealed class MesCall : IMesCall
    {
        private readonly ILogger<MesCall> _logger;
        private readonly IOptionsMonitor<DiagnosticsOptions> _diagnostics;
        private readonly MesCallStatistics _statistics;
        private readonly TimeProvider _timeProvider;
        private readonly ResiliencePipeline _retry;

        // The MES calls are synchronous and block a thread-pool thread each: bounding them keeps many lots in flight (at a
        // high speed) from starving the pool. A slot is held for one attempt, not during the retry delay.
        private readonly SemaphoreSlim _slots;

        public MesCall(ILogger<MesCall> logger, IOptions<MesOptions> options, IOptionsMonitor<DiagnosticsOptions> diagnostics,
            MesCallStatistics statistics, TimeProvider timeProvider)
        {
            _logger = logger;
            _diagnostics = diagnostics;
            _statistics = statistics;
            _timeProvider = timeProvider;

            var mesOptions = options.Value;
            _slots = new SemaphoreSlim(mesOptions.MaxConcurrentCalls, mesOptions.MaxConcurrentCalls);
            _retry = new ResiliencePipelineBuilder { TimeProvider = timeProvider }
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransient),
                    MaxRetryAttempts = mesOptions.RetryMaxAttempts,
                    Delay = mesOptions.RetryDelay,
                    BackoffType = DelayBackoffType.Linear,
                    UseJitter = false,
                    OnRetry = args =>
                    {
                        _logger.LogDebug("MES call retry {Attempt} in {DelayMs} ms after: {Error}",
                            args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
                        return default;
                    }
                })
                .Build();
        }

        /// <summary>
        /// MES concurrency errors that succeed when the same call is simply repeated.
        /// </summary>
        public static bool IsTransient(Exception ex) =>
            ex.Message.Contains("has changed since last viewed") ||
            // Same error inside a BatchExecute output, which the client fails to deserialize
            ex.Message.Contains("DataChangedSinceLastViewed") ||
            ex.Message.Contains("deadlocked on lock resources");

        public T Run<T>(string operation, Func<T> call, string? subject = null, bool retry = true)
        {
            int attempts = 0;
            long start = _timeProvider.GetTimestamp();
            T Attempt()
            {
                attempts++;
                _slots.Wait();
                try
                {
                    return call();
                }
                finally
                {
                    _slots.Release();
                }
            }

            try
            {
                var result = retry ? _retry.Execute(Attempt) : Attempt();
                Complete(operation, subject, start, attempts, succeeded: true, error: null);
                return result;
            }
            catch (Exception ex)
            {
                Complete(operation, subject, start, attempts, succeeded: false, error: ex.Message);
                throw;
            }
        }

        private void Complete(string operation, string? subject, long start, int attempts, bool succeeded, string? error)
        {
            var elapsed = _timeProvider.GetElapsedTime(start);
            _statistics.Record(operation, elapsed, succeeded);

            if (!_diagnostics.CurrentValue.LogMesCallDurations)
            {
                return;
            }

            if (succeeded)
            {
                _logger.LogInformation("MES {Operation} {Subject} took {ElapsedMs:F0} ms ({Attempts} attempt(s))",
                    operation, subject ?? "-", elapsed.TotalMilliseconds, attempts);
            }
            else
            {
                _logger.LogWarning("MES {Operation} {Subject} failed after {ElapsedMs:F0} ms ({Attempts} attempt(s)): {Error}",
                    operation, subject ?? "-", elapsed.TotalMilliseconds, attempts, error);
            }
        }
    }
}
