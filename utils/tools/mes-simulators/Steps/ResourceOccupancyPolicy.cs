using System.Collections.Concurrent;
using System.Data;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Safety valve for resources that hold one material at a time ("Line:ResourceOccupancy" in simulationConfiguration.json).
    /// </summary>
    public sealed class ResourceOccupancyOptions
    {
        public const string SectionName = "Line:ResourceOccupancy";

        /// <summary>Real time between occupancy checks; not scaled by speed (the MES calls of the lot in process are not faster).</summary>
        public TimeSpan WaitBetweenChecks { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Checks before materials in process that this run did not put there (leftovers) are aborted. Lots of this run
        /// are waited for as long as they process.
        /// </summary>
        public int MaxChecks { get; set; } = 10;

        /// <summary>
        /// How long (simulated seconds) a lot of this run may keep the resource before it is taken for a leftover (its flow
        /// failed and left it there), and the least real time that is (at a high speed, the MES calls are the slow part).
        /// </summary>
        public int OwnLotMaxWaitSeconds { get; set; } = 6 * 3600;

        public TimeSpan OwnLotMinRealWait { get; set; } = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Before a track-in on a single-material resource (e.g. an Expose stepper): we cannot tell whether the material
    /// already there is part of this run (wait for it) or a leftover (abort it), so we wait and, as a last resort, abort.
    /// </summary>
    public interface IResourceOccupancyPolicy
    {
        /// <summary>
        /// Waits until the resource is free (aborting leftovers after the last check). Returns a lease that keeps other
        /// lots of this simulator queued behind; dispose it once the lot's own track-in is done (or failed).
        /// </summary>
        Task<IDisposable> AcquireFreeResourceAsync(string resourceName, Material lot);

        /// <summary>The lot let through by <see cref="AcquireFreeResourceAsync"/> left the resource (or its flow failed).</summary>
        void Done(string lotName);
    }

    public sealed class ResourceOccupancyPolicy(
        IMesGateway mes,
        ResourceLocks locks,
        IOptions<ResourceOccupancyOptions> options,
        SimulationClock clock,
        ILogger<ResourceOccupancyPolicy> logger) : IResourceOccupancyPolicy
    {
        // Simulated time between checks while a lot of this run processes on the resource (at least 1 s real time)
        private const int ownLotCheckSeconds = 60;

        // One gate per single-material resource: only one waiting lot at a time decides to wait or abort
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.OrdinalIgnoreCase);

        // Lots of this run let onto a single-material resource and not done yet: worth waiting for, never aborted
        private readonly ConcurrentDictionary<string, byte> _ours = new(StringComparer.OrdinalIgnoreCase);

        public void Done(string lotName) => _ours.TryRemove(lotName, out _);

        public async Task<IDisposable> AcquireFreeResourceAsync(string resourceName, Material lot)
        {
            var gate = _gates.GetOrAdd(resourceName, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();

            try
            {
                var settings = options.Value;
                int ownWaits = 0;
                int maxOwnWaits = clock.PollCount(settings.OwnLotMaxWaitSeconds, ownLotCheckSeconds, settings.OwnLotMinRealWait);
                for (int check = 1; check <= settings.MaxChecks; check++)
                {
                    if (!(mes.Resources.GetByName(resourceName)?.MaterialsInProcessCount > 0))
                    {
                        break;
                    }

                    // A lot of this run is processing there: wait for it (an Expose runs ~1 h at speed 1). Only when the MES
                    // lists lots in process, and all of them are ours: an empty list is not "ours"
                    var inProcess = GetMaterialsInProcess(resourceName).Where(m => m.SystemState == MaterialSystemState.InProcess).ToList();
                    if (inProcess.Count > 0 && inProcess.All(m => _ours.ContainsKey(m.Name)))
                    {
                        if (++ownWaits <= maxOwnWaits)
                        {
                            logger.LogDebug($"'{resourceName}' busy with a lot of this run, {lot.Name} waiting");
                            await Task.Delay(clock.PollInterval(ownLotCheckSeconds));
                            check--;
                            continue;
                        }

                        // Never done (its flow failed and left it in process): not worth waiting for any more
                        logger.LogWarning($"'{resourceName}' has been busy with {string.Join(", ", inProcess.Select(m => $"'{m.Name}'"))} for too long: treating it as a leftover");
                        foreach (var material in inProcess)
                        {
                            _ours.TryRemove(material.Name, out _);
                        }
                    }

                    if (check < settings.MaxChecks)
                    {
                        logger.LogDebug($"'{resourceName}' occupied, {lot.Name} waiting {settings.WaitBetweenChecks.TotalMilliseconds}ms ({check}/{settings.MaxChecks})");
                        await Task.Delay(settings.WaitBetweenChecks);
                    }
                    else
                    {
                        await AbortMaterialsInProcessAsync(resourceName, exceptMaterialName: lot.Name);
                        if (mes.Resources.GetByName(resourceName)?.MaterialsInProcessCount > 0)
                        {
                            // The track-in will tell (and the executor waits or tries another resource)
                            logger.LogWarning($"'{resourceName}' still has materials in process after aborting the leftovers: {lot.Name} tries anyway");
                        }
                    }
                }
            }
            catch
            {
                gate.Release();
                throw;
            }

            _ours[lot.Name] = 0;
            return new GateLease(gate);
        }

        /// <summary>
        /// Aborts the process of every material in process on the resource (they go back to queued at their step).
        /// </summary>
        private async Task AbortMaterialsInProcessAsync(string resourceName, string? exceptMaterialName)
        {
            var materialsInProcess = new MaterialCollection();
            foreach (var material in GetMaterialsInProcess(resourceName))
            {
                if (material.SystemState == MaterialSystemState.InProcess && material.Name != exceptMaterialName && !_ours.ContainsKey(material.Name))
                {
                    materialsInProcess.Add(material);
                }
            }

            if (materialsInProcess.Count == 0)
            {
                return;
            }

            logger.LogInformation($"'{resourceName}' still occupied, aborting {string.Join(", ", materialsInProcess.Select(m => $"'{m.Name}'"))}");
            try
            {
                using var _ = await locks.AcquireAsync(resourceName);
                mes.Materials.AbortProcess(materialsInProcess);
            }
            catch (Exception ex) when (ex.Message.Contains("is not assigned with Resource"))
            {
                // The material left the resource meanwhile (tracked out or already aborted): nothing left to free
                logger.LogDebug($"'{resourceName}' abort skipped, material no longer on the resource: {ex.Message}");
            }
        }

        private List<Material> GetMaterialsInProcess(string resourceName)
        {
            var resource = mes.Resources.GetByName(resourceName) ?? throw new InvalidOperationException($"Resource '{resourceName}' not found");

            var dataSet = Utilities.ToDataSet(mes.MasterData.ExecuteQueryByName("GetResourceMaterialsForResourceViewMaterialsAtResource", new QueryParameterCollection()
            {
                new QueryParameter() { Name = "Material_SystemState", Value = new[] { 2 } },
                new QueryParameter() { Name = "Name", Value = null },
                new QueryParameter() { Name = "ResourceId", Value = resource.Id }
            }));

            var materials = new List<Material>();
            if (dataSet.Tables.Count > 0)
            {
                foreach (DataRow row in dataSet.Tables[0].Rows)
                {
                    var material = mes.Materials.GetByName((string)row["Name"]);
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }
            return materials;
        }

        private sealed class GateLease(SemaphoreSlim gate) : IDisposable
        {
            private int _released;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                {
                    gate.Release();
                }
            }
        }
    }
}
