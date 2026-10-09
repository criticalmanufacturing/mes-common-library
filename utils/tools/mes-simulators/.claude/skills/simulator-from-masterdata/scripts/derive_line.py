"""Derive a draft MESSimulator simulationConfiguration.json from a master data dump (see md_dump.py), plus a report of flags.

Usage:
  set MD_DUMP=<dump folder>
  python derive_line.py --product 2EDN7524F --flow Y31_CSP_3L --char Quality=QA --char Coating=PI
                        [--out line.derived.json] [--compare simulationConfiguration.json] [--exclude-rework RWK_PRECLEAN,RWK_WETETCH]

Everything printed under FLAGS is a question for the user or a live check: never resolve it silently.
"""
import argparse, json, os, re
from collections import Counter

from md_lib import MD, by, flow_name, last_step, load

ap = argparse.ArgumentParser()
ap.add_argument("--product", required=True)
ap.add_argument("--flow", required=True, help="top production flow of the product's route")
ap.add_argument("--char", action="append", default=[], help="lot characteristic Name=Value (BOM conditions use them)")
ap.add_argument("--out", default="line.derived.json")
ap.add_argument("--compare", help="an existing simulationConfiguration.json to diff the Steps against")
ap.add_argument("--exclude-rework", default="", help="comma-separated rework flows that are not core")
args = ap.parse_args()

P, TOP = args.product, args.flow
CHARS = dict(c.split("=", 1) for c in args.char)
EXCLUDED_REWORK = {x.strip() for x in args.exclude_rework.split(",") if x.strip()}
FLAGS = []
flag = FLAGS.append

steps = {r["Name"]: r for r in load("DMStep")}
flows = {r["Name"]: r for r in load("DMFlow")}
items = by(load("FlowItems"), "Flow")
products = {r["Name"]: r for r in load("DMProduct")}
resources = {r["Name"]: r for r in load("DMResource")}
ressvc = by(load("ResourceService"), "Service")
subres = by(load("DMSubResource"), "SourceEntity")
svcctx = load("STServiceContext")
bomctx = load("STBOMContext")
durctx = load("STMaterialDurablesContext")
bomprod = by(load("BOMProducts"), "BOM")
areas = {r["Name"]: r for r in load("DMArea")}
facilities = {r["Name"]: r for r in load("DMFacility")}
capacity = load("STResourceCapacity")
linectx = load("STStepLineFlowContext")
splitctx = {r["Step"] for r in load("STStepSplitTrackOutContext")}
cycle = load("LOOKUPStepPRODYCTContext")
rework = [r for r in load("ReworkPaths") if r["Flow"] == TOP]
rwk_limits = load("LOOKUPReasonRwkLimit")
step_reasons = by(load("StepReason"), "Step")
reasons = {r["Name"]: r for r in load("DMReason")}
future = load("DMFutureAction")
rules = {r["Name"]: r for r in load("DMRule")}
dees = {r["Action"]: r for r in load("SMDEEAction")}
lookups = load("SMLookupTableValues")
tconstraints = load("STTimeConstraintsContext")
employees = load("DMEmployee")
orders = load("DMProductionOrder")
order_chars = by(load("LOOKUPProductionOrderChar"), "ProductionOrder")
prod_chars = [r for r in load("DMProductCharacteristic") if r["Product"] == P]
mats = load("DMMaterial")
mounted = load("DMResourceDurable")

prod = products[P]
group = prod["ProductGroup"]


def ctx_match(row, step, logical):
    """A context row applies when each filled key matches (empty keys are wildcards)."""
    return (not row.get("Step") or row["Step"] == step) and \
           (not row.get("LogicalFlowPath") or row["LogicalFlowPath"] == logical) and \
           (not row.get("Product") or row["Product"] == P) and \
           (not row.get("ProductGroup") or row["ProductGroup"] == group)


def best(rows, step, logical):
    """Matching context rows, most specific first (the MES resolves smart tables the same way)."""
    keys = ("Step", "LogicalFlowPath", "Product", "ProductGroup", "Resource", "Flow")
    return sorted((r for r in rows if ctx_match(r, step, logical)), key=lambda r: -sum(1 for k in keys if r.get(k)))


