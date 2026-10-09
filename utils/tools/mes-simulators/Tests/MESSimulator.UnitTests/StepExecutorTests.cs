using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class StepExecutorTests
    {
        private readonly List<string> _calls = [];

        // FirstValueRandom always picks the first resource of the step
        private const string CoatResource = "SUSS Coat-001";

        [Fact]
        public async Task ExecuteAsync_RunsActionsAroundTheTrackInAndTrackOut()
        {
            var tracker = new FakeTracker(_calls);
            var executor = Executor(tracker,
            [
                new FakeAction("before", StepHook.BeforeTrackIn, _calls),
                new FakeAction("after", StepHook.AfterTrackIn, _calls)
            ]);

            var lots = await executor.ExecuteAsync(new StepDefinition { Name = "COAT", Actions = ["before", "after"] }, Lot("Lot.1"), new LotRunData());

            Assert.Equal(
            [
                "before Lot.1",
                $"trackIn Lot.1 @ {CoatResource}",
                "after Lot.1",
                "trackOut Lot.1"
            ], _calls);
            Assert.Equal("Lot.1", Assert.Single(lots).Name);
        }

        [Fact]
        public async Task ExecuteAsync_RunsActionsOfTheSameHookInTheOrderTheStepListsThem()
        {
            var executor = Executor(new FakeTracker(_calls),
            [
                new FakeAction("first", StepHook.BeforeTrackIn, _calls),
                new FakeAction("second", StepHook.BeforeTrackIn, _calls)
            ]);

            await executor.ExecuteAsync(new StepDefinition { Name = "COAT", Actions = ["second", "first"] }, Lot("Lot.1"), new LotRunData());

            Assert.Equal(["second Lot.1", "first Lot.1"], _calls.Take(2));
        }

        [Fact]
        public async Task ExecuteAsync_UsesTheTrackOutActionInsteadOfTheStandardTrackOut_AndReturnsItsLots()
        {
            var split = new FakeAction("split", StepHook.TrackOut, _calls,
                context => context.OutputLots.AddRange([Lot("Lot.1.01"), Lot("Lot.1.02")]));
            var executor = Executor(new FakeTracker(_calls), [split]);

            var lots = await executor.ExecuteAsync(new StepDefinition { Name = "ASHING PRE", Actions = ["split"] }, Lot("Lot.1"), new LotRunData());

            Assert.DoesNotContain(_calls, c => c.StartsWith("trackOut"));
            Assert.Equal(["Lot.1.01", "Lot.1.02"], lots.Select(l => l.Name));
        }

        [Fact]
        public async Task ExecuteAsync_HoldsTheSingleMaterialLeaseFromTheSetupUntilTheTrackIn()
        {
            var executor = Executor(new FakeTracker(_calls), [new FakeAction("durables", StepHook.BeforeTrackIn, _calls)]);

            await executor.ExecuteAsync(new StepDefinition { Name = "Expose", Actions = ["durables"], SingleMaterial = true }, Lot("Lot.1"), new LotRunData());

            // No other lot can change the stepper (e.g. swap the reticle) between this lot's setup and its track-in;
            // once tracked out, lots waiting for the stepper stop waiting for this one
            Assert.Equal(
            [
                "acquire Rudolph Steppers-001",
                "durables Lot.1",
                "trackIn Lot.1 @ Rudolph Steppers-001",
                "release Rudolph Steppers-001",
                "trackOut Lot.1",
                "done Lot.1"
            ], _calls);
        }

        [Fact]
        public async Task ExecuteAsync_LotsWaitingForAFullResource_TakeTurnsRetrying()
        {
            var tracker = new FakeTracker(_calls);
            tracker.FullFor["Lot.1"] = 3;
            tracker.FullFor["Lot.2"] = 3;
            var executor = Executor(tracker);
            // One resource only: with more, a refused lot first tries the others
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0 };

            await Task.WhenAll(
                executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData()),
                executor.ExecuteAsync(step, Lot("Lot.2"), new LotRunData()));

            // Apart from each lot's first try, one lot retries until it gets in, then the other: their retries never interleave
            var tries = _calls.Where(c => c.StartsWith("trackIn")).Select(c => c.Split(' ')[1]).ToList();
            var retries = tries.ToList();
            retries.Remove("Lot.1");
            retries.Remove("Lot.2");
            Assert.Equal(6, retries.Count);
            int switches = retries.Zip(retries.Skip(1)).Count(pair => pair.First != pair.Second);
            Assert.True(switches <= 1, string.Join(", ", tries));
        }

        [Fact]
        public async Task ExecuteAsync_ARefusalBecauseLotsOfAnotherBomAreInProcess_WaitsAndRetries()
        {
            var tracker = new FakeTracker(_calls);
            tracker.OtherBomFor["Lot.1"] = 2;
            var executor = Executor(tracker);
            var step = new StepDefinition { Name = "COAT", MinSeconds = 0, MaxSeconds = 0 };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            // Refused twice, then in: the lot was not stopped by the refusals
            Assert.Equal(3, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
            Assert.Contains("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_AResourceHeldByAnotherBom_SendsTheLotToTheNextResource()
        {
            var tracker = new FakeTracker(_calls);
            tracker.OtherBomAt.Add("SUSS Coat-001");
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));
            var step = new StepDefinition { Name = "COAT", MinSeconds = 0, MaxSeconds = 0 };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            // Refused once by Coat-001, then straight to Coat-002 instead of waiting at Coat-001
            Assert.Equal(
            [
                "checkIn operator @ SUSS Coat-001",
                "trackIn Lot.1 @ SUSS Coat-001",
                "checkOut operator @ SUSS Coat-001",
                "checkIn operator @ SUSS Coat-002",
                "trackIn Lot.1 @ SUSS Coat-002",
                "trackOut Lot.1",
                "checkOut operator @ SUSS Coat-002"
            ], _calls);
        }

        [Fact]
        public async Task ExecuteAsync_ARefusalLongerThanTheWaitBudget_DoesNotDropTheLot()
        {
            var tracker = new FakeTracker(_calls);
            // More refusals than the wait budget (6 h simulated in 2-minute polls, 21600 polls on the test clock)
            tracker.OtherBomFor["Lot.1"] = 21_610;
            var executor = Executor(tracker);
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0 };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            Assert.Contains("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_AnOperatorCheckedOutByTheMes_IsCheckedInAgain_AndTheTrackInRetried()
        {
            var tracker = new FakeTracker(_calls);
            tracker.NotCheckedInFor["Lot.1"] = 1;
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0 };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            Assert.Equal(
            [
                "checkIn operator @ SUSS Devs-001",
                "trackIn Lot.1 @ SUSS Devs-001",
                "checkIn again operator @ SUSS Devs-001",
                "trackIn Lot.1 @ SUSS Devs-001",
                "trackOut Lot.1",
                "checkOut operator @ SUSS Devs-001"
            ], _calls);
        }

        [Fact]
        public async Task ExecuteAsync_ALotSetUpOnTheResource_IsReleasedOnceItHasTrackedIn()
        {
            var setUp = new SetUpLots();
            var duringTrackIn = -1;
            var tracker = new FakeTracker(_calls) { OnTrackIn = (lot, resource) => duringTrackIn = setUp.CountOthers(resource, "Other") };
            var executor = Executor(tracker,
                [new FakeAction("feed", StepHook.BeforeTrackIn, _calls, c => setUp.Add(c.ResourceName, c.Lot.Name))],
                setUpLots: setUp);
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0, Actions = ["feed"] };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            // Held between the setup and the track-in (the feeder can't be swapped then), released after
            Assert.Equal(1, duringTrackIn);
            Assert.Equal(0, setUp.CountOthers(CoatResource, "Other"));
        }

        [Fact]
        public async Task ExecuteAsync_ALotSetUpOnTheResource_IsReleasedWhenItsTrackInFails()
        {
            var setUp = new SetUpLots();
            var tracker = new FakeTracker(_calls);
            tracker.FailTrackInFor.Add("Lot.1");
            var executor = Executor(tracker,
                [new FakeAction("feed", StepHook.BeforeTrackIn, _calls, c => setUp.Add(c.ResourceName, c.Lot.Name))],
                setUpLots: setUp);
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0, Actions = ["feed"] };

            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData()));

            Assert.Equal(0, setUp.CountOthers(CoatResource, "Other"));
        }

        [Fact]
        public void SetUpLots_CountTheLotsOfAResourceOtherThanTheAsker()
        {
            var setUp = new SetUpLots();
            setUp.Add("Devs-001", "Lot.A");
            setUp.Add("Devs-001", "Lot.B");
            setUp.Add("Devs-002", "Lot.C");

            Assert.Equal(1, setUp.CountOthers("Devs-001", "Lot.A"));
            Assert.Equal(2, setUp.CountOthers("devs-001", "Lot.X"));
            Assert.Equal(0, setUp.CountOthers("Devs-003", "Lot.A"));

            setUp.Release("Lot.B");

            Assert.Equal(0, setUp.CountOthers("Devs-001", "Lot.A"));
        }

        [Fact]
        public async Task ExecuteAsync_ARefusalBecauseLotsOfAnotherProductAreInProcess_WaitsAndRetries()
        {
            var tracker = new FakeTracker(_calls);
            tracker.ProductMixFor["Lot.1"] = 2;
            var executor = Executor(tracker);
            var step = new StepDefinition { Name = "COAT", MinSeconds = 0, MaxSeconds = 0 };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            Assert.Equal(3, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
            Assert.Contains("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_AFeederSwappedAfterTheSetup_SetsTheResourceUpAgain()
        {
            var tracker = new FakeTracker(_calls);
            tracker.FeederSwappedFor["Lot.1"] = 2;
            var executor = Executor(tracker, [new FakeAction("feed", StepHook.BeforeTrackIn, _calls)]);
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0, Actions = ["feed"] };

            await executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData());

            // The setup and the track-in each ran three times: twice the feeder was swapped under the lot
            Assert.Equal(3, _calls.Count(c => c == "feed Lot.1"));
            Assert.Equal(3, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
            Assert.Contains("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_AFeederSwappedAgainAndAgain_GivesUpAfterFourAttempts()
        {
            var tracker = new FakeTracker(_calls);
            tracker.FeederSwappedFor["Lot.1"] = 99;
            var executor = Executor(tracker, [new FakeAction("feed", StepHook.BeforeTrackIn, _calls)]);
            var step = new StepDefinition { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0, Actions = ["feed"] };

            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData()));

            Assert.Equal(4, _calls.Count(c => c == "feed Lot.1"));
            Assert.DoesNotContain("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_AnotherTrackInFailure_StillFailsTheLot()
        {
            var tracker = new FakeTracker(_calls);
            tracker.FailTrackInFor.Add("Lot.1");
            var executor = Executor(tracker);
            var step = new StepDefinition { Name = "COAT", MinSeconds = 0, MaxSeconds = 0 };

            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(step, Lot("Lot.1"), new LotRunData()));

            Assert.Equal(1, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
        }

        [Theory]
        [InlineData(1, 120, 120)]      // speed 1: the simulated wait as it is
        [InlineData(12, 120, 10)]      // scaled
        [InlineData(3600, 120, 5)]     // 33 ms scaled: floored to the 5 s default, not to flood the MES
        public void PollInterval_IsScaled_ButNeverUnderTheMinimum(int speed, int seconds, int expectedSeconds)
        {
            var clock = new SimulationClock(Microsoft.Extensions.Options.Options.Create(new SimulationOptions { Speed = speed }));

            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), clock.PollInterval(seconds));
        }

        [Theory]
        [InlineData(1, 180)]           // 6 h simulated in 2-minute polls
        [InlineData(3600, 180)]        // 6 s scaled is too short: 15 min real in 5-second polls
        public void PollCount_CoversTheSimulatedBudget_AndAtLeastTheRealOne(int speed, int expected)
        {
            var clock = new SimulationClock(Microsoft.Extensions.Options.Options.Create(new SimulationOptions { Speed = speed }));

            Assert.Equal(expected, clock.PollCount(6 * 3600, 120, TimeSpan.FromMinutes(15)));
        }

        [Theory]
        [InlineData(1, 25, 600 + 25 * 90)]
        [InlineData(10, 25, (600 + 25 * 90) / 10)]
        [InlineData(1, 0, 600)]
        public void ProcessTime_IsTheLotTimePlusTheWaferTimeForEachWafer_ScaledBySpeed(int speed, int wafers, int expectedSeconds)
        {
            var clock = new SimulationClock(Microsoft.Extensions.Options.Options.Create(new SimulationOptions { Speed = speed }));
            var step = new StepDefinition { Name = "COAT", MinSeconds = 600, MaxSeconds = 900, MinSecondsPerWafer = 90, MaxSecondsPerWafer = 120 };

            // FirstValueRandom picks the minimum of each range
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), clock.ProcessTime(new FirstValueRandom(), step, wafers));
        }

        [Fact]
        public async Task ExecuteAsync_ReleasesTheSingleMaterialLease_WhenTheTrackInFails()
        {
            var tracker = new FakeTracker(_calls);
            tracker.FailTrackInFor.Add("Lot.1");
            var executor = Executor(tracker);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync(new StepDefinition { Name = "Expose", SingleMaterial = true }, Lot("Lot.1"), new LotRunData()));

            // The lease is released, and the lot no longer counts as one of ours in process there (it isn't)
            Assert.Equal(["release Rudolph Steppers-001", "done Lot.1"], _calls[^2..]);
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotTakeALease_ForOrdinaryResources()
        {
            var executor = Executor(new FakeTracker(_calls));

            await executor.ExecuteAsync(new StepDefinition { Name = "COAT" }, Lot("Lot.1"), new LotRunData());

            Assert.DoesNotContain(_calls, c => c.StartsWith("acquire"));
        }

        [Fact]
        public async Task ExecuteAsync_KeepsTheOperatorCheckedInFromBeforeTheActionsUntilAfterTheTrackOut()
        {
            var tracker = new FakeTracker(_calls);
            var executor = Executor(tracker, [new FakeAction("feeders", StepHook.BeforeTrackIn, _calls)],
                operatorCheckIn: new FakeOperatorCheckIn(_calls));

            await executor.ExecuteAsync(new StepDefinition { Name = "COAT", Actions = ["feeders"] }, Lot("Lot.1"), new LotRunData());

            Assert.Equal(
            [
                $"checkIn operator @ {CoatResource}",
                "feeders Lot.1",
                $"trackIn Lot.1 @ {CoatResource}",
                "trackOut Lot.1",
                $"checkOut operator @ {CoatResource}"
            ], _calls);
        }

        [Fact]
        public async Task ExecuteAsync_ChecksTheOperatorOut_WhenTheTrackInFails()
        {
            var tracker = new FakeTracker(_calls);
            tracker.FailTrackInFor.Add("Lot.1");
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync(new StepDefinition { Name = "COAT" }, Lot("Lot.1"), new LotRunData()));

            Assert.Equal($"checkOut operator @ {CoatResource}", _calls[^1]);
        }

        [Fact]
        public async Task ExecuteAsync_FailsClearly_ForAnUnknownAction()
        {
            var executor = Executor(new FakeTracker(_calls));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync(new StepDefinition { Name = "COAT", Actions = ["feedrs"] }, Lot("Lot.1"), new LotRunData()));

            Assert.Contains("unknown action 'feedrs'", ex.Message);
            Assert.Empty(_calls);
        }

        [Fact]
        public async Task ExecuteAsync_FailsClearly_ForAStepWithoutResources()
        {
            var executor = Executor(new FakeTracker(_calls));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.ExecuteAsync(new StepDefinition { Name = "AOI" }, Lot("Lot.1"), new LotRunData()));

            Assert.Contains("Step 'AOI' has no resources, in simulationConfiguration.json or in the MES", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_SkipsResourcesThatDoNotOfferTheLotsService()
        {
            var tracker = new FakeTracker(_calls);
            var eligibility = new FakeEligibility();
            eligibility.Ineligible.Add("SUSS Coat-001");
            var executor = Executor(tracker, eligibility: eligibility);

            await executor.ExecuteAsync(TestLine().FindStep("COAT")!, Lot("Lot.1", atStep: "COAT"), new LotRunData());

            Assert.Contains("trackIn Lot.1 @ SUSS Coat-002", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_TriesTheNextResource_WhenABeforeTrackInActionFindsTheResourceUnsuitable()
        {
            var tracker = new FakeTracker(_calls);
            var feeders = new FakeAction("feeders", StepHook.BeforeTrackIn, _calls, context =>
            {
                if (context.ResourceName == "SUSS Coat-001")
                {
                    throw new ResourceUnsuitableException("none of its feeders takes 'PI Photoresist'");
                }
            });
            var line = TestLine();
            var step = line.FindStep("COAT")! with { Actions = ["feeders"] };

            await Executor(tracker, [feeders], line: line).ExecuteAsync(step, Lot("Lot.1", atStep: "COAT"), new LotRunData());

            Assert.Equal(["feeders Lot.1", "feeders Lot.1", "trackIn Lot.1 @ SUSS Coat-002", "trackOut Lot.1"], _calls);
        }

        [Fact]
        public async Task ExecuteAsync_Fails_WhenNoResourceCanServeTheLot()
        {
            var tracker = new FakeTracker(_calls);
            var feeders = new FakeAction("feeders", StepHook.BeforeTrackIn, _calls, _ => throw new ResourceUnsuitableException("no feeder"));
            var line = TestLine();
            var step = line.FindStep("COAT")! with { Actions = ["feeders"] };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Executor(tracker, [feeders], line: line).ExecuteAsync(step, Lot("Lot.1", atStep: "COAT"), new LotRunData()));

            Assert.Contains("No COAT resource can run 'Lot.1'", error.Message);
            Assert.DoesNotContain(_calls, c => c.StartsWith("trackIn"));
        }

        [Fact]
        public async Task ExecuteAsync_WaitsAndRetries_WhenNoResourceCanServeTheLotForNow()
        {
            var tracker = new FakeTracker(_calls);
            int attempts = 0;
            var feeders = new FakeAction("feeders", StepHook.BeforeTrackIn, _calls, _ =>
            {
                // Both coaters busy in the first round, free in the second
                if (++attempts <= 2)
                {
                    throw new ResourceUnsuitableException("shared feeder in use", temporary: true);
                }
            });
            var line = TestLine();
            var step = line.FindStep("COAT")! with { Actions = ["feeders"] };

            await Executor(tracker, [feeders], line: line).ExecuteAsync(step, Lot("Lot.1", atStep: "COAT"), new LotRunData());

            Assert.Equal(3, attempts);
            Assert.Contains("trackIn Lot.1 @ SUSS Coat-001", _calls);
        }
    }
}
