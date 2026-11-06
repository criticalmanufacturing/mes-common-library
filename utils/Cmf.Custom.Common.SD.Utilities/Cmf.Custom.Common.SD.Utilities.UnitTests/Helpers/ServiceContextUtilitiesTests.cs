using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class ServiceContextUtilitiesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunWithStartAndEndMethod_ReturnsResultAndPropagatesExceptions(bool throwException)
    {
        Mock<IUtilities> utilities = new();
        ServiceContextUtilities helper = new(utilities.Object);
        Func<int, int> method = value => throwException ? throw new InvalidOperationException("failure") : value + 1;

        if (throwException)
        {
            Action act = () => helper.RunWithStartAndEndMethod("class", 2, method);
            act.Should().Throw<InvalidOperationException>().WithMessage("failure");
            utilities.Verify(x => x.StartMethod("class", nameof(RunWithStartAndEndMethod_ReturnsResultAndPropagatesExceptions), It.IsAny<KeyValuePair<string, object>>()), Times.Once);
            utilities.Verify(x => x.EndMethod(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<KeyValuePair<string, object>>(), It.IsAny<KeyValuePair<string, object>>()), Times.Never);
        }
        else
        {
            int result = helper.RunWithStartAndEndMethod("class", 2, method);
            result.Should().Be(3);
            utilities.Verify(x => x.StartMethod("class", nameof(RunWithStartAndEndMethod_ReturnsResultAndPropagatesExceptions), It.IsAny<KeyValuePair<string, object>>()), Times.Once);
            utilities.Verify(x => x.EndMethod(-1, -1, It.IsAny<KeyValuePair<string, object>>(), It.IsAny<KeyValuePair<string, object>>()), Times.Once);
        }
    }
}
