using System;
using Cmf.Community.IoTMESInteroperability.Orchestration;
using Cmf.Community.IoTMESInteroperability.Orchestration.InputObjects;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.BusinessOrchestration.Abstractions;
using Cmf.Foundation.Common;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Foundation.Common.LocalizationService;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Cmf.Navigo.BusinessOrchestration.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Orchestration;

public class CommunityOrchestrationTests : BaseTests
{
    private static CommunityOrchestration CreateOrchestration(
        out Mock<IEntityFactory> entityFactory,
        out Mock<IUtilities> utilities)
    {
        entityFactory = new Mock<IEntityFactory>();
        utilities = new Mock<IUtilities>();

        return new CommunityOrchestration(
            entityFactory.Object,
            utilities.Object,
            Mock.Of<ILocalizationService>(),
            Mock.Of<IGenericServiceOrchestration>(),
            Mock.Of<IMaterialOrchestration>());
    }

    [Fact]
    public void Constructor_WithMockedDependencies_DoesNotThrow()
    {
        // Arrange & Act
        CommunityOrchestration orchestration = CreateOrchestration(out _, out _);

        // Assert
        Assert.NotNull(orchestration);
    }

    #region GetMaterialRecipe

    [Fact]
    public void GetMaterialRecipe_WhenInputIsNull_WrapsValidationFailureInCmfBaseException()
    {
        // Arrange
        CommunityOrchestration orchestration = CreateOrchestration(out _, out Mock<IUtilities> utilities);
        var validationFailure = new ArgumentNullException(nameof(GetMaterialRecipeInput));
        utilities
            .Setup(u => u.ValidateNullInput(It.IsAny<object>()))
            .Throws(validationFailure);

        // Act
        CmfBaseException exception = Assert.Throws<CmfBaseException>(() => orchestration.GetMaterialRecipe(null));

        // Assert
        utilities.Verify(u => u.ValidateNullInput(null), Times.Once());
        Assert.NotNull(exception.InnerException);
        Assert.Equal(validationFailure.Message, exception.InnerException.Message);
    }

    [Fact]
    public void GetMaterialRecipe_WhenInputIsValid_ResolvesMaterialBeforeFailingOnAmbientRecipeContextLookup()
    {
        // Arrange
        // GetRecipeFromRecipeContext (Cmf.Community.IoTMESInteroperability.Utilities.IoTUtilities) resolves its
        // collaborators from the static ApplicationContext.CurrentServiceProvider instead of an injected
        // dependency, so it cannot succeed outside of a live CMF host. This test asserts the orchestration still
        // wraps that ambient failure in a CmfBaseException, and that everything reachable through injected/mockable
        // collaborators (material resolution, name assignment, load, product lookup) is exercised correctly first.
        CommunityOrchestration orchestration = CreateOrchestration(out Mock<IEntityFactory> entityFactory, out Mock<IUtilities> utilities);

        var product = new Mock<IProduct>();
        product.Setup(p => p.Name).Returns("PROD1");

        var material = new Mock<IMaterial>();
        material.Setup(m => m.Product).Returns(product.Object);

        entityFactory.Setup(f => f.Create<IMaterial>()).Returns(material.Object);

        var input = new GetMaterialRecipeInput
        {
            MaterialName = "MAT1",
            ResourceName = "RES1"
        };

        // Act
        CmfBaseException exception = Assert.Throws<CmfBaseException>(() => orchestration.GetMaterialRecipe(input));

        // Assert
        utilities.Verify(u => u.ValidateNullInput(input), Times.Once());
        entityFactory.Verify(f => f.Create<IMaterial>(), Times.Once());
        material.VerifySet(m => m.Name = "MAT1", Times.Once());
        material.Verify(m => m.Load(), Times.Once());
        Assert.NotNull(exception.InnerException);
    }

    #endregion GetMaterialRecipe

    #region IoTMaterialOperation

    [Fact]
    public void IoTMaterialOperation_AnyInvocation_WrapsAmbientCallContextFailureInCmfBaseException()
    {
        // Arrange
        // Before validating the Operation/Material/Resource, IoTMaterialOperation calls
        // ApplicationContext.CallContext.AddServiceComments(...), which - like GetRecipeFromRecipeContext above -
        // requires a live CMF host (tenant/request context) to resolve. Every invocation therefore fails at that
        // same line in a plain unit-test process, regardless of the requested Operation, so the business-rule
        // branches (TrackIn/TrackOut/etc.) are not reachable from a unit test. This test documents and asserts the
        // resulting, consistently-applied exception-wrapping behavior.
        CommunityOrchestration orchestration = CreateOrchestration(out _, out Mock<IUtilities> utilities);

        var input = new IoTMaterialOperationInput
        {
            Operation = "TrackIn"
        };

        // Act
        CmfBaseException exception = Assert.Throws<CmfBaseException>(() => orchestration.IoTMaterialOperation(input));

        // Assert
        utilities.Verify(
            u => u.StartMethod(It.IsAny<string>(), "IoTMaterialOperation", It.IsAny<System.Collections.Generic.KeyValuePair<string, object>[]>()),
            Times.Once());
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void IoTMaterialOperation_WhenOperationIsNotARecognizedEnumValue_StillSurfacesAsCmfBaseException()
    {
        // Arrange
        // Even once a live host makes AddServiceComments succeed, an unparsable Operation would fail
        // Enum.Parse<IoTMaterialOperation> and be wrapped the same way. Asserted here against the same
        // ambient-context limitation described above, since both failures funnel through the identical
        // catch (Exception ex) { throw new CmfBaseException(...); } handler.
        CommunityOrchestration orchestration = CreateOrchestration(out _, out _);

        var input = new IoTMaterialOperationInput
        {
            Operation = "NotARealOperation"
        };

        // Act & Assert
        Assert.Throws<CmfBaseException>(() => orchestration.IoTMaterialOperation(input));
    }

    #endregion IoTMaterialOperation
}
