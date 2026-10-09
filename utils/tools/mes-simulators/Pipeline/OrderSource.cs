using Cmf.Navigo.BusinessObjects;
using Microsoft.Extensions.Logging;
using MESSimulator.Line;
using MESSimulator.Mes;
using MESSimulator.Steps;

namespace MESSimulator.Pipeline
{
    /// <summary>
    /// Feeds the line: every launch interval creates a production order with its lot and wafers, moves the lot to
    /// the start flow path and hands it to <see cref="LotFlow"/>.
    /// </summary>
    public sealed class OrderSource(
        LineDefinition line,
        IMesGateway mes,
        IMaterialTracker tracker,
        LotFlow flow,
        InFlightLots inFlight,
        BatchQueues batchQueues,
        SimulationClock clock,
        IRandomSource random,
        ILogger<OrderSource> logger)
    {
        private OrderOptions Order => line.Order;

        private readonly IReadOnlyList<OrderProfile> _profiles = line.Order.Profiles();

        /// <summary>
        /// The product of the next order: one of <paramref name="profiles"/>, picked at random in proportion to its
        /// weight. A product with weight 0 is never picked.
        /// </summary>
        public static OrderProfile PickProfile(IReadOnlyList<OrderProfile> profiles, IRandomSource random)
        {
            int total = profiles.Sum(p => p.Weight);
            if (total <= 0)
            {
                throw new InvalidOperationException("No product has a weight above 0");
            }

            int pick = random.Next(0, total);
            foreach (var profile in profiles)
            {
                if (pick < profile.Weight)
                {
                    return profile;
                }
                pick -= profile.Weight;
            }
            throw new InvalidOperationException("Unreachable: the pick is below the total weight");
        }

        // Required materials (by product) promised to orders launched by this run whose lots are still running
        private readonly Dictionary<string, decimal> _reserved = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// How many units the stock covers: for each required product, its stock not yet promised to running lots divided
        /// by the quantity per unit; the smallest of those, rounded down.
        /// </summary>
        public static int UnitsCovered(IReadOnlyList<RequirementOptions> requires, IReadOnlyDictionary<string, decimal> available) =>
            requires.Count == 0
                ? int.MaxValue
                : requires.Min(r => (int)Math.Floor(Math.Max(0, available.GetValueOrDefault(r.Product)) / r.QuantityPerUnit));

        /// <summary>
        /// The order to launch for a product with requirements: the product itself, sized to what the stock covers (and
        /// its materials reserved), or, when the stock doesn't cover its minimum quantity, another product without
        /// requirements (e.g. one of the missing semi-finished goods).
        /// </summary>
        private (OrderProfile? Profile, int? Quantity, Dictionary<string, decimal>? Reservation) CheckRequirements(OrderProfile profile)
        {
            lock (_reserved)
            {
                var available = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var requirement in profile.Requires)
                {
                    var product = mes.MasterData.GetByName<Product>(requirement.Product)
                        ?? throw new InvalidOperationException($"Required product '{requirement.Product}' not found");
                    var stock = mes.Materials.StockAt(product.Id, profile.Facility, profile.RequiresAtStep!);
                    available[requirement.Product] = stock - _reserved.GetValueOrDefault(requirement.Product);

                    // A bought-in product (the line doesn't make it) that can't cover the largest order arrives as a new lot
                    if (requirement.Replenish is { } replenish
                        && available[requirement.Product] < requirement.QuantityPerUnit * profile.MaxLotQuantity)
                    {
                        Replenish(product, profile.Facility, replenish);
                        available[requirement.Product] += replenish.Quantity;
                    }
                }

                int covered = UnitsCovered(profile.Requires, available);
                if (covered >= profile.MinLotQuantity)
                {
                    int quantity = random.Next(profile.MinLotQuantity, Math.Min(profile.MaxLotQuantity, covered) + 1);
                    var reservation = profile.Requires.ToDictionary(r => r.Product, r => r.QuantityPerUnit * quantity, StringComparer.OrdinalIgnoreCase);
                    foreach (var (product, reserved) in reservation)
                    {
                        _reserved[product] = _reserved.GetValueOrDefault(product) + reserved;
                    }
                    logger.LogInformation($"'{profile.Product}': stock at {profile.RequiresAtStep} covers {covered} unit(s), launching {quantity}");
                    return (profile, quantity, reservation);
                }

                var short_ = profile.Requires.Where(r => available.GetValueOrDefault(r.Product) < r.QuantityPerUnit * profile.MinLotQuantity).ToList();
                string missing = string.Join(", ", short_
                    .Select(r => $"{r.Product} {Math.Max(0, available.GetValueOrDefault(r.Product)):0.##}/{r.QuantityPerUnit * profile.MinLotQuantity:0.##}"));

                // Pull: launch a missing product the line makes (a profile without requirements, whatever its weight), unless
                // what is already on its way covers the gap. A missing product the line can't make (e.g. the pre-stocked
                // roofs) only means waiting.
                var makesMissing = _profiles
                    .Where(p => p.Requires.Count == 0)
                    .Where(p => short_.Any(r => string.Equals(r.Product, p.Product, StringComparison.OrdinalIgnoreCase)
                        && available.GetValueOrDefault(r.Product) + _incoming.GetValueOrDefault(r.Product) < r.QuantityPerUnit * profile.MinLotQuantity))
                    .ToList();
                var unmakeable = short_.Where(r => !_profiles.Any(p => p.Requires.Count == 0 && string.Equals(p.Product, r.Product, StringComparison.OrdinalIgnoreCase))).ToList();
                if (makesMissing.Count == 0)
                {
                    var waiting = unmakeable.Count > 0
                        ? $"'{profile.Product}' waits for stock at {profile.RequiresAtStep} ({missing}); the line doesn't make {string.Join(", ", unmakeable.Select(r => $"'{r.Product}'"))}: nothing launched"
                        : $"'{profile.Product}' waits for stock at {profile.RequiresAtStep} ({missing}); what is missing is already on its way: nothing launched";
                    // The same wait repeats every launch interval: log it once, then only when it changes
                    logger.Log(waiting == _lastWait ? LogLevel.Debug : LogLevel.Information, waiting);
                    _lastWait = waiting;
                    return (null, null, null);
                }

                var instead = makesMissing[random.Next(0, makesMissing.Count)];
                logger.LogInformation($"'{profile.Product}' waits for stock at {profile.RequiresAtStep} ({missing}): launching '{instead.Product}' instead");
                return (instead, null, null);
            }
        }

