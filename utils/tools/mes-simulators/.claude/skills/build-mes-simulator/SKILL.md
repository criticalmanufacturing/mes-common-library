---
name: build-mes-simulator
description: How to build a Critical Manufacturing MES lot simulator from a Core-features specification (a markdown describing the flow and what each step needs) and a live integration MES to run against. The simulator creates production orders and lots and drives them step by step through the MES flow over the LightBusinessObjects (LBO) API until the orders close. Use when asked to build, extend or debug such a simulator, e.g. MESSimulator.
---

# Building an MES lot simulator

You will build a .NET console app that drives lots through a real Critical Manufacturing MES flow, the
way an operator would in the MES UI. This skill gives you the goal, the rules, the structure, the method
and the validation process. **Follow the phases in order. Do not skip validation.**

This method was used to build MESSimulator (`Tools/MESSimulator` in the cm-demo-repos workspace). Its
names are used below as examples. If that code is available to you, read it as a reference
implementation; if not, everything you need is in this skill and in
[references/code-recipes.md](references/code-recipes.md).

## 1. What you receive

1. **The Core specification**, a markdown file. It describes:
   - the MES flow (sub-flows and steps, in order);
   - for each step, the **Core** features it needs: consumption (feeders), durables, batches, split,
     sub-material tracking, line flows, packing, shipping, conditional steps;
   - often also Engineering and Quality columns (recipes, data collection, SPC, sampling...). **Those are
     out of scope** unless the user says otherwise.
2. **An integration MES** you can run against: an environment address, a client id and a personal access
   token. It already holds the master data: products, flows, steps, resources, BOMs, feeders, durables.

The spec says **what** each step needs. The MES says **how**: which resources, which services, which
feeders. When they disagree, the MES wins, and you tell the user.

## 2. The goal and the definition of done

The simulator must:

1. Create and release production orders, lots and wafers in the MES.
2. Move every lot through the flow: dispatch, track-in, setup, process time, track-out, move-next, plus
   each step's Core features from the spec.
3. **Close production orders from beginning to end.** This is the definition of done. A production order
   that reaches `Closed` proves that every step its lots passed through works.
4. Run at a real-fab pace at `--speed 1`, and much faster for tests (`--speed 3600` = 1 simulated hour per
   real second).
5. Later, if asked: chaos (rework and scrap) through the MES's own rework paths.

## 3. Hard rules

Read these again before every change.

1. **Let the MES route the lot.** Never hardcode the step order, not even from the spec. Read the lot's
   current step from the MES, run it, and let the MES's move-next choose the next step. Conditional steps,
   sub-flow choices and rework returns then need no code.
2. **Configure steps by MES step name** in a line file (`simulationConfiguration.json`). The key is the last segment of the
   lot's FlowPath without the `:N` suffix (`.../PHOTO FUSE:A:1/COAT:3` → `COAT`).
3. **New behaviour = new action class**, referenced by key from the line file. No
   `if (stepName == "...")` in the engine.
4. **The MES has the final say.** Do not copy MES rules into the simulator. Try the call and handle the
   error. Only mirror a rule the MES enforces anyway (example: a 70% batch minimum).
5. **Flag differences; never normalize them.** When a step differs from the spec, or from similar steps
   (another service, feeder, facility or state model, a split instead of a track-out, an action before
   instead of after track-in), **stop and tell the user**: what you found, what you expected, why it may
   matter. Keep the current behaviour until the user answers.
6. **Only touch MES data your simulator created.** Never change or delete other lots, orders, batches,
   packages, DEE actions or configuration without asking. Demo master data may use the same name prefix
   as your orders, so never select data by a name prefix alone.
7. **Never print, log or commit the access token**, or `.har` captures (they hold bearer tokens).
8. **Keep a build journal** (section 9) and update it in the same turn you learn something.
9. **Do not commit.** The user commits.

## 4. Phase 0: turn the spec into a step plan

Do this before you write any code.

**If you have the master data files** (the `.xlsx` exports that loaded the MES), use the
`simulator-from-masterdata` skill first: it derives this step plan, a draft `simulationConfiguration.json` and the list of mismatches
and questions, without the MES. Then continue at step 4 below.

1. Read the spec. Make a table with one row per step, in flow order:

   | Sub-flow | Step (MES name) | Core features (from the spec) | Kind | Open questions |
   |---|---|---|---|---|
   | WAFER PREP | SILICON WAFER LAMINATOR | consumption (tape) | plain + `feeders` | — |
   | — | PRE_CURE | batch | batch | min/max quantity? |
   | PHOTO FUSE | ASHING PRE | split & track-out | `splitTrackOut` | — |

   Kinds: plain, batch, pass-through, line step, ship (next step in another facility), conditional.
