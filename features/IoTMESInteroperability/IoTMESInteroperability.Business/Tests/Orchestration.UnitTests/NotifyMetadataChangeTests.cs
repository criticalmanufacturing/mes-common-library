using System;
using System.Collections.Generic;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class NotifyMetadataChangeTests : ActionBaseTests
{
    [Fact]
    public void DeeTestCondition_WhenSmartTableKeyIsMissing_ReturnsFalse()
    {
        var result = new NotifyMetadataChange().DeeTestCondition(new Dictionary<string, object>());

        Assert.False(result);
    }

    [Fact]
    public void DeeTestCondition_WhenSmartTableNameMatchesTheTarget_ReturnsTrue()
    {
        var smartTable = Mock.Of<ISmartTable>(t => t.Name == "IoTMetadataDefinition");
        var input = new Dictionary<string, object> { [Cmf.Foundation.Common.Constants.SmartTable] = smartTable };

        var result = new NotifyMetadataChange().DeeTestCondition(input);

        Assert.True(result);
    }

    [Fact]
    public void DeeTestCondition_WhenSmartTableNameDoesNotMatchTheTarget_ReturnsFalse()
    {
        var smartTable = Mock.Of<ISmartTable>(t => t.Name == "SomeOtherSmartTable");
        var input = new Dictionary<string, object> { [Cmf.Foundation.Common.Constants.SmartTable] = smartTable };

        var result = new NotifyMetadataChange().DeeTestCondition(input);

        Assert.False(result);
    }

    [Fact]
    public void DeeActionCode_AlwaysRequiresALiveCmfHostToPublish()
    {
        // Cmf.Foundation.Common.Utilities.PublishMessage is called unconditionally and relies on the ambient CMF
        // message-bus context, which a plain unit test never bootstraps.
        Assert.ThrowsAny<Exception>(() => new NotifyMetadataChange().DeeActionCode(new Dictionary<string, object>()));
    }
}