        private string? _lastWait;

        /// <summary>Creates a lot of a bought-in product at its stock step (no production order: it was bought, not made).</summary>
        private void Replenish(Product product, string facilityName, ReplenishOptions replenish)
        {
            var facility = mes.MasterData.GetByName<Facility>(facilityName)
                ?? throw new InvalidOperationException($"Facility '{facilityName}' not found");
            var lot = mes.MasterData.Create(new Material()
            {
                Facility = facility,
                Name = NewName(product.Name),
                Product = product,
                Form = replenish.Form,
                FlowPath = replenish.FlowPath,
                PrimaryQuantity = replenish.Quantity,
                PrimaryUnits = Order.PrimaryUnits,
                Type = Order.MaterialType
            }) ?? throw new InvalidOperationException($"Replenishment lot of '{product.Name}' was not created");
            logger.LogInformation($"Replenished '{product.Name}': created '{lot.Name}' ({replenish.Quantity:0.##}) at {replenish.FlowPath}");
        }

        // Quantity of each product launched to fill a gap (above) whose lots are still running
        private readonly Dictionary<string, decimal> _incoming = new(StringComparer.OrdinalIgnoreCase);

        private void AddIncoming(string product, decimal quantity, int sign)
        {
            lock (_reserved)
            {
                _incoming[product] = Math.Max(0, _incoming.GetValueOrDefault(product) + sign * quantity);
            }
        }

        private void Release(Dictionary<string, decimal> reservation)
        {
            lock (_reserved)
            {
                foreach (var (product, reserved) in reservation)
                {
                    _reserved[product] = Math.Max(0, _reserved.GetValueOrDefault(product) - reserved);
                }
            }
        }

        private int LotsOnTheLine => inFlight.Count + batchQueues.WaitingCount;

        // The order failed before its lot started: nothing will consume the reserved stock
        private void ReleaseUnused(Dictionary<string, decimal>? reservation, bool started)
        {
            if (reservation != null && !started)
            {
                Release(reservation);
            }
        }