2. **Map every Core feature to an action** with the table in section 6.
3. **Check the plan against the MES** with probes (section 7.3): does the step exist with that name? What
   resources does it have? What BOM, feeders and durables? Is it a batch or line step?
4. **Write down every mismatch** between the spec and the MES, and every Engineering/Quality item you will
   skip. Show the table and the mismatches to the user and ask about anything unclear. Do not guess.

## 5. Phase 1: the skeleton

Build the project (recipe 0 in the code recipes): .NET 8 console app, Generic Host,
`Cmf.LightBusinessObjects`, configuration in `appsettings.json` (connection) and `simulationConfiguration.json` (the line).

```
Program → SimulationHost (BackgroundService)
  LineStartup       disable what blocks the flow (mail, failing DEE actions, time constraints);
                    create the operator employee; set resources to Standby
  OrderSource       every LaunchIntervalSeconds: create + RELEASE a production order, its lot and wafers
                    → start a LotFlow for the lot
  LotFlow           loop: read the lot's current step → look it up in simulationConfiguration.json →
                      batch step       → BatchQueues / BatchProcessor
                      pass-through     → move next (or close the order and ship at the end)
                      condition false  → move next
                      not in simulationConfiguration.json → STOP and log "stopped at <step>"
                      otherwise        → StepExecutor
  StepExecutor      pick resource → operator check-in → BeforeTrackIn actions → dispatch & track-in
                    → AfterTrackIn actions → process time → track-out & move next (or a TrackOut action)
  Mes/*Gateway      the only code that calls LBO; every call goes through MesCall (retry + timing)
```

| Folder | Holds |
|---|---|
| `Mes/` | Gateways (Materials, Resources, Batches, Setup, Labor, MasterData) and `MesCall`. |
| `Steps/` | `StepExecutor`, `StepDefinition` with `IStepAction`/`IStepCondition`, resource selection and locks, operator check-in, order completion. |
| `Steps/Actions/` | One class per action. |
| `Pipeline/` | `LotFlow`, `OrderSource`, batch classes, startup, host. |
| `Line/` | `simulationConfiguration.json` options, the line definition, and a validator that rejects unknown action keys at startup. |
| `Tests/<Name>.UnitTests` | MES-free unit tests with hand-written fakes. |

Step hooks (when an action runs):

| Hook | Runs | Examples |
|---|---|---|
| `BeforeTrackIn` | Before dispatch and track-in. Throws `ResourceUnsuitableException` to try the next resource. | `feeders`, `durables` |
| `AfterTrackIn` | After track-in, before the process time. | `compose`, `subMaterialTracking`, `lineFlow`, `pack` |
| `TrackOut` | Instead of the standard track-out and move-next. | `splitTrackOut`, `ship` |

Resource selection: candidates come from the MES (`GetResourcesForStep`, cached); keep those offering
the lot's required service; pick in random order; on `ResourceUnsuitableException` try the next one; on
"maximum number of concurrent materials in process" wait and retry.

Time: every wait goes through one `SimulationClock` that divides simulated seconds by `--speed`. Polling
waits have a **1 s real-time floor**, so a high speed doesn't flood the MES. All random values come from one
injectable random source, so a seed makes a run repeatable. No `Thread.Sleep`, no `new Random()`.

## 6. Core feature → implementation

Use this table to implement each Core feature of the spec. Services are LBO service names.

