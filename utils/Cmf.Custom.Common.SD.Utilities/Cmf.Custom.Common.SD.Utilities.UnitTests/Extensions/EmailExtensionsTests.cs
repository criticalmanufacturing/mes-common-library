using Cmf.Custom.Common.SD.Utilities.Extensions;
using FluentAssertions;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class EmailExtensionsTests
{
    [Fact]
    public void SendEmail_RetainsOptionalHtmlParameterDefault()
    {
        typeof(EmailExtensions).GetMethod(nameof(EmailExtensions.SendEmail))!
            .GetParameters()[3].DefaultValue.Should().Be(false);
    }
}
