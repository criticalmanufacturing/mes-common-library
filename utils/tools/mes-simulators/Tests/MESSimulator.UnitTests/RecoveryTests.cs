using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    /// <summary>
    /// Lots the simulator used to lose: an operator the MES keeps checking out, a refusal during the setup, a lot left
    /// in process after a failed track-out, a lot already processed at its step.
    /// </summary>
    public class RecoveryTests
    {
        private readonly List<string> _calls = [];

        private static StepDefinition Developer => new() { Name = "DEVELOPER", MinSeconds = 0, MaxSeconds = 0 };

        [Fact]
        public async Task ExecuteAsync_ChecksTheOperatorInUpToThreeTimes()
        {
            var tracker = new FakeTracker(_calls);
            tracker.NotCheckedInFor["Lot.1"] = OperatorRetry.MaxCheckIns;
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));

            await executor.ExecuteAsync(Developer, Lot("Lot.1"), new LotRunData());

            Assert.Equal(OperatorRetry.MaxCheckIns, _calls.Count(c => c.StartsWith("checkIn again")));
            Assert.Contains("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task ExecuteAsync_GivesUpWhenTheOperatorCantBeCheckedIn()
        {
            var tracker = new FakeTracker(_calls);
            tracker.NotCheckedInFor["Lot.1"] = OperatorRetry.MaxCheckIns + 1;
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(Developer, Lot("Lot.1"), new LotRunData()));

            Assert.Contains("not checked in", ex.Message);
        }

        [Fact]
        public async Task ExecuteAsync_TheOperatorCheckedOutAtTheTrackOut_IsCheckedInAgain()
        {
            var tracker = new FakeTracker(_calls);
            tracker.NotCheckedInAtTrackOutFor["Lot.1"] = 1;
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));

            await executor.ExecuteAsync(Developer, Lot("Lot.1"), new LotRunData());

            Assert.Equal(["trackOut failed Lot.1", "checkIn again operator @ SUSS Devs-001", "trackOut Lot.1"],
                _calls.Where(c => c.StartsWith("trackOut") || c.StartsWith("checkIn again")));
        }

        [Theory]
        [InlineData("It's not possible to track-in the Material to Resource SUSS Coat-001 because the required Material BOM does not match the current BOM at the Resource and the Resource is configured to only accept Materials in-process with the same BOM.")]
        [InlineData("Employee 'admin' is not checked in.")]
        public async Task ExecuteAsync_ARefusalDuringTheSetup_TriesTheNextResource(string refusal)
        {
            var tracker = new FakeTracker(_calls);
            var setup = new FakeAction("setup", StepHook.BeforeTrackIn, _calls, context =>
            {
                if (context.ResourceName == "SUSS Coat-001")
                {
                    throw new InvalidOperationException(refusal);
                }
            });
            var line = TestLine();
            var executor = Executor(tracker, [setup], line: line);

            await executor.ExecuteAsync(line.FindStep("COAT")! with { Actions = ["setup"], MinSeconds = 0, MaxSeconds = 0 }, Lot("Lot.1"), new LotRunData());

            Assert.Contains("trackIn Lot.1 @ SUSS Coat-002", _calls);
            Assert.DoesNotContain("trackIn Lot.1 @ SUSS Coat-001", _calls);
        }

        [Fact]
        public async Task ResumeAsync_TracksOutTheLotLeftInProcess()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["DEVELOPER", "PRE_CURE"]);
            var lot = Lot("Lot.1", atStep: "DEVELOPER");
            await tracker.DispatchAndTrackInAsync(lot, "SUSS Devs-001");
            var executor = Executor(tracker, operatorCheckIn: new FakeOperatorCheckIn(_calls));

            var lots = await executor.ResumeAsync(Developer, lot, new LotRunData());

            Assert.Equal("PRE_CURE", MESSimulator.Line.LineDefinition.CurrentStepName(Assert.Single(lots)));
            Assert.Contains("checkIn operator @ SUSS Devs-001", _calls);
            Assert.Contains("trackOut Lot.1", _calls);
        }

        [Fact]
        public async Task Start_ALotWhoseTrackOutFailed_IsResumed_AndContinues()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            tracker.FailTrackOutTimesFor["Lot.1"] = 1;
            var (flow, queues) = Flow(tracker);

            await flow.Start(Lot("Lot.1", atStep: "COAT", quantity: 5), new LotRunData());

            Assert.Equal(1, _calls.Count(c => c.StartsWith("trackIn Lot.1")));
            Assert.Equal(["trackOut failed Lot.1", "trackOut Lot.1"], _calls.Where(c => c.StartsWith("trackOut")));
            Assert.DoesNotContain(_calls, c => c.StartsWith("failed"));
            Assert.Equal((1, 5), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public async Task Start_ALotThatCantBeResumed_GoesToTheFallback()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            tracker.FailTrackOutTimesFor["Lot.1"] = 10;
            var (flow, _) = Flow(tracker);

            await flow.Start(Lot("Lot.1", atStep: "COAT"), new LotRunData());

            // The step's track-out, then two resumes
            Assert.Equal(3, _calls.Count(c => c == "trackOut failed Lot.1"));
            Assert.Equal(["failed Lot.1 at COAT"], _calls.Where(c => c.StartsWith("failed")));
        }

        [Fact]
        public async Task RunAsync_ALotAlreadyProcessedAtItsStep_IsMovedOnNotRunAgain()
        {
            var tracker = new FakeTracker(_calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            var lot = Lot("Lot.1", atStep: "COAT", quantity: 5);
            lot.SystemState = MaterialSystemState.Processed;
            var (flow, queues) = Flow(tracker);

            await flow.RunAsync(lot, new LotRunData());

            Assert.DoesNotContain(_calls, c => c.StartsWith("trackIn"));
            Assert.Contains("moveNext Lot.1", _calls);
            Assert.Equal((1, 5), queues.Peek("PRE_CURE"));
        }

        [Fact]
        public void ASplitChild_KeepsTheParentsReworks()
        {
            var parent = new LotRunData { Reworks = 1 };

            Assert.Equal(1, parent.ForChild().Reworks);
        }
    }
}