| Core feature | Action / config | MES services | Watch out for |
|---|---|---|---|
| Plain step | none | `ComplexDispatchAndTrackInMaterials`, `ComplexTrackOutAndMoveMaterialsToNextStep` | Resource state transitions: only ask for one from the state the resource is in. Set resources to Standby at startup. |
| Lot start / compose wafers | `compose` (AfterTrackIn) | `CreateObject` (lot, wafers), `ComposeMaterial` | Create the wafers on their own flow path first. |
| Consumption (feeders) | `feeders` (BeforeTrackIn) | `GetDataForPerformSetupWizard`, `EvaluateBOMProductsCondition`, `GetDispatchListForResource`, `ManageResourceConsumableFeeds` | Do what the Perform Setup wizard does. `CloneObject` on a material fails: create a new consumable modelled on one in a dispatchable location. A shared feeder can't be swapped while another lot is in process: unsuitable, temporary. "Changed by another user": reload and start over. |
| Durables | `durables` (BeforeTrackIn) | `GetDataToManageDurables`, `ManageResourceMaterialDurables` | Another lot can swap the durable between your mount and your track-in: hold the resource from before the mount until the track-in. |
| One lot at a time | `SingleMaterial` option | `AbortMaterialsProcess` (leftovers only) | Never abort a lot your own run put there. |
| Batch | `Batch { MinQuantity, MaxQuantity, CheckIntervalSeconds }` | `BatchExecute` (CreateBatch, TrackInBatch, TrackOutBatch), `ReleaseBatches`, `CancelBatches` | A batch that fails to release still holds its lots: cancel it. A batch can mix lots from different sub-flows: move next per group of lots with the same next flow path. |
| Split & track-out | `splitTrackOut` (TrackOut) | `ComplexTrackOutMaterials` (split parameters per wafer group), then `GetDataForMultipleMoveNextWizard` + `ComplexMoveMaterialsToNextStep` | Each split lot continues as its own LotFlow. |
| Sub-material tracking | `subMaterialTracking` (AfterTrackIn) | `ComplexTrackInMaterials`, `ComplexTrackOutMaterials` | If the resource has Process sub-resources (chambers), track the wafers onto one of them. |
| Line flow (lanes, chambers) | `lineFlow` (AfterTrackIn) | `LoadResourceLanesFromResource`, `ResolveStepLineFlowForMaterial`, `GetMaterialLineData`, `ComplexTrackInMaterials` with a `Resource` | A lane is required at track-in. Chambers come from `GetMaterialLineData` → `LineStepsData[].LineResourcesData`. Make it resumable. |
| Operator check-in | built into the executor | `ClockInEmployees`, `ManageResourceEmployees` | Every check-in/out changes the employee: reload it each time. The employee for the API user may need creating. |
| Conditional step | usually nothing | — | The MES skips the step through its flow. Add a condition only if the MES doesn't, and **ask the user for the rule**. |
| Ship to another facility | `ship` (TrackOut) or `PassThrough` + `Ship` | `GetDataForMultipleMoveNextWizard`, `ShipMaterials`, `ReceiveMaterials` | Get the receive FlowPath **before** shipping: a lot in transit has none. |
| Packing | `pack` (AfterTrackIn) | `GetPackingInformation`, `PackMaterial` | `CreatePackages` does **not** count as packing: use `PackMaterial`. Pack only live wafers (scrapped ones are still offered). |
| End of the line | `PassThrough` + `ClosesProductionOrder` | `ReleaseProductionOrders`, `CloseProductionOrders` | See section 8.2. |
| Rework (chaos) | before each step, random | `GetPossibleReworkPathsForMaterial`, `ComplexReworkMaterial` | Only from steps where the MES offers a path. Allow-list the rework flows that count as core. |
| Scrap (chaos) | after a rework, random | `RecordSubMaterialsLoss` | Use only loss reasons the step defines (its `StepReason` relations). |

## 7. Phase 2: walk the flow one step at a time

Never configure many unknown steps at once.

### 7.1 Order of the work

Each phase must build, run and pass tests before you start the next.

1. Skeleton + order creation + the **first plain step**.
2. The rest of the plain steps, then setup steps (feeders, durables, operator check-in).
3. Special steps: batches, splits, sub-material tracking, line flows.
4. The order lifecycle: packing, shipping, the end of the line, release → complete → close.
5. Cadence: realistic process times, launch rate (section 8.3).
6. Chaos, only if asked.

### 7.2 The loop

1. **Run** a few orders (`MaxOrders` 2–5) until lots log "stopped at <step>".
2. **Find that step in your step plan.** If it isn't in the plan, the MES routed the lot somewhere the spec
   didn't say: tell the user.
3. **Probe the step** in the MES: resources, services, BOM, feeders, durables, batch or line step,
   operator check-in.
4. **Compare** with the spec and with similar steps. A difference → rule 5: ask.
5. **Configure** the step in `simulationConfiguration.json`. Reuse an existing action when it fits; write a new one only if
   none does.
6. **Drive the stopped lots on** with the `diag` tool (7.3), instead of starting new orders.
7. **Fix each error** (7.4) and write it in the journal.
8. **Repeat** until lots reach the end of the line.

### 7.3 Build two helper tools first

Make them early: they save hours. Both are small console apps that reference the simulator project.

- **`probe`:** read-only MES queries, chosen by the first argument. Examples: the flow tree with the
  resources of each step, a resource's lanes/chambers/feeders/durables, a lot's BOM, the next flow path, the
  rework paths of a lot, the loss reasons of a step, a production order's quantities and state. Add a
  query each time you need to look at something. Mark clearly any probe that changes data.
