using System.Collections.Concurrent;
using Cmf.Navigo.BusinessObjects;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Whether a resource can run a lot at its current step. The same MES step can need a different service in each
    /// sub-flow (e.g. INSP Overlay needs "Insp Overlay Layer AZ" in PHOTO AZ, which LEICA-001 does not offer).
    /// </summary>
    public interface IResourceEligibility
    {
        bool CanRun(Material lot, string resourceName);
    }

    /// <summary>
    /// A resource can run the lot when it offers the lot's required service (resources' services are cached).
    /// </summary>
    public sealed class ResourceServiceEligibility(IMesGateway mes) : IResourceEligibility
    {
        private readonly ConcurrentDictionary<string, HashSet<long>> _services = new(StringComparer.OrdinalIgnoreCase);

        public bool CanRun(Material lot, string resourceName)
        {
            if (lot.RequiredService == null)
            {
                return true;
            }

            return _services.GetOrAdd(resourceName, LoadServices).Contains(lot.RequiredService.Id);
        }

        private HashSet<long> LoadServices(string resourceName)
        {
            var resource = mes.Resources.GetByName(resourceName)
                ?? throw new InvalidOperationException($"Resource '{resourceName}' not found");
            var loaded = mes.MasterData.LoadRelations(resource, "ResourceService")!;
            return loaded.RelationCollection.ContainsKey("ResourceService")
                ? loaded.RelationCollection["ResourceService"].Cast<ResourceService>().Select(s => s.TargetEntity.Id).ToHashSet()
                : [];
        }
    }

    /// <summary>
    /// Thrown by a BeforeTrackIn action when the picked resource cannot serve the lot: the step tries its next
    /// resource. <see cref="Temporary"/> when it may be able to later (e.g. its shared feeder cannot be swapped while
    /// another lot is in process): when no resource can serve the lot now, the step waits and tries again.
    /// </summary>
    public sealed class ResourceUnsuitableException(string message, bool temporary = false) : InvalidOperationException(message)
    {
        public bool Temporary { get; } = temporary;
    }
}
