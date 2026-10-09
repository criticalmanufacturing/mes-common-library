using System.Collections.Concurrent;
using Cmf.Foundation.BusinessObjects;
using Cmf.Navigo.BusinessObjects;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// State models loaded on first use, by name: every track-in/out uses the state model of its resource.
    /// </summary>
    public sealed class StateModelCache(IMesGateway mes)
    {
        private readonly ConcurrentDictionary<string, StateModel> _stateModels = new();

        /// <summary>
        /// The state model the resource is configured with (e.g. SEMI E10, SEMI E58), with its transitions loaded.
        /// Null when the resource has no main state model.
        /// </summary>
        public StateModel? ForResource(Resource? resource)
        {
            var resourceStateModel = resource?.CurrentMainState?.StateModel;
            if (resourceStateModel == null)
            {
                return null;
            }

            string? name = resourceStateModel.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = mes.MasterData.GetById<StateModel>(resourceStateModel.Id)?.Name;
            }

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return _stateModels.GetOrAdd(name, stateModelName => mes.MasterData.GetByName<StateModel>(stateModelName)!);
        }
    }
}
