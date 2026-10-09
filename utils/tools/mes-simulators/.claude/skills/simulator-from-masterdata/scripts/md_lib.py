"""Load the TSV dump written by md_dump.py.

load("DMStep") returns the rows of that sheet from every workbook that has it, as dicts keyed by header.
Each row also carries "_file" (the workbook it came from).
"""
import glob, os
from collections import defaultdict

MD = os.environ.get("MD_DUMP", "md")


def load(sheet):
    rows = []
    for path in sorted(glob.glob(os.path.join(MD, f"*__{sheet}.tsv"))):
        tag = os.path.basename(path).split("__")[0]
        with open(path, encoding="utf-8") as f:
            lines = f.read().split("\n")
        header = lines[0].split("\t")
        for line in lines[1:]:
            if not line.strip():
                continue
            cells = line.split("\t")
            cells += [""] * (len(header) - len(cells))
            row = dict(zip(header, cells))
            row["_file"] = tag
            rows.append(row)
    return rows


def by(rows, key):
    d = defaultdict(list)
    for r in rows:
        d[r[key]].append(r)
    return d


def nonempty(r):
    return {k: v for k, v in r.items() if v not in ("", None) and not k.startswith("_")}


def flow_name(ref):
    """'PHOTO FUSE [A]' or 'PHOTO FUSE:A:1' -> 'PHOTO FUSE'."""
    return ref.split(" [")[0].split(":")[0]


def last_step(flow_path):
    """'Y31_CSP_3L:1/PRE_CURE:2' -> 'PRE_CURE'."""
    return flow_path.split("/")[-1].rsplit(":", 1)[0] if flow_path else ""
