using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class LotFlowTests
    {
        private readonly List<string> _calls = [];

        [Fact]
        public async Task RunAsync_FollowsTheMesRouteThroughConfiguredSteps_AndQueuesAtTheBatchStep()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "DEVELOPER", "PRE_CURE"]);
            var (flow, queues) = Flow(tracker);

            await flow.RunAsync(Lot("Lot.1", atStep: "COAT", quantity: 25), new LotRunData());

            Assert.Equal(
            [
                "trackIn Lot.1 @ SUSS Coat-001",
                "trackOut Lot.1",
                "trackIn Lot.1 @ SUSS Devs-001",
                "trackOut Lot.1"
            ], _calls);
            Assert.Equal((1, 25), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task RunAsync_LeavesTheLot_AtAStepThatIsNotInTheLine()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "SAR CAPTURE RESULT", "DEVELOPER"]);
            var (flow, queues) = Flow(tracker);

            await flow.RunAsync(Lot("Lot.1", atStep: "COAT"), new LotRunData());

            Assert.Equal(["trackIn Lot.1 @ SUSS Coat-001", "trackOut Lot.1"], _calls);
            Assert.Equal((0, 0), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task RunAsync_LeavesTheLot_AtAStepWithoutResources()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "AOI", "DEVELOPER"]);
            var (flow, _) = Flow(tracker);

            await flow.RunAsync(Lot("Lot.1", atStep: "COAT"), new LotRunData());

            Assert.DoesNotContain(_calls, c => c.Contains("SUSS Devs"));
        }

        [Fact]
        public async Task RunAsync_MovesTheLotThroughPassThroughSteps_UntilTheEndOfItsFlow()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["DEVELOPER", "Wafer PACKING", "Wafer Reception BE"]);
            var (flow, _) = Flow(tracker);
            var lot = Lot("Lot.1", atStep: "DEVELOPER");

            await flow.RunAsync(lot, new LotRunData());

            Assert.Equal(
            [
                "trackIn Lot.1 @ SUSS Devs-001",
                "trackOut Lot.1",
                "moveNext Lot.1",
                "moveNext Lot.1: end of flow"
            ], _calls);
            Assert.Equal("Wafer Reception BE", MESSimulator.Line.LineDefinition.CurrentStepName(lot));
        }

        [Fact]
        public async Task RunAsync_AtTheEndOfTheLine_ClosesTheOrderWhenComplete_ThenShipsWithoutReceiving()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["DEVELOPER", "Wafer Shipping FE", "Wafer Reception BE"]);
            var (flow, _) = Flow(tracker);
            var lot = Lot("Lot.1", atStep: "DEVELOPER");

            await flow.RunAsync(lot, new LotRunData());

            Assert.Equal(
            [
                "trackIn Lot.1 @ SUSS Devs-001",
                "trackOut Lot.1",
                "close order of Lot.1 at Wafer Shipping FE",
                "ship Lot.1 (not received)"
            ], _calls);
            Assert.Equal("Wafer Shipping FE", MESSimulator.Line.LineDefinition.CurrentStepName(lot));
        }

        [Fact]
        public async Task RunAsync_ALotSentToRework_FollowsItsReworkFlow_AndReturns()
        {
            var tracker = new FakeTracker(_calls);
            // The MES route: COAT -> (rework) RwCOAT -> back to DEVELOPER
            tracker.Route.AddRange(["RwCOAT", "DEVELOPER", "PRE_CURE"]);
            var chaos = new FakeChaos(tracker);
            chaos.ReworkAt["COAT"] = "RwCOAT";
            var (flow, queues) = Flow(tracker, chaos: chaos);

            await flow.RunAsync(Lot("Lot.1", atStep: "COAT", quantity: 25), new LotRunData());

            Assert.Equal(
            [
                "rework Lot.1 from COAT to RwCOAT",
                "trackIn Lot.1 @ SUSS Coat-002",
                "trackOut Lot.1",
                "trackIn Lot.1 @ SUSS Devs-001",
                "trackOut Lot.1"
            ], _calls);
            Assert.Equal((1, 25), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task RunAsync_ALotWithAllItsWafersScrapped_LeavesTheLine_AndChecksItsOrder()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "DEVELOPER"]);
            var chaos = new FakeChaos(tracker);
            chaos.ScrapAllAt.Add("COAT");
            var (flow, _) = Flow(tracker, chaos: chaos);

            await flow.RunAsync(Lot("Lot.1", atStep: "COAT"), new LotRunData());

            // Its order may be complete now (the scrapped wafers were all it still had in progress)
            Assert.Equal(["scrap all of Lot.1 at COAT", "close order of Lot.1 at COAT"], _calls);
        }

        [Fact]
        public async Task RunAsync_MovesTheLotPastAConditionalStep_WhenTheConditionSaysNo()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["INSP CD", "DEVELOPER"]);
            var (flow, _) = Flow(tracker, conditions: [new FakeCondition("never", _ => false)], inspCdCondition: "never");

            await flow.RunAsync(Lot("Lot.1", atStep: "INSP CD"), new LotRunData());

            // The queued lot's process is skipped before it is moved next (the MES refuses to move a queued lot)
            Assert.Equal(
            [
                "skipProcess Lot.1",
                "moveNext Lot.1",
                "trackIn Lot.1 @ SUSS Devs-001",
                "trackOut Lot.1"
            ], _calls);
        }

        [Fact]
        public async Task RunAsync_ProcessesAConditionalStep_WhenTheConditionSaysYes()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["INSP CD"]);
            var (flow, _) = Flow(tracker, conditions: [new FakeCondition("always", _ => true)], inspCdCondition: "always");

            await flow.RunAsync(Lot("Lot.1", atStep: "INSP CD"), new LotRunData());

            Assert.Contains("trackIn Lot.1 @ VISTEC-001", _calls);
        }

        [Fact]
        public async Task RunAsync_Stops_WhenTheMesLeavesTheLotAtTheStepItWasJustProcessedAt()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT"]); // no next step: the lot stays at COAT
            var (flow, _) = Flow(tracker);

            await flow.RunAsync(Lot("Lot.1", atStep: "COAT"), new LotRunData());

            Assert.Equal(["trackIn Lot.1 @ SUSS Coat-001", "trackOut Lot.1"], _calls);
        }

        [Fact]
        public async Task Start_ALotThatFailsWhileStillQueued_IsRunAgain()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            tracker.FailTrackInTimesFor["Lot.1"] = 1;
            var (flow, queues) = Flow(tracker);

            // The fake lot stays Queued when its track-in fails: nothing was done to it, so it is run again
            await flow.Start(Lot("Lot.1", atStep: "COAT", quantity: 5), new LotRunData());

            Assert.Equal(2, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
            Assert.Equal((1, 5), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task Start_ALotThatKeepsFailing_GoesToTheFailedLotFallback_Once()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            tracker.FailTrackInFor.Add("Lot.1");
            var (flow, queues) = Flow(tracker);

            await flow.Start(Lot("Lot.1", atStep: "COAT", quantity: 5), new LotRunData());

            // The first run and its 3 retries while still queued, then the fallback
            Assert.Equal(4, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
            Assert.Equal(["failed Lot.1 at COAT"], _calls.Where(c => c.StartsWith("failed")));
            Assert.Equal((0, 0), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task Start_ALotThatSucceeds_NeverGoesToTheFailedLotFallback()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            var (flow, _) = Flow(tracker);

            await flow.Start(Lot("Lot.1", atStep: "COAT", quantity: 5), new LotRunData());

            Assert.DoesNotContain(_calls, c => c.StartsWith("failed"));
        }

        [Fact]
        public async Task RunAsync_ContinuesEverySplitLotOnItsOwn_AndOneFailureDoesNotStopTheOthers()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["ASHING PRE", "COAT", "PRE_CURE"]);
            tracker.FailTrackInFor.Add("Lot.1.02");
            var split = new FakeAction("split", StepHook.TrackOut, _calls, context =>
                context.OutputLots.AddRange([Lot("Lot.1.01", "COAT", 5), Lot("Lot.1.02", "COAT", 5), Lot("Lot.1.03", "COAT", 5)]));
            var (flow, queues) = Flow(tracker, actions: [split]);

            await flow.RunAsync(Lot("Lot.1", atStep: "ASHING PRE"), new LotRunData());

            Assert.Equal((2, 10), queues.Peek("PRE_CURE"));
            Assert.Equal(["Lot.1.01", "Lot.1.03"], queues.TakeAll("PRE_CURE").Select(l => l.Name).Order());
        }
    }
}
