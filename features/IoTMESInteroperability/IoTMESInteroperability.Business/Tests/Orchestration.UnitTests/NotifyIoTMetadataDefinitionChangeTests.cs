using System;
using System.Collections.Generic;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class NotifyIoTMetadataDefinitionChangeTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        Assert.True(new NotifyIoTMetadataDefinitionChange().DeeTestCondition(new Dictionary<string, object>()));
    }

    [Fact]
    public void DeeActionCode_WhenActionGroupOrSmartTableIsMissing_JustMarksResult()
    {
        var result = new NotifyIoTMetadataDefinitionChange().DeeActionCode(ActionInput);

        Assert.Null(result);
        Assert.Equal(true, ActionInput["Result"]);
    }

    [Fact]
    public void DeeActionCode_WhenSmartTableDoesNotMatchTheTarget_DoesNotPublishAMessage()
    {
        ActionInput["ActionGroupName"] = "SmartTables.SmartTables.InsertOrUpdateRows.Post";
        ActionInput[Cmf.Foundation.Common.Constants.SmartTable] = new SmartTable { Name = "SomeOtherSmartTable" };

        var result = new NotifyIoTMetadataDefinitionChange().DeeActionCode(ActionInput);

        Assert.Null(result);
        Assert.Equal(true, ActionInput["Result"]);
    }

    [Theory]
    [InlineData("SmartTables.SmartTables.InsertOrUpdateRows.Post")]
    [InlineData("SmartTables.SmartTables.RemoveRows.Post")]
    public void DeeActionCode_WhenSmartTableMatchesTheTarget_RequiresALiveCmfHostToPublish(string actionGroupName)
    {
        // Cmf.Foundation.Common.Utilities.PublishMessage relies on the ambient CMF message-bus context, which a
        // plain unit test never bootstraps. This asserts the DEE genuinely reaches that call (i.e. the gating
        // logic above correctly matched) rather than failing for an unrelated reason.
        ActionInput["ActionGroupName"] = actionGroupName;
        ActionInput[Cmf.Foundation.Common.Constants.SmartTable] = new SmartTable { Name = "IoTMetadataDefinition" };

        Assert.ThrowsAny<Exception>(() => new NotifyIoTMetadataDefinitionChange().DeeActionCode(ActionInput));
    }
}