def cond_ok(expr):
    """BOM item condition like 'Coating = "PI"  and Quality = "QA"', evaluated on the lot characteristics."""
    return all(CHARS.get(n) == v for n, v in re.findall(r'(\w+)\s*=\s*"([^"]*)"', expr or ""))


def facility(step):
    return areas.get(steps[step]["Areas"], {}).get("Facility")


def rev(flow):
    return flows.get(flow, {}).get("Revision") or "A"


# ---- 1. walk the route: main flow, then rework flows, then line flows -------------------------------------------
route, seen = [], set()


def walk(flow, path, kind):
    if flow in seen:
        return
    seen.add(flow)
    for it in sorted(items.get(flow, []), key=lambda r: int(r["Position"] or 0)):
        if it["Type"] == "Flow":
            sub = flow_name(it["Target"])
            walk(sub, f"{path}/{sub}:{rev(sub)}:{it['Position']}", kind)
        else:
            route.append({"step": it["Target"], "logical": it["LogicalName"] or it["Target"], "flow": flow,
                          "item": it, "kind": kind, "path": f"{path}/{it['Target']}:{it['Position']}"})


walk(TOP, f"{TOP}:{rev(TOP)}:1", "main")
main = [r for r in route if r["kind"] == "main"]
rework_flows = sorted({flow_name(r["GotoFlow"]) for r in rework})
for rf in rework_flows:
    if rf not in EXCLUDED_REWORK:
        walk(rf, f"{rf}:{rev(rf)}:1", "rework")
line_steps = set()
for r in list(route):
    if r["item"]["IsLine"] == "Yes":
        for lf in best(linectx, r["step"], r["logical"]):
            line_steps |= {it["Target"] for it in items.get(lf["LineFlow"], [])}

# end of the simulated line: the step that marks product completion
end_idx = next((i for i, r in enumerate(main) if steps[r["step"]]["MarksProductCompletion"] == "Yes"), len(main) - 1)


def resources_for(step, logical):
    ctx = best(svcctx, step, logical)
    if not ctx:
        return None, []
    svc = ctx[0]["Service"]
    return svc, [resources[x["Resource"]] for x in ressvc.get(svc, []) if x["IsEnabled"] == "Yes" and x["Resource"] in resources]