- **`diag`:** runs `LineStartup` and then `LotFlow` on lot names given as arguments. It resumes a lot left
  in process, moves on a processed lot, and runs waiting batches. Use it to drive stopped lots without new
  orders.

### 7.4 When something fails

Use this order:

1. Look the message up in your journal's errors table, then in section 8.1.
2. Probe the state of the lot and the resource. Most errors are a state you didn't expect.
3. Find a working call in another simulator in the workspace, if one exists (in cm-demo-repos:
   IndustrialEquipmentSimulator, SMTSimulator, OIBSimulator).
4. Only then change the engine. Keep it small; add a unit test if the logic is MES-free.

### 7.5 Finding the right MES service

1. Search other simulators for a call that does the same thing.
2. Search the LBO assembly with reflection (recipe 4 in the code recipes) for `*Input` types.
3. Ask the user for a `.har` capture of the MES UI doing the action: it shows the exact input. Never
   commit it.
4. Try the call in `probe` on one object your simulator created before you add it to the simulator.

## 8. MES behaviours to know before you start

These were learned the hard way. Expect them on any similar MES.

### 8.1 Errors and fixes

| Symptom | Cause | Fix |
|---|---|---|
| Move-next fails: "The data for the From was not found" | Mail future actions | Disable mail at startup |
| A DEE action fails (e.g. on notification type "General") | Notification DEEs on Enter/Exit step | Disable those DEE actions at startup, **after asking the user** |
| Time constraints block the flow | Defined on the flow, out of scope | Disable time constraints at startup |
| Track-in: invalid resource state transition | Asked for a transition from another state | Read the resource's state model and current state |
| "maximum number of concurrent materials in process" | Resource full | Wait and retry |
| Check-in fails / employee changed | The resource needs an operator; check-ins change the employee | Check in; reload the employee each time |
| No feeder takes a product the BOM needs | That resource can't run this BOM | Unsuitable → next resource (and tell the user: it may be a master-data gap) |
| "has been changed by another user" while preparing | Another lot changed the resource | Reload and start the preparation again |
| "has changed since last viewed", deadlocks | MES concurrency | Retry the same call (in `MesCall`) |
| Not enough quantity of a durable at track-in | Another lot swapped it after your mount | Hold the resource from mount to track-in |
| Lots refused with `InvalidBatchMaterialAlreadyInUse` | A failed batch still holds them | Cancel a batch whose release fails |
| After a batch: "Step X not found in Material Flow" | One move-next for lots of different sub-flows | Move next per group with the same next path |
| Batch refused below a minimum | MES batch minimum | `MinQuantity` = that minimum; wait for more lots |
| Can't track out of packing after `CreatePackages` | It doesn't count as packing | `PackMaterial` |
| `ReceiveMaterials` needs a FlowPath | Not known once in transit | Read it before `ShipMaterials` |
| "Close… SystemState is not Completed" | The order was never released | Release at creation |
| "Loss Reason X is not defined in Step Y" | Each step has its own loss reasons | Use only the step's reasons |
| Track-in refused: "…Resource is configured to only accept Materials in-process with the same BOM" | Several products on the line; the resource verifies the BOM | Wait and retry like for a full resource |
| Lots wait forever for a resource ("full") or a durable change is refused, though the simulator has nothing there | **Lots left in process by killed runs** (stopping the process leaves them in process) occupy the resource's slots, BOM and setup | Probe the in-process materials per resource before a live run; stop runs with Ctrl+C; after a test, abort and terminate the run's own orders (never other orders: ask the user) |
| "Material is not Processed" when moving a lot past a conditional step | A queued lot can't be moved next | `SkipMaterialsProcess`, then move next |
| "The Resource X does not allow Product mixes" | Lots of another product are in process there | Wait and retry like for a full resource |
| Track-in: "no Material of Product X found in the Consumable Feeds" | Another lot swapped a shared feeder after this lot's setup (a set-up lot isn't "in process" yet) | Register set-up lots (`SetUpLots`) so the swap check counts them; set up again as a fallback |
| "The object <durable> is not Queued" / duplicate key `UC_ResourceDurable` | Two lots picked the same queued durable, or one pick listed the same durable twice (a BOM with two wheels) | Pick a durable not already picked, retry on "not Queued" |
| "The data for object X of type Resource has changed since last viewed" survives the automatic retries | The retried call reuses a resource object read once before it | Re-read the resource inside the retried section (`WithFreshResource`) |
| Terminate: "Step X must have Loss Reasons defined" / "…Resource Job has dependencies… (Batch)" | The step doesn't accept the reason; a leftover batch holds the lot | Use a reason the step accepts (or move the lot to a step that has some); cancel the batch |
| "HttpClient.Timeout of 60 seconds elapsing" at startup | MES or VPN unreachable | Not a code bug: check the address and rerun |

