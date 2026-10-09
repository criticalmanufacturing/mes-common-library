---
name: simulator-from-masterdata
description: Derive an MES lot simulator's configuration (MESSimulator simulationConfiguration.json - route, step actions, batches, shipping, consumables, startup, chaos) and its list of open questions directly from Critical Manufacturing master data files (.xlsx exports plus DEE code and business workflow .xml), without an MES. Use when given a master data folder and asked to build, configure or plan a simulator for a product or flow, or to explain how the master data links steps, services, resources, BOMs and facilities.
---

# Building the simulator from master data

The master data that loads an MES already says almost everything the simulator needs: the route, what each step
consumes, which resources run it, where batches, splits, line flows and shipping happen, and which MES settings will
break a run. This skill turns those files into a draft `simulationConfiguration.json` and a list of **FLAGS** (questions and checks).

Use it with the `build-mes-simulator` skill:
- this skill gives **the configuration and the step plan** (the "Phase 0" of build-mes-simulator, done offline);
- build-mes-simulator gives **the code, the method and the validation**, plus the MES runtime behaviours that master
  data cannot show.

Read [references/master-data-model.md](references/master-data-model.md) for how the sheets link and every derivation
rule. The scripts are in `scripts/`.

## 1. Inputs

1. A master data folder: one or more `.xlsx` workbooks (CMF master data format: one sheet per entity, header in row
   1), plus DEE code (`*.txt`) and business workflows (`*.xml`).
2. The product to simulate and its top production flow. If not given, list the products with
   `ProductType = FinishedGood` and the flows of `Type = Production` and **ask the user**.
3. The lot characteristics (e.g. `Quality=QA`, `Coating=PI`). BOM conditions depend on them. If not given, show the
   values the demo production orders use and **ask the user**.

## 2. Procedure

Run every step. Do not skip the report.

1. **Dump the workbooks** (needs `openpyxl`):
   ```powershell
   python scripts/md_dump.py "<master data folder>" "<scratch>/md"
   ```
2. **Derive the draft simulationConfiguration.json and the FLAGS:**
   ```powershell
   $env:MD_DUMP="<scratch>/md"; $env:MD_SOURCE="<master data folder>"
   python scripts/derive_line.py --product <Product> --flow <TopFlow> --char Name=Value ... --out <scratch>/line.derived.json
   ```
   Add `--compare <existing simulationConfiguration.json>` when a hand-built one exists. Add `--exclude-rework A,B` once the user has
   said which rework flows are not core.
3. **Read the draft and every FLAG.** Group the flags:
   - **questions for the user**: rework flows that are core and their chances, scrap reasons, characteristics,
     uncovered BOM assembly types, DEE actions to disable, the order prefix collision;
   - **live checks**: MES conditions on optional steps, business workflows on route steps;
   - **facts to design for**: shared or single feeders, check-in, batch steps in several sub-flows, services per
     sub-flow, durables or consumables to create at run time.
4. **Write the step plan** the build-mes-simulator skill asks for (its section 4): one row per step with the derived
   kind and actions, and the flags as open questions. Show it to the user before writing code.
5. **Tune what master data only approximates:** process times (demo cycle times are much shorter than a real fab),
   `LowQuantity` of consumables, the launch interval (from the bottleneck: see build-mes-simulator section 8.3).
6. **Build and validate** with the build-mes-simulator skill. Master data is not a substitute for the live run: it
   shows the configuration, not the MES behaviour.

## 3. Rules

1. **Only derive what a rule supports.** When a value is a heuristic (e.g. `compose`) or a placeholder (launch
   interval, chaos chances), say so in the step plan.
2. **Never resolve a FLAG silently.** Each one is a question or a check (build-mes-simulator rule 5: flag step
   variations, never normalize them).
3. **Context tables resolve by specificity**: the row with the most matching keys wins; empty keys are wildcards.
   `LogicalFlowPath` is the FlowItem's `LogicalName`, so one step name can need different services, BOMs and
   reticles per sub-flow. Keep that in mind when you read a sheet by hand.
4. **Master data shows definitions, not runtime state.** Stock, mounted durables and demo lots in the instance
   workbooks may have changed in the MES. The demo lots and orders are not yours: never touch them.
5. **The MES runtime behaviours** (state transitions, retries, packing, shipping, batch release, order lifecycle)
   are not in master data. Use build-mes-simulator section 8.
6. **Keep the user's workbooks read-only.** Write dumps and drafts to a scratch folder.

## 4. Reading a sheet by hand

When a script doesn't cover a question, load the dump in Python:

```python
import os, sys; sys.path.insert(0, "scripts"); os.environ["MD_DUMP"] = "<scratch>/md"
from md_lib import load, by, nonempty
steps = {r["Name"]: r for r in load("DMStep")}
print(nonempty(steps["COAT"]))
```

Write the script to a file and run it; don't pipe it into `python -` (it can hang on Windows).

## 5. Known result

On the KommSemi master data the derived Steps match the hand-built MESSimulator `simulationConfiguration.json` 37/37, and Order
and Startup match field for field. The only exception is `DiesPerWafer`: the master data gives 983, while simulationConfiguration.json
uses the simulator default 500. Details are in the reference, section 5.
