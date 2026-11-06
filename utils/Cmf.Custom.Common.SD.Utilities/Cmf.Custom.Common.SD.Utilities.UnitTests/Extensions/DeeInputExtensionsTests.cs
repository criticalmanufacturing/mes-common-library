using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessOrchestration;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class DeeInputExtensionsTests
{
    [Fact]
    public void InputDictionaryExtensions_ReturnTypedItemsAndServiceObjects()
    {
        var input = new TestInput { ServiceComments = "comment" };
        var output = new TestOutput { Message = "first" };
        Dictionary<string, object> deeInput = new() { ["input"] = input, ["output"] = output, ["typed"] = 8, ["wrongType"] = 10 };

        deeInput.GetInputItem("missing", "fallback").Should().Be("fallback");
        deeInput.GetInputItem("wrongType", "fallback").Should().Be("fallback");
        deeInput.GetInputItem<int>("typed").Should().Be(8);
        deeInput.GetServiceInputObject().Should().BeSameAs(input);
        deeInput.GetServiceOutputObject().Should().BeSameAs(output);
    }

    [Fact]
    public void ServiceCommentAndOutputExtensions_PreserveAndAppendMessages()
    {
        var input = new TestInput { ServiceComments = "service comment" };
        input.GetAppendedServiceComment("additional").Should().Be($"service comment{Environment.NewLine}additional");
        input.GetAppendedServiceComment(null).Should().Be("service comment" + Environment.NewLine);

        var original = new TestOutput { Message = "first" };
        var next = new TestOutput { Message = "second", FeedbackMessages = new Collection<FeedbackMessage> { new() { Message = "feedback" } } };
        var additional = new FeedbackMessage { Message = "additional" };
        original.MergeServiceOutputMessages(next, additional);
        original.Message.Should().Be($"first{Environment.NewLine}second");
        original.FeedbackMessages.Should().HaveCount(2);
        original.FeedbackMessages.Should().Contain(message => message.Message == "feedback");
        original.FeedbackMessages.Should().Contain(message => message.Message == "additional");
    }

    private sealed class TestInput : BaseInput { }
    private sealed class TestOutput : BaseOutput { }
}
