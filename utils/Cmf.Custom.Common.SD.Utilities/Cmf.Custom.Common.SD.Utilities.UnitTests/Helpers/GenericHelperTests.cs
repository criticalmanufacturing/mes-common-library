using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using Cmf.Custom.Common.SD.Utilities.Helpers;
using Cmf.Custom.Common.SD.Utilities.Extensions;
using Cmf.Custom.Common.SD.Utilities.Helpers.Abstractions;
using Cmf.Foundation.BusinessObjects;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement;
using Cmf.Foundation.BusinessOrchestration.EntityTypeManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.OutputObjects;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Foundation.Common.Contexts;
using Cmf.Foundation.Configuration.Abstractions;
using Cmf.Foundation.BaseTestsUtils.Extensions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using FluentAssertions;
using Moq;

namespace Cmf.Custom.Common.SD.Utilities.UnitTests.Helpers;

public class GenericHelperTests
{
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ILaborOrchestration> _laborOrchestration = new();
    private readonly Mock<IGenericServiceOrchestration> _genericServiceOrchestration = new();
    private readonly Mock<IEntityTypeOrchestration> _entityTypeOrchestration = new();
    private readonly Mock<IEntityFactory> _entityFactory = new();

    private GenericHelper CreateHelper() => new(
        _localizationService.Object,
        _laborOrchestration.Object,
        _genericServiceOrchestration.Object,
        _entityTypeOrchestration.Object,
        _entityFactory.Object);

    [Fact]
    public void LocalizationAndChildConfigurationMethods_PreserveDefaultsAndMissingValueBehavior()
    {
        _localizationService.Setup(x => x.Localize(It.IsAny<string>(), "Greeting")).Returns("Hello {0}");
        _localizationService.Setup(x => x.Localize(It.IsAny<string>(), "Failure")).Returns("Failure {0}");
        GenericHelper helper = CreateHelper();

        helper.GetLocalizedMessage("Greeting", "MES").Should().Be("Hello MES");
        Action throwLocalized = () => helper.ThrowLocalizedException("Failure", "detail");
        throwLocalized.Should().Throw<CmfBaseException>().WithMessage("*Failure detail");

    }

    [Fact]
    public void RetrievalRelationsAndLoadMethods_HandleResultsAndInvalidIds()
    {
        GenericHelper helper = CreateHelper();
        Mock<IEntity> loadedEntity = new();
        Collection<object> results = new() { loadedEntity.Object };
        _genericServiceOrchestration
            .Setup(x => x.GetObjectsByFilter(It.IsAny<GetObjectsByFilterInput>()))
            .Returns(new GetObjectsByFilterOutput { Instance = results });

        helper.GetObjectsByIds(new List<long> { 12 }, typeof(IEntity)).Should().BeSameAs(results);
        helper.GetVersionableObjectsByIds(new List<long> { 12 }, typeof(IEntity)).Should().BeSameAs(results);
        helper.GetObjectsByIds(null!, typeof(IEntity)).Should().BeEmpty();

        Mock<ICoreBaseCollection<IEntity>> coreCollection = new();
        _entityFactory.Setup(x => x.Create<IEntity>()).Returns(loadedEntity.Object);
        helper.LoadByIDs(coreCollection.Object, new List<long> { 0, 12, 12, -1 });
        coreCollection.Verify(x => x.Clear(), Times.Once);
        coreCollection.Verify(x => x.Add(loadedEntity.Object), Times.Once);

        Mock<IEntityCollection<IEntity>> entityCollection = new();
        helper.LoadByIDs(entityCollection.Object, new Collection<long> { 12 });
        entityCollection.Verify(x => x.Clear(), Times.Once);
        entityCollection.Verify(x => x.Add(loadedEntity.Object), Times.Once);
    }

    private const string TestTenantName = "task6-test";

    private static void InitializeCallContext()
    {
        PropertyInfo tenantsProperty = typeof(ApplicationContext).GetProperty("Tenants", BindingFlags.Static | BindingFlags.NonPublic)!;
        IDictionary<string, TenantInformation> tenants = (IDictionary<string, TenantInformation>)tenantsProperty.GetValue(null)!;
        tenants[TestTenantName] = new TenantInformation(TestTenantName);
        MethodInfo createContext = typeof(ApplicationContext).GetMethod("CreateRequestCallContext", BindingFlags.NonPublic | BindingFlags.Static)!;
        createContext.Invoke(null, new object?[] { TestTenantName, ContextType.System, null, CancellationToken.None });
    }

    private static void ClearCallContext()
    {
        typeof(ApplicationContext).GetMethod("DeleteRequestContext", BindingFlags.NonPublic | BindingFlags.Static, Type.EmptyTypes)?.Invoke(null, null);
        PropertyInfo tenantsProperty = typeof(ApplicationContext).GetProperty("Tenants", BindingFlags.Static | BindingFlags.NonPublic)!;
        ((IDictionary<string, TenantInformation>)tenantsProperty.GetValue(null)!).Remove(TestTenantName);
    }

    [Fact]
    public void AddAttachments_CopiesInternalFilesAndPreservesExternalAttachmentMetadata()
    {
        string workingDirectory = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        string sourceFile = Path.Combine(workingDirectory, "source.txt");
        File.WriteAllText(sourceFile, "attachment content");
        FullUpdateEntityAttachmentsInput? capturedInput = null;
        _entityTypeOrchestration
            .Setup(x => x.FullUpdateEntityAttachments(It.IsAny<FullUpdateEntityAttachmentsInput>()))
            .Callback<FullUpdateEntityAttachmentsInput>(input => capturedInput = input);

        Mock<IEntityDocumentation> internalAttachment = new();
        internalAttachment.SetupGet(x => x.Filename).Returns("source.txt");
        internalAttachment.SetupGet(x => x.StorageType).Returns(ContentStorageType.Internal);
        internalAttachment.SetupGet(x => x.ContentLocation).Returns(sourceFile);
        internalAttachment.SetupGet(x => x.Description).Returns("internal");

        Mock<IEntityDocumentation> externalAttachment = new();
        externalAttachment.SetupGet(x => x.Filename).Returns("");
        externalAttachment.SetupGet(x => x.StorageType).Returns(ContentStorageType.External);
        externalAttachment.SetupGet(x => x.Url).Returns("https://example.invalid/file");

        Mock<IEntityDocumentationCollection> attachments = new();
        attachments.MockCollectionWithList(new List<IEntityDocumentation> { internalAttachment.Object, externalAttachment.Object });

        try
        {
            InitializeCallContext();
            CreateHelper().AddAttachments(new Mock<IEntityInstance>().Object, attachments.Object, workingDirectory);

            capturedInput.Should().NotBeNull();
            capturedInput!.AttachmentsToAdd.Should().HaveCount(2);
            capturedInput.AttachmentsToAdd[0].EntityDocumentation.Filename.Should().Be("source.txt");
            File.ReadAllText(capturedInput.AttachmentsToAdd[0].FullFileName).Should().Be("attachment content");
            capturedInput.AttachmentsToAdd[1].EntityDocumentation.StorageType.Should().Be(ContentStorageType.External);
            _entityTypeOrchestration.Verify(x => x.FullUpdateEntityAttachments(It.IsAny<FullUpdateEntityAttachmentsInput>()), Times.Once);
        }
        finally
        {
            ClearCallContext();
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
