using System.Collections.ObjectModel;
using Cmf.Foundation.BusinessOrchestration.GenericServiceManagement.InputObjects;
using Cmf.Navigo.BusinessObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.InputObjects;
using Cmf.Navigo.BusinessOrchestration.MaterialManagement.OutputObjects;

namespace MESSimulator.Mes
{
    /// <summary>
    /// Batch lifecycle operations (create, release, track in, track out, cancel), sent as BatchExecute calls.
    /// </summary>
    public interface IBatchGateway
    {
        BatchCollection Create(BatchCollection batches);
        BatchCollection Release(BatchCollection batches);
        BatchCollection TrackIn(BatchCollection batches);
        List<Material> TrackOut(BatchCollection batches);

        /// <summary>Cancels batches that will not run, so their materials can join another batch.</summary>
        void Cancel(BatchCollection batches);

        /// <summary>Aborts the processing of a batch in process (before it can be cancelled).</summary>
        void Abort(Batch batch);
    }

    public sealed class BatchGateway(IMesCall mes) : IBatchGateway
    {
        public BatchCollection Create(BatchCollection batches) =>
            ToCollection(Execute("BatchExecute(CreateBatch)", batches, b => new CreateBatchInput() { Batch = b })
                .Cast<CreateBatchOutput>().Select(o => o.Batch));

        public BatchCollection Release(BatchCollection batches) =>
            mes.Run("ReleaseBatches", () => new ReleaseBatchesInput()
            {
                Batches = batches
            }.ReleaseBatchesSync(), $"{batches.Count} batch(es)").Batches;

        public BatchCollection TrackIn(BatchCollection batches) =>
            ToCollection(Execute("BatchExecute(TrackInBatch)", batches, b => new TrackInBatchInput() { Batch = b })
                .Cast<TrackInBatchOutput>().Select(o => o.Batch));

        public List<Material> TrackOut(BatchCollection batches) =>
            Execute("BatchExecute(TrackOutBatch)", batches, b => new TrackOutBatchInput() { Batch = b })
                .Cast<TrackOutBatchOutput>().SelectMany(o => o.Materials.Keys).ToList();

        public void Cancel(BatchCollection batches) =>
            mes.Run("CancelBatches", () => new CancelBatchesInput()
            {
                Batches = batches
            }.CancelBatchesSync(), $"{batches.Count} batch(es)");

        public void Abort(Batch batch) =>
            mes.Run("AbortBatch", () => new AbortBatchInput()
            {
                Batch = batch
            }.AbortBatchSync(), batch.Name);

        private IEnumerable<object> Execute(string operation, BatchCollection batches, Func<Batch, object> toInput)
        {
            var inputs = new Collection<object>();
            foreach (var batch in batches)
            {
                inputs.Add(toInput(batch));
            }

            // Not retried: the MES may have applied some of the inputs before the error, and they would be sent again
            return mes.Run(operation, () => new BatchExecuteInput()
            {
                Inputs = inputs
            }.BatchExecuteSync(), $"{batches.Count} batch(es)", retry: false).Outputs;
        }

        private static BatchCollection ToCollection(IEnumerable<Batch> batches)
        {
            var collection = new BatchCollection();
            collection.AddRange(batches);
            return collection;
        }
    }

    public static class BatchCancellation
    {
        /// <summary>
        /// Cancels a batch that will not run (or that a stopped run left behind), aborting it first when it is in process,
        /// so its lots can join another batch. Reloads it by name; a batch already terminated is left alone. Returns
        /// whether it was cancelled.
        /// </summary>
        public static bool AbortAndCancel(this IMesGateway mes, string batchName)
        {
            var batch = mes.MasterData.GetByName<Batch>(batchName);
            if (batch == null || batch.UniversalState == Cmf.Foundation.Common.Base.UniversalState.Terminated)
            {
                return false;
            }

            if (batch.SystemState == BatchSystemState.InProcess)
            {
                mes.Batches.Abort(batch);
                batch = mes.MasterData.GetByName<Batch>(batchName) ?? batch;
            }
            mes.Batches.Cancel(new BatchCollection { batch });
            return true;
        }
    }
}