### 8.2 Production orders

- States: Created → Released → InProgress → Completed → Closed. **Release every order right after
  creating it**: a `Created` order is never completed.
- The MES counts completion itself: when lots pass the step that marks product completion, it adds their
  wafers to `CompletedQuantity`, and moves the order to `Completed` when the quantity is reached.
- `CloseProductionOrders` works only from `Completed`. There is no "complete order" service.
- An order that lost wafers (scrap) never reaches its quantity. When nothing is in progress any more
  (`InProgressQuantity` 0), lower its quantity to the completed quantity
  (`ChangeProductionOrdersProductAndQuantity`); the MES then completes it, and you close it. **Ask the user
  before you choose this.**

### 8.3 Cadence

- `--speed N` divides every simulated time: process times, launch interval, batch checks, retry waits.
- Process time per lot = a per-lot range + a per-wafer range × wafers. Use typical industry values.
- Set the launch interval from the **bottleneck** resource: estimate its tool time per order and keep it
  under ~80% busy. Faster launches make queues grow without limit.
- Retry budgets must cover hours of simulated time **and** a minimum of real time (e.g. 15 min): at high
  speed the MES calls, not the simulated times, set the pace.
- Batch steps wait for their minimum quantity. Orders whose lots end in a final batch below the minimum
  wait for more lots. That is expected, not a bug.

## 9. Validation

A change is done only when **all** these pass, in order. When you report, say which ones you ran and what
you saw. Never call a change "working" if you only built it.

1. **Build:** `dotnet build`. A running simulator locks its exe: build into a scratch folder
   (`dotnet build -o <scratch>/build`).
2. **Unit tests:** `dotnet test Tests/<Name>.UnitTests`. Include one test that builds the real
   dependency-injection graph from the real `simulationConfiguration.json`: it catches unknown action keys and invalid config
   without the MES.
3. **Live run:** build into a scratch folder, copy `simulationConfiguration.json` there, set `Order:MaxOrders` (e.g. 20), run
   `dotnet <Name>.dll --speed 3600 > e2e.log`.
4. **Read the log** with a line-buffered filter:
   ```
   grep --line-buffered -E "Closed production order|quantity lowered|Rework:|Scrap:|refused|fail:|crit:|Unhandled|stopped at|batch run failed|Invalid configuration" e2e.log
   ```
5. **Check the MES:** production orders reach `Closed`. In a 20-order run most should close by themselves.
   An open order is acceptable only if its lots wait for a final batch below the minimum. Any other open
   order is a bug: find its lot and the step where it stopped.
6. **Stop the run safely:** stop only the `dotnet` process whose command line contains your dll name.
   Check the command line first. Lots in flight stay where they are; `diag` resumes them.

## 10. The build journal

Keep a `README.md` next to the code. It is the memory of the project for the next session or model.
Update it in the same turn you learn something, and write only what you actually observed:

| Section | Content |
|---|---|
| Scope | What is simulated and what is skipped (from the spec), and the user's decisions. |
| Running | Commands, flags, configuration, tests. |
| Architecture | The diagram, folders, actions table, `simulationConfiguration.json` options. |
| The flow | One row per step: what the simulator does and the MES facts behind it. |
| MES behaviours worth remembering | Facts like those in section 8. |
| Errors met while building it | Symptom → cause → fix, one row per error. |
| How it was built | The method, validation, probes, shell tricks. |

## 11. When to stop and ask the user

- The spec and the MES disagree, or a step differs from similar steps.
- A spec feature is unclear, or looks like Engineering or Quality scope.
- A fix would change or delete MES data your simulator didn't create (including disabling DEE actions).
- The MES offers several valid choices and the demo intent decides (which rework flows are core, whether
  to lower an order's quantity or leave it open).
- An error isn't in the journal or in section 8, and two fix attempts failed.

## 12. Pitfalls on Windows

- Keep the files' line endings and encoding when you edit (in cm-demo-repos: CRLF and a UTF-8 BOM). In
  Python, read with `encoding='utf-8-sig'` and write with `newline='\r\n'`.
- Don't use `python - <<'EOF'` heredocs: they can hang. Write the script to a file and run it.
- A log filter must flush each line (`grep --line-buffered`). Don't pipe it into `head` or `cut`.
