using Cmf.Foundation.Security;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MESSimulator.Line;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class OperatorAndCancellationTests
    {
        [Fact]
        public async Task CheckInAgain_ClocksTheEmployeeInAgain_AndChecksItIn()
        {
            var mes = new FakeMes();
            var employee = new Employee { Id = 5, Name = "admin", RequireClockIn = true, ClockedState = ClockedState.ClockedIn };
            mes.LaborFake.Handlers["GetCurrentUser"] = _ => new User { UserAccount = "admin" };
            mes.LaborFake.Handlers["FindEmployeeByUserAccount"] = _ => employee;
            // A shift change clocked it out
            mes.MasterDataFake.Handlers["GetById"] = _ => new Employee { Id = 5, Name = "admin", RequireClockIn = true, ClockedState = ClockedState.ClockedOut };
            mes.ResourcesFake.Handlers["GetByName"] = args => new Resource { Name = (string)args[0]!, RequireCheckInForMaterialOperations = true };
            var checkIn = new OperatorCheckIn(mes, TestLine(), NullLogger<OperatorCheckIn>.Instance);
            checkIn.EnsureOperator();
            mes.Calls.Clear();

            await checkIn.CheckInAgainAsync("NEXX-001");

            Assert.Equal(["LaborGateway.ClockIn", "LaborGateway.CheckIn"], mes.Calls.Where(c => c.StartsWith("LaborGateway")));
        }

        [Fact]
        public async Task CheckInAgain_AnOperatorTheMesStillListsAsCheckedIn_IsCheckedInAnyway()
        {
            var mes = new FakeMes();
            var employee = new Employee { Id = 5, Name = "admin" };
            mes.LaborFake.Handlers["GetCurrentUser"] = _ => new User { UserAccount = "admin" };
            mes.LaborFake.Handlers["FindEmployeeByUserAccount"] = _ => employee;
            mes.LaborFake.Handlers["GetCheckedInEmployees"] = _ => new List<Employee> { employee };
            mes.MasterDataFake.Handlers["GetById"] = _ => employee;
            mes.ResourcesFake.Handlers["GetByName"] = args => new Resource { Name = (string)args[0]! };
            var checkIn = new OperatorCheckIn(mes, TestLine(), NullLogger<OperatorCheckIn>.Instance);
            checkIn.EnsureOperator();

            await checkIn.CheckInAgainAsync("NEXX-001");

            Assert.Contains("LaborGateway.CheckIn", mes.Calls);
        }

        [Fact]
        public async Task ALotWaitingBeforeItsTrackIn_StopsCleanlyWhenTheSimulatorStops()
        {
            var calls = new List<string>();
            var tracker = new FakeTracker(calls);
            tracker.Route.AddRange(["COAT", "PRE_CURE"]);
            // A slow clock: the queue time before the track-in is long
            var line = TestLine();
            var executor = new StepExecutor(tracker, new FakeOccupancy(calls), new SilentOperatorCheckIn(),
                new ResourceCatalog(line, new FakeStepResources(), new FirstValueRandom()), new FakeEligibility(),
                new SimulationClock(Options.Create(new SimulationOptions { Speed = 1m })), new FirstValueRandom(), [],
                NullLogger<StepExecutor>.Instance);
            using var stop = new CancellationTokenSource();

            var run = executor.ExecuteAsync(line.FindStep("COAT")! with { MinSeconds = 3600, MaxSeconds = 3600 }, Lot("Lot.1", atStep: "COAT"), new LotRunData(), stop.Token);
            stop.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.DoesNotContain(calls, c => c.StartsWith("trackIn"));
        }
    }
}
