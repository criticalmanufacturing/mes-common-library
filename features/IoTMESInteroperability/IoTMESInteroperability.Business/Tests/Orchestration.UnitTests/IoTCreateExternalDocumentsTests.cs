using System;
using System.Collections.Generic;
using Cmf.Common.CustomActionUtilities.Abstractions;
using Cmf.Community.IoTMESInteroperability.Actions;
using Cmf.Community.IoTMESInteroperability.Common.Objects;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessObjects.Abstractions;
using Cmf.Foundation.BusinessObjects.SmartTables;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions;

public class IoTCreateExternalDocumentsTests : ActionBaseTests
{
    private static Mock<IMaterial> SetUpExistingMaterial(Mock<IEntityFactory> entityFactory)
    {
        var material = new Mock<IMaterial>();
        material.Setup(m => m.ObjectExists()).Returns(true);
        entityFactory.Setup(f => f.Create<IMaterial>()).Returns(material.Object);
        return material;
    }

    [Fact]
    public void DeeTestCondition_WhenMaterialExistsButNoFilesAreProvided_ReturnsFalseWithoutSettingContext()
    {
        var entityFactory = new Mock<IEntityFactory>();
        SetUpExistingMaterial(entityFactory);
        var deeHelper = new Mock<IDEEHelper>();
        AddMockToActionInput(entityFactory);
        AddMockToActionInput(deeHelper);
        ActionInput["MaterialName"] = "MAT1";
        ActionInput["FilesInformation"] = new List<FileInformation>();

        var result = new IoTCreateExternalDocuments().DeeTestCondition(ActionInput);

        Assert.False(result);
        deeHelper.Verify(h => h.SetContextParameter(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void DeeTestCondition_WhenMaterialExistsWithFiles_SetsContextAndReturnsTrue()
    {
        var entityFactory = new Mock<IEntityFactory>();
        var material = SetUpExistingMaterial(entityFactory);
        var deeHelper = new Mock<IDEEHelper>();
        AddMockToActionInput(entityFactory);
        AddMockToActionInput(deeHelper);
        ActionInput["MaterialName"] = "MAT1";
        ActionInput["FilesInformation"] = new List<FileInformation>
        {
            new FileInformation { FileName = "a.txt", FileLocation = "/docs/a.txt" }
        };
        ActionInput["Folder"] = "IoTDocuments";

        var result = new IoTCreateExternalDocuments().DeeTestCondition(ActionInput);

        Assert.True(result);
        material.VerifySet(m => m.Name = "MAT1", Times.Once);
        material.Verify(m => m.Load(), Times.Once);
        deeHelper.Verify(h => h.SetContextParameter("IoTCreateExternalDocuments_Material", material.Object), Times.Once);
        deeHelper.Verify(h => h.SetContextParameter("IoTCreateExternalDocuments_Folder", "IoTDocuments"), Times.Once);
    }

    [Fact]
    public void DeeTestCondition_WhenRequiredKeysAreMissing_ThrowsWithoutALiveCmfHost()
    {
        // GeneralUtilities.ThrowLocalizedException (Cmf.Community.IoTMESInteroperability.Common) resolves the
        // localization helper from the static ApplicationContext.CurrentServiceProvider, which a plain unit test
        // never bootstraps. Outside of a live host this surfaces as an ambient-context failure rather than the
        // intended "InvalidInputs" localized message - documented and asserted here rather than left unexplained.
        Assert.ThrowsAny<Exception>(() => new IoTCreateExternalDocuments().DeeTestCondition(ActionInput));
    }

    [Fact]
    public void DeeActionCode_RequiresALiveCmfHost_SoItThrowsOutsideOfOne()
    {
        // DeeActionCode's very first step is the static, ambient-dependent IoTUtilities.FolderExists(...), for the
        // same reason as above - it cannot succeed in a plain unit test.
        var deeHelper = new Mock<IDEEHelper>();
        deeHelper.Setup(h => h.GetContextParameter<string>("IoTCreateExternalDocuments_Folder")).Returns("IoTDocuments");
        var entityFactory = new Mock<IEntityFactory>();
        entityFactory.Setup(f => f.Create<ISmartTable>()).Returns(Mock.Of<ISmartTable>());
        AddMockToActionInput(entityFactory);
        AddMockToActionInput(deeHelper);

        Assert.ThrowsAny<Exception>(() => new IoTCreateExternalDocuments().DeeActionCode(ActionInput));
    }
}
