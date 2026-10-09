using System.Collections.Concurrent;
using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Mes;

namespace MESSimulator.Steps
{
    /// <summary>
    /// Closes a lot's production order at the step that marks product completion (Wafer Shipping FE).
    /// </summary>
    public interface IProductionOrderCompletion
    {
        /// <summary>Closes the lot's production order when it reached its goal at <paramref name="stepName"/>.</summary>
        void CloseIfComplete(Material lot, string stepName);
    }

    /// <summary>
    /// The MES counts the order's completed quantity when its lots reach the step that marks product completion, and
    /// moves a released order to Completed once the quantity is reached; a Completed order is then closed. An order
    /// still Created (not released, e.g. from an older run) is released first, so the MES can complete it.
    /// An order that lost wafers (scrap) never reaches its quantity: once nothing of it is in progress any more, its
    /// quantity is lowered to what was completed, the MES completes it, and it is closed.
    /// </summary>
    public sealed class ProductionOrderCompletion(IMesGateway mes, ILogger<ProductionOrderCompletion> logger) : IProductionOrderCompletion
    {
        // The lots of one order arrive concurrently (they were split): one check at a time per order
        private readonly ConcurrentDictionary<long, object> _locks = new();

        public void CloseIfComplete(Material lot, string stepName)
        {
            if (lot.ProductionOrder == null)
            {
                return;
            }

            lock (_locks.GetOrAdd(lot.ProductionOrder.Id, _ => new object()))
            {
                var order = mes.MasterData.GetById<ProductionOrder>(lot.ProductionOrder.Id)!;
                if (order.SystemState == ProductionOrderSystemState.Created)
                {
                    mes.MasterData.ReleaseProductionOrder(order);
                    order = mes.MasterData.GetById<ProductionOrder>(order.Id)!;
                    logger.LogInformation($"Released production order '{order.Name}' ({order.SystemState})");
                }

                // Wafers were scrapped: the order can't reach its quantity; once nothing is in progress, it is lowered to
                // the completed quantity and the MES completes the order
                if (order.SystemState == ProductionOrderSystemState.InProgress
                    && order.InProgressQuantity is null or 0
                    && order.CompletedQuantity is > 0
                    && order.CompletedQuantity < order.Quantity)
                {
                    var planned = order.Quantity;
                    mes.MasterData.ChangeProductionOrderQuantity(order, order.CompletedQuantity.Value);
                    order = mes.MasterData.GetById<ProductionOrder>(order.Id)!;
                    logger.LogInformation($"Production order '{order.Name}': nothing left in progress, quantity lowered from {planned:0.##} to {order.Quantity:0.##} (the rest was scrapped) ({order.SystemState})");
                }

                if (order.SystemState != ProductionOrderSystemState.Completed)
                {
                    logger.LogInformation($"Production order '{order.Name}': {order.CompletedQuantity:0.##}/{order.Quantity:0.##} completed, {order.InProgressQuantity:0.##} in progress, at {stepName} ({order.SystemState})");
                    return;
                }

                mes.MasterData.CloseProductionOrder(order);
                logger.LogInformation($"Closed production order '{order.Name}' ({order.CompletedQuantity:0.##}/{order.Quantity:0.##} completed at {stepName})");
            }
        }
    }
}
