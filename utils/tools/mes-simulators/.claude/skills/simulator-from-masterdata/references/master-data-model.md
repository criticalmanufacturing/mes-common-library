# How CMF master data links, and what each link tells the simulator

Sheet names are as `md_dump.py` writes them (`<DM>Step` → `DMStep`). Examples are from the KommSemi master data
(`Tools/MESSimulator/master data`), product `2EDN7524F`, flow `Y31_CSP_3L`.

## 1. The workbooks

| File | Holds |
|---|---|
| `masterdata_semi_base1.xlsx` (+ its folder) | All definitions (~255 sheets, ~110 with data): flows, steps, resources, services, BOMs, products, smart tables, rules, DEEs, users, orders. The folder also has DEE code (`*.txt`), exported business workflows (`*.xml`) and images. |
| `masterdata_semi_base2..5.xlsx` | Instances: `DMMaterial` (lots, wafers, consumable batches, durables), `DMMaterialCharacteristic`, `DMResourceDurable` (mounted durables), `DMFutureAction`. |

## 2. The link graph

```
Product ──ProductGroup──► ProductGroup            Product.FlowPath = where NEW material of it starts (often a store, not the route)
   │  Maturity, CapacityClass, Min/MaximumMaterialSize
   │
ProductionOrder (+ LOOKUPProductionOrderChar) ── characteristic values used by BOM conditions
   │
Flow ──FlowItems(Position, Type=Step|Flow, LogicalName, IsLine, LineFlows, Condition*, On*Rule, Reworks)──► Step / Flow
   │                                           └─ ReworkPaths: source step → rework flow → return step
   │
Step ──Areas──► Area ──Facility──► Facility ──Shipping Facilities──► Facility
   │     IsPassThrough, MarksProductCompletion, UseSplitAndTrackout, SubMaterialTrackStateDepth, IsShippingAllowed
   │
   ├─ STServiceContext(Step, LogicalFlowPath, Product, ProductGroup…) ──► Service ──ResourceService──► Resource
   ├─ STBOMContext(Step, LogicalFlowPath, Product…, Resource) ──► BOM ──BOMProducts(AssemblyStep, AssemblyLogicalFlowPath, Condition, Step=source location)──► Product
   ├─ STMaterialDurablesContext(Step, LogicalFlowPath, Product) ──► BOM (durables, Position)
   ├─ STStepLineFlowContext(Step, ProductGroup|Product) ──► line Flow (its steps run on chambers)
   ├─ STStepSplitTrackOutContext(Step)
   ├─ STTimeConstraintsContext(FromStep → ToStep)
   ├─ LOOKUPStepPRODYCTContext(Step, Product…) ── yield, fixed + variable cycle time
   └─ StepReason(Step, Reason, ApplicableToRecordLoss/Rework/Terminate)

Resource ── ProcessingType (Process|Line|ConsumableFeed…), ProcessingMode=Batch, MinimumBatchCapacityUsage, Model,
   │        MaxConcurrentMaterialsInProcess, EnableCheckIn, IsSubMaterialTrackingEnabled, IsMultiLaneActive
   ├─ DMSubResource ──► Load ports, chambers (Lane), feeders (ConsumableFeedPosition; feeder.IsSharedConsumableFeed, ConsumableFeedType)
   ├─ ResourceLane
   ├─ STResourceCapacity(Model, CapacityClass) ── batch capacity
   └─ DMResourceDurable ── durables mounted now

Rule ──DEERule──► SMDEEAction ──CodeFileRelativePath──► DEE code (.txt next to the workbook)
FutureAction (Step, Action=SendMail|Hold|…)      Business workflow .xml (event + condition on Step.Name + action)
```

**Context tables resolve by specificity.** A row applies when every filled key matches the lot (empty = wildcard);
the most specific row wins. `LogicalFlowPath` holds the **FlowItem's LogicalName** (e.g. `FUSE`, `AZ`, `POLYMIDE`), so
the same step name gets different services, BOMs and reticles per sub-flow.

**Flow paths.** Top flow `Name:Rev:1`, sub-flow `Name:Rev:Position`, step `Name:Position`, e.g.
`Y31_CSP_3L:A:1/PHOTO FUSE:A:3/COAT:2`. An empty revision means `A`. Material rows in the instance files write the
short form without the revision (`Y31_CSP_3L:1/PRE_CURE:2`).

