using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessObjects;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using System.Text;
using System.Text.RegularExpressions;

namespace Cmf.Custom.Common.SD.Utilities.Helpers;

public class FlowSearchHelper : IFlowSearchHelper
{
    private readonly IEntityFactory _entityFactory;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public FlowSearchHelper(IEntityFactory entityFactory)
    {
        _entityFactory = entityFactory;
    }

    public string CalculateFlowPath(string flowName, string stepName, string[] subFlowNames = null)
    {
        List<IFlow> subFlows = null;

        IFlow flow;
        if (!string.IsNullOrWhiteSpace(flowName))
        {
            flow = _entityFactory.Create<IFlow>();
            flow.Load(flowName);
        }
        else
        {
            throw new ArgumentNullCmfException("FlowName");
        }

        IStep step;
        if (!string.IsNullOrWhiteSpace(stepName))
        {
            step = _entityFactory.Create<IStep>();
            step.Load(stepName);
        }
        else
        {
            throw new ArgumentNullCmfException("StepName");
        }

        if (subFlowNames != null && subFlowNames.Length > 0)
        {
            subFlows = new List<IFlow>();
            for (int i = 0; i < subFlowNames.Length; i++)
            {
                IFlow subFlow = _entityFactory.Create<IFlow>();
                subFlow.Load(subFlowNames[i]);
                subFlows.Add(subFlow);
            }
        }

        if (flow != null && step != null)
        {
            return flow.CalculateFlowPath(step, subFlows, 1);
        }

        return null;
    }



    public void GetFlowStepFromFlowPath(string flowPath, out IFlowCollection flows, out IStep step, int levelsToLoad = 0)
    {
        if (string.IsNullOrEmpty(flowPath))
        {
            throw new ArgumentNullCmfException("FlowPath");
        }

        flows = _entityFactory.CreateCollection<IFlowCollection>();
        step = null;

        string[] flowParts = flowPath.Split(Cmf.Navigo.Common.Constants.FlowPathSpliter.ToCharArray());
        for (int index = 0; index < flowParts.Count(); index++)
        {
            string[] flowInfo = flowParts[index].Split(Cmf.Navigo.Common.Constants.FlowPathCorrelationIdSpliter.ToCharArray());
            if (index == flowParts.Count() - 1)
            {
                step = _entityFactory.Create<IStep>();
                step.Name = flowInfo[0];
            }
            else
            {
                IFlow flowToAdd = _entityFactory.Create<IFlow>();
                flowToAdd.Name = flowInfo[0];
                flows.Add(flowToAdd);
            }
        }

        if (levelsToLoad >= 0)
        {
            if (flows.Any())
            {
                flows.Load(levelsToLoad);
            }
            if (step != null)
            {
                step.Load(levelsToLoad);
            }
        }
    }
}
