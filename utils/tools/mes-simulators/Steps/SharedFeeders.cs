using System.Collections.Concurrent;
using Cmf.Navigo.BusinessObjects;
using MESSimulator.Line;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Feeders shared by several resources (e.g. Coater Feeder-001 under both SUSS coaters): which resources a feeder
    /// belongs to, and whether any of them is busy. A shared feeder must not have its consumable swapped or replaced while
    /// a lot on any of its resources is in process or set up to track in: that lot would lose the consumable it needs.
    /// </summary>
    public sealed class SharedFeeders(IMesGateway mes, LineDefinition line, SetUpLots setUpLots)
    {
        private readonly object _lock = new();
        private readonly Dictionary<long, HashSet<string>> _parents = [];
        private readonly HashSet<string> _scanned = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The resources the feeder is a sub-resource of: among the line's resources (scanned once) and
        /// <paramref name="resourceName"/>, the resource being prepared.
        /// </summary>
        public IReadOnlyCollection<string> ParentsOf(Resource feeder, string resourceName)
        {
            Scan(resourceName);
            foreach (var configured in line.ResourcesByStep.Values.SelectMany(r => r).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Scan(configured);
            }

            lock (_lock)
            {
                return _parents.TryGetValue(feeder.Id, out var parents) ? [.. parents] : [resourceName];
            }
        }

        /// <summary>Why the feeder's consumables can't change now (a busy resource sharing it), or null when they can.</summary>
        public string? WhyBusy(Resource feeder, string resourceName, string lotName)
        {
            foreach (var parent in ParentsOf(feeder, resourceName))
            {
                if (mes.Resources.GetByName(parent)?.MaterialsInProcessCount > 0)
                {
                    return $"materials are in process on '{parent}', which shares feeder '{feeder.Name}'";
                }
                if (setUpLots.CountOthers(parent, lotName) > 0)
                {
                    return $"another lot is set up on '{parent}', which shares feeder '{feeder.Name}', and about to track in";
                }
            }
            return null;
        }

        private void Scan(string resourceName)
        {
            lock (_lock)
            {
                if (!_scanned.Add(resourceName))
                {
                    return;
                }
            }

            var resource = mes.Resources.GetByName(resourceName);
            var loaded = resource == null ? null : mes.MasterData.LoadRelations(resource, "SubResource");
            if (loaded?.RelationCollection == null || !loaded.RelationCollection.ContainsKey("SubResource"))
            {
                return;
            }

            var feeders = loaded.RelationCollection["SubResource"].Cast<SubResource>().Select(s => s.TargetEntity).Where(r => r.Type == "Feeder");
            lock (_lock)
            {
                foreach (var feeder in feeders)
                {
                    if (!_parents.TryGetValue(feeder.Id, out var parents))
                    {
                        _parents[feeder.Id] = parents = new(StringComparer.OrdinalIgnoreCase);
                    }
                    parents.Add(resourceName);
                }
            }
        }
    }
}
