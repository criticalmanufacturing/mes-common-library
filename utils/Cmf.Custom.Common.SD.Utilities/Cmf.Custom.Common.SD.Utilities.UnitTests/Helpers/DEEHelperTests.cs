using Cmf.Custom.Common.SD.Utilities.DataStructures;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.DynamicExecutionEngineManagement.OutputObjects;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class DEEHelperTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ContextParameters_HandleMissingCallContext(bool callContextIsNull)
    {
        Mock<ICallContext> callContext = new();
        callContext.Setup(x => x.GetInformationContext("key")).Returns("value");
        DEEHelper helper = CreateHelper(callContextIsNull ? null : callContext.Object);

        helper.SetContextParameter("key", "value");
        object result = helper.GetContextParameter("key");

        if (callContextIsNull)
        {
            result.Should().BeNull();
            callContext.Verify(x => x.SetInformationContext(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
            callContext.Verify(x => x.GetInformationContext(It.IsAny<string>()), Times.Never);
            helper.GetContextParameter<int>("missing").Should().Be(default);
        }
        else
        {
            result.Should().Be("value");
            helper.GetContextParameter<string>("key").Should().Be("value");
            callContext.Verify(x => x.SetInformationContext("key", "value"), Times.Once);
            callContext.Verify(x => x.GetInformationContext("key"), Times.Exactly(2));
        }

        Dictionary<string, object> input = new() { ["ActionGroupName"] = "Main", ["typed"] = 8, ["wrongType"] = 10 };
        helper.GetActionGroup(input).Should().Be("Main");
        helper.IsActionGroupValid(input, new Collection<string> { "main" }).Should().BeTrue();
        helper.IsActionGroupValid(input, new Collection<string> { "other" }).Should().BeFalse();
        helper.IsActionGroupValid(new Dictionary<string, object>(), null!, true).Should().BeTrue();

        Mock<IDynamicExecutionEngineOrchestration> orchestration = new();
        IAction action = Mock.Of<IAction>();
        Dictionary<string, object> expectedOutput = new() { ["result"] = 9 };
        orchestration.Setup(x => x.GetActionByName(It.IsAny<GetActionByNameInput>()))
            .Returns(new GetActionByNameOutput { Action = action });
        orchestration.Setup(x => x.ExecuteAction(It.IsAny<ExecuteActionInput>()))
            .Returns(new ExecuteActionOutput { Output = expectedOutput });
        DEEHelper actionHelper = CreateHelper(dynamicExecutionEngineOrchestration: orchestration.Object);

        actionHelper.ExecuteAction("action", input).Should().BeSameAs(expectedOutput);
        orchestration.Verify(x => x.GetActionByName(It.Is<GetActionByNameInput>(request => request.Name == "action")), Times.Once);
        orchestration.Verify(x => x.ExecuteAction(It.Is<ExecuteActionInput>(request => ReferenceEquals(request.Action, action) && ReferenceEquals(request.Input, input))), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServiceContextAccessors_HandleMissingCallContext(bool callContextIsNull)
    {
        DeeContext context = new() { MethodName = "test", TriggerPoint = DeeTriggerPoint.Unknown };
        Mock<IHistoryObjectCollection> history = new();
        history.SetupGet(x => x.ServiceComment).Returns("comment");
        Mock<ICallContext> callContext = new();
        callContext.Setup(x => x.GetInformationContext("rule_CurrentServiceContext")).Returns(context);
        callContext.Setup(x => x.GetInformationContext("rule_InitialServiceContext")).Returns(context);
        callContext.Setup(x => x.GetHistoryCollection()).Returns(history.Object);
        DEEHelper helper = CreateHelper(callContextIsNull ? null : callContext.Object);

        DeeContext current = helper.GetCurrentServiceContext("rule");
        DeeContext initial = helper.GetInitialServiceContext("rule");
        string comment = helper.GetServiceComments();

        if (callContextIsNull)
        {
            current.Should().BeNull();
            initial.Should().BeNull();
            comment.Should().BeNull();
            callContext.Verify(x => x.GetInformationContext(It.IsAny<string>()), Times.Never);
            callContext.Verify(x => x.GetHistoryCollection(), Times.Never);
        }
        else
        {
            current.Should().BeSameAs(context);
            initial.Should().BeSameAs(context);
            comment.Should().Be("comment");
            callContext.Verify(x => x.GetInformationContext("rule_CurrentServiceContext"), Times.Once);
            callContext.Verify(x => x.GetInformationContext("rule_InitialServiceContext"), Times.Once);
            callContext.Verify(x => x.GetHistoryCollection(), Times.Once);
        }

        PayloadServiceInput input = new() { ServiceComments = "service comment" };
        PayloadServiceOutput output = new() { Message = "first" };
        Dictionary<string, object> deeInput = new() { ["input"] = input, ["output"] = output };
        helper.GetServiceComments(deeInput).Should().Be("service comment");

        PayloadServiceOutput nextOutput = new() { Message = "second" };

        Payload payload = new();
        helper.GetObjectFromStructure<Payload>(payload).Should().BeSameAs(payload);
        helper.GetObjectFromStructure<Payload>(new[] { payload }).Should().BeSameAs(payload);
        helper.GetObjectFromStructure<Payload>(new Dictionary<string, object> { ["payload"] = payload }, new[] { "payload" }).Should().BeSameAs(payload);
        helper.GetObjectFromStructure<Payload>(new PayloadEnvelope(payload)).Should().BeSameAs(payload);
        helper.GetObjectFromStructure<Payload>(new PayloadEnvelope(payload), searchDepth: 0).Should().BeNull();
        helper.GetObjectFromStructure<Payload>(null).Should().BeNull();

        helper.GetObjectFromDeeInput<Payload>(new Dictionary<string, object> { ["input"] = new PayloadServiceInput { Payload = payload } }).Should().BeSameAs(payload);
        helper.GetObjectFromDeeInput<Payload>(new Dictionary<string, object> { ["output"] = new PayloadServiceOutput { Payload = payload } }, skipServiceInputs: true).Should().BeSameAs(payload);
        helper.GetObjectFromDeeInput<Payload>(new Dictionary<string, object> { ["operation"] = new PayloadEnvelope(payload) }, skipServiceInputs: true, skipServiceOutputs: true).Should().BeSameAs(payload);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void SetCurrentServiceContext_RespectsCallContextAndInitialContextMode(bool callContextIsNull, bool skipInitialContext)
    {
        Mock<ICallContext> callContext = new();
        DEEHelper helper = CreateHelper(callContextIsNull ? null : callContext.Object);

        DeeContext result = helper.SetCurrentServiceContext("rule", skipInitialContext);

        result.Should().NotBeNull();
        result.TriggerPoint.Should().Be(DeeTriggerPoint.Unknown);
        if (callContextIsNull)
        {
            callContext.Verify(x => x.SetInformationContext(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }
        else
        {
            callContext.Verify(x => x.SetInformationContext("rule_CurrentServiceContext", result), Times.Once);
            callContext.Verify(x => x.SetInformationContext("rule_InitialServiceContext", result), skipInitialContext ? Times.Never : Times.Once);
        }

        DEEHelper attributeHelper = CreateHelper();
        AttributeCollection original = new() { ["keep"] = 1, ["remove"] = 2, ["replace"] = 3 };
        FullUpdateAttributesInput updateInput = new()
        {
            AttributesToAddOrUpdate = new AttributeCollection { ["replace"] = 30, ["added"] = 4 },
            RelatedAttributesToAddOrUpdate = new AttributeCollection { ["related"] = 5 },
            AttributesNamesToRemove = new Collection<string> { "remove" },
            RelatedAttributesNamesToRemove = new Collection<string> { "relatedRemove" }
        };

        IAttributeCollection updated = attributeHelper.GetUpdatedAttributes(updateInput);
        updated.Should().ContainKey("replace").WhoseValue.Should().Be(30);
        updated.Should().ContainKey("related").WhoseValue.Should().Be(5);
        attributeHelper.GetRemovedAttributeNames(updateInput).Should().Contain(new[] { "remove", "relatedRemove" });
        attributeHelper.GetUpdatedAndRemovedAttributeNames(updateInput).Should().Contain(new[] { "replace", "added", "related", "remove", "relatedRemove" });
        IAttributeCollection final = attributeHelper.ResolveFinalAttributeValues(original, updateInput);
        final.Should().ContainKey("keep").WhoseValue.Should().Be(1);
        final.Should().NotContainKey("remove");
        final.Should().ContainKey("replace").WhoseValue.Should().Be(30);
        final.Should().ContainKey("related").WhoseValue.Should().Be(5);

        attributeHelper.GetUpdatedAttributes(new PayloadServiceInput()).Should().BeEmpty();
        attributeHelper.GetRemovedAttributeNames(new RemoveAttributesInput { AttributeNames = new Collection<string> { "one" } }).Should().ContainSingle().Which.Should().Be("one");

        Mock<IMaterial> shallowMaterial = new();
        shallowMaterial.SetupGet(x => x.Id).Returns(42);
        Mock<IMaterial> reloadedMaterial = new();
        Mock<IEntityType> materialType = new();
        materialType.Setup(x => x.GetInstance()).Returns(reloadedMaterial.Object);
        shallowMaterial.SetupGet(x => x.EntityType).Returns(materialType.Object);
        DEEHelper materialHelper = CreateHelper();
        materialHelper.GetMaterialFromDeeInput(new Dictionary<string, object> { ["material"] = shallowMaterial.Object }).Should().BeSameAs(reloadedMaterial.Object);
        materialType.Verify(x => x.GetInstance(), Times.Once);
        reloadedMaterial.As<ICoreBase>().Verify(x => x.Load(42), Times.Once);
    }

    private static DEEHelper CreateHelper(
        ICallContext? callContext = null,
        IDynamicExecutionEngineOrchestration? dynamicExecutionEngineOrchestration = null,
        IEntityHelper? entityHelper = null)
        => new(
            dynamicExecutionEngineOrchestration ?? Mock.Of<IDynamicExecutionEngineOrchestration>(),
            callContext!,
            entityHelper ?? Mock.Of<IEntityHelper>());

    public sealed class Payload { }

    private sealed class PayloadEnvelope(Payload payload)
    {
        public Payload Payload { get; } = payload;
    }

    private sealed class PayloadServiceInput : BaseInput
    {
        public Payload Payload { get; set; }
    }

    private sealed class PayloadServiceOutput : BaseOutput
    {
        public Payload Payload { get; set; }
    }

    private sealed class FullUpdateAttributesInput : BaseInput
    {
        public IAttributeCollection AttributesToAddOrUpdate { get; set; }
        public IAttributeCollection RelatedAttributesToAddOrUpdate { get; set; }
        public Collection<string> AttributesNamesToRemove { get; set; }
        public Collection<string> RelatedAttributesNamesToRemove { get; set; }
    }

    private sealed class RemoveAttributesInput : BaseInput
    {
        public Collection<string> AttributeNames { get; set; }
    }

    private sealed class ThrowingEnvelope
    {
        public object Value => throw new InvalidOperationException();
        public object this[int index] => index;
    }
}
