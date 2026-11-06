using Cmf.Custom.Common.SD.Utilities.Exceptions;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.QueryObject;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using FluentAssertions;
using Moq;
using System.Data;
using System.Text;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class ERPHelperTests
{
    private readonly Mock<IEntityFactory> _entityFactory = new();
    private readonly Mock<IIntegrationEntry> _entry = new();
    private readonly Mock<IIntegrationMessage> _message = new();

    public ERPHelperTests()
    {
        _entry.SetupAllProperties();
        _message.SetupAllProperties();
        _entityFactory.Setup(x => x.Create<IIntegrationEntry>()).Returns(_entry.Object);
        _entityFactory.Setup(x => x.Create<IIntegrationMessage>()).Returns(_message.Object);
    }

    [Theory]
    [InlineData(null, "target", "type", "event", "messageName", "message", null, false, "sourceSystem")]
    [InlineData("source", null, "type", "event", "messageName", "message", null, false, "targetSystem")]
    [InlineData("source", "target", null, "event", "messageName", "message", null, false, "messageType")]
    [InlineData("source", "target", "type", null, "messageName", "message", null, false, "eventName")]
    [InlineData("source", "target", "type", "event", null, "message", null, false, "messageName")]
    [InlineData("source", "target", "type", "event", "messageName", null, null, false, "message")]
    [InlineData("source", "target", "type", "event", "messageName", "message", null, true, "batchId")]
    public void CreateIntegrationEntry_ValidatesRequiredValues(string source, string target, string type, string eventName, string messageName, string message, string batchId, bool queueing, string missingValue)
    {
        Action act = () => CreateHelper().CreateIntegrationEntry(source, target, type, eventName, messageName, message, batchId, null, queueing);

        act.Should().Throw<ArgumentNullCmfException>().Which.Message.Should().Contain(missingValue);
    }

    [Fact]
    public void CreateIntegrationEntry_SerializesDataAndControlsBatchQueueing()
    {
        var helper = CreateHelper();
        IIntegrationEntry result = helper.CreateIntegrationEntry("MES", "ERP", "Order", "Changed", "OrderChanged", "payload", "batch-1", "entry-1", false);

        result.Should().BeSameAs(_entry.Object);
        result.Name.Should().Be("entry-1");
        result.BatchId.Should().Be("batch-1");
        result.ParentIntegrationEntry.Should().BeNull();
        result.IntegrationMessage.Message.Should().Equal(Encoding.Default.GetBytes("payload"));
        result.SystemState.Should().Be(IntegrationEntrySystemState.Received);
        _entry.Verify(x => x.Create(), Times.Once);
        _entityFactory.Verify(x => x.Create<IQueryObject>(), Times.Never);

        helper.CreateIntegrationEntry("MES", "ERP", "Order", "Changed", "OrderChanged", "payload")
            .Name.Should().NotBeNullOrWhiteSpace();

        Mock<IQueryObject> query = new();
        query.Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>())).Returns(new DataSet());
        _entityFactory.Setup(x => x.Create<IQueryObject>()).Returns(query.Object);
        IIntegrationEntry queued = helper.CreateIntegrationEntry("MES", "ERP", "Order", "Changed", "OrderChanged", "payload", "batch-2", null, true);
        queued.ParentIntegrationEntry.Should().BeNull();
        query.Verify(q => q.Execute(false, It.IsAny<QueryParameterCollection>()), Times.Once);

        DataSet parentData = new();
        DataTable table = parentData.Tables.Add();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("CreatedOn", typeof(DateTime));
        table.Rows.Add(42L, DateTime.UtcNow);
        query.Setup(q => q.Execute(false, It.IsAny<QueryParameterCollection>())).Returns(parentData);
        Mock<IIntegrationEntry> failedParent = new();
        failedParent.SetupAllProperties();
        failedParent.Object.Name = "parent-failed";
        failedParent.Object.SystemState = IntegrationEntrySystemState.Failed;
        _entityFactory.SetupSequence(x => x.Create<IIntegrationEntry>())
            .Returns(_entry.Object)
            .Returns(failedParent.Object);

        IIntegrationEntry failedChild = helper.CreateIntegrationEntry("MES", "ERP", "Order", "Changed", "OrderChanged", "payload", "batch-3", null, true);

        failedChild.ParentIntegrationEntry.Should().BeSameAs(failedParent.Object);
        failedChild.IsRetriable.Should().BeTrue();
        failedChild.NumberOfRetries.Should().Be(-1);
        _entry.Verify(x => x.Fail("Parent Integration Entry parent-failed has failed."), Times.Once);
    }



    private ERPHelper CreateHelper() => new(_entityFactory.Object);
}