# ---- 2. one simulationConfiguration.json entry per step name ---------------------------------------------------------------------
derived, per_logical = {}, {}
for i, r in enumerate(route):
    s, logical, st = r["step"], r["logical"], steps[r["step"]]
    if s in line_steps:
        continue                                   # run inside the line step by the lineFlow action
    if r["kind"] == "main" and i == 0 and st["IsPassThrough"] == "Yes":
        continue                                   # the lot is created here and started at the next step
    if r["kind"] == "main" and i > end_idx:
        continue                                   # after the end of the simulated line
    d, actions = {}, []
    svc, res = resources_for(s, logical)
    per_logical.setdefault(s, []).append((logical, svc, [x["Name"] for x in res]))

    if st["IsPassThrough"] == "Yes":
        d["PassThrough"] = True
        if r["kind"] == "main" and i == end_idx:
            d["ClosesProductionOrder"] = True
            nxt = main[i + 1]["step"] if i + 1 < len(main) else None
            if nxt and facility(nxt) != facility(s):
                d.update(Ship=True, ShipTo=facility(nxt), Receive=False)
        derived.setdefault(s, d)
        continue
    if not res:
        flag(f"{s} ({logical}): no service/resource resolves for {P}. Leave it out of simulationConfiguration.json if the MES skips it "
             f"(condition: {r['item']['ConditionExpression'] or 'none'}); otherwise ask the user.")
        continue

    if all(x["ProcessingMode"] == "Batch" for x in res):
        cap = [int(c["Capacity"]) for c in capacity if c["Model"] == res[0]["Model"] and c["CapacityClass"] == prod["CapacityClass"]]
        usage = float(res[0]["MinimumBatchCapacityUsage"] or 0)
        d["Batch"] = {"MinQuantity": round(usage * cap[0]) if cap else None, "MaxQuantity": cap[0] if cap else None,
                      "CheckIntervalSeconds": 600}
        if not cap:
            flag(f"{s}: batch resource model {res[0]['Model']} has no ResourceCapacity for {prod['CapacityClass']}")

    if svc and "compose" in svc.lower():
        actions.append("compose")
    for b in best(bomctx, s, logical):
        here = [p for p in bomprod.get(b["BOM"], []) if p["AssemblyStep"] == s
                and (not p["AssemblyLogicalFlowPath"] or p["AssemblyLogicalFlowPath"] in (logical, s)) and cond_ok(p["Condition"])]
        if b["AssemblyType"] == "Packing":
            actions += ["feeders", "pack"]
            break
        if here and b["AssemblyType"] in ("AutomaticAtTrackIn", "AutomaticAtTrackOut"):
            actions.append("feeders")
            break
        if here:
            flag(f"{s}: BOM {b['BOM']} uses AssemblyType {b['AssemblyType']} for {[p['SourceProduct'] for p in here]}; "
                 f"no simulator action covers it. Ask whether it is core.")
            break
    if "feeders" not in actions and any(b["Step"] == s and b["Resource"] for b in bomctx):
        actions.append("feeders")                  # one BOM per resource (e.g. METAL WET ETCH)
    for b in best(durctx, s, logical):
        if any(p["AssemblyStep"] == s for p in bomprod.get(b["BOM"], [])):
            actions.append("durables")
            break
    if r["item"]["IsLine"] == "Yes" or any(x["ProcessingType"] == "Line" for x in res):
        lf = best(linectx, s, logical)
        chambers = {it["Target"] for it in items.get(lf[0]["LineFlow"], [])} if lf else set()
        if any(b["Step"] in chambers for b in bomctx):
            actions.append("feeders")
        actions.append("lineFlow")
    elif st["SubMaterialTrackStateDepth"] == "1" and any(x["IsSubMaterialTrackingEnabled"] == "Yes" for x in res):
        actions.append("subMaterialTracking")
    if st["UseSplitAndTrackout"] == "Yes" or s in splitctx:
        actions.append("splitTrackOut")
    if r["kind"] == "main" and i + 1 < len(main) and facility(main[i + 1]["step"]) != facility(s):
        actions.append("ship")
    if all(x["MaxConcurrentMaterialsInProcess"] == "1" for x in res):
        d["SingleMaterial"] = True

    ct = best(cycle, s, logical)
    if ct:
        scale = {"Seconds": 1, "Minutes": 60, "Hours": 3600}[ct[0]["CycleTimeScale"] or "Minutes"]
        fixed, var = float(ct[0]["FixedCycleTime"] or 0) * scale, float(ct[0]["VariableCycleTime"] or 0) * scale
        d.update(MinSeconds=round(fixed * 0.8), MaxSeconds=round(fixed * 1.2) or 1)
        if var:
            d.update(MinSecondsPerWafer=round(var * 0.8), MaxSecondsPerWafer=round(var * 1.2))
    if actions:
        d["Actions"] = list(dict.fromkeys(actions))
    prev = derived.get(s)
    if prev and prev.get("Actions", []) != d.get("Actions", []):
        flag(f"{s}: needs different actions per sub-flow ({prev.get('Actions')} vs {d.get('Actions')} in {logical}); "
             f"simulationConfiguration.json keys by step name, so the union is used. Confirm with the user.")
        d["Actions"] = list(dict.fromkeys(prev.get("Actions", []) + d.get("Actions", [])))
    derived[s] = {**(prev or {}), **d}

    # resource-side facts worth flagging
    if any(x["EnableCheckIn"] == "Yes" for x in res):
        flag(f"{s}: operator check-in required on {[x['Name'] for x in res if x['EnableCheckIn'] == 'Yes']}")
    if r["item"]["ConditionExpression"] or r["item"]["ConditionRule"]:
        flag(f"{s} ({logical}): MES condition '{r['item']['ConditionExpression'] or r['item']['ConditionRule']}'. "
             f"Check live that the MES skips the step; add a simulator Condition only if it doesn't.")
    for x in res:
        feeders = [resources[f["TargetEntity"]] for f in subres.get(x["Name"], []) if resources.get(f["TargetEntity"], {}).get("Type") == "Feeder"]
        shared = [f["Name"] for f in feeders if f["IsSharedConsumableFeed"] == "Yes"]
        if shared and "feeders" in actions:
            flag(f"{s}: {x['Name']} has shared feeders {shared}: a swap must wait until nothing else is in process")

