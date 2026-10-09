using System.Collections.Concurrent;
using Cmf.Navigo.BusinessObjects;
using MESSimulator.Line;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// The resources that can run an MES step.
    /// </summary>
    public interface IStepResourceSource
    {
        IReadOnlyList<string> ResourcesFor(string stepName);
    }

    /// <summary>
    /// The step's resources as the MES has them (GetResourcesForStep, like the Step view in the UI).
    /// </summary>
    public sealed class MesStepResourceSource(IMesGateway mes) : IStepResourceSource
    {
        public IReadOnlyList<string> ResourcesFor(string stepName)
        {
            var step = mes.MasterData.GetByName<Step>(stepName);
            return step == null ? [] : mes.Setup.GetResourcesForStep(step).Select(r => r.Name).ToList();
        }
    }

    /// <summary>
    /// The resources available at each step: the ones listed in simulationConfiguration.json when there are any (to restrict a step to
    /// some resources), otherwise the step's resources in the MES (looked up once per step).
    /// </summary>
    public sealed class ResourceCatalog(LineDefinition line, IStepResourceSource source, IRandomSource random)
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _fromMes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every resource of the configured (non pass-through) steps, once each (a resource can serve several steps).</summary>
        public IReadOnlyList<string> AllResources =>
            line.Steps.Where(s => !s.PassThrough).SelectMany(s => ResourcesFor(s.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public bool HasResources(string stepName) => ResourcesFor(stepName).Count > 0;

        /// <summary>The step's resources: from simulationConfiguration.json when listed there, else from the MES.</summary>
        public IReadOnlyList<string> ResourcesFor(string stepName)
        {
            if (line.ResourcesByStep.TryGetValue(stepName, out var configured) && configured.Count > 0)
            {
                return configured;
            }
            return _fromMes.GetOrAdd(stepName, source.ResourcesFor);
        }

        /// <summary>
        /// The step's resources in the order to try them: each next one picked at random from those left (the first
        /// is what <see cref="PickResource"/> would give).
        /// </summary>
        public IReadOnlyList<string> PickOrder(string stepName)
        {
            var left = PickableResources(stepName).ToList();
            var order = new List<string>(left.Count);
            while (left.Count > 0)
            {
                int index = random.Next(0, left.Count);
                order.Add(left[index]);
                left.RemoveAt(index);
            }
            return order;
        }

        /// <summary>A random resource of the step.</summary>
        public string PickResource(string stepName)
        {
            var resources = PickableResources(stepName);
            return resources[random.Next(0, resources.Count)];
        }

        private IReadOnlyList<string> PickableResources(string stepName)
        {
            if (line.FindStep(stepName) == null)
            {
                throw new InvalidOperationException($"Step '{stepName}' is not in simulationConfiguration.json");
            }

            var resources = ResourcesFor(stepName);
            if (resources.Count == 0)
            {
                throw new InvalidOperationException($"Step '{stepName}' has no resources, in simulationConfiguration.json or in the MES");
            }
            return resources;
        }
    }
}
