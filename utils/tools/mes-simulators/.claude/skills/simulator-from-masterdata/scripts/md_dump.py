"""Dump every non-empty sheet of the CMF master data workbooks to TSV files.

Usage: python md_dump.py <master data folder> <output folder>

Each sheet becomes <workbook>__<sheet>.tsv (sheet name without <..> prefixes, e.g. "<DM>Step" -> "DMStep").
Row 1 is the header. Tabs and newlines inside cells are replaced. Needs openpyxl.
"""
import glob, os, re, sys

import openpyxl

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
# Sheets that are only workbook metadata or GUI configuration
SKIP = {"Cover", "Index", "Formats", "Assumptions", "WorksheetNameMapping", "Enums", "<SM>Features",
        "<GT>GUIElementDefaultValue", "<ST>KeyboardShortcut", "<LOOKUP>LayoutPContext"}

for f in sorted(glob.glob(os.path.join(src, "**", "*.xlsx"), recursive=True)):
    tag = os.path.splitext(os.path.basename(f))[0]
    wb = openpyxl.load_workbook(f, read_only=True, data_only=True)
    for ws in wb.worksheets:
        if ws.title in SKIP:
            continue
        rows = [r for r in ws.iter_rows(values_only=True) if any(c is not None for c in r)]
        if len(rows) <= 1:
            continue
        name = re.sub(r"[<>]", "", ws.title)
        with open(os.path.join(out, f"{tag}__{name}.tsv"), "w", encoding="utf-8") as o:
            for r in rows:
                cells = list(r)
                while cells and cells[-1] is None:
                    cells.pop()
                o.write("\t".join("" if c is None else str(c).replace("\t", " ").replace("\n", "\\n") for c in cells) + "\n")
        print(f"{tag}\t{ws.title}\t{len(rows) - 1} rows")
