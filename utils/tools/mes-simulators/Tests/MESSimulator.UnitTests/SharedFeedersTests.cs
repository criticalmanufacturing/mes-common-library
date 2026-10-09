using Cmf.Navigo.BusinessObjects;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class SharedFeedersTests
    {
        private static readonly Resource Feeder = new() { Id = 7, Name = "Coater Feeder-001", Type = "Feeder" };

        private readonly FakeMes _mes = new();
        private readonly SetUpLots _setUpLots = new();
        private readonly Dictionary<string, int> _inProcess = new(StringComparer.OrdinalIgnoreCase);

        private SharedFeeders Create()
        {
            // Both coaters have the same feeder
            _mes.ResourcesFake.Handlers["GetByName"] = args =>
            {
                var name = (string)args[0]!;
                return new Resource { Name = name, MaterialsInProcessCount = _inProcess.GetValueOrDefault(name) };
            };
            _mes.MasterDataFake.Handlers["LoadRelations"] = args =>
            {
                var resource = (Resource)args[0]!;
                if (resource.Name.StartsWith("SUSS Coat"))
                {
                    resource.RelationCollection = new() { ["SubResource"] = [new SubResource { SourceEntity = resource, TargetEntity = Feeder }] };
                }
                return resource;
            };
            return new SharedFeeders(_mes, TestLine(), _setUpLots);
        }

        [Fact]
        public void AFeederBelongsToEveryResourceThatHasIt()
        {
            Assert.Equal(["SUSS Coat-001", "SUSS Coat-002"], Create().ParentsOf(Feeder, "SUSS Coat-001").Order());
        }

        [Fact]
        public void ALotInProcessOnTheOtherResource_MakesTheSharedFeederBusy()
        {
            var feeders = Create();
            _inProcess["SUSS Coat-002"] = 1;

            Assert.Contains("SUSS Coat-002", feeders.WhyBusy(Feeder, "SUSS Coat-001", "Lot.1"));
        }

        [Fact]
        public void ALotSetUpOnTheOtherResource_MakesTheSharedFeederBusy()
        {
            var feeders = Create();
            _setUpLots.Add("SUSS Coat-002", "Lot.2");

            Assert.Contains("set up on 'SUSS Coat-002'", feeders.WhyBusy(Feeder, "SUSS Coat-001", "Lot.1"));
        }

        [Fact]
        public void NothingOnAnyResource_TheFeederIsFree()
        {
            var feeders = Create();
            _setUpLots.Add("SUSS Coat-001", "Lot.1");

            // The lot being prepared doesn't count against itself
            Assert.Null(feeders.WhyBusy(Feeder, "SUSS Coat-001", "Lot.1"));
        }
    }
}