# one feeder position but different BOM products per sub-flow: the feeder must be swapped between lots
consumed_by_step = {}
for r in route:
    for b in best(bomctx, r["step"], r["logical"])[:1]:
        consumed_by_step.setdefault(r["step"], set()).update(
            p["SourceProduct"] for p in bomprod.get(b["BOM"], []) if p["AssemblyStep"] == r["step"]
            and (not p["AssemblyLogicalFlowPath"] or p["AssemblyLogicalFlowPath"] in (r["logical"], r["step"])) and cond_ok(p["Condition"]))
for s, prods in consumed_by_step.items():
    for x in resources_for(s, s)[1] or [res for l, sv, rs in per_logical.get(s, []) for res in (resources[n] for n in rs)]:
        if x["ConsumableFeedPositions"] and len(prods) > int(x["ConsumableFeedPositions"]):
            flag(f"{s}: {x['Name']} has {x['ConsumableFeedPositions']} feeder position(s) for {sorted(prods)}: the feeder is "
                 f"swapped between lots of different sub-flows, and only when nothing else is in process")

# business workflows (exported .xml) that act on route steps, e.g. an automatic ship
for root, _, files in os.walk(os.environ.get("MD_SOURCE", ".")):
    for fn in files:
        if fn.endswith(".xml"):
            raw = open(os.path.join(root, fn), encoding="utf-8-sig", errors="replace").read()
            for s in route_steps if "route_steps" in dir() else {r["step"] for r in route}:
                if f"Step.Name == \\&quot;{s}\\&quot;" in raw or f'Step.Name == &quot;{s}&quot;' in raw.replace("\\", ""):
                    flag(f"Business workflow '{fn[:-4]}' acts when a lot reaches {s}: check it doesn't duplicate a simulator action")

# same step name with different services/resources per sub-flow
for s, uses in per_logical.items():
    if len({u[1] for u in uses}) > 1:
        flag(f"{s}: service differs per sub-flow: " + "; ".join(f"{l} -> {sv} on {rs}" for l, sv, rs in uses))
    if len(uses) > 1 and derived.get(s, {}).get("Batch"):
        flag(f"{s}: batch step in several sub-flows: one batch can mix them; move next per group of the same next path")

# ---- 3. Order, Startup, Consumables, Chaos --------------------------------------------------------------------
first = main[0]
wafers = [m for m in mats if m["Product"] == P and m["Form"] == "Wafer" and last_step(m["FlowPath"]).startswith("Kanban")]
wafer_flow = flow_name(wafers[0]["FlowPath"]) if wafers else None
po_names = [o["Name"] for o in orders if o["Product"] == P]
prefix = Counter(n.rsplit(".", 1)[0] for n in po_names).most_common(1)[0][0] if po_names else "PO SIM"
if po_names:
    flag(f"Order: demo orders of {P} already use the prefix '{prefix}' ({len(po_names)} orders, e.g. {po_names[0]}). "
         f"ProductionOrderNameFormat only matches '<prefix>.<8 hex>', but a distinct prefix is safer.")
if not CHARS and prod_chars:
    flag(f"Order: {P} has characteristics {[c['Name'] for c in prod_chars]} that BOM conditions use; pass --char. "
         f"Values used on its demo orders: {Counter((c['Name'], c['Value']) for n in po_names for c in order_chars.get(n, [])).most_common(6)}")
order = {
    "Product": P,
    "Facility": facility(first["step"]),
    "LotFlowPath": first["path"],
    "WaferFlowPath": f"{wafer_flow}:{rev(wafer_flow)}:1/{wafer_flow}:1" if wafer_flow else None,
    "StartFlowPath": main[1]["path"],
    "ProductionOrderNameFormat": prefix + ".{id}",
    "MinLotQuantity": int(prod["MinimumMaterialSize"] or 1),
    "MaxLotQuantity": int(prod["MaximumMaterialSize"] or 25),
    "DiesPerWafer": int(float(wafers[0]["PrimaryQuantity"])) if wafers and wafers[0]["PrimaryUnits"] == "Dies" else
                    int(float(wafers[0]["SecondaryQuantity"])) if wafers and wafers[0]["SecondaryQuantity"] else 500,
    "LaunchIntervalSeconds": 4800,
    "MaxOrders": 0,
    "Characteristics": [{"Name": k, "Value": v} for k, v in CHARS.items()],
}
flag("Order: LaunchIntervalSeconds is a placeholder: set it from the bottleneck resource (see the skill, cadence).")