        /// <summary>The stock reserved for orders being launched or whose lots are still running, by product (for tests).</summary>
        internal decimal Reserved(string product)
        {
            lock (_reserved)
            {
                return _reserved.GetValueOrDefault(product);
            }
        }

        public async Task RunAsync(CancellationToken token)
        {
            var interval = clock.Scale(Order.LaunchIntervalSeconds);

            for (int created = 0; !token.IsCancellationRequested; created++)
            {
                if (Order.MaxOrders > 0 && created >= Order.MaxOrders)
                {
                    logger.LogInformation($"Created {created} order(s), the Line:Order:MaxOrders limit: no more orders");
                    return;
                }

                // Work-in-process cap: the next order waits until the line has room (lots moving, and lots waiting at a
                // batch step, whose runs pause until the batch hands them back)
                if (Order.MaxLotsInFlight > 0 && LotsOnTheLine >= Order.MaxLotsInFlight)
                {
                    logger.LogDebug($"{LotsOnTheLine} lot(s) on the line (Line:Order:MaxLotsInFlight {Order.MaxLotsInFlight}): next order waits");
                    while (LotsOnTheLine >= Order.MaxLotsInFlight && !token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(clock.PollInterval(Order.LaunchIntervalSeconds / 4), token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }

                // Stock reserved for this order: freed when its lot run ends, or right away when the lot never starts
                Dictionary<string, decimal>? reservation = null;
                bool started = false;
                try
                {
                    var profile = PickProfile(_profiles, random);
                    int? quantity = null;
                    (string Product, int Quantity)? incoming = null;
                    if (profile.Requires.Count > 0)
                    {
                        var picked = profile;
                        (var launch, quantity, reservation) = CheckRequirements(profile);
                        if (launch == null)
                        {
                            // Nothing to launch this time (the stock is short of what the line can't make, or of what is on its way)
                            created--;
                            await Task.Delay(interval, token);
                            continue;
                        }
                        profile = launch;
                        if (!ReferenceEquals(profile, picked))
                        {
                            // Launched to fill a gap: on its way until its lot run ends
                            quantity = random.Next(profile.MinLotQuantity, profile.MaxLotQuantity + 1);
                            incoming = (profile.Product, quantity.Value);
                        }
                    }

                    var (lot, wafers) = CreateOrder(profile, quantity);

                    // A discrete product's lot is created directly at its first step; there is no pre-compose step to move from.
                    if (!profile.DiscreteProduct)
                    {
                        logger.LogDebug($"Moving Lot '{lot.Name}' to '{profile.StartFlowPath}'");
                        lot = mes.Materials.MoveToNextStep(tracker.Reload(lot), profile.StartFlowPath);
                        logger.LogInformation($"Moved Lot '{lot.Name}' to '{profile.StartFlowPath}'");
                    }

                    var run = flow.Start(lot, new LotRunData { Wafers = wafers }, token);
                    started = true;
                    if (reservation != null)
                    {
                        // The lot consumed what it needed (or stopped): its share of the stock is free again
                        _ = run.ContinueWith(_ => Release(reservation), TaskScheduler.Default);
                    }
                    if (incoming is { } arriving)
                    {
                        AddIncoming(arriving.Product, arriving.Quantity, +1);
                        _ = run.ContinueWith(_ => AddIncoming(arriving.Product, arriving.Quantity, -1), TaskScheduler.Default);
                    }
                }
                catch (OperationCanceledException)
                {
                    ReleaseUnused(reservation, started);
                    break;
                }
                catch (Exception ex)
                {
                    ReleaseUnused(reservation, started);
                    logger.LogError($"Order preparation failed: {ex.Message}");
                }

                try
                {
                    await Task.Delay(interval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Creates a production order of a random size for the product, its lot (dispatchable, with the order characteristics) and its wafers.
        /// </summary>
        private (Material Lot, MaterialCollection Wafers) CreateOrder(OrderProfile profile, int? orderQuantity = null)
        {
            int quantity = orderQuantity ?? random.Next(profile.MinLotQuantity, profile.MaxLotQuantity + 1);

            var product = mes.MasterData.GetByName<Product>(profile.Product)
                ?? throw new InvalidOperationException($"Product '{profile.Product}' not found");
            var facility = mes.MasterData.GetByName<Facility>(profile.Facility)
                ?? throw new InvalidOperationException($"Facility '{profile.Facility}' not found");

            var characteristics = new ProductionOrderCharacteristicCollection();
            for (int i = 0; i < profile.Characteristics.Count; i++)
            {
                characteristics.Add(new ProductionOrderCharacteristic { Name = profile.Characteristics[i].Name, Value = profile.Characteristics[i].Value, Order = i + 1 });
            }

            logger.LogDebug($"Creating Production Order for product '{profile.Product}'");
            var productionOrder = mes.MasterData.Create(new ProductionOrder()
            {
                Facility = facility,
                DueDate = DateTime.UtcNow.AddDays(7),
                PlannedStartDate = DateTime.UtcNow,
                PlannedEndDate = DateTime.UtcNow.AddDays(1),
                Name = ProductionOrderNaming.NewName(Order.ProductionOrderNameFormat),
                Quantity = quantity,
                Type = Order.OrderType,
                Units = Order.PrimaryUnits,
                Product = product,
                UseProductCharacteristicRules = characteristics.Count > 0,
                ProductionOrderCharacteristics = characteristics.Count > 0 ? characteristics : null
            }) ?? throw new InvalidOperationException("Production order was not created");
            logger.LogInformation($"Created Production Order '{productionOrder.Name}' for product '{profile.Product}' (Qty={quantity})");

            // Released, the MES follows the order's progress and completes it when its quantity reaches Wafer Shipping FE
            mes.MasterData.ReleaseProductionOrder(productionOrder);

            MaterialCharacteristicCollection? lotCharacteristics = null;
            if (characteristics.Count > 0)
            {
                lotCharacteristics = [];
                lotCharacteristics.AddRange(characteristics.Select(c => new MaterialCharacteristic { Name = c.Name, Value = c.Value, Order = c.Order }));
            }

            var lot = mes.MasterData.Create(new Material()
            {
                Facility = facility,
                Name = NewName("Lot"),
                ProductionOrder = productionOrder,
                Product = product,
                Form = Order.LotForm,
                FlowPath = profile.LotFlowPath,
                PrimaryQuantity = profile.DiscreteProduct ? quantity : 0,
                PrimaryUnits = Order.PrimaryUnits,
                // A discrete lot has no secondary quantity: the MES rejects SecondaryUnits equal to PrimaryUnits, and an
                // undefined unit with a quantity set
                SecondaryQuantity = profile.DiscreteProduct ? null : 0,
                SecondaryUnits = profile.DiscreteProduct ? null : Order.SecondaryUnits,
                Type = profile.MaterialType,
                CapacityClass = profile.DiscreteProduct && string.IsNullOrEmpty(profile.CapacityClass) ? null : profile.CapacityClass,
                MaterialCharacteristics = lotCharacteristics
            }) ?? throw new InvalidOperationException("Lot was not created");
            lot = mes.Materials.SetDispatchable(lot);
            logger.LogInformation($"Created Lot '{lot.Name}' for Production Order '{productionOrder.Name}'");

            // A discrete product has no wafers: its lot carries the order's quantity directly (no compose step).
            if (profile.DiscreteProduct)
            {
                return (lot, new MaterialCollection());
            }

            logger.LogDebug($"Creating {quantity} wafer material(s) for Production Order '{productionOrder.Name}'");
            var wafers = new MaterialCollection();
            for (int i = 0; i < quantity; i++)
            {
                var wafer = mes.MasterData.Create(new Material()
                {
                    Facility = facility,
                    Name = NewName($"{profile.Product}-WF"),
                    ProductionOrder = productionOrder,
                    Product = product,
                    Form = Order.WaferForm,
                    FlowPath = profile.WaferFlowPath,
                    PrimaryQuantity = 1,
                    PrimaryUnits = Order.PrimaryUnits,
                    SecondaryQuantity = profile.DiesPerWafer,
                    SecondaryUnits = Order.SecondaryUnits,
                    Type = profile.MaterialType
                }) ?? throw new InvalidOperationException("Wafer was not created");
                wafers.Add(mes.Materials.SetDispatchable(wafer));
            }
            logger.LogInformation($"Created {wafers.Count} wafer material(s) for Production Order '{productionOrder.Name}'");

            return (lot, wafers);
        }

        private static string NewName(string prefix) => $"{prefix}.{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}