## 3. Derivation rules (implemented in `scripts/derive_line.py`)

### Route and steps

| simulationConfiguration.json | Rule |
|---|---|
| Steps to configure | Walk `FlowItems` from the top flow (sub-flows recursively). Add the rework flows of `ReworkPaths` (minus the non-core ones) and the line flows of line steps. Skip: the first step if pass-through (lot creation), steps after the product-completion step, and line-flow steps (run inside `lineFlow`). |
| `PassThrough` | `Step.IsPassThrough = Yes`. |
| `ClosesProductionOrder` | `Step.MarksProductCompletion = Yes` (also the end of the simulated line). |
| `Ship` + `ShipTo` + `Receive:false` | On the end step: the next route step's area is in another facility; `ShipTo` = that facility. |
| `ship` action | A processing step whose next route step's area is in another facility (SORTER → Wafer PACKING: Production FE SC → Warehouse FE SC). |
| `Batch` | Every resource of the step's service has `ProcessingMode = Batch`. `MaxQuantity` = `STResourceCapacity(Model, product CapacityClass)`; `MinQuantity` = `MinimumBatchCapacityUsage × MaxQuantity` (0.7 × 100 = 70). |
| `feeders` | A BOM from `STBOMContext` has items with `AssemblyStep` = the step, the matching `AssemblyLogicalFlowPath`, a `Condition` true for the lot's characteristics, and `AssemblyType` AutomaticAtTrackIn/TrackOut. Also: per-resource BOM rows (METAL WET ETCH); a line step whose chamber steps have BOMs (METAL PLATING). |
| `pack` (+ `feeders`) | BOM `AssemblyType = Packing` (Wafer PACKING). `Step.IsPackingStep` is **not** used for it. |
| `durables` | `STMaterialDurablesContext` BOM has items with `AssemblyStep` = the step. |
| `SingleMaterial` | Every candidate resource has `MaxConcurrentMaterialsInProcess = 1` (steppers). |
| `subMaterialTracking` | `Step.SubMaterialTrackStateDepth = 1` and a candidate resource has `IsSubMaterialTrackingEnabled`. |
| `lineFlow` | FlowItem `IsLine = Yes` or resource `ProcessingType = Line`; line flow from `STStepLineFlowContext`; lanes from `ResourceLane`; chambers are `DMSubResource` rows with a `Lane`. |
| `splitTrackOut` | `Step.UseSplitAndTrackout = Yes` or a `STStepSplitTrackOutContext` row. |
| `compose` | The step's service composes (`WP Compose`); the wafer stock has `ParentMaterial` lots. **Heuristic**: confirm. |
| Process times | `LOOKUPStepPRODYCTContext`: Fixed (per lot) and Variable (per wafer) cycle time, ±20%. These are **demo values** (CureWafers 1 h), far shorter than real-fab ones: tune them. |

### Order, Startup, Consumables, Chaos

