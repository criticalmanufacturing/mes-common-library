# MESSimulator

A .NET 8 console app (Generic Host, Cmf.LightBusinessObjects) that drives lots through the KommSemi
front-end flow `Y31_CSP_3L` on the MES system. It creates
production orders, lots and wafers, and runs them step by step the way the MES routes them, up to
**Wafer Shipping FE**. There it closes the production order and ships the lot to the back end.

This file is a working guide for future runs: how to run the simulator, how it is built, what the MES
flow looks like, and the MES behaviours that took effort to discover.

## Scope

- Only the **Core** ("Base") column of the user's flow tables is simulated: consumption, batches,
  split & track-out, durables, sub-material tracking, line flows, packing and shipping.
- Engineering features (recipes, data collection, checklists, certifications) and Quality features (SPC,
  sampling) are out of scope.
- Rework is simulated as chaos (see "Chaos: rework and scrap"). SAR (send-ahead) and engineering lots are
  skipped.
- Time constraints are ignored: they are disabled at startup.
- The line **ends at Wafer Shipping FE**. The back end (SAWING, Flow #BGA, …) is mapped below but not
  simulated yet.
- The current goal is to **close production orders from beginning to end**.

## Building

`Cmf.LightBusinessObjects` comes from the Critical Manufacturing NuGet feed (`nuget.config`, source key `CMF`), which
needs credentials. Give NuGet the credentials for that source key once, in your **user** NuGet.Config (always pass
`--configfile`: without it the command writes into the repository's `nuget.config`, and the password would be committed):

```powershell
# User NuGet.Config: Windows $env:APPDATA\NuGet\NuGet.Config, Linux/macOS ~/.nuget/NuGet/NuGet.Config
dotnet nuget add source https://criticalmanufacturing.io/repository/nuget-hosted/index.json --name CMF `
    --username <user> --password <token> --store-password-in-clear-text --configfile <user NuGet.Config>
# (use "dotnet nuget update source CMF ..." when your user NuGet.Config already has a CMF source)
# or, e.g. in CI, only an environment variable:
$env:NuGetPackageSourceCredentials_CMF = "Username=<user>;Password=<token>"
dotnet build
```

`nuget.config` maps `Cmf.*` packages to that feed only and everything else to nuget.org. For a local `docker build`, put
`Username=<user>;Password=<token>` in `.nuget-cmf-credentials` next to the `Dockerfile` (git-ignored; only the build
stage reads it, the runtime image doesn't contain it). `deploy.ps1` does that for the cluster build.

## Running

```powershell
dotnet run -- --line simulationConfiguration.semi.json --speed 1     # real-fab cadence (see "Cadence")
dotnet run -- -l simulationConfiguration.semi.json --speed 3600      # 1 simulated hour per real second: test/demo runs
dotnet run -- -l simulationConfiguration.semi.json --speed 100 --timings   # also log the duration of every MES call
dotnet run -- -l simulationConfiguration.semi.json --dry-run         # only read the MES and log what the startup would change
dotnet run -- -l simulationConfiguration.semi.json --terminateonstart        # first terminate what earlier runs left (asks first)
dotnet run -- -l simulationConfiguration.semi.json --terminateonstart --yes  # same, without asking (e.g. in a container)
dotnet run -- -l simulationConfiguration.industrial.json             # the industrial line
```

- **The line file is required:** `--line`/`-l <file>`, or the `MESSIM_LINE_FILE` variable (the argument wins). Without
  one the simulator lists the line files it finds and stops. A relative path is looked up in the current folder, then
  next to the executable (the build copies `simulationConfiguration.*.json` there). In this README,
  "simulationConfiguration.json" means the line file you run with.
- **Flags:** `--speed`/`-s N`, `--timings`/`-t`, `--terminateonstart [true|false]`, `--yes`/`-y` (no confirmation before
  terminating previous runs), `--dry-run`, `--line`/`-l <file>`.
- **Exit codes:** 0 done; 1 bad arguments or no line file; 2 invalid configuration; 3 the run failed (the error is
  logged as critical).
- **Ctrl+C** stops the run. Lots in flight finish their current step first (10-minute shutdown timeout); lots still
  waiting for their track-in stop where they are (queued).
- **Configuration**, from lowest to highest precedence (`SimulatorConfiguration`):
  1. `appsettings.json` (optional) holds the client configuration and the personal access token. Start from
     `appsettings.example.json` (copy it to `appsettings.json`, which git ignores). **Never print or commit the token.**
  2. The line file (`--line`, or `MESSIM_LINE_FILE`) describes the line.
  3. Environment variables with the `MESSIM_` prefix, `__` between the levels, so one build serves several
     environments:
     ```powershell
     $env:MESSIM_ClientConfiguration__Connection__EnvironmentAddress = "https://<mes-host>"
     $env:MESSIM_ClientConfiguration__Authentication__SecurityAccessToken = "<token>"
     $env:MESSIM_Simulation__Speed = "100"
     $env:MESSIM_Line__Order__MaxOrders = "20"
     $env:MESSIM_Line__Order__Products__4__Weight = "1"          # list items are numbered from 0
     $env:MESSIM_Line__Chaos__ScrapReasons__0 = "Broken Wafer"
     ```
  4. Command-line flags.

  An environment variable can't name a step: the step names have spaces ("SILICON WAFER COMPOSE"). To change
  steps per environment, give that environment its own line file. Without a prefix a variable is ignored. The
  `.dockerignore` keeps `appsettings.json` out of the image: pass the token as an environment variable there.
- **Building while a run is going:** a run locks the exe, so build into a scratch output folder
  (`dotnet build -o <scratch>/build`) and run from there.
- **Tests:**
  - Unit tests: `dotnet test Tests/MESSimulator.UnitTests`. All tests are MES-free, including a test
    that builds the real dependency-injection graph from the shipped line files.
  - Integration tests (change a live MES): `Tests/MESSimulator.IntegrationTests`. They are **skipped** unless
    `MESSIM_INTEGRATION=1` is set, so `dotnet test` on the solution never reaches a MES; they also need the lot names
    in `MESSIM_LOTS`, the MES connection (`appsettings.json` or the `MESSIM_` variables) and optionally
    `MESSIM_LINE_FILE` (default: the semi line).

## Deploying to OpenShift

The image is built in the cluster and runs as a single-replica Deployment, configured with the `MESSIM_` variables. Steps, settings and operation: [deploy/openshift/README.md](deploy/openshift/README.md). The `Dockerfile` and `.dockerignore` keep `appsettings.json` (the token) out of the image.

## Architecture

```
Program → SimulationHost (BackgroundService)
  LineStartup.Prepare()       mail off, DEE actions off, optional cleanup, operator employee,
                              resources to Standby, time constraints off
  OrderSource                 new PO + lot + wafers every LaunchIntervalSeconds (up to MaxOrders) → LotFlow
  LotFlow                     reads the lot's current step (last segment of its FlowPath), looks it up in simulationConfiguration.json:
                                batch step       → BatchQueues
                                pass-through     → move next, or close the PO and ship (end of the line)
                                not in simulationConfiguration.json → stop
                                condition false  → move next
                                otherwise        → StepExecutor
  StepExecutor                resource pick → operator check-in → BeforeTrackIn actions
                              → dispatch & track-in → AfterTrackIn actions → process time
                              → track-out & move next (or the step's TrackOut action)
  BatchProcessor/BatchPlanner wait until MinQuantity; batch the oldest lots first, up to MaxQuantity
                              (BatchExecute create/release/track-in/track-out; cancel on a failed release)
  Mes/*Gateway                thin wrapper over the LBO *Sync calls; every call goes through MesCall
                              (Polly retry on transient errors, optional timings, statistics)
```

### Folders

- `Mes/` holds the gateways: Materials, Resources, Batches, Setup, Labor and MasterData.
- `Steps/` holds:
  - the executor and the tracker;
  - resource handling: catalog, eligibility, locks, occupancy;
  - the operator check-in;
  - production order completion.
- `Steps/Actions/` holds the step actions.
- `Pipeline/` holds:
  - LotFlow and the order source;
  - the batch classes;
  - startup and cleanup;
  - the host.
- `Line/` holds the options, the line definition and the validator.

### Resource selection

1. **Candidates:** a step's resources come from the MES (`GetResourcesForStep`, cached per step). A
   `Resources` list in `simulationConfiguration.json` only *restricts* that list.
2. **Eligibility:** only resources that offer the lot's `RequiredService` are used. If none of them do,
   all candidates are tried and the MES has the final say.
3. **Order:** resources are picked in random order.
4. **Fallback:** a `ResourceUnsuitableException` thrown by a BeforeTrackIn action moves on to the next
   resource. Examples: no feeder takes a product the BOM needs; the needed reticle is mounted on another
   stepper.
5. **Refused for now:** when the track-in is refused because of the lots in process (the resource is full,
   or holds lots of another BOM or product), the lot first tries the step's other resources. Only when all of
   them refused does it wait on one and retry. When that wait budget runs out, it starts over on all
   resources. It is never dropped for this.
6. **Operator checked out:** "Employee 'admin' is not checked in." on a setup, track-in or track-out
   checks the operator in again and repeats the call once (`AsOperatorAsync`).
7. **Safety net:** a lot whose step fails while it is still `Queued` there (nothing was done to it) is run
   again after 10, 20 and 40 simulated minutes before it is left there (`LotFlow`).

### Step actions (`Actions` in simulationConfiguration.json)

| Key | Hook | What it does |
|---|---|---|
| `feeders` | BeforeTrackIn | Makes sure the feeders hold every consumable the BOM needs, as the Perform Setup wizard would (`GetDataForPerformSetupWizard` + `EvaluateBOMProductsCondition`). <br>• Fills an empty feeder from its dispatch list.<br>• Swaps a shared feeder holding a product this lot doesn't need, only when nothing is in process.<br>• Creates a consumable when none is dispatchable, modelled on one a feeder can dispatch.<br>• Replaces low consumables: creates a new one, detaches and attaches in one `ManageResourceConsumableFeeds` call, then terminates the old one.<br>• On a **line** resource, fills the feeders of the chambers that run the lot's line flow. |
| `durables` | BeforeTrackIn | Mounts the durables the BOM needs (reticles, blades). If none is queued, the resource is unsuitable. |
| `compose` | AfterTrackIn | Composes the wafers into the lot (SILICON WAFER COMPOSE). |
| `subMaterialTracking` | AfterTrackIn | Tracks the wafers in and out. If the resource has Process sub-resources (chambers), the wafers go onto one of them. |
| `lineFlow` | AfterTrackIn | METAL PLATING: runs the wafers through the line flow on the lane's chambers. Resumable. |
| `pack` | AfterTrackIn | Wafer PACKING: `PackMaterial` by wafer ids (no PackageQuantity). Skips the lot if it's already packed. |
| `splitTrackOut` | TrackOut | ASHING PRE: splits the wafers into random groups, each tracked out as a new lot. |
| `ship` | TrackOut | SORTER: tracks the lot out, ships it to the facility's shipping facility and receives it at the next step. |

Conditions: `inspCdRequired`.

### Step options (simulationConfiguration.json `Steps`)

| Option | Meaning |
|---|---|
| `Resources` | Restricts the step to these resources (optional). |
| `MinSeconds`, `MaxSeconds` | Per-lot process time range (load, setup, or the whole run of a batch tool), in simulated seconds. |
| `MinSecondsPerWafer`, `MaxSecondsPerWafer` | Per-wafer time (single-wafer tools), added once for each wafer of the lot. Default 0. |
| `LineStepMinSeconds`, `LineStepMaxSeconds` | `lineFlow` only: time of each line flow step (chamber). |
| `Actions` | Action keys, as in the table above. |
| `Condition` | Condition key. |
| `SingleMaterial` | Expose: one material per resource at a time. It waits, then aborts leftovers (`Line:ResourceOccupancy`). |
| `Batch` | `{ MinQuantity, MaxQuantity, CheckIntervalSeconds }`. |
| `PassThrough` | No track-in: the lot is moved next. |
| `Ship` | With `PassThrough`: ship instead of moving next. |
| `ShipTo` | Destination facility, when the facility has several. |
| `Receive` | With `Ship`. `false` ends the line and leaves the lot in transit. |
| `ClosesProductionOrder` | With `PassThrough`: closes the lot's PO once it reached its goal. |

### Other simulationConfiguration.json sections

- **`Order`:**
  - facility `Production FE SC`;
  - flow paths;
  - lot size 5–25 wafers;
  - characteristics `Quality=QA`, `Coating=PI`;
  - **`Products`**: the products launched, each order for one of them, picked at random by `Weight` (the
    default is 1; 0 keeps a product configured but never launches it). A product lists only what differs from
    the fields above: `Facility`, the three flow paths, `MaterialType`, `CapacityClass`, `DiesPerWafer`,
    `Min`/`MaxLotQuantity`, `Characteristics`. Without `Products`, the single `Product` field is used. All
    products share the `Steps` of simulationConfiguration.json: the MES resolves service, BOM, durables and plating line flow
    per lot, so the same step list ran the five front-end products in the master data (see "Products");
  - `ProductionOrderNameFormat` `PO SC.{id}`: the naming convention of the simulator's orders, `{id}` being 8
    random uppercase hex digits (`PO SC.4592FB2C`). Only orders whose name matches it exactly are ever terminated
    (`TerminatePreviousRuns`, `FailedLots`); the MES's own `PO SC.0001` doesn't. It must contain `{id}` once,
    after at least 2 characters. (It replaces `ProductionOrderPrefix`: `"PO SC"` is now `"PO SC.{id}"`.)
  - `LaunchIntervalSeconds`;
  - `MaxOrders` (0 = no limit).
- **`Startup`:** `DisableMail`, `DisableDeeActions` and `DisableTimeConstraints` change the **whole MES**, not only
  the simulator's orders, and are not undone on exit. They are off unless the line file turns them on, and the
  simulator logs a warning naming the MES before it applies them. Only turn them on for an MES the simulator owns.
  - `DisableMail`: mail future actions break move-next with "The data for the From was not found".
  - `DisableDeeActions`: `Dee_Notification_ExitStep` and `Dee_Notification_EnterStep`. Both fail on
    notification type "General".
  - `TerminatePreviousRuns` and `TerminateReason`, which is `Full Loss` because there's no `Terminate`
    reason in this MES.
  - `CheckInOperator` and `Operator { Calendar, Type, CostCenter }`. The employee for user `admin` is
    created when it's missing: Calendar and EmployeeNumber are mandatory.
  - `DisableTimeConstraints`.
- **`FailedLots`:** `{ Terminate: true, TerminateReason: "Full Loss" }`. Before giving up on a lot, the simulator
  retries it: a lot still queued at its step runs the step again (3 times), a lot left in process (it failed after its
  track-in, e.g. at the track-out) has its step finished on the resource it is on (twice; the resumable actions,
  such as the split and pack actions and the line flow, continue where they stopped), and a lot already processed at its
  step is moved on. A lot it still gives up on is aborted when in process and terminated, so it doesn't hold its
  resource (a lot in process keeps the resource's BOM, setup and slots, and lots of other products are refused there).
  Only lots of orders matching `ProductionOrderNameFormat`. `false`: the lot is left where it failed.
- **`Startup` guards for `TerminatePreviousRuns`:** `ConfirmTerminate` (true: ask "Terminate N materials of M orders on
  &lt;MES&gt;? [y/N]" first; without a console, e.g. in a container, the termination is refused: use `--yes` or set it to
  false), `MaxTerminatedOrders` (100: more simulator orders than that stops the run with an error; check the naming
  convention first), and `--dry-run` (`DryRun`), which only lists them.
- **`Mes`:** `RetryMaxAttempts` (5, 0–10), `RetryDelay` (1 s, up to 30 s), `MaxConcurrentCalls` (16: the MES calls are
  synchronous, so at most this many run at once and the thread pool isn't starved at a high speed). A `BatchExecute` is
  never retried (some of its inputs may already have been applied).
- **`Consumables`:**
  - `Default` is `{LowQuantity 2000, ReplacementQuantity 60000, TerminateReason "Full Loss"}`, with
    overrides per product.
  - Targets: 0.1 / 1. Plating chemicals: 5 / 50. Wet-etch chemicals: 5 / 25.

## The front-end flow `Y31_CSP_3L` (what each step needs)

| Sub-flow | Step | Simulator handling / MES facts |
|---|---|---|
| WAFER PREP | PREPARATION WPREP | Lot is created here, then moved to COMPOSE. |
| | SILICON WAFER COMPOSE | `compose` |
| | SILICON WAFER LAMINATOR | `feeders` (BG Tape) |
| | SILICON WAFER BACK GRINDING | `durables` |
| | SILICON WAFER PEELING | `subMaterialTracking` |
| — | PRE_CURE | Batch on the Koyo ovens. The MES also enforces a **70% minimum batch size**. |
| PHOTO FUSE | ASHING PRE | `splitTrackOut` |
| | COAT / EXPOSE / DEVELOPER | `feeders` / `durables` + SingleMaterial / `feeders`. The Developer has a **single shared feeder** (PI Spray vs AZ Spray swap). |
| | SAR Capture Result | Send-ahead only, not configured. |
| | INSP CD / INSP Overlay | Conditional in the MES. |
| | CureWafers | Batch on the Koyo ovens, which are shared with PRE_CURE; one batch per oven at a time. |
| | AOI, METAL THK | Plain steps. |
| METAL | METAL DEPOSITION | `feeders` (Al/Ag targets). **NEXX-001 needs operator check-in.** **NEXX-003 has no Ag target feeder** (unsuitable for this BOM). |
| | METAL THK | Plain step. |
| PHOTO AZ | COAT / EXPOSE / DEVELOPER / INSP CD / INSP Overlay | Same steps as PHOTO FUSE, but each **sub-flow needs its own service** (e.g. "Insp Overlay Layer AZ"; LEICA only offers FUSE). |
| | METAL PLATING | **Line step** on `Semitool Raider ECD312`:<br>• 2 lanes; a lane is required at track-in.<br>• Line flow `Clean-Cu-SRD`: PreClean → Cu - Copper → Spin Rinse and Dry, on lane-bound chambers (CP-001…012) with shared chamber feeders.<br>• `feeders` + `lineFlow`.<br>• Max 2 lots at a time. |
| PLATING ANALYSIS | METAL (DEPOSITION/PLATING) ANALYSIS | One of three steps, chosen by the MES from `Product.Maturity`. Resource: FE Laboratory. |
| | METAL WET STRIP | `subMaterialTracking` (onto a WSM chamber). |
| | METAL WET STRIP MACRO INSP, METAL WET ETCH MACRO INSP | Plain steps. |
| | METAL WET ETCH | `feeders` (AZ 100 Removal, CuEtch, TiEtch). |
| PHOTO POLYMIDE | DESCUM | `subMaterialTracking` |
| | COAT / EXPOSE / DEVELOPER / INSP CD / INSP Overlay | As above. |
| | CureWafers | Batch (70% minimum). |
| | AOI | Plain step. |
| — | SORTER | `ship`: Wafer PACKING is in **Warehouse FE SC**, reached only by Ship + Receive. |
| — | Wafer PACKING | `feeders` (Shielding Foil Packaging 360) + `pack`. |
| — | Wafer Shipping FE | **End of the line.**<br>• `PassThrough` + `ClosesProductionOrder` + `Ship` to Production BE SC with `Receive: false`.<br>• `IsPassThrough`, `MarksProductCompletion`. |
| — | Wafer Reception BE | Not reached. When received in BE, the MES routes the lot to `SAWING/Wafer Mounting`. |

### Products

The master data has six products on the front-end flow `Y31_CSP_3L`. They share the same 37 configured steps
(the derivation from the master data gives identical Steps for all six). What differs is resolved by the MES per
lot, from the product or its product group: the services of a step (e.g. COAT, METAL DEPOSITION), the BOM, the
durables (reticles, grinding wheels) and the plating line flow.

| Product | Group | Material type | Wafer | Dies per wafer | METAL DEPOSITION service | METAL PLATING line flow | In simulationConfiguration.json |
|---|---|---|---|---|---|---|---|
| `2EDN7524F` | 2EDN Product Family | Production | 300mm | 983 in the master data; simulationConfiguration.json uses 500 | Sputtering Al_Ag (NEXX-001) | Clean-Cu-SRD | launched |
| `2EGN7524G` | 2EGN Product Familiy | Production | 300mm | 984 | Sputtering Cu_Al_Zn (NEXX-001, NEXX-003) | Clean-Ni-SRD | launched |
| `KommSemi Generic Brand Eice Driver` | Generic | Production | **200mm** | 500 | Sputtering Ag_Zn_Cu (NEXX-001) | Clean-Au-SRD | launched |
| `KommSemi Generic Brand Gate Driver` | Generic | Production | **200mm** | 501 | Sputtering Ag_Zn_Cu (NEXX-001) | Clean-Au-SRD | launched |
| `2EDN8524F*` | 2EDN Product Family | **Engineering** | 300mm | 990 | Sputtering Al_Ag | Clean-Cu-SRD | `Weight` 0: engineering lots are out of scope |
| `2EGN7523G*` | 2EGN Product Familiy | **Engineering** | 300mm | 984 | Sputtering Cu_Al_Zn | Clean-Ni-SRD | `Weight` 0: engineering lots are out of scope |

- **Validated live (2026-10-05, `--speed 3600 --terminateonstart`):** the final run launched 16 orders across the four production products: 10 closed, 0 stopped lots, 2 reworks; the other 6 waited for batch minimums (PRE_CURE had 69 of its 70 wafers). Each product closed at least one order, so Cu, Ni and Au plating, the 300mm and 200mm packing and the grinder wheel swap all work. Earlier runs on the way found and fixed six problems that only show with several products or under that load (see the errors table). Engineering products were not run.
- **The drivers' grinder:** their durables BOM needs **200mm** grinding wheels, which the master data mounts on
  grinder DFG8570. The only grinder with their service (`Standard Grinding`) is DFG8560, with 300mm wheels mounted,
  so the `durables` action swaps the wheels there. On a clean MES it works (live, 2026-10-05: 8 driver orders on
  their own, 3 Gate Driver orders closed, 200mm packing included). The MES refuses the swap while any lot is in
  process on the grinder ("When changing the resource durables with materials in-process at the resource, the
  resource attached durable products and positions must remain unchanged."), so the action reports the grinder as
  temporarily unsuitable and the lot waits, like for a shared feeder. (The first live attempt failed only because
  8 lots left by killed runs sat in process on DFG8560.)
- **Same step, different BOM at the same time:** the coaters verify the BOM at track-in (`VerifyMaterialBOMAtTrackIn`)
  and only accept lots with the BOM of the lots already in process. With one product this never happened; with
  several, a lot of one product waits (like for a full resource) until the other's lots are done. The packing
  stations behave the same way ("does not allow Product mixes").
- **Engineering products:** their wafer stock is of type Engineering, and the demo business workflow and rules
  treat engineering lots differently (e.g. "Rule Only Production Material" skips AOI). The README scope says
  engineering lots are skipped, so they stay off. `MaterialType: "Engineering"` is already set for them.
- **Order mix:** a weight is a share of the orders, and the launch interval is unchanged. METAL DEPOSITION
  (NEXX-001 serves every product) and the plating line (2 lots at a time) stay the bottlenecks. Check the
  cadence table again if the mix changes much.

### Production order closing

- **Release at creation:** `OrderSource` releases every production order right after creating it
  (`ReleaseProductionOrders`). An order left in `Created` is never completed by the MES.
- **The MES does the counting:** when lots reach the step that marks product completion (Wafer Shipping
  FE), the MES adds to the order's `CompletedQuantity`, counting the lots' wafers. Once the order's
  quantity is reached, it moves the order to `Completed`.
- **Closing:** at Wafer Shipping FE, `ProductionOrderCompletion` releases the order if it's still
  `Created` (orders from older runs), then closes it (`CloseProductionOrders`) once it's `Completed`.
  Closing only works from `Completed`.
- **Shipping:** the lot is then shipped to Production BE SC and not received. An order closes fine even
  when some of its lots have already shipped.
- **Verified end to end:** on 2026-10-03 a 20-order run (`MaxOrders` 20, `--speed 20`) closed 14 orders
  by itself. The rest were lots waiting for a final batch below the minimum, plus one lot that hit the
  feeder conflict fixed since.
- **Orders that lost wafers (scrap):** they never reach their quantity, so the MES never completes them.
  The MES has no "complete order" service. So when the order has nothing left in progress
  (`InProgressQuantity` 0) and it is still under quantity, `ProductionOrderCompletion` lowers its quantity
  to the completed quantity (`ChangeProductionOrdersProductAndQuantity`). The MES then moves it to
  `Completed`, and it is closed. This check runs at Wafer Shipping FE, and also when a scrap takes all of
  a lot's wafers. Verified live:
  - by hand on PO SC.BEA8A8DF: 11 → 9, then Completed, then Closed;
  - by the simulator in run 5 (2026-10-04) on PO SC.201A0772: "quantity lowered from 12 to 7", then
    Closed.
- **Batch minimums still apply:** a production order closes only when all its wafers get there.
  PRE_CURE and CureWafers need at least 70 wafers per batch (the MES enforces 70%), so orders whose lots
  end up in a final batch below the minimum wait until more lots arrive.

## Cadence: real-fab timing at `--speed 1`

At speed 1 the line runs at the pace of a real wafer-level-packaging front end. `--speed N` makes every
simulated time N times shorter: process times, launch interval, batch checks and retry waits. The numbers
are typical industry values, and they are set so that this MES's resources run below saturation.

**Process times** (simulated; per lot + per wafer; a lot has 5–25 wafers):

| Step(s) | Per lot | Per wafer | Why |
|---|---|---|---|
| COMPOSE, SORTER | 5–10 min | 20–40 s | Wafer sorter |
| LAMINATOR, PEELING | 5–10 min | 1–1.5 min | Tape laminate/peel |
| BACK GRINDING | 10–15 min | 4–6 min | Grind + polish, the slowest wafer prep step |
| PRE_CURE (batch) | 1.5–2.5 h | — | Oven cycle, ramp included |
| ASHING PRE, DESCUM, RwASHING PRE | 10–15 min | 1–2 min | Plasma, multi-chamber |
| COAT / RwCOAT | 3–7 min | 1.5–2.5 min | Spin coat (thick resist / polyimide) |
| Expose / RwEXPOSE | 10–15 min | 1–1.5 min | Reticle setup + alignment, then 40–60 wafers/h |
| DEVELOPER / RwDEVELOPER | 3–7 min | 1.5–2 min | |
| INSP CD, INSP Overlay (+ Rw) | 10–20 min | — | Sampled metrology |
| CureWafers (batch) | 3.5–4.5 h | — | Polyimide cure |
| AOI / RwAOI | 5–10 min | 1.5–3 min | Camtek, 20–40 wafers/h |
| RwAOI Manual | 10–15 min | 2–4 min | Manual review |
| METAL THK | 5–10 min | — | Sampled thickness |
| METAL DEPOSITION | 5–10 min | 2.5–3.5 min | PVD cluster |
| METAL PLATING | 10–15 min + 3 × 2–5 min (line steps) | 3–5 min | Cu ECD, 2 lots on the line at a time |
| … ANALYSIS (FE Laboratory) | 15–30 min | — | Lab sample |
| WET STRIP, WET ETCH | 10–15 min | 1–2 min | Single-wafer spray tools |
| … MACRO INSP | 5–10 min | 30–60 s | |
| RwPGMA | 15–25 min | 3–5 min | iCVD |
| RwInspPGMA | 15–30 min | — | |
| Wafer PACKING | 10–15 min | 30–60 s | |

**Launch rate: one order every 80 minutes** (`LaunchIntervalSeconds` 4800). That's about 18 orders,
roughly 270 wafers, a day. The rate comes from the bottleneck. An order averages 15 wafers and splits
into 2.5 lots at ASHING PRE, and each of those lots pays the per-lot time again. Estimated tool time per
order:

| Resource | Tool time per order | Busy at 80 min/order |
|---|---|---|
| METAL DEPOSITION (only NEXX-001 has the Ag target) | ~64 min | ~80% |
| COAT (2 tools, 3 layers) | ~64 min of the 2 tools' time | ~80% |
| FE Laboratory (1 resource) | ~56 min | ~70% |
| INSP CD (2 VISTEC, 3 layers) | ~56 min of the 2 tools' time | ~70% |
| Koyo ovens (2, PRE_CURE + both CureWafers) | ~34 oven-hours/day of 48 | ~70% |
| Steppers (3, 3 layers) | ~50 min of the 3 tools' time | ~60% |

Faster launches saturate METAL DEPOSITION and the coaters, so queues then grow without limit. The ovens
also wait for 70 wafers per batch, which takes a few hours at this rate.

**Cycle time:** about 30 h of raw process time per order, plus queues and batch waits, so roughly
1.5–2 days at speed 1. That's about 1.5–2 h at `--speed 20` and 20–30 min at `--speed 100`. A real
plant's cycle time is longer: lots also queue behind other products that aren't simulated here.

**Rework and scrap rates** (`Chaos`). A chance applies at each step where the MES offers the flow, so the
per-lot rate is the chance × the number of steps:

| Flow | Chance per step | Offered at | Per lot and layer |
|---|---|---|---|
| RWK_FUSE / POLYMIDE Photoresist (litho not OK) | 0.75% | COAT, EXPOSE, DEVELOPER, INSP CD | ~3%, typical litho rework |
| … Photoresist TC (time constraint before cure) | 0.2% | ~6 steps up to CureWafers | ~1% |
| RWK_AOI FUSE / POLYMIDE | 2% | AOI | ~2% |
| RWK_PRECURE | 0.25% | ASHING PRE, FUSE COAT | ~0.5% |

- **Reworked lots:** about 1 lot in 8 is reworked at least once.
- **Scrap:** 10% of reworked lots lose 1 to 20% of their wafers, so about 0.2% of wafers are scrapped. A
  front-end line typically yields about 99.5% of its wafers.
- **Short runs:** a short demo run (e.g. 20 orders) may see only a couple of reworks and no scrap. For
  more chaos, raise the chances in a copy of simulationConfiguration.json, or set `DIAG_CHAOS=1` in the diag runner.

**Fast runs (tests, demos): `--speed 3600`.** One simulated hour then takes one real second: a 10-minute
step takes ~0.17 s and a 4 h cure ~4 s. At that speed, the MES calls set the pace instead: each step makes
several calls of 0.5–2 s each. So:
- **Polling waits have a real-time floor:** `Simulation:MinPollInterval`, 5 s by default
  (`SimulationClock.PollInterval`). This covers the full/busy resource retries, batch queue checks, and
  waiting for this run's lot on a stepper.
- **Lots take turns retrying:** lots waiting for the same full resource (or the same busy step) take
  turns, and only one of them retries the MES at a time.
- **Retry budgets** cover 6 simulated hours *and* at least 15 real minutes (`PollCount`). A lot ahead is
  slowed by its MES calls, not by its simulated time.
- **Cap on lots in flight:** `Order:MaxLotsInFlight` (20 in simulationConfiguration.json) holds back new orders while that
  many lots are running; lots waiting at a batch step don't count. At speed 3600 the 80-minute launch
  interval is 1.3 s, much faster than the line can absorb. For a long speed-1 run, raise the cap or set it
  to 0: a real-cadence line holds about 70 lots.
- **Why all this:** run 6 (2026-10-04, `--speed 3600`, before the cap and the turn-taking) put 36 lots in
  the queue for METAL PLATING (one line, 2 lots at a time). Each of them retried a dispatch + track-in
  every second. The MES answered with SQL deadlocks ("chosen as the deadlock victim"), then stopped
  answering. The LBO client's circuit breaker opened ("the circuit breaker is open after exceeding the
  failure threshold"), and every lot stopped.

**Waits that had to change for real-time cadence:**
- **Full or busy resource:** the lot retries every 2 simulated minutes for up to 6 h (it was 30 s ×
  20). At speed 1, the lot ahead can run for an hour.
- **Single-material resources (Expose):**
  - The occupancy policy remembers which lots of this run it let onto a stepper, and it waits for them
    for as long as they process.
  - Only materials this run didn't put there (leftovers) are aborted, after `ResourceOccupancy:MaxChecks`.
  - Before this, a waiting lot aborted the simulator's own lot after 5 real minutes, mid-process.

## Chaos: rework and scrap

Configured in `simulationConfiguration.json` → `Line:Chaos`:

| Option | Meaning |
|---|---|
| `ReworkFlows` | The rework flows the simulator may use, by flow name (the 7 core flows), each with the chance (0..1) that a lot goes there **from each step where the MES offers it**. Other flows the MES offers (RWK_PRECLEAN, RWK_WETETCH) are ignored. Empty: no rework. |
| `MaxReworksPerLot` | How many times one lot can be reworked over the whole run (the count survives batches). 2 in simulationConfiguration.json. A lot split off at ASHING PRE has its own count. |
| `ScrapProbability` | Chance (0..1) that a reworked lot loses wafers. 0.1 in simulationConfiguration.json. |
| `MaxScrapFraction` | The most wafers a scrap takes, as a fraction of the lot (at least one wafer). 0.2 in simulationConfiguration.json. |
| `ScrapReasons` | Loss reasons to scrap with: Broken Wafer, Contamination, Scratch-Loss. |
| `Seed` | Optional. Makes a run repeatable (resources, times, rework, scrap). |

How it works (`ReworkChaos`, called by LotFlow before each non-pass-through step of the main flow):

1. **Roll:** one random roll per flow in `ReworkFlows`, against its chance. With no hit, nothing happens
   and the MES isn't called; that's the case for almost every lot at almost every step.
2. **Ask the MES:** `GetPossibleReworkPathsForMaterial` lists the paths for the lot at its step. Each
   path has a reason, a goto flow path and a return flow path. Only paths into a flow whose roll hit are
   kept; if none is left, nothing happens.
3. **Rework:** pick one path at random and call `ComplexReworkMaterial` with a `MaterialOffFlow` built
   from it, the same way IndustrialEquipmentSimulator does. A refused rework (e.g. reason limit reached)
   is only logged; the lot carries on.
4. **Scrap:** a second roll against `ScrapProbability`. On a hit, `RecordSubMaterialsLoss` scraps 1 to
   `MaxScrapFraction` of the lot's wafers, with `TerminateOnZeroQuantity`. A lot left with no wafers is
   gone. The reason is picked from the `ScrapReasons` the step accepts: the loss reasons in its
   `StepReason` relations. A reason the step doesn't define is refused ("Loss Reason … is not defined in
   Step …").
   Scrapped wafers are terminated, but the MES still offers them when packing (`PossibleTargetMaterials`).
   `pack` therefore packs only the lot's live wafers.
5. **Back to the main flow:** the lot follows its rework flow like any other route; the rework steps are
   configured in simulationConfiguration.json. At the end, the MES returns it to the path's return step.

Rework paths the MES offers for a **queued** lot (checked 2026-10-03):

| From | Reason | Rework flow | Returns to |
|---|---|---|---|
| FUSE COAT / Expose / DEVELOPER / INSP CD | LITHO PHOTO Nok OK | RWK_FUSE Photoresist | FUSE INSP CD |
| FUSE … / INSP Overlay / CureWafers | Time Constrain Violation Before Cure | RWK_FUSE Photoresist TC | FUSE CureWafers |
| POLYMIDE (same steps) | (same reasons) | RWK_POLYMIDE Photoresist (TC) | POLYMIDE INSP CD / CureWafers |
| AOI | — | RWK_AOI FUSE / POLYMIDE | — |
| ASHING PRE, FUSE COAT | Time Constrain Violation | RWK_PRECURE | FUSE COAT |
| METAL WET STRIP / WET ETCH | Process Aborted Rework | RWK_PRECLEAN / RWK_WETETCH (step RwPreClean) | WET … MACRO INSP. **Not core: filtered out by `ReworkFlows`.** |
| METAL / PHOTO AZ, PRE_CURE (queued) | none offered | — | — |

The AOI row wasn't seen for queued lots. It may be offered from another state (e.g. processed); check
it in a run log ("Rework: … sent from …").

Rework flows and their steps (resources come from the MES):

| Rework flow | Steps |
|---|---|
| RWK_FUSE / RWK_POLYMIDE Photoresist | RwPGMA (iCVD PGMA) → RwInspPGMA (LEICA) → RwCOAT (`feeders`) → RwEXPOSE (`durables`, single material) → RwDEVELOPER (`feeders`) |
| … Photoresist TC | Same as above, then RwINSP CD → RwINSP Overlay |
| RWK_AOI FUSE / POLYMIDE | RwAOI (Camtek) → RwAOI Manual (NIKON-MAN) |
| RWK_PRECURE | PRE_CURE (batch) → RwASHING PRE (`splitTrackOut`) |

## MES behaviours worth remembering

- **Tracking:**
  - Track-in/out on a resource needs the resource's own state model. Transitions are "Standby to
    Productive" and "Productive to Standby". Only ask for a transition from the state the resource is
    actually in.
  - In-line wafers are tracked onto a chamber with `ComplexTrackInMaterials` and a `Resource` (not
    dispatched). A plain track-out moves them to the next line step.
  - The chambers a lot may use come from `GetMaterialLineData` → `LineStepsData[].LineResourcesData`.
- **Shipping:**
  - Ship is `ShipMaterials(DestinationFacility)`; the lot is then InTransit.
  - Receive is `ReceiveMaterials` with a mandatory FlowPath. Take it from `GetMoveNextFlowPath` *before*
    shipping; it isn't available once the lot is in transit.
- **Packing:** `CreatePackages` puts wafers in a package but does **not** count as packing the lot.
  Always use `PackMaterial`.
- **Batches:**
  - A batch that fails to release stays in the MES and holds its lots
    (`InvalidBatchMaterialAlreadyInUse`), so cancel it.
  - The two CureWafers steps (PHOTO FUSE and PHOTO POLYMIDE) share one batch queue, because it's keyed by
    step name, so a batch can mix lots from both sub-flows. The MES accepts this, but after the batch each
    lot has to be moved to *its own* next flow path. Lots are grouped by their next path.
- **Consumables and durables:**
  - `CloneObject` on a Material fails; create a new material instead.
  - A new consumable is dispatchable only if modelled on a material in a dispatchable location (e.g.
    Kanban DRY&WET), not in the warehouse.
  - A shared feeder (the developers' single feeder for PI Spray and AZ Spray) can't be swapped while
    another lot is in process. The step tries the other resource, then waits and retries.
  - The resource can change while feeders are being prepared (another lot tracks in or out): "has been
    changed by another user". The feeder preparation then reloads everything and starts over.
- **Expose (single material):** another lot can swap the reticle between this lot's mount and its
  track-in. So the stepper is held from *before* the durables setup until the track-in.
- **Transient errors (retried):** "has changed since last viewed", `DataChangedSinceLastViewed`,
  deadlocks.
- **Employee:** every check-in/out changes the employee, so reload it before the next check-in/out.
- **Production orders:** the order states are Created → Released → InProgress → Completed → Closed (or
  Canceled). There are services to release, start, close, reopen, cancel and change quantity, but none
  to complete an order. `InProgressQuantity` drops to 0 once none of the order's wafers is still on the
  line.
- **Rework:** a lot can only be reworked from a step where the MES offers a rework path for it
  (`GetPossibleReworkPathsForMaterial`). When the rework flow ends, the MES moves the lot back to the
  path's return step; the simulator does nothing special for that.
- **Killed runs leave lots in process, and they keep occupying their resource.** Stopping the process (task
  manager, `Stop-Process`) instead of Ctrl+C leaves the lots it had tracked in "in process" in the MES.
  Probed on 2026-10-05: the MES held **806 in-process materials** of `PO SC.*` orders from earlier runs, among them
  2 lots on the Raider (its two slots), 27 and 40 lots on the two coaters, and 8 on grinder DFG8560. They block
  other lots: a full Raider never takes a lot, a coater only takes lots with the BOM of the lots in it, and the
  MES refuses to change a resource's durables while any material is in process. `--terminateonstart` clears
  them (see below).
- **`--terminateonstart` (`PreviousRunCleanup`)** terminates every open material of the simulator's orders
  (`PO SC.<8 hex>`; never the demo orders `PO SC.0001`…). Checked live on 2026-10-05, with an independent query of
  the in-process materials by product: it found all 791 simulator materials in process (264 lots, 528 wafers, no
  orphan wafer), and left only a demo lot. It aborts the in-process ones first, then terminates, and repairs what
  the MES refuses (the clean-up of ~13,300 materials in 891 orders took 10 minutes):
  - "Step X must have Loss Reasons defined": the step doesn't accept the cleanup's reason (`RwPreClean` only
    accepts Scratch-Loss), so a reason the step accepts is used; a step with none (`PREPARATION WPREP`) gets the
    lot moved to the start step first;
  - "…Resource Job has dependencies… BATCH-26-31 (Batch)": a leftover batch still holds the lot, so the batch is
    cancelled (aborted first when in process). Cancelling works on *Created* and *Queued* batches (live); aborting
    an in-process batch is untested.
  Afterwards the MES held only the 654 demo materials of `PO SC.*` orders plus the new run's own.
- **Several products on one line:** resources can refuse a lot because of the lots of *another* product in
  process: the coaters (BOM verification) and the packing stations ("does not allow Product mixes"). The lot waits
  like for a full resource. Everything else about a lot's product is resolved by the MES from the lot.
- **Skipping a conditional step:** a queued lot can't be moved next ("Material is not Processed"). `SkipMaterialsProcess`
  makes it processed, then it moves next. Only lots of a product that fails the condition need it (INSP CD runs for
  the 2EDN family only).
- **Races between lots on shared setup**, which a single product hit rarely: the Developer's single feeder (a lot
  swaps it between another lot's setup and track-in) and the reticles shared by the steppers (two lots pick the
  same queued reticle). The step sets the resource up again (4 attempts), and the durables action picks another
  reticle.

## Errors met while building it, and their fixes

Most of the simulator's behaviour comes from an error that a live run hit. When a run fails, look the
message up here first.

| Error / symptom | Cause | Fix in the simulator |
|---|---|---|
| Move-next fails: "The data for the From was not found" | Mail future actions | `Startup:DisableMail` |
| DEE fails on notification type "General" | `Dee_Notification_ExitStep` / `EnterStep` | `Startup:DisableDeeActions` |
| Time constraints between steps get in the way (out of scope) | Time constraints are defined on the flow | `Startup:DisableTimeConstraints`. Clear the step list even when the smart table is already empty. |
| Track-in: resource state transition invalid | Asking for a transition from a state the resource isn't in | Read the current state, set resources to Standby at startup |
| "maximum number of concurrent materials in process" | Resource full | The track-in waits and retries |
| Check-in fails on NEXX-001 / the employee changed | The resource needs an operator check-in; every check-in changes the employee | `OperatorCheckIn`; reload the employee each time |
| No feeder accepts a product the BOM needs (e.g. NEXX-003 has no Ag target) | That resource can't run this BOM | `ResourceUnsuitableException` → try the next resource |
| A shared feeder can't be swapped while another lot is in process (Developers) | One feeder for PI Spray and AZ Spray | Temporary unsuitable: wait (30 s scaled) and retry, up to 20 times |
| "has been changed by another user" while preparing feeders | Another lot tracked in or out on the same resource | Reload and start the feeder preparation again (3 tries) |
| `CloneObject` on a consumable fails | Not supported for materials | Create a new material modelled on a dispatchable one (Kanban DRY&WET, not the warehouse) |
| Expose: "not enough quantity of Durable Reticle Layer FUSE" | Another lot swapped the reticle between this lot's mount and its track-in | Hold the stepper from before the `durables` setup until the track-in |
| Batch release fails, then lots are refused with `InvalidBatchMaterialAlreadyInUse` | The failed batch still holds the lots | Cancel the batch when its release fails |
| After a CureWafers batch: "Step AOI not found in Material Flow" | The batch mixed FUSE and POLYMIDE lots and moved them all to one path | Move next per group of lots with the same next flow path |
| Batch below 70% refused | MES batch minimum | `Batch.MinQuantity` 70; the batch waits for more lots |
| `CreatePackages` succeeds, but the lot can't track out of Wafer PACKING | `CreatePackages` doesn't count as packing | `PackMaterial` by wafer ids |
| `ReceiveMaterials` needs a FlowPath that a lot in transit doesn't have | The next path is only known before shipping | `GetMoveNextFlowPath` before `ShipMaterials` |
| "Invalid operation Close… SystemState is not Completed" | The order was never released, so the MES never completed it | Release at creation; release at completion if still Created |
| Order stuck below quantity (e.g. 9/11 InProgress) after scrap | Scrapped wafers never complete | Lower the quantity to the completed quantity, then close |
| Pack took 11 wafers of a lot left with 9 | `PossibleTargetMaterials` still lists scrapped wafers | Pack only live wafers |
| Rework into RWK_PRECLEAN / RWK_WETETCH (step RwPreClean) | The MES offers non-core paths from METAL WET STRIP / WET ETCH | `Chaos:ReworkFlows` allow-list |
| "Loss Reason Contamination is not defined in Step RwPreClean" | Each step only accepts its own loss reasons | Use only reasons in the step's `StepReason` relations |
| A lot reworked twice despite `MaxReworksPerLot` 1 | Run data is new after a batch | Count reworks per lot name for the whole run |
| (Design) At speed 1, a lot waiting for a stepper would abort this run's own lot after 5 real minutes | The occupancy policy counted real-time checks and aborted whatever was in process | Wait for this run's lots for as long as they process; abort only leftovers |
| (Design) At speed 1, a lot gave up after 10 simulated minutes waiting for a full or busy resource | Retry budget of 20 × 30 s | Budget of 6 h simulated and at least 15 min real |
| (Design) At high speeds, polling waits shrink to milliseconds and flood the MES | Waits scaled by speed with no floor | 1 s real-time floor on polling waits |
| SQL "deadlock victim" errors, then every lot stops: "the circuit breaker is open after exceeding the failure threshold" | Fast run (`--speed 3600`): launches outran the line, and dozens of lots queued at METAL PLATING retried a track-in every second | `MaxLotsInFlight` cap, lots take turns retrying, 5 s poll floor (see "Cadence") |
| Startup: "HttpClient.Timeout of 60 seconds elapsing" | The MES (or the network/VPN) is unreachable | Not a code problem: check `curl <EnvironmentAddress>` and rerun |
| `ObjectDisposedException` in `Program.Main` after a failed run | The host was disposed before the MES call summary was read | The statistics are resolved before `RunAsync` |
| RemoveSubMaterials: "Material Package Count is greater than zero" (back end) | Wafers are still packed | Unpack before Die Attach Bank (not simulated yet) |
| Track-in at COAT: "…the required Material BOM does not match the current BOM at the Resource and the Resource is configured to only accept Materials in-process with the same BOM" | Two products on the line: the coaters verify the BOM, and a lot of another product is in process | Wait and retry like for a full resource (`StepExecutor`) |
| BACK GRINDING: "When changing the resource durables with materials in-process at the resource, the resource attached durable products and positions must remain unchanged." | The driver products (200mm wheels) need the wheels of DFG8560 swapped, and the MES refuses it while any lot is in process there (leftover lots of killed runs made it permanent) | The `durables` action reports the grinder temporarily unsuitable: the lot waits. Clean the leftovers with `--terminateonstart` |
| Lots wait for METAL PLATING forever ("can't take … yet"), none ever tracks in | Lots left in process by killed runs fill the Raider (it takes 2 materials) | Abort or terminate those lots (not done: they aren't from the current task), then rerun. Check with a probe that lists the in-process materials per resource |
| "Invalid operation GetDataForMultipleMoveNextWizard on Material X. Material is not Processed." when a lot skips a conditional step | Moving a *queued* lot next | `SkipMaterialsProcess`, then move next (`SkipAndMoveNext`) |
| Track-in at the packing station: "The Resource PACK_WS-001 does not allow Product mixes." | Lots of another product are in process there | Wait and retry like for a full resource |
| Track-in at DEVELOPER: "There was no Material of Product AZ Spray found in the Consumable Feeds of Resource SUSS Devs-001" | Another lot swapped the shared feeder between this lot's setup and its track-in (a set-up lot isn't "in process" yet) | `SetUpLots`: a lot set up and not yet tracked in blocks other lots from swapping that resource's shared feeders; the step also sets the resource up again, up to 4 attempts |
| Mounting a reticle: "The object RET L1 2EDN.015 is not Queued." | Another stepper's lot attached the same queued reticle first | Pick another queued durable (4 attempts) |
| BACK GRINDING: "A record with the same key value for key 'UC_ResourceDurable' already exists … (Grinder_Disco DFG8560, G_Wheel 300…)" | The BOM lists the same durable product twice (two wheels) and the random pick chose the same queued wheel twice | Each BOM line picks a different queued durable (`PickableDurables`) |
| Cleanup: "Step RwPreClean must have Loss Reasons defined." / "Step PREPARATION WPREP must have Loss Reasons defined." | The step doesn't accept the termination reason (or defines none) | A reason the step accepts; for a step with none, move the lot to the start step first |
| Cleanup: "Terminate could not be completed. The object JOB-… of type Resource Job has dependencies … BATCH-26-31 (Batch)" | A leftover batch holds the lot | Cancel the batch, then terminate |
| "The data for object Sorter_Asyst-001 of type Resource has changed since last viewed" survives the 5 automatic retries (lot stopped at SORTER) | The tracker read the resource once before the call; `MesCall` repeated the call with that stale object | The tracker's track-in/out/split re-read the resource and call again, up to 3 times (`WithFreshResource`) |
| In a container: startup fails with "Missing value for mandatory property CultureName" (then "The element No WIP is not in the list E58Standby", the fallback reason failing) | The container has no locale, so the current culture is the invariant one, whose name is empty, and the MES client sends that name | `Program.EnsureNamedCulture` sets en-US when the current culture has no name (the Dockerfile also sets `LANG`). The IndustrialEquipmentSimulator does the same: `CultureInfo("en-US")` |
| An environment variable has no effect | The `MESSIM_` prefix is missing, or the key is a step name (spaces) | Prefix it and use `__` between levels; use a line file for steps |
| OpenShift run (speed 10, 2026-10-08/09): 35 lots "stopped at DEVELOPER / METAL DEPOSITION: …only accept Materials in-process with the same BOM" on SUSS Devs-002 and NEXX-001 | NEXX-001 refused every lot from 21:41 to 07:02, although the simulator had nothing on it after 21:13: something with another BOM stayed in process there (not ours; source unknown). Each lot stayed pinned to the resource it was set up on and was dropped when its wait budget (36 min real at speed 10) ran out, one every ~37 min as they took turns. NEXX-003 took lots when one happened to pick it | A refused lot tries the step's other resources first; it waits only when all of them refused, and starts over on all of them instead of failing when the budget runs out (`StepExecutor`). A step whose resources are all temporarily unsuitable keeps waiting too |
| Same run, 07:19–07:20: 7 lots "stopped at METAL DEPOSITION: Employee 'admin' is not checked in." | The MES checked admin out (no "Checked out" in our log, so not the simulator; probably the shift change around 07:00). `OperatorCheckIn` only checked in when a resource's first lease opened, and lots kept leases open there all night | Check-in is verified for every lease; on "is not checked in" the operator is checked in again (clocked in first when the shift clocked it out, and even when the MES still lists it) and the call repeated, up to 3 times with a pause (`OperatorRetry`, `CheckInAgainAsync`), also on the chambers of line steps and sub-material track-ins. A setup action refused the same way tries the next resource |
| A lot that failed after its track-in (e.g. its track-out) stayed in process for good, holding its resource's BOM, so every lot of another product was refused there | Only lots still queued were retried | The lot's step is finished where it is (`StepExecutor.ResumeAsync`, twice); only then the failed-lot fallback (abort and terminate) |
| A split that failed half-way lost the lots already split off (tracked out, never moved on) | The new lots were only handed on once every split had worked | Every new lot is kept in the lot's run data as soon as it exists; a re-run splits only what is left and hands all of them on; a lot that can't be moved is moved on by its flow |
| A failed batch track-in or track-out left the batch holding its lots, which were requeued and failed again every interval | Only a failed release cancelled the batch | Any failure after the batch exists aborts (when in process) and cancels it; queued lots are requeued up to 3 times, the others go to the failed-lot fallback |

## How it was built (and how to keep extending it)

1. **Refactor first.** The tool started as one `ScenarioRunner` partial class with the line hardcoded:
   - global locks, `Thread.Sleep` retries and `new Random()`;
   - about 600 lines of dead SMT code.

   It was rebuilt in phases, each one still buildable and runnable:
   1. cleanup;
   2. the MES gateway with timings;
   3. `StepExecutor` and the step actions;
   4. the `simulationConfiguration.json` pipeline, with MES-driven routing;
   5. unit tests.
2. **Let the MES route.** LotFlow never hardcodes the step order. It reads the lot's current step, runs
   it as `simulationConfiguration.json` describes it, and the MES's move-next decides what comes next. So:
   - conditional steps (INSP CD, the analysis steps) and rework returns need no code;
   - a new step usually needs only a simulationConfiguration.json entry, plus an action if it does something special.
3. **Walk the flow one step further each time.**
   1. Run lots until they stop at a step that isn't configured.
   2. Probe that step in the MES (resources, BOM, feeders, durables, services, line flow).
   3. Add the step to simulationConfiguration.json, and an action if needed.
   4. Drive the stopped lots on with `diag` rather than starting new orders.
   5. Repeat.

   Steps that differed from similar ones were flagged to the user instead of being normalized. Examples:
   AZ needs its own services, NEXX-003 has no Ag target, and the Developer feeder is shared.
4. **Then the order lifecycle:** ship, pack, end at Wafer Shipping FE, release/complete/close the order.
5. **Then chaos:** rework through the MES's own rework paths, then scrap, then closing orders that lost
   wafers. The user chose to lower the quantity and close.

### Validating a change end to end

- **Unit tests first:** `dotnet test Tests/MESSimulator.UnitTests`. Fakes for the tracker, resources,
  chaos and completion are in `StepFakes.cs`. There is no mocking library, so the gateway-heavy classes
  (e.g. `ProductionOrderCompletion`, `ReworkChaos` apart from `WafersToScrap`) are verified live.
- **Live run:**
  - Build into a scratch folder: `dotnet build -o <scratch>/e2e`.
  - Set `Order:MaxOrders` to e.g. 20 in that copy's simulationConfiguration.json. A build into an existing folder doesn't
    overwrite a newer simulationConfiguration.json there, so copy simulationConfiguration.json over first when it changed.
  - Run `dotnet MESSimulator.dll --speed 3600 > e2e.log` (real-fab times; see "Cadence").
  - With the realistic chaos rates, a 20-order run sees only a few reworks. For chaos testing, raise the
    `ReworkFlows` chances in the copy.
- **Watch the log** for the lines that matter:
  ```
  Closed production order|quantity lowered|Rework:|Scrap:|refused|fail:|crit:|Unhandled|stopped at|batch run failed|Invalid configuration
  ```
  "Production order 'X': c/q completed, n in progress" shows an order that is still open.
- **Stopping a run:** stop the `dotnet` process whose command line is `MESSimulator.dll`. Check the
  command line first, so nothing else is killed. Lots in flight stay where they are; `diag` can resume
  them.
- **Forcing chaos:** in `diag`, `DIAG_CHAOS=1` sets the rework and scrap probabilities to 1.
- **Order state:** the probe `po <lot>…` shows quantity, completed quantity and state.
  `poshrink "<order>"` lowers a finished order's quantity and closes it. Use it only on orders the
  simulator created.

### Finding the right MES service

- **Search the client library:** the LBO assembly has every service input. Find one with reflection in
  PowerShell:
  ```powershell
  $a=[Reflection.Assembly]::LoadFrom("bin\Debug\net8.0\Cmf.LightBusinessObjects.dll")
  try { $t=$a.GetTypes() } catch { $t=$_.Exception.InnerException.Types | ? { $_ } }
  $t | ? { $_.Name -match 'ProductionOrders?Input$' } | % FullName
  ```
  Then list the properties of the input with `GetProperties()`.
- **Copy the other simulators:** IndustrialEquipmentSimulator, SMTSimulator and OIBSimulator already
  call many services, and their calls are known to work. This simulator's rework call is copied from
  IndustrialEquipmentSimulator.
- **Use UI captures:** `.har` captures of the MES UI show the exact inputs the GUI sends. Never commit
  them: they contain bearer tokens.
- **Try it with a probe:** make the call in a small probe on one simulator-created object before putting
  it in the simulator.

### Deriving the line from the master data

- `master data/` holds the KommSemi master data: definitions in `masterdata_semi_base1.xlsx` (with DEE code and
  business workflow `.xml` next to it), and instances (materials, mounted durables) in `base2`–`base5`.
- The skill `.claude/skills/simulator-from-masterdata` dumps the workbooks and derives a draft simulationConfiguration.json plus a list of
  flags. On 2026-10-05 its Steps matched this simulationConfiguration.json 37/37 (actions, batch min/max, ship, close, single material),
  and Order/Startup matched field for field. The link model and every rule are in its `references/master-data-model.md`.
- Facts the master data shows that the live runs had to discover:
  - the INSP CD condition;
  - rework `ApplicableToQueued` (why AOI rework isn't offered for queued lots);
  - services per `LogicalName` (LEICA has no AZ overlay);
  - the 70% / 100 batch size (`MinimumBatchCapacityUsage` × `STResourceCapacity`);
  - shipping (step area → facility);
  - the DEE notification type "General", which isn't in the `NotificationType` lookup;
  - SendMail future actions;
  - consumable batch sizes.
- Not yet acted on:
  - the master data gives 983 dies per wafer, while simulationConfiguration.json uses the default 500;
  - the business workflow "Shipping to Facility Production BE SC" also ships lots when they enter Wafer Shipping FE.
- Master data doesn't show the MES runtime behaviours (the errors table above). Process times in
  `StepPRODYCTContext` are demo values, much shorter than the real-fab cadence used here.

### Shell pitfalls on this machine (Windows, Git Bash + PowerShell)

- **Python heredocs:**
  - `python - <<'EOF'` sometimes hangs (it opens the interactive interpreter). Write the script to a
    file and run that.
  - Heredocs also mangle `\r\n` and backslashes.
- **Missing tools:** there is no `strings`; use PowerShell reflection.
- **Watching logs:** a log filter must flush every line (`grep --line-buffered`). Don't end it with
  `cut` or `head`, which hold lines back.
- **CRLF and BOM:** a script that edits files must keep CRLF and the UTF-8 BOM. In Python, read with
  `encoding='utf-8-sig'` and write with `newline='\r\n'`.

## Tooling for diagnosing runs (outside the repo)

A per-session scratch folder held:

- **`diag/`:** a console app referencing `MESSimulator.csproj`. It runs `LineStartup` and then
  `LotFlow` on the lot names given as arguments:
  - a lot left in process is resumed (its AfterTrackIn actions run, then it is tracked out);
  - a processed lot is moved next or shipped;
  - lots waiting at batch steps run when they reach the simulationConfiguration.json minimum.

  It is useful for driving existing lots without creating orders.
- **`probe/`:** a console app with read-only (mostly) MES probes, selected by the first argument:
  - flow tree + resources per step (`<flow name> [start after step]`);
  - `flows <name fragment>`, `lanes`, `chambers`, `bom`, `feeds`, `wafers`, `linedata`, `atstep`;
  - `simlots`, `targets`, `batches`, `ship`, `po`, `packinfo`, `catalog`;
  - `rework <lot>…` (rework paths), `stepreasons <step>…` (loss reasons), `next <lot>…` (next flow paths);
  - `polife`, `polots`, and `poshrink "<order>"`. `poshrink` lowers the order's quantity to its
    completed quantity and closes it; it **changes data**.

  The LBO TypeScript typings under
  `AMQP/Cmf.Custom.IoT/Cmf.Custom.IoT.Packages/node_modules/@criticalmanufacturing/lbos-node/Cmf/Navigo/BusinessOrchestration/<Area>/<Service>.d.ts`
  give the namespace (area) and the input/output shape of any service.
- **`.har`/prompt files** captured from the UI are good references for the exact service inputs. They
  contain bearer tokens: never commit them.

## Industrial line (InduTech, `simulationConfiguration.industrial.json`)

The same program also drives the InduTech HVAC rooftop-unit (RTU) flow, from the master data in
`master data/industrialequipment/`. Only the line file and the MES differ. Every engine change made for it is
opt-in, so the semiconductor line runs exactly as before.

**Running**

```powershell
Copy-Item appsettings.industrial.json appsettings.json   # points at the industrial MES (keep the token out of git)
dotnet bin/Debug/net8.0/MESSimulator.dll --line simulationConfiguration.industrial.json --speed 3600 --terminateonstart
```

- `--terminateonstart` takes an optional `true`/`false`: `--terminateOnStart false` turns it off.

**What it simulates**

- **Semi-finished goods**, each launched as its own production order and run through its fabrication flow
  to `Kanban Final Assembly`, where the lot stays as stock:
  - Metal plates: `RTU-LATERAL C`, `RTU-FRONT C`, `RTU-FRAME C`.
    - Flow: PUNCH (durables, feeders), then BEND (durables), then PLATE COLORING (feeders).
    - The White plates' `COATING`/`PLATE PAINTING` steps are configured but never run, because only Color
      plates are launched.
  - Coils: `ALU-COIL-4ROW`, `COP-COIL-4ROW`.
    - Flow: START COIL (pass-through), then COIL or COPPER BEND (durables, assemble), then INDOOR and OUTDOOR
      BRAZE (assemble), then COMPRESSOR TUB BRAZE (`lotSplitTrackOut`), then COIL TREATMENT (batch).
  - Electric: `VFD-10HP` (MAIN BOARD, DOOR PREP, FILTER PREP and STAGING MB, all assemble).
  - Foam: `FOAM-XR435` (FOAM PREP, then FOAM, which assembles).
- **Finished good** `Rooftop Unit - HVAC RTU`, with `Size=10TON` and `Color=Color` (the characteristics
  `Tools/IndustrialEquipmentSimulator` uses):
  - Assembly steps: BASE ASM, INSTALL OD COIL TUBES, NITROGEN, APPLY FOAM, FINAL WIRING and INSTALL COVERS
    all assemble, consuming the semi-finished stock.
  - Then RTU PACKING (`lotPack`), and SHIPPING WH INDUTECH ships the lot to the warehouse and receives it at
    RTU FINAL PACKAGING (`palletPack`).
  - SHIPPING FINAL CUSTOMER closes the order and ships the lot to `Final Customer` without receiving it.
- **The finished good only starts when its semi-finished goods are in stock** (`Products[].Requires`):
  - Before each finished-good order, the stock at `RequiresAtStep` is read.
  - Lots already running reserve their share of that stock until they finish. This is conservative: the
    reservation is only released when the lot leaves the line.
  - The order is sized to what the stock covers.
- **Semi-finished goods are pulled, not pushed.**
  - Their products have `Weight` 0, so they are only launched when the finished good is short of them: one
    missing product the line makes, chosen at random.
  - A missing product isn't launched again while what is already on its way covers the gap.
  - With `Weight` above 0 they would also be launched on their own. A first run did that and made 624
    semi-finished orders for 19 finished goods.
- **Bought-in parts are replenished** (`Requires[].Replenish`).
  - `Roof 10T HVAC` and `Cover Fan HVAC` aren't made by any flow; they were pre-stocked (30 and 60) and a run
    used them up.
  - When their stock can't cover the largest order, a new lot is created at Kanban Final Assembly, with no
    production order, as if bought: 30 roofs (`Lot`) or 60 fan covers (`Batch`).
  - Without `Replenish`, the finished good waits and logs why once.

**Engine additions (all opt-in; the defaults keep the semi behaviour)**

| Where | What |
|---|---|
| `Order.DiscreteProduct` (also per product) | The lot is created with the order quantity, no wafers and no secondary units, directly at its first step. `WaferFlowPath`/`DiesPerWafer` are then not required. |
| `Products[].Requires`, `RequiresAtStep`, `Requires[].Replenish` | The stock gate, the pull of semi-finished goods, and the bought-in lots described above (`OrderSource.CheckRequirements`, `MaterialGateway.StockAt`). |
| `assemble` action | Mixed/explicit BOM assembly (`AssembleMaterial`): <ul><li>Draws each BOM item from as many source lots as it needs.</li><li>Raw materials (product type `RawMaterial`) that are short are topped up, as the setup wizard would.</li><li>Semi-finished lots are never topped up.</li><li>One assembly runs at a time, with retries on stale data.</li></ul> |
| `lotPack`, `palletPack` actions | Whole-lot packing by quantity, then multi-level packing into pallets. |
| `lotSplitTrackOut` action | Split & track-out by quantity, for lots without sub-materials. |
| `dataCollection` action, `Steps.X.DataCollection` | Posts nominal ±5% readings. It is configured on PLATE PAINTING and PLATE COLORING with the sibling simulator's paint-thickness values. In this MES the lots have no data collection instance there, so the action logs that and does nothing. |
| `Steps.X.FeederProducts` | The feeders action only prepares these products. Example: FINAL WIRING feeds only `Screws`; the rest of its BOM is assembled by hand. |
| `Ship` with `Receive: false` | No next step is needed, so the lot can be shipped to a facility that has no flow. |

**MES behaviours worth remembering (InduTech)**

- **The finished good needs `Size` and `Color`.** They are mandatory product characteristics. Without them,
  order creation fails: "Production Order Characteristic Size not found".
- **Secondary units on a discrete lot.** `SecondaryUnits` equal to `PrimaryUnits` (`Unit-`) is refused, and so
  is an empty unit with a quantity. Leave both null.
- **Flow paths carry the revision.** They are `Final Assembly:A:1/BASE ASM:1`, while the master data shows
  `Final Assembly:1/...`.
- **COMPRESSOR TUB BRAZE only allows split & track-out.** A plain track-out fails with "Missing value for
  mandatory property splitAndTrackOutParameters". The last part stays on the parent lot ("ChildExceptLast").
- **COIL TREATMENT has a minimum batch size.** A batch must be at least 70% of the capacity of 100, so
  `Batch.MinQuantity` is 70.
- **Don't close a semi-finished order while its lots are still stock.**
  - Consuming one of those lots changes the lot's quantity. The MES then tries to change its production order,
    and refuses: "Invalid operation ChangeQuantity on Production Order … SystemState is Closed".
  - So `Kanban Final Assembly` doesn't close orders. Semi-finished orders stay **Completed**, and only the
    finished goods' orders are **Closed**.
- **Quality INSP** is skipped by the MES itself (flow condition `Attributes.InspectionInduQual = true`).
- **Feeder consumables are replaced with "Scrap Wastes".** Kanban Fabrication only accepts its own loss
  reasons: Broken Coil, Broken-Loss, Electric Module broken, Scrap Wastes and Scratch-Loss.
- **Consumable thresholds.** `LowQuantity` must exceed one lot's need. Blue Ink needs up to about 30 per lot.
- **Resource state model.** Resources use the `InduTech State Model`, whose Productive-to-Standby reason is
  "No WIP". The startup's first attempt with "SBY" logs a `fail:` line, then succeeds with "No WIP".
- **Durables on BEND.** Durables can't be changed while another product is in process. The engine tries the
  other resource, and the lot waits.
- **Concurrent assemblies.** Two lots assembling from the same source lot at once get "changed by another
  user". That is why assemblies are serialized.

**Open points**

- **Stock the simulator didn't create.** The stock check and the assemblies also use semi-finished lots that
  were already in the MES (demo data or other simulators), not only the simulator's own.
- **Pre-existing consumables are replaced.** The feeders action terminates low ones and creates new ones; the
  semi line does the same.
- **Semi-finished orders are never closed.** Closing them once all their lots are consumed would need a sweep.
- **Some lots resist `--terminateonstart`.** A few test lots can't be terminated, e.g. those left in process
  by an aborted split. The log reports how many (22 after the first long run).
- **Cleanup takes time.** Cleaning up a long run takes minutes (1,084 lots took about 6), because the Kanban
  lots are terminated one by one.
- **The demo stock was consumed.** The pre-stocked roofs and fan covers, and some semi-finished lots that
  were already at Kanban, were used up during the build runs.

## Conventions

- **Line endings:** files are CRLF (`.gitattributes` `eol=crlf`) with a UTF-8 BOM. Normalize after editing.
- **Commits:** the user commits; don't commit.
- **Live MES runs:** authorized for this work.
  - Don't change or delete MES data that the simulator didn't create in the current task without asking:
    lots, batches, packages, DEE actions, configuration.
  - Demo master data uses the same `PO SC.` prefix (e.g. `PO SC.0001`), so never match orders by the
    prefix alone.
- **Step variations:** when a step behaves differently from similar steps (another service, feeder or
  facility), flag it instead of silently working around it.
- **Other simulators** in `Tools/` (IndustrialEquipmentSimulator, SMTSimulator, OIBSimulator) are the
  reference when in doubt. Examples: `--terminateonstart`, employee check-in, `CloseProductionOrders`,
  `ShipMaterials`.

## Back end (mapped, not simulated)

- **SAWING** (Production BE SC):
  - Wafer Mounting;
  - Laser Grooving, conditional on `Quality = "QA" and CapacityClass = "300mm"`;
  - Wafer Sawing (durables: blades);
  - Die Attach Bank (no resource; detaches the wafers).

  Lots must be **unpacked** before Die Attach Bank: the RemoveSubMaterials error says "Material Package
  Count is greater than zero".
- **Flow #BGA** runs on substrate lots starting at *Substrate Lot Start*, which consume the dies and the
  resin:
  - Assemble: Die Attach (feeders), Die Attach cure (batch, Mermmet ovens), Wire Plasma, Wire Bond,
    Encapsulation, LaserMark (check-in), Ball Attach (cluster, lanes), 3D Inspection, Ball Shear,
    Singulation, SAM Inspect, BI Gate.
  - TEST: Burn-IN (check-in), BI Disposition, Test Gate, Hot Test, Cold Test (check-in).
  - Then Inspection before final destination, followed by REEL PACKING (Reel Packing → Shipping WH BE
    → Final Packaging BE → Shipping Final Customer BE) or MODULE ASSEMBLY BANK, depending on the product
    group.
- **Flow Resin preparation:** Blend → Stabilization → Blend → Fridge Stabilization → Kanban FOL&EOL.
- **Flow Module INVECO 5G:** a separate product fed from MODULE ASSEMBLY BANK.
- **PRINTING (Substrate Printing):** not modelled in the MES; only a `Substrate Bank` flow exists.
