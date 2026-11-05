#!/usr/bin/env python3
"""Analyze ShadowLab exports. Standard library only; total-frame GPU timings."""
import argparse
import csv
import json
import math
import statistics
from pathlib import Path

MODES = ("Hard", "PCF3x3", "PCF5x5", "PCSS")

def read_run(folder):
    conditions = json.loads((folder / "conditions.json").read_text())
    summary = (folder / "summary.txt").read_text()
    if "Complete benchmark: True" not in summary:
        raise ValueError(f"{folder}: incomplete/diagnostic capture")
    with (folder / "samples.csv").open(newline="") as stream:
        rows = list(csv.DictReader(stream))
    values = [float(row["total_gpu_ms"]) for row in rows]
    stamps = [int(row["frame_start_timestamp"]) for row in rows]
    if len(values) != 300 or len(set(stamps)) != 300 or any(s <= 0 for s in stamps):
        raise ValueError(f"{folder}: expected 300 unique positive frame timestamps")
    if any(not math.isfinite(v) or v <= 0 for v in values):
        raise ValueError(f"{folder}: invalid GPU timing")
    if conditions.get("editor") is not False:
        raise ValueError(f"{folder}: final report requires standalone Player")
    if conditions.get("mode") not in MODES:
        raise ValueError(f"{folder}: unknown mode")
    ordered = sorted(values)
    return conditions, statistics.median(values), ordered[math.ceil(.95 * len(values)) - 1]

def report(root):
    folders = sorted(p.parent for p in root.rglob("samples.csv"))
    if not folders:
        raise ValueError("No capture folders found")
    groups = {mode: [] for mode in MODES}
    reference = None
    for folder in folders:
        conditions, median, p95 = read_run(folder)
        fixed = {k: v for k, v in conditions.items() if k != "mode"}
        if reference is None:
            reference = fixed
        elif fixed != reference:
            keys = sorted(k for k in fixed.keys() | reference.keys()
                          if fixed.get(k) != reference.get(k))
            raise ValueError(f"{folder}: conditions differ: {', '.join(keys)}")
        groups[conditions["mode"]].append((median, p95))
    if any(len(runs) < 3 for runs in groups.values()):
        raise ValueError("Need at least three complete captures for EACH mode")
    baseline = statistics.median(m for m, _ in groups["Hard"])
    lines = [
        "# Repeated GPU capture analysis", "",
        "Total-frame GPU time from standalone Player captures. "
        "Differences include the full frame; they are not isolated shader/pass costs.", "",
        "| Mode | Runs | Median of run medians (ms) | Run median range (ms) | "
        "Median of run P95s (ms) | Difference vs Hard (ms) |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for mode, runs in groups.items():
        medians = [m for m, _ in runs]
        value = statistics.median(medians)
        lines.append(f"| {mode} | {len(runs)} | {value:.3f} | "
                     f"{min(medians):.3f}–{max(medians):.3f} | "
                     f"{statistics.median(p for _, p in runs):.3f} | "
                     f"{value-baseline:+.3f} |")
    lines += ["", "Each run is summarized separately; frames are not pooled as independent trials.",
              "Ranges describe observed run variability, not confidence intervals. "
              "Overlapping ranges require caution; this report does not establish statistical significance.",
              "This measures performance, not image correctness or visual quality.",
              "", "## Recorded fixed conditions", "", "```json",
              json.dumps(reference, indent=2), "```", ""]
    return "\n".join(lines)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path, help="Folder containing ONLY repeated captures for one configuration")
    args = parser.parse_args()
    try:
        print(report(args.root))
    except (ValueError, KeyError, OSError) as error:
        parser.exit(1, f"Capture validation failed: {error}\n")
