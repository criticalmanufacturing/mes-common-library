using MESSimulator.Steps.Actions;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class DurablesActionTests
    {
        [Fact]
        public void PickableDurables_LeaveOutTheOnesAlreadyPickedForTheSameMount()
        {
            var queued = new[] { new Cmf.Navigo.BusinessObjects.Material { Id = 1 }, new() { Id = 2 }, new() { Id = 3 } };

            Assert.Equal([1, 3], DurablesAction.PickableDurables(queued, [2]).Select(m => m.Id));
            Assert.Equal([1, 2, 3], DurablesAction.PickableDurables(queued, []).Select(m => m.Id));
            Assert.Empty(DurablesAction.PickableDurables(queued, [1, 2, 3]));
        }

        [Theory]
        [InlineData("When changing the resource durables with materials in-process at the resource, the resource attached durable products and positions must remain unchanged.", true)]
        [InlineData("The object RET L1 2EDN.015 is not Queued.", false)]
        [InlineData("no queued 'Grinding Wheel 200mm' durable to mount", false)]
        public void DurableChangeBlockedByLotsInProcess_RecognizesTheMesRefusal(string message, bool expected)
        {
            Assert.Equal(expected, DurablesAction.DurableChangeBlockedByLotsInProcess(message));
        }

        [Theory]
        [InlineData("The object RET L1 2EDN.015 is not Queued.", true)]
        [InlineData("the object Blade 0.21#001 IS NOT QUEUED", true)]
        [InlineData("no queued 'Reticle Layer FUSE#2EDN7524F' durable to mount", false)]
        [InlineData("When changing the resource durables with materials in-process at the resource, the resource attached durable products and positions must remain unchanged.", false)]
        public void DurableTakenMeanwhile_RecognizesTheMesRefusalOfADurableAnotherLotAttached(string message, bool expected)
        {
            Assert.Equal(expected, DurablesAction.DurableTakenMeanwhile(message));
        }
    }
}
