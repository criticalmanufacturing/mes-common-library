using System.Data;
using Cmf.Foundation.BusinessObjects;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MESSimulator.Steps;
using Xunit;
using static MESSimulator.UnitTests.StepTestFactory;

namespace MESSimulator.UnitTests
{
    public class OccupancyAndXmlTests
    {
        private static NgpDataSet Rows(params string[] names)
        {
            var ds = new DataSet("NewDataSet");
            var table = ds.Tables.Add("Table1");
            table.Columns.Add("Name");
            foreach (var name in names)
            {
                table.Rows.Add(name);
            }
            return new NgpDataSet { XMLSchema = ds.GetXmlSchema(), DataXML = ds.GetXml() };
        }

        [Fact]
        public void ToDataSet_ReadsTheRows()
        {
            var ds = Utilities.ToDataSet(Rows("Lot.A", "Lot.B"));

            Assert.Equal(["Lot.A", "Lot.B"], ds.Tables[0].Rows.Cast<DataRow>().Select(r => (string)r["Name"]));
        }

        [Fact]
        public void ToDataSet_RefusesADtd()
        {
            var data = Rows("Lot.A");
            data.DataXML = "<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]>" + data.DataXML;

            Assert.Throws<System.Xml.XmlException>(() => Utilities.ToDataSet(data));
        }

        private static ResourceOccupancyPolicy Policy(FakeMes mes) =>
            new(mes, new ResourceLocks(),
                Options.Create(new ResourceOccupancyOptions
                {
                    MaxChecks = 2,
                    WaitBetweenChecks = TimeSpan.Zero,
                    OwnLotMaxWaitSeconds = 180,
                    OwnLotMinRealWait = TimeSpan.Zero
                }),
                FastClock(), NullLogger<ResourceOccupancyPolicy>.Instance);

        [Fact]
        public async Task AResourceCountingMaterialsThatTheQueryDoesntList_IsNotTakenForOurs()
        {
            // The resource says 1 in process, but the query lists none: before, "all of them are ours" held for the
            // empty list and the lot waited forever
            var mes = new FakeMes();
            mes.ResourcesFake.Handlers["GetByName"] = _ => new Resource { Id = 1, Name = "Stepper", MaterialsInProcessCount = 1 };
            mes.MasterDataFake.Handlers["ExecuteQueryByName"] = _ => Rows();

            var acquire = Policy(mes).AcquireFreeResourceAsync("Stepper", Lot("Lot.1"));

            Assert.Same(acquire, await Task.WhenAny(acquire, Task.Delay(TimeSpan.FromSeconds(10))));
            (await acquire).Dispose();
        }

        [Fact]
        public async Task OurOwnLot_IsWaitedFor_ButNotForever()
        {
            var mes = new FakeMes();
            mes.ResourcesFake.Handlers["GetByName"] = _ => new Resource { Id = 1, Name = "Stepper", MaterialsInProcessCount = 1 };
            mes.MasterDataFake.Handlers["ExecuteQueryByName"] = _ => Rows("Lot.Stuck");
            mes.MaterialsFake.Handlers["GetByName"] = args => new Material { Name = (string)args[0]!, SystemState = MaterialSystemState.InProcess };
            var policy = Policy(mes);

            // Lot.Stuck was let on by this run and never done (its flow failed and left it there)
            (await policy.AcquireFreeResourceAsync("Stepper", Lot("Lot.Stuck"))).Dispose();
            var acquire = policy.AcquireFreeResourceAsync("Stepper", Lot("Lot.Next"));

            Assert.Same(acquire, await Task.WhenAny(acquire, Task.Delay(TimeSpan.FromSeconds(30))));
            (await acquire).Dispose();
            // Taken for a leftover in the end: aborted
            Assert.Contains("MaterialGateway.AbortProcess", mes.Calls);
        }
    }
}
