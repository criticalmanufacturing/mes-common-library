using Cmf.Navigo.BusinessObjects;
using MESSimulator.Pipeline;
using Xunit;

namespace MESSimulator.UnitTests
{
    public class PreviousRunCleanupAbortTests
    {
        private static Material Lot(string name, MaterialSystemState state) => new() { Name = name, SystemState = state };

        private static Material Wafer(string name, MaterialSystemState state, string lot) =>
            new() { Name = name, SystemState = state, ParentMaterial = new Material { Name = lot } };

        [Fact]
        public void AbortTargets_AreTheInProcessLots_NotTheirWafers()
        {
            var materials = new[]
            {
                Lot("Lot.A", MaterialSystemState.InProcess),
                Wafer("WF.1", MaterialSystemState.InProcess, "Lot.A"),
                Wafer("WF.2", MaterialSystemState.InProcess, "Lot.A")
            };

            Assert.Equal(["Lot.A"], PreviousRunCleanup.AbortTargets(materials).Select(m => m.Name));
        }

        [Fact]
        public void AbortTargets_IncludeAnInProcessWaferWhoseLotIsNotInProcess()
        {
            var materials = new[]
            {
                Lot("Lot.A", MaterialSystemState.Queued),
                Wafer("WF.1", MaterialSystemState.InProcess, "Lot.A"),
                Wafer("WF.2", MaterialSystemState.Queued, "Lot.A")
            };

            Assert.Equal(["WF.1"], PreviousRunCleanup.AbortTargets(materials).Select(m => m.Name));
        }

        [Fact]
        public void AbortTargets_IncludeAnInProcessWaferWhoseLotIsNotAmongTheMaterials()
        {
            var materials = new[] { Wafer("WF.1", MaterialSystemState.InProcess, "Lot.GONE") };

            Assert.Equal(["WF.1"], PreviousRunCleanup.AbortTargets(materials).Select(m => m.Name));
        }

        [Fact]
        public void AbortTargets_IgnoreMaterialsThatAreNotInProcess()
        {
            var materials = new[]
            {
                Lot("Lot.A", MaterialSystemState.Queued),
                Lot("Lot.B", MaterialSystemState.Processed),
                Wafer("WF.1", MaterialSystemState.Queued, "Lot.A")
            };

            Assert.Empty(PreviousRunCleanup.AbortTargets(materials));
        }

        [Theory]
        [InlineData("Terminate could not be completed. The object JOB-000000010 of type Resource Job has dependencies in the system. The Objects are BATCH-26-10 (Batch).", "BATCH-26-10")]
        [InlineData("Terminate could not be completed. The object Lot.756BE268 of type Material has dependencies in the system. The Objects are BATCH-26-31 (Batch).", "BATCH-26-31")]
        [InlineData("The Objects are BATCH-26-1 (Batch), BATCH-26-2 (Batch), BATCH-26-1 (Batch).", "BATCH-26-1,BATCH-26-2")]
        [InlineData("Step PREPARATION WPREP must have Loss Reasons defined.", "")]
        [InlineData("The object JOB-000000010 of type Resource Job has dependencies in the system.", "")]
        public void BlockingBatches_AreTheBatchesTheErrorNames(string message, string expected)
        {
            var batches = PreviousRunCleanup.BlockingBatches(message);

            Assert.Equal(expected, string.Join(",", batches));
        }

        [Fact]
        public void AbortTargets_TheLotNameIsMatchedIgnoringCase()
        {
            var materials = new[]
            {
                Lot("Lot.A", MaterialSystemState.InProcess),
                Wafer("WF.1", MaterialSystemState.InProcess, "LOT.a")
            };

            Assert.Equal(["Lot.A"], PreviousRunCleanup.AbortTargets(materials).Select(m => m.Name));
        }
    }
}
