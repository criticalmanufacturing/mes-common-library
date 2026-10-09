namespace MESSimulator.Line
{
    /// <summary>
    /// The simulated line ("Line" section, from simulationConfiguration.json): how orders are created and how each MES step is run.
    /// </summary>
    public sealed class LineOptions
    {
        public const string SectionName = "Line";

        public OrderOptions Order { get; set; } = new();

        /// <summary>What the simulator changes in the MES before lots start.</summary>
        public ChaosOptions Chaos { get; set; } = new();

        public StartupOptions Startup { get; set; } = new();

        /// <summary>What happens to a lot the simulator gives up on.</summary>
        public FailedLotsOptions FailedLots { get; set; } = new();

        /// <summary>When attached consumables are replaced, and with how much.</summary>
        public ConsumablesOptions Consumables { get; set; } = new();

        /// <summary>Steps by MES step name (case-insensitive). A lot at a step not listed here leaves the simulation.</summary>
        public Dictionary<string, StepOptions> Steps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// MES preparation at startup. DisableMail, DisableDeeActions and DisableTimeConstraints change the whole MES, not
    /// just the simulator's orders, and are not undone on exit: they are off unless the line file turns them on.
    /// </summary>
    public sealed class StartupOptions
    {
        /// <summary>
        /// Set /Cmf/System/Configuration/Mail/Enabled/ to false (e-mail future actions break move-next). MES-wide.
        /// </summary>
        public bool DisableMail { get; set; }

        /// <summary>DEE actions to disable (e.g. notifications). MES-wide.</summary>
        public List<string> DisableDeeActions { get; set; } = [];

        /// <summary>Make sure the simulator's user has an employee (created when missing), checked in on resources that require it.</summary>
        public bool CheckInOperator { get; set; } = true;

        /// <summary>The operator employee created when the user has none (Calendar is mandatory in the MES).</summary>
        public OperatorOptions Operator { get; set; } = new();

        /// <summary>
        /// Before starting, terminate what earlier runs left behind: every material of the simulator's production
        /// orders (those matching Line:Order:ProductionOrderNameFormat), then the orders themselves. Also set by
        /// --terminateonstart.
        /// </summary>
        public bool TerminatePreviousRuns { get; set; }

        /// <summary>Loss reason for those terminations.</summary>
        public string TerminateReason { get; set; } = "Full Loss";

        /// <summary>
        /// Ask before terminating previous runs ("Terminate N materials of M orders on &lt;MES&gt;? [y/N]"). Without a console
        /// to ask (a container), the termination is refused unless this is false. --yes sets it to false.
        /// </summary>
        public bool ConfirmTerminate { get; set; } = true;

        /// <summary>Terminating previous runs stops with an error when they have more production orders than this.</summary>
        public int MaxTerminatedOrders { get; set; } = 100;

        /// <summary>
        /// --dry-run: only read the MES and log what the startup would change (the previous runs it would terminate, the
        /// MES-wide settings), then stop. Nothing is created or changed.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// Clear the TimeConstraintsContext smart table and switch time constraints off on every configured step. MES-wide.
        /// </summary>
        public bool DisableTimeConstraints { get; set; }

        /// <summary>Whether any of the MES-wide changes above is on.</summary>
        public bool ChangesTheWholeMes => DisableMail || DisableDeeActions.Count > 0 || DisableTimeConstraints;
    }

    /// <summary>
    /// The fallback for a lot the simulator gives up on (it failed and can't be run again): instead of leaving it where
    /// it is, where it may hold its resource (a lot in process keeps the resource's BOM, setup and slots), it is
    /// aborted when in process and terminated. Only lots of the simulator's own production orders (matching
    /// Line:Order:ProductionOrderNameFormat) are ever terminated.
    /// </summary>
    public sealed class FailedLotsOptions
    {
        public bool Terminate { get; set; } = true;

        /// <summary>Loss reason for the termination (a step that doesn't accept it gets one it accepts).</summary>
        public string TerminateReason { get; set; } = "Full Loss";
    }

    /// <summary>
    /// Chaos on the line: some lots go to rework (the rework paths the MES offers at their step), and some reworked
    /// lots lose wafers (scrap). Off when ReworkFlows is empty. Seed makes a run repeatable.
    /// </summary>
    public sealed class ChaosOptions
    {
        /// <summary>Random seed for the whole run (resources, times, rework, scrap); none: a different run each time.</summary>
        public int? Seed { get; set; }

        /// <summary>A lot is reworked at most this many times.</summary>
        public int MaxReworksPerLot { get; set; } = 1;

        /// <summary>
        /// Rework flows the simulator may send lots to (by flow name), each with the chance (0..1) that a lot goes there
        /// from a step where the MES offers it (a flow offered at 4 steps gets 4 chances per pass). The MES can offer
        /// other flows (e.g. RWK_PRECLEAN after a mid-process abort), which are ignored. Empty: no rework.
        /// </summary>
        public Dictionary<string, decimal> ReworkFlows { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Chance (0..1) that a reworked lot has wafers scrapped.</summary>
        public decimal ScrapProbability { get; set; }

        /// <summary>At most this fraction (0..1] of the lot's wafers is scrapped (at least one).</summary>
        public decimal MaxScrapFraction { get; set; } = 0.5m;

        /// <summary>Loss reasons to scrap with (one is picked at random).</summary>
        public List<string> ScrapReasons { get; set; } = [];
    }

    /// <summary>
    /// Fields of the employee created for the simulator's user; defaults follow the existing "KommSemi Operator".
    /// </summary>
    public sealed class OperatorOptions
    {
        public string? Calendar { get; set; }
        public string Type { get; set; } = "Standard";
        public string CostCenter { get; set; } = "Generic";
    }

    /// <summary>
    /// Consumable rules: <see cref="Default"/>, overridden per product name in <see cref="Products"/>
    /// (units differ: BG Tape is in cm², targets in units).
    /// </summary>
    public sealed class ConsumablesOptions
    {
        public ConsumableRules Default { get; set; } = new() { LowQuantity = 2000, ReplacementQuantity = 60000, TerminateReason = "Full Loss" };

        public Dictionary<string, ConsumableRules> Products { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The rules for a product: its overrides on top of <see cref="Default"/>.</summary>
        public ConsumableRules For(string? productName)
        {
            var product = productName != null ? Products.GetValueOrDefault(productName) : null;
            return new ConsumableRules
            {
                LowQuantity = product?.LowQuantity ?? Default.LowQuantity,
                ReplacementQuantity = product?.ReplacementQuantity ?? Default.ReplacementQuantity,
                TerminateReason = product?.TerminateReason ?? Default.TerminateReason
            };
        }
    }

    public sealed class ConsumableRules
    {
        /// <summary>An attached consumable below this primary quantity is replaced before the track-in.</summary>
        public decimal? LowQuantity { get; set; }

        /// <summary>Primary quantity of a newly created consumable (replacement, or when no feeder has one to dispatch).</summary>
        public decimal? ReplacementQuantity { get; set; }

        /// <summary>Loss reason used to terminate a replaced consumable.</summary>
        public string? TerminateReason { get; set; }
    }

    /// <summary>
    /// How production orders, lots and wafers are created.
    /// </summary>
    public sealed class OrderOptions
    {
        /// <summary>
        /// The product to launch when <see cref="Products"/> is empty. The fields below (facility, flow paths, material
        /// type, capacity class, dies per wafer, lot quantity, characteristics) are the defaults of every product in
        /// <see cref="Products"/>.
        /// </summary>
        public string Product { get; set; } = "";

        /// <summary>
        /// Several products on the same line: each order is for one of them, picked at random by <see cref="ProductOptions.Weight"/>.
        /// Each product only lists what differs from the defaults of this section. Empty: the single <see cref="Product"/>.
        /// </summary>
        public List<ProductOptions> Products { get; set; } = [];

        public string Facility { get; set; } = "";

        /// <summary>Flow path the lot is created at.</summary>
        public string LotFlowPath { get; set; } = "";

        /// <summary>Flow path the wafers are created at.</summary>
        public string WaferFlowPath { get; set; } = "";

        /// <summary>Flow path the lot is moved to right after creation, where the simulation picks it up.</summary>
        public string StartFlowPath { get; set; } = "";

        /// <summary>
        /// Naming convention of the simulator's production orders: {id} becomes 8 random uppercase hex digits
        /// ("PO SIM.{id}" → "PO SIM.4592FB2C"). Only orders whose name matches it exactly are ever terminated (previous
        /// runs, failed lots), so pick a format the MES's own orders don't follow. See <see cref="ProductionOrderNaming"/>.
        /// </summary>
        public string ProductionOrderNameFormat { get; set; } = "PO SIM.{id}";

        /// <summary>Replaced by <see cref="ProductionOrderNameFormat"/> ("&lt;prefix&gt;.{id}"); only read to report it.</summary>
        public string? ProductionOrderPrefix { get; set; }

        public string OrderType { get; set; } = "Standard";
        public string MaterialType { get; set; } = "Production";
        public string LotForm { get; set; } = "Lot";
        public string WaferForm { get; set; } = "Wafer";
        public string PrimaryUnits { get; set; } = "Wafers";
        public string SecondaryUnits { get; set; } = "Dies";
        public string CapacityClass { get; set; } = "300mm";

        /// <summary>
        /// True for a discrete product (no wafers): the lot is created with its primary quantity directly, no wafers are
        /// made and there is no compose step. False (the default): wafer-based products.
        /// </summary>
        public bool DiscreteProduct { get; set; }

        /// <summary>Wafers per lot, picked at random in [Min, Max].</summary>
        public int MinLotQuantity { get; set; } = 5;
        public int MaxLotQuantity { get; set; } = 25;

        public int DiesPerWafer { get; set; } = 500;

        /// <summary>Time between new orders, in simulated seconds (scaled by speed).</summary>
        public int LaunchIntervalSeconds { get; set; } = 120;

        /// <summary>Stop creating orders after this many (0: no limit). The lots already started keep running.</summary>
        public int MaxOrders { get; set; }

        /// <summary>
        /// No new order while this many lots are running on the line (0: no limit); lots waiting at a batch step don't
        /// count. At a high speed the MES calls, not the simulated times, set the pace: without a cap the launches
        /// outrun the line and the queues (and MES load) grow without end.
        /// </summary>
        public int MaxLotsInFlight { get; set; }

        /// <summary>Production order (and lot) characteristics, e.g. those BOM conditions evaluate.</summary>
        public List<CharacteristicOptions> Characteristics { get; set; } = [];

        /// <summary>
        /// What each order is made of, one entry per product: the entries of <see cref="Products"/> with the defaults of
        /// this section filled in, or the single <see cref="Product"/> when there are none.
        /// </summary>
        public IReadOnlyList<OrderProfile> Profiles()
        {
            var products = Products.Count > 0 ? Products : [new ProductOptions { Product = Product }];
            return products.Select(p => new OrderProfile
            {
                Product = p.Product,
                Weight = Products.Count > 0 ? p.Weight : 1,
                Facility = p.Facility ?? Facility,
                LotFlowPath = p.LotFlowPath ?? LotFlowPath,
                WaferFlowPath = p.WaferFlowPath ?? WaferFlowPath,
                StartFlowPath = p.StartFlowPath ?? StartFlowPath,
                MaterialType = p.MaterialType ?? MaterialType,
                CapacityClass = p.CapacityClass ?? CapacityClass,
                MinLotQuantity = p.MinLotQuantity ?? MinLotQuantity,
                MaxLotQuantity = p.MaxLotQuantity ?? MaxLotQuantity,
                DiesPerWafer = p.DiesPerWafer ?? DiesPerWafer,
                DiscreteProduct = p.DiscreteProduct ?? DiscreteProduct,
                Characteristics = p.Characteristics ?? Characteristics,
                Requires = p.Requires ?? [],
                RequiresAtStep = p.RequiresAtStep
            }).ToList();
        }
    }

    /// <summary>
    /// A product launched by the line. Everything but <see cref="Product"/> and <see cref="Weight"/> is optional: what
    /// is left out is taken from <see cref="OrderOptions"/>.
    /// </summary>
    public sealed class ProductOptions
    {
        /// <summary>MES product name.</summary>
        public string Product { get; set; } = "";

        /// <summary>
        /// Relative share of the orders (1 and 3: a quarter and three quarters). 0 keeps the product configured but never
        /// launches it.
        /// </summary>
        public int Weight { get; set; } = 1;

        public string? Facility { get; set; }
        public string? LotFlowPath { get; set; }
        public string? WaferFlowPath { get; set; }
        public string? StartFlowPath { get; set; }

        /// <summary>Material type of the lots and wafers: "Production", or "Engineering" for an engineering product.</summary>
        public string? MaterialType { get; set; }

        /// <summary>The product's wafer size, e.g. "300mm" or "200mm".</summary>
        public string? CapacityClass { get; set; }

        /// <summary>True for a discrete product (overrides <see cref="OrderOptions.DiscreteProduct"/>); null: the order default.</summary>
        public bool? DiscreteProduct { get; set; }

        /// <summary>
        /// Materials an order of this product consumes, per unit (e.g. the semi-finished goods a finished good is assembled
        /// from): the order is only launched when the stock at <see cref="RequiresAtStep"/> covers its whole quantity,
        /// on top of what the product's lots already running will consume. Otherwise another product (one without
        /// requirements) is launched instead. Empty (the default): no check.
        /// </summary>
        public List<RequirementOptions>? Requires { get; set; }

        /// <summary>Step where the required materials are stocked (e.g. Kanban Final Assembly), in the order's facility.</summary>
        public string? RequiresAtStep { get; set; }

        public int? DiesPerWafer { get; set; }
        public int? MinLotQuantity { get; set; }
        public int? MaxLotQuantity { get; set; }

        /// <summary>Characteristics of the product's orders; none: those of <see cref="OrderOptions.Characteristics"/>.</summary>
        public List<CharacteristicOptions>? Characteristics { get; set; }
    }

    /// <summary>
    /// One product's order settings, resolved: its <see cref="ProductOptions"/> over the defaults of <see cref="OrderOptions"/>.
    /// </summary>
    public sealed class OrderProfile
    {
        public required string Product { get; init; }
        public required int Weight { get; init; }
        public required string Facility { get; init; }
        public required string LotFlowPath { get; init; }
        public required string WaferFlowPath { get; init; }
        public required string StartFlowPath { get; init; }
        public required string MaterialType { get; init; }
        public required string CapacityClass { get; init; }
        public required int MinLotQuantity { get; init; }
        public required int MaxLotQuantity { get; init; }
        public required int DiesPerWafer { get; init; }
        public required bool DiscreteProduct { get; init; }
        public required IReadOnlyList<CharacteristicOptions> Characteristics { get; init; }
        public IReadOnlyList<RequirementOptions> Requires { get; init; } = [];
        public string? RequiresAtStep { get; init; }
    }

    /// <summary>One material an order consumes: its product and the quantity per unit of the order.</summary>
    public sealed class RequirementOptions
    {
        public string Product { get; set; } = "";
        public decimal QuantityPerUnit { get; set; }

        /// <summary>
        /// A product the line doesn't make (e.g. pre-stocked roofs): when its stock can't cover the order, a new lot of it
        /// is created at the stock step, as if it had been bought. Null: the order waits for stock.
        /// </summary>
        public ReplenishOptions? Replenish { get; set; }
    }

    /// <summary>How a bought-in required product is replenished.</summary>
    public sealed class ReplenishOptions
    {
        /// <summary>Flow path of the stock step, where the new lot is created (e.g. "Kanban Final Assembly:A:1/Kanban Final Assembly:1").</summary>
        public string FlowPath { get; set; } = "";

        /// <summary>Primary quantity of each new lot.</summary>
        public decimal Quantity { get; set; } = 30;

        /// <summary>Material form of the new lot (the product's default form, e.g. "Lot" or "Batch").</summary>
        public string Form { get; set; } = "Lot";
    }

    public sealed class CharacteristicOptions
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// One MES step.
    /// </summary>
    public sealed class StepOptions
    {
        /// <summary>
        /// Resources to restrict the step to; one is picked at random. Empty (the default): the step's resources in
        /// the MES (GetResourcesForStep). A step without resources in either stops the lots reaching it.
        /// </summary>
        public List<string> Resources { get; set; } = [];

        /// <summary>
        /// Per-lot process time range, in simulated seconds (scaled by speed): load, setup and anything that does not
        /// depend on the lot size; the whole run on a batch tool (oven, bench).
        /// </summary>
        public int MinSeconds { get; set; } = 20;
        public int MaxSeconds { get; set; } = 60;

        /// <summary>Per-wafer process time range (single-wafer tools), added once per wafer of the lot. 0: none.</summary>
        public int MinSecondsPerWafer { get; set; }
        public int MaxSecondsPerWafer { get; set; }

        /// <summary>Line steps (lineFlow action): time range of each line flow step (chamber), in simulated seconds.</summary>
        public int LineStepMinSeconds { get; set; } = 5;
        public int LineStepMaxSeconds { get; set; } = 15;

        /// <summary>Step action keys (e.g. feeders, durables, compose, subMaterialTracking, splitTrackOut), in order.</summary>
        public List<string> Actions { get; set; } = [];

        /// <summary>Condition key: when it says no, the lot is moved to the next step without being processed.</summary>
        public string? Condition { get; set; }

        /// <summary>The resource holds one material at a time (safety valve before the track-in).</summary>
        public bool SingleMaterial { get; set; }

        /// <summary>Set for batch steps: lots queue here and are processed together.</summary>
        public BatchOptions? Batch { get; set; }

        /// <summary>
        /// A pass-through (logistic) step without resources, e.g. Wafer Shipping FE: the lot is moved to the next step
        /// without being tracked in. At the end of the flow the lot stays there.
        /// </summary>
        public bool PassThrough { get; set; }

        /// <summary>
        /// With <see cref="PassThrough"/>: the next step is in another facility (e.g. Wafer Shipping FE to Wafer
        /// Reception BE), so the lot is shipped there and received at it instead of moved next.
        /// </summary>
        public bool Ship { get; set; }

        /// <summary>
        /// Facility to ship to (Ship, or the 'ship' action) when the lot's facility can ship to several (Warehouse FE SC
        /// ships to Production FE SC and Production BE SC). Not needed when there is only one.
        /// </summary>
        public string? ShipTo { get; set; }

        /// <summary>
        /// With <see cref="Ship"/>: receive the lot at the next step in the destination facility. Off at the end of
        /// the simulated line (Wafer Shipping FE): the lot is shipped and left in transit.
        /// </summary>
        public bool Receive { get; set; } = true;

        /// <summary>
        /// The step marks product completion (Wafer Shipping FE): the lot's production order is closed once all its
        /// lots reached the step with the order's quantity.
        /// </summary>
        public bool ClosesProductionOrder { get; set; }

        /// <summary>
        /// The feeders action only prepares these BOM products (by product name), e.g. Screws at FINAL WIRING, where the
        /// rest of the BOM is assembled by hand. Empty (the default): every BOM product of the step.
        /// </summary>
        public List<string> FeederProducts { get; set; } = [];

        /// <summary>The dataCollection action's readings (e.g. paint thickness at PLATE COLORING); null: none.</summary>
        public DataCollectionOptions? DataCollection { get; set; }
    }

    /// <summary>
    /// Readings the dataCollection action posts to the lot's data collection: one point per parameter, its nominal value
    /// ±<see cref="Variation"/>, read with <see cref="Instrument"/> when the parameters need one.
    /// </summary>
    public sealed class DataCollectionOptions
    {
        /// <summary>Nominal value by data collection parameter name.</summary>
        public Dictionary<string, decimal> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Relative variation around the nominal value (0.05: ±5%).</summary>
        public decimal Variation { get; set; } = 0.05m;

        /// <summary>Instrument resource the readings are taken with; null: none.</summary>
        public string? Instrument { get; set; }
    }

    /// <summary>
    /// Batch step rules: lots wait until they add up to MinQuantity; a batch holds at most MaxQuantity (lots that do
    /// not fit wait for the next one); the queue is checked every CheckIntervalSeconds (scaled by speed).
    /// </summary>
    public sealed class BatchOptions
    {
        public int MinQuantity { get; set; } = 70;
        public int MaxQuantity { get; set; } = 100;
        public int CheckIntervalSeconds { get; set; } = 120;
    }
}
