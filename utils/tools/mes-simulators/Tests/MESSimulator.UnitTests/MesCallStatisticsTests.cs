using MESSimulator.Mes;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class MesCallStatisticsTests
    {
        [Fact]
        public void Summarize_ComputesCountFailuresAverageP95AndMaxPerOperation()
        {
            var statistics = new MesCallStatistics();
            for (int ms = 1; ms <= 20; ms++)
            {
                statistics.Record("GetObjectByName", TimeSpan.FromMilliseconds(ms * 10), succeeded: ms != 20);
            }

            var summary = Assert.Single(statistics.Summarize());

            Assert.Equal("GetObjectByName", summary.Operation);
            Assert.Equal(20, summary.Count);
            Assert.Equal(1, summary.Failures);
            Assert.Equal(105, summary.AverageMilliseconds, precision: 3);
            Assert.Equal(190, summary.P95Milliseconds, precision: 3);
            Assert.Equal(200, summary.MaxMilliseconds, precision: 3);
            Assert.Equal(2100, summary.TotalMilliseconds, precision: 3);
        }

        [Fact]
        public void Summarize_OrdersOperationsByTotalTime()
        {
            var statistics = new MesCallStatistics();
            statistics.Record("Fast", TimeSpan.FromMilliseconds(5), succeeded: true);
            statistics.Record("Slow", TimeSpan.FromMilliseconds(500), succeeded: true);

            Assert.Equal(["Slow", "Fast"], statistics.Summarize().Select(s => s.Operation));
        }

        [Fact]
        public void FormatSummary_ListsEachOperation()
        {
            var statistics = new MesCallStatistics();
            statistics.Record("ComplexTrackOutMaterials", TimeSpan.FromMilliseconds(1500), succeeded: true);

            var text = statistics.FormatSummary();

            Assert.Contains("Operation", text);
            Assert.Contains("ComplexTrackOutMaterials", text);
            Assert.Contains("1500", text);
        }

        [Fact]
        public void FormatSummary_SaysWhenNothingWasRecorded()
        {
            Assert.Equal("No MES calls recorded.", new MesCallStatistics().FormatSummary());
        }
    }
}
