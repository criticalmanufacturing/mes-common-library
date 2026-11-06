using Cmf.Custom.Common.SD.Utilities.Extensions;
using FluentAssertions;
using System.Reflection;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class ReflectionExtensionsTests
{
    [Fact]
    public void TryGetPropertyValue_ReturnsValuesAndIgnoresIndexersOrThrowingGetters()
    {
        PropertyInfo property = typeof(Envelope).GetProperty(nameof(Envelope.Value))!;
        property.TryGetPropertyValue(new Envelope()).Should().Be("value");
        typeof(ThrowingEnvelope).GetProperty(nameof(ThrowingEnvelope.Value))!
            .TryGetPropertyValue(new ThrowingEnvelope()).Should().BeNull();
        typeof(ThrowingEnvelope).GetProperty("Item")!
            .TryGetPropertyValue(new ThrowingEnvelope()).Should().BeNull();
    }

    private sealed class Envelope { public string Value => "value"; }
    private sealed class ThrowingEnvelope { public object Value => throw new InvalidOperationException(); public object this[int index] => index; }
}