route_steps = {r["step"] for r in route}
on_route_rules = {r["item"][k] for r in route for k in ("OnEnterRule", "OnExitRule") if r["item"][k]} | \
                 {p["OnReworkRule"] for p in rework if p["OnReworkRule"]}
notif_types = {v["Lookup Table Value"] for v in lookups if v.get("LookUp Table") == "NotificationType"}
dee_off = []
for rn in sorted(on_route_rules):
    dee = rules.get(rn, {}).get("DEERule")
    code_file = dees.get(dee, {}).get("CodeFileRelativePath")
    code = ""
    for root, _, files in os.walk(os.environ.get("MD_SOURCE", ".")):
        if code_file in files:
            code = open(os.path.join(root, code_file), encoding="utf-8-sig", errors="replace").read()
    bad = [t for t in re.findall(r'\.Type\s*=\s*"([^"]+)"', code) if notif_types and t not in notif_types]
    if bad:
        dee_off.append(dee)
        flag(f"Startup: rule '{rn}' runs DEE {dee}, which creates notifications of type {bad}; NotificationType only has "
             f"{sorted(notif_types)}, so it will fail. Ask the user before disabling it.")
    elif dee:
        flag(f"Startup: rule '{rn}' runs DEE {dee} on the route; check it live (code {'found' if code else 'not found: set MD_SOURCE'}).")
mail = [f for f in future if f["Action"] == "SendMail" and f["Step"] in route_steps]
terminate = [n for n, r in reasons.items() if r["ReasonType"] == "Loss" and n.lower() in ("terminate", "full loss")]
calendar = facilities.get(order["Facility"], {}).get("DefaultCalendar")
cost_center = Counter(e["CostCenter"] for e in employees).most_common(1)[0][0] if employees else "Generic"
startup = {
    "DisableMail": bool(mail),
    "DisableDeeActions": dee_off,
    "TerminatePreviousRuns": False,
    "TerminateReason": terminate[0] if terminate else None,
    "CheckInOperator": any(x["EnableCheckIn"] == "Yes" for s in route_steps for x in resources_for(s, s)[1]) or True,
    "Operator": {"Calendar": calendar, "Type": "Standard", "CostCenter": cost_center},
    "DisableTimeConstraints": any(t["FromStep"] in route_steps or t["ToStep"] in route_steps for t in tconstraints),
}
if mail:
    flag(f"Startup: SendMail future actions on {sorted({m['Step'] for m in mail})} (they break move-next): DisableMail.")

consumed = {}
for b in {b["BOM"] for b in bomctx if b["Step"] in route_steps or b["Step"] in line_steps}:
    for p in bomprod.get(b, []):
        if p["AssemblyStep"] in route_steps | line_steps and cond_ok(p["Condition"]):
            consumed.setdefault(p["SourceProduct"], set()).add(p["Step"])
cons_rules = {}
for cp, sources in sorted(consumed.items()):
    stock = [m for m in mats if m["Product"] == cp and m["Type"] == "Raw Material"]
    sizes = Counter(float(m["PrimaryQuantity"]) for m in stock if m["PrimaryQuantity"]).most_common(1)
    at_kanban = [m["Name"] for m in stock if last_step(m["FlowPath"]) in sources]
    if sizes:
        size = sizes[0][0]
        cons_rules[cp] = {"LowQuantity": round(size * 0.1, 4), "ReplacementQuantity": size}
    if not at_kanban:
        flag(f"Consumables: no {cp} in its source location {sorted(sources)}; the feeders action must create one "
             f"modelled on a dispatchable material.")

chaos_flows = {}
for p in rework:
    f = flow_name(p["GotoFlow"])
    chaos_flows.setdefault(f, set()).add((p["SourceStep"], p["ReworkReason"], p["ApplicableToQueued"], p["ApplicableToProcessed"]))
