using Cmf.Custom.Common.SD.Utilities.Helpers;
using FluentAssertions;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class DebugUtilitiesTests
{
    [Fact]
    public void Log_AppendsFormattedEntryAndDebuggerApiRetainsOptionalDefaults()
    {
        string filePath = Path.Combine(Environment.CurrentDirectory, $"{Guid.NewGuid():N}.log");

        try
        {
            DebugUtilities.Log("TestRule", "test message", filePath);

            string contents = File.ReadAllText(filePath);
            contents.Should().Contain("Rule: TestRule");
            contents.Should().Contain("Mesg: test message");
            contents.Should().Contain("-----------------------------");

            System.Reflection.ParameterInfo[] parameters = typeof(DebugUtilities)
                .GetMethod(nameof(DebugUtilities.LaunchDebugger))!
                .GetParameters();
            parameters[0].DefaultValue.Should().BeNull();
            parameters[1].DefaultValue.Should().BeNull();
            typeof(DebugUtilities).GetMethod(nameof(DebugUtilities.Log))!
                .GetParameters()[2].DefaultValue.Should().Be("CustomActionUtilities.log");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