| simulationConfiguration.json | Rule |
|---|---|
| `Order.Facility` | Facility of the first route step's area. |
| `LotFlowPath` / `StartFlowPath` | Paths of the first and second route steps. |
| `WaferFlowPath` | The Kanban flow where the product's wafer stock sits (`Kanban WPREP:A:1/Kanban WPREP:1`). |
| `Min/MaxLotQuantity` | `Product.MinimumMaterialSize` / `MaximumMaterialSize`. |
| `DiesPerWafer` | Wafer stock's dies quantity (983 for 2EDN7524F). |
| `Characteristics` | Names: `DMProductCharacteristic`. Values: chosen by the user; BOM `Condition`s use them (Coating=PI → PI Photoresist/PI Spray). Demo order values in `LOOKUPProductionOrderChar`. |
| `ProductionOrderNameFormat` | `<prefix of the demo orders>.{id}` (`PO SC.{id}`). The demo orders share the prefix (`PO SC.0001`), but only names matching the format exactly (8 hex digits for `{id}`) are ever terminated. |
| `DisableMail` | `DMFutureAction` with `Action = SendMail` on route steps (AOI, RwPGMA). |
| `DisableDeeActions` | Rules used by `FlowItems.OnEnterRule/OnExitRule` and `ReworkPaths.OnReworkRule` → DEE → code. The code sets `notification.Type = "General"`, but lookup `NotificationType` has only Standard / Non-Standard, so it fails. **Ask before disabling.** |
| `DisableTimeConstraints` | `STTimeConstraintsContext` rows touch route steps (including in-step minimums such as PRE_CURE 0.1 h). |
| `TerminateReason` | A Loss reason; there is no `Terminate` reason, so `Full Loss`. |
| `Operator` | API user has no employee in `DMEmployee` → create one: Calendar = facility `DefaultCalendar`, CostCenter as the other employees, Type Standard. `CheckInOperator` when a route resource has `EnableCheckIn`. |
| `Consumables.Products` | `ReplacementQuantity` = batch size of the product's raw-material stock (targets 1, Copper Plating 50, CuEtch 25); `LowQuantity` = 10% (tune). |
| `Chaos.ReworkFlows` | Rework flows of `ReworkPaths` for the top flow. Which are core and their chances: **user**. Where they are offered: `ApplicableToQueued` / `ApplicableToProcessed`. Limits: `LOOKUPReasonRwkLimit`, `STStepReworkLimitsContext`. |
| `Chaos.ScrapReasons` | Loss reasons most route steps accept (`StepReason.ApplicableToRecordLoss`). Each step only accepts its own. |

### Facts to flag (generated as FLAGS)

- Same step name, different service per sub-flow (COAT, INSP CD/Overlay, AOI; LEICA has no AZ overlay service).
- Shared feeders (`IsSharedConsumableFeed`: coaters, plating chambers, packing); one feeder position for several BOM
  products (Developer: PI Spray vs AZ Spray → swap only when nothing else is in process).
- Batch step in several sub-flows (CureWafers, PRE_CURE): batches mix lots → move next per group.
- Operator check-in resources (NEXX-001); the only resource with a service for the BOM (NEXX-001 for Al/Ag).
- MES conditions on optional steps (INSP CD, INSP Overlay, AOI rule, analyses by `Product.Maturity`).
- Steps with no service for the product (SAR Capture Result: send-ahead only).
- BOM assembly types no action covers (PEELING: `ReplaceAndDisassemble` of BG Tape).
- Durables not mounted anywhere (AZ reticle) → mounted at run time.
- Consumables without stock at their Kanban location → created at run time.
- Business workflows acting on route steps (`Shipping to Facility Production BE SC` ships automatically when a lot
  enters Wafer Shipping FE).

## 4. What master data cannot tell you

These are **MES runtime behaviours**. Take them from the `build-mes-simulator` skill (section 8) and confirm them
against an MES when one is available:

- state transitions at track-in/out, resources left in non-Standby states;
- "has changed since last viewed" / deadlocks (retry); "changed by another user" during setup (reload);
- `CloneObject` fails on materials; only dispatchable locations give dispatchable consumables;
- `CreatePackages` ≠ packing (`PackMaterial`); receive FlowPath must be read before shipping;
- a failed batch release keeps its lots (cancel it);
- the production order lifecycle: release at creation, close only from Completed, lower the quantity after scrap;
- scrapped wafers are still offered at packing;
- the reticle can be swapped between mount and track-in by another lot;
- every employee check-in changes the employee;
- real-fab process times, the launch rate, and the rework/scrap chances (demo intent).

## 5. Validation on KommSemi (2026-10-05)

`derive_line.py --product 2EDN7524F --flow Y31_CSP_3L --char Quality=QA --char Coating=PI
--exclude-rework RWK_PRECLEAN,RWK_WETETCH,"Rwk_Back Grinding" --compare simulationConfiguration.json`:

- **Steps: 37/37 identical** to the hand-built simulationConfiguration.json (actions, pass-through, ship, close, single material,
  batch min/max). Process times differ by design (demo vs real-fab values).
- **Order and Startup: identical**, except `DiesPerWafer` (master data 983, simulationConfiguration.json uses the default 500).
- **Consumables**: identical batch sizes; LowQuantity differs (10% here vs 20% for the wet-etch chemicals).
- Every fact in the "Facts to flag" list above was first discovered in live runs; each one can also be read from
  the master data.
- Also run for `2EGN7524G` (Coating=WPR): 43 steps, no errors (not validated live).