for f, uses in chaos_flows.items():
    q = sorted({u[0] for u in uses if u[2] == "Yes"})
    pr = sorted({u[0] for u in uses if u[3] == "Yes"})
    lim = [x["ReworkLimit"] for x in rwk_limits if x["Reason"] in {u[1] for u in uses} and x["Product"] in ("", P) and x["ProductGroup"] in ("", group)]
    flag(f"Chaos: {f}{' (EXCLUDED)' if f in EXCLUDED_REWORK else ''}: reasons {sorted({u[1] for u in uses})}; "
         f"offered when queued at {q or 'none'}, when processed at {pr or 'none'}; reason limits {lim or 'none'}")
# loss reasons accepted by most route steps (the simulator still picks, per step, only reasons that step accepts)
loss_sets = [{x["Reason"] for x in step_reasons.get(s, []) if x["ApplicableToRecordLoss"] == "Yes"} for s in route_steps if s not in line_steps]
loss_count = Counter(x for ls in loss_sets for x in ls)
common_loss = sorted(x for x, n in loss_count.items() if n >= 0.8 * len(loss_sets) and x != startup["TerminateReason"])
flag(f"Chaos: ScrapReasons = loss reasons accepted by at least 80% of route steps: {common_loss}. Ask the user which to use.")
if order["DiesPerWafer"] != 500:
    flag(f"Order: DiesPerWafer {order['DiesPerWafer']} comes from the wafer stock of {P} (500 is the simulator default).")
chaos = {"Seed": None, "MaxReworksPerLot": 2,
         "ReworkFlows": {f: 0.0 for f in sorted(chaos_flows) if f not in EXCLUDED_REWORK},
         "ScrapProbability": 0.1, "MaxScrapFraction": 0.2, "ScrapReasons": common_loss}
flag("Chaos: rework chances are 0 (placeholders). Ask the user which flows are core and the target rates; "
     "step yields from StepPRODYCTContext can guide scrap.")

# durables: reticles not mounted anywhere must be mounted by the durables action
for b in {b["BOM"] for b in durctx if ctx_match(b, b["Step"], b["LogicalFlowPath"] or b["Step"])}:
    for p in bomprod.get(b, []):
        mats_p = {m["Name"] for m in mats if m["Product"] == p["SourceProduct"]}
        if mats_p and not any(m["Durable"] in mats_p for m in mounted):
            flag(f"Durables: no {p['SourceProduct']} mounted on any resource ({len(mats_p)} in stock): mounted at run time.")

failed_lots = {"Terminate": True, "TerminateReason": startup["TerminateReason"]}
line = {"Line": {"Order": order, "Chaos": chaos, "Startup": startup, "FailedLots": failed_lots,
                 "Consumables": {"Default": {"LowQuantity": 2000, "ReplacementQuantity": 60000, "TerminateReason": startup["TerminateReason"]},
                                 "Products": cons_rules},
                 "ResourceOccupancy": {"WaitBetweenChecks": "00:00:30", "MaxChecks": 10},
                 "Steps": derived}}
with open(args.out, "w", encoding="utf-8") as o:
    json.dump(line, o, indent=2)
print(f"wrote {args.out}: {len(derived)} steps")

if args.compare:
    real = json.load(open(args.compare, encoding="utf-8-sig"))["Line"]["Steps"]
    real_ci = {k.lower(): v for k, v in real.items()}
    keys = ("Actions", "PassThrough", "Ship", "ShipTo", "Receive", "ClosesProductionOrder", "SingleMaterial")
    norm = lambda d: {**{k: d[k] for k in keys if d.get(k) not in (None, [], False)},
                      **({"Batch": {k: d["Batch"][k] for k in ("MinQuantity", "MaxQuantity")}} if d.get("Batch") else {})}
    same = 0
    print("\nCOMPARE (structure only, not times)")
    for s, d in derived.items():
        rv = real_ci.get(s.lower())
        ok = rv is not None and norm(d) == norm(rv)
        same += ok
        if not ok:
            print(f"  {s}: derived {norm(d)} | simulationConfiguration.json {norm(rv) if rv is not None else 'absent'}")
    for k in real:
        if k.lower() not in {s.lower() for s in derived}:
            print(f"  {k}: only in simulationConfiguration.json")
    print(f"  identical: {same}/{len(derived)}")

print("\nFLAGS")
for f in dict.fromkeys(FLAGS):
    print("  -", f)
