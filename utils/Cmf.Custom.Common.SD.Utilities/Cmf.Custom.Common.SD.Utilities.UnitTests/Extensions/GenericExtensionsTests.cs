using Cmf.Custom.Common.SD.Utilities.Extensions;
using FluentAssertions;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Extensions;

public class GenericExtensionsTests
{
    public static IEnumerable<object[]> StringCases =>
    [
        ["FOO", "foo", true, "///value", "/", true, "//value", null!, true, "helloWorld", "hello World"],
        ["FOO", "bar", false, "///value", "/", false, "value", "  ", true, "XMLParser", "XMLParser"],
        ["text", null!, false, "value", "", false, "value", "content", false, "aB", "a B"]
    ];

    public static IEnumerable<object[]> CollectionCases =>
    [
        [null!, 1, true, true],
        [Array.Empty<int>(), 3, true, false],
        [new[] { 1, 2 }, 2, false, true],
        [GenerateValues(), 2, false, true]
    ];

    [Theory]
    [MemberData(nameof(StringCases))]
    public void StringMethods_ShouldHandleComparisonsAndPrefixes(
        string? first,
        string? second,
        bool expectedEqual,
        string target,
        string trimString,
        bool trimFirstOccurrence,
        string expectedTrimmed,
        string? whitespace,
        bool expectedWhitespace,
        string sentence,
        string expectedSentence)
    {
        GenericExtensions.IgnoreCaseEquals(first!, second!).Should().Be(expectedEqual);
        GenericExtensions.TrimStart(target, trimString, trimFirstOccurrence).Should().Be(expectedTrimmed);
        GenericExtensions.IsNullOrWhiteSpace(whitespace!).Should().Be(expectedWhitespace);
        GenericExtensions.AddSpacesToSentenceBeforeCapitalLetters(sentence).Should().Be(expectedSentence);
    }

    [Theory]
    [MemberData(nameof(CollectionCases))]
    public void CollectionMethods_ShouldHandleEmptyMembershipAndAddRange(
        IEnumerable<int>? values,
        int lookupValue,
        bool expectedEmpty,
        bool expectedContains)
    {
        GenericExtensions.IsNullOrEmpty(values).Should().Be(expectedEmpty);
        GenericExtensions.In(lookupValue, 1, 2).Should().Be(expectedContains);

        Dictionary<int, string> destination = new() { [1] = "existing" };
        Dictionary<int, string> additions = new() { [1] = "replacement", [2] = "new" };
        GenericExtensions.AddRange(destination, additions);

        destination.Should().BeEquivalentTo(new Dictionary<int, string> { [1] = "existing", [2] = "new" });
        Action addNullCollection = () => GenericExtensions.AddRange(destination, null!);
        addNullCollection.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(typeof(int), false, typeof(int), 2)]
    [InlineData(typeof(uint), false, typeof(uint), 3)]
    [InlineData(typeof(decimal), false, typeof(decimal), 1)]
    [InlineData(typeof(int?), true, typeof(int), 2)]
    [InlineData(typeof(string), false, typeof(string), 0)]
    [InlineData(typeof(SampleEnum), false, typeof(SampleEnum), 0)]
    [InlineData(typeof(int), false, typeof(int), 2, "000", 3, 5, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", true, false, "005")]
    [InlineData(typeof(int), false, typeof(int), 2, "00Z", 3, 2, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", true, false, "012")]
    [InlineData(typeof(int), false, typeof(int), 2, "000", 3, 0, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ", true, false, "000")]
    [InlineData(typeof(int), false, typeof(int), 2, "AAB", 3, 1, "ABC", true, false, "AAC")]
    [InlineData(typeof(int), false, typeof(int), 2, "AAC", 3, 1, "ABC", true, false, "ABB")]
    [InlineData(typeof(int), false, typeof(int), 2, "AAC", 3, 1, "ABC", true, true, "ABA")]
    [InlineData(typeof(int), false, typeof(int), 2, "CCC", 3, 1, "ABC", true, true, "AAA")]
    [InlineData(typeof(int), false, typeof(int), 2, "CCC", 3, 1, "ABC", true, false, "AAB")]
    [InlineData(typeof(int), false, typeof(int), 2, null, 3, 5, "ABC", true, false, "AAA")]
    [InlineData(typeof(int), false, typeof(int), 2, " ", 3, 5, "ABC", false, false, "AAB")]
    [InlineData(typeof(int), false, typeof(int), 2, "B", 3, 1, "ABC", true, false, "BAB")]
    [InlineData(typeof(int), false, typeof(int), 2, "BAAC", 3, 1, "ABC", true, false, "BAB")]
    public void TypeMethods_ShouldClassifyNullableAndNumericTypes(
        Type type,
        bool expectedNullable,
        Type expectedNonNullableType,
        int expectedNumericKind,
        string? sequence = "00Z",
        int maxNumberOfDigits = 3,
        int valueToAdd = 1,
        string charList = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ",
        bool startWithFirstValue = true,
        bool repeatWithFirstValue = false,
        string expectedNextValue = "011")
    {
        type.IsNullableType().Should().Be(expectedNullable);
        type.GetNonNullableType().Should().Be(expectedNonNullableType);
        type.GetNumericTypeKind().Should().Be(expectedNumericKind);
        type.IsNumericType().Should().Be(expectedNumericKind != 0);

        ((string)null!).GetNextBase36Value(3).Should().Be("000");
        "00Z".GetNextBase36Value(3).Should().Be("011");
        sequence!.GetNextBase36Value(maxNumberOfDigits, valueToAdd, charList, startWithFirstValue, repeatWithFirstValue)
            .Should().Be(expectedNextValue);
    }

    private enum SampleEnum
    {
        Value
    }

    private static IEnumerable<int> GenerateValues()
    {
        yield return 1;
        yield return 2;
    }
}
