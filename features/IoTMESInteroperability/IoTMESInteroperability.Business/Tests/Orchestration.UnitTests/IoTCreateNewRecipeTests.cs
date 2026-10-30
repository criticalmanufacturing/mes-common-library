using System.Collections.Generic;
using Cmf.Community.IoTMESInteroperability.Actions.ConnectIoT;
using Cmf.Foundation.BaseTestsUtils;
using Cmf.Foundation.Common.Abstractions;
using Cmf.Navigo.BusinessObjects.Abstractions;
using Moq;
using Xunit;

namespace Cmf.Community.IoTMESInteroperability.Tests.Actions.ConnectIoT;

public class IoTCreateNewRecipeTests : ActionBaseTests
{
    private readonly Mock<IEntityFactory> _entityFactory = new();
    private readonly Mock<IResource> _resource = new();
    private readonly Mock<IRecipe> _recipe = new();

    public IoTCreateNewRecipeTests()
    {
        _recipe.SetupProperty(r => r.Name);
        _recipe.SetupProperty(r => r.Revision);
        _entityFactory.Setup(f => f.Create<IResource>()).Returns(_resource.Object);
        _entityFactory.Setup(f => f.Create<IRecipe>()).Returns(_recipe.Object);
        AddMockToActionInput(_entityFactory);
        ActionInput["ResourceName"] = "RES1";
        ActionInput["ResourceRecipeName"] = "RRN1";
        ActionInput["RecipeName"] = "RECIPE1";
    }

    [Fact]
    public void DeeTestCondition_AlwaysReturnsTrue()
    {
        Assert.True(new IoTCreateNewRecipe().DeeTestCondition(new Dictionary<string, object>()));
    }

    [Fact]
    public void DeeActionCode_WithRevision_WhenResourceAlreadyHasThatExactRecipeAndRevision_ReturnsSuccessWithoutChangingResource()
    {
        ActionInput["RecipeRevision"] = "2";
        _recipe.Setup(r => r.ObjectExists("RECIPE1", "2")).Returns(true);
        _recipe.Setup(r => r.ResourceRecipeName).Returns("RRN1");

        var currentRecipeOnResource = new Mock<IRecipe>();
        currentRecipeOnResource.Setup(r => r.Name).Returns("RECIPE1");
        currentRecipeOnResource.Setup(r => r.Revision).Returns("2");
        _resource.Setup(r => r.ObjectExists()).Returns(true);
        _resource.Setup(r => r.CurrentRecipe).Returns(currentRecipeOnResource.Object);

        var result = new IoTCreateNewRecipe().DeeActionCode(ActionInput);

        Assert.Equal("Success", result["Result"]);
        _resource.Verify(r => r.Load(1), Times.Once);
        _recipe.Verify(r => r.Load(), Times.Once);
        _resource.Verify(r => r.SetResourceRecipe(It.IsAny<IRecipe>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void DeeActionCode_WithRevision_WhenResourceHasADifferentRevision_SetsTheRecipeOnTheResource()
    {
        ActionInput["RecipeRevision"] = "2";
        _recipe.Setup(r => r.ObjectExists("RECIPE1", "2")).Returns(true);
        _recipe.Setup(r => r.ResourceRecipeName).Returns("RRN1");

        var currentRecipeOnResource = new Mock<IRecipe>();
        currentRecipeOnResource.Setup(r => r.Name).Returns("RECIPE1");
        currentRecipeOnResource.Setup(r => r.Revision).Returns("1");
        _resource.Setup(r => r.ObjectExists()).Returns(true);
        _resource.Setup(r => r.CurrentRecipe).Returns(currentRecipeOnResource.Object);

        var result = new IoTCreateNewRecipe().DeeActionCode(ActionInput);

        Assert.Equal("Success", result["Result"]);
        _resource.Verify(r => r.SetResourceRecipe(_recipe.Object, true), Times.Once);
    }

    [Fact]
    public void DeeActionCode_WithoutRevision_WhenResourceAlreadyHasThatRecipe_ReturnsSuccessWithoutChangingResource()
    {
        _recipe.Setup(r => r.ObjectExists()).Returns(true);
        _recipe.Setup(r => r.ResourceRecipeName).Returns("RRN1");

        var currentRecipeOnResource = new Mock<IRecipe>();
        currentRecipeOnResource.Setup(r => r.Name).Returns("RECIPE1");
        _resource.Setup(r => r.CurrentRecipe).Returns(currentRecipeOnResource.Object);

        var result = new IoTCreateNewRecipe().DeeActionCode(ActionInput);

        Assert.Equal("Success", result["Result"]);
        _recipe.Verify(r => r.Load(), Times.Once);
        _resource.Verify(r => r.SetResourceRecipe(It.IsAny<IRecipe>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void DeeActionCode_WithoutRevision_WhenResourceDoesNotHaveThatRecipeYet_SetsTheRecipeOnTheResource()
    {
        _recipe.Setup(r => r.ObjectExists()).Returns(true);
        _recipe.Setup(r => r.ResourceRecipeName).Returns("RRN1");
        _resource.Setup(r => r.CurrentRecipe).Returns((IRecipe)null);

        var result = new IoTCreateNewRecipe().DeeActionCode(ActionInput);

        Assert.Equal("Success", result["Result"]);
        _resource.Verify(r => r.SetResourceRecipe(_recipe.Object, true), Times.Once);
    }
}
