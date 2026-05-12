"""Merge 7 Gemini batch JSON files into a single 1666-cell catalog.

Policy:
- Gemini batches are authoritative for content cells they cover.
- Missing coordinates are auto-filled with `unknown` defaults.
- Missing fields in Gemini entries (spriteName/stateGroupId/stateRole) are
  patched (spriteName recomputed, others default to null).
- enumName uniqueness enforced (1666 must all be unique).
- Conflicts with the existing catalog (docs/art/modern-ui-style2-icon-catalog.json)
  are reported but Gemini wins.
"""
from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[3]  # repo root (file is in docs/art/style2-gemini-batches/)
BATCH_DIR = ROOT / "docs" / "art" / "style2-gemini-batches"
EXISTING_CATALOG = ROOT / "docs" / "art" / "modern-ui-style2-icon-catalog.json"
OUTPUT_PATH = ROOT / "docs" / "art" / "modern-ui-style2-icon-catalog-gemini-merged.json"
OUTPUT_LEAN_PATH = ROOT / "docs" / "art" / "modern-ui-style2-icon-catalog-gemini-lean.json"
CONFLICT_REPORT = ROOT / "docs" / "art" / "modern-ui-style2-icon-catalog-gemini-conflicts.md"

ROWS = 34
COLS = 49
TOTAL = ROWS * COLS  # 1666

BATCH_FILES = [
    "batch-01-r0-r5.json",
    "batch-02-r6-r10.json",
    "batch-03-r11-r15.json",
    "batch-04-r16-r20.json",
    "batch-05-r21-r25.json",
    "batch-06-r26-r30.json",
    "batch-07-r31-r33.json",
]

REQUIRED_FIELDS = [
    "row", "column", "coordinate", "spriteName", "semanticId",
    "enumName", "category", "stateGroupId", "stateRole",
    "confidence", "note",
]


def strip_trailing_comment(text: str) -> str:
    """Remove `// CONTINUE: ...` trailing comments after the closing ]."""
    return re.sub(r"\]\s*//[^\n]*$", "]", text.strip())


def load_batch(path: Path) -> list[dict[str, Any]]:
    raw = path.read_text(encoding="utf-8")
    cleaned = strip_trailing_comment(raw)
    return json.loads(cleaned)


def make_unknown(row: int, col: int) -> dict[str, Any]:
    return {
        "row": row,
        "column": col,
        "coordinate": f"r{row}_c{col}",
        "spriteName": f"ModernUI_16_Style2_r{row}_c{col}",
        "semanticId": f"unknown.r{row}.c{col}",
        "enumName": f"UnknownR{row}C{col}",
        "category": "unknown",
        "stateGroupId": None,
        "stateRole": None,
        "confidence": "unknown",
        "note": "Auto-filled empty cell (not emitted by Gemini).",
    }


def patch_entry(entry: dict[str, Any]) -> dict[str, Any]:
    """Ensure all 11 fields present; recompute derived fields when missing."""
    row = entry["row"]
    col = entry["column"]
    entry.setdefault("coordinate", f"r{row}_c{col}")
    entry.setdefault("spriteName", f"ModernUI_16_Style2_r{row}_c{col}")
    entry.setdefault("stateGroupId", None)
    entry.setdefault("stateRole", None)
    entry.setdefault("note", "")
    return {k: entry.get(k) for k in REQUIRED_FIELDS}


def main() -> None:
    coords: dict[tuple[int, int], dict[str, Any]] = {}
    batch_stats = []
    for fname in BATCH_FILES:
        path = BATCH_DIR / fname
        items = load_batch(path)
        kept = 0
        for raw in items:
            entry = patch_entry(raw)
            key = (entry["row"], entry["column"])
            if key in coords:
                print(f"[WARN] duplicate coordinate {entry['coordinate']} in {fname}; keeping first")
                continue
            coords[key] = entry
            kept += 1
        batch_stats.append((fname, len(items), kept))

    print(f"\nBatch summary:")
    for fname, total, kept in batch_stats:
        print(f"  {fname:32s} loaded={total:4d} kept={kept:4d}")
    print(f"  Gemini content cells:  {len(coords)}")

    # Fill missing coordinates
    filled = 0
    for row in range(ROWS):
        for col in range(COLS):
            if (row, col) not in coords:
                coords[(row, col)] = make_unknown(row, col)
                filled += 1
    print(f"  Auto-filled unknowns:  {filled}")
    print(f"  Total entries:         {len(coords)} (expected {TOTAL})")
    assert len(coords) == TOTAL, "Coordinate count mismatch"

    # enumName uniqueness check
    enum_seen: dict[str, list[str]] = {}
    for (r, c), entry in coords.items():
        enum_seen.setdefault(entry["enumName"], []).append(entry["coordinate"])
    dupes = {k: v for k, v in enum_seen.items() if len(v) > 1}
    if dupes:
        print(f"\n[WARN] enumName duplicates ({len(dupes)} sets):")
        for name, locs in list(dupes.items())[:10]:
            print(f"  {name}: {locs}")
        # Auto-suffix duplicates
        for name, locs in dupes.items():
            for i, coord in enumerate(locs[1:], start=1):
                row_col = coord  # r{R}_c{C}
                m = re.match(r"r(\d+)_c(\d+)", row_col)
                r, c = int(m.group(1)), int(m.group(2))
                new_name = f"{name}_Dup{i}R{r}C{c}"
                coords[(r, c)]["enumName"] = new_name
                print(f"  [PATCH] {coord}: {name} -> {new_name}")

    # Conflict report against existing catalog
    conflicts = []
    if EXISTING_CATALOG.exists():
        existing = json.loads(EXISTING_CATALOG.read_text(encoding="utf-8"))
        for ex_entry in existing.get("entries", []):
            r, c = ex_entry["row"], ex_entry["column"]
            new_entry = coords[(r, c)]
            ex_id = ex_entry.get("semanticId")
            new_id = new_entry["semanticId"]
            ex_conf = ex_entry.get("confidence", "unknown")
            new_conf = new_entry["confidence"]
            if ex_id != new_id and ex_conf != "unknown":
                conflicts.append({
                    "coord": new_entry["coordinate"],
                    "existing": ex_id,
                    "existing_conf": ex_conf,
                    "gemini": new_id,
                    "gemini_conf": new_conf,
                    "gemini_note": new_entry["note"],
                })
    print(f"\n  Conflicts vs existing: {len(conflicts)}")

    # Write merged catalog
    confidence_counts = {"confirmed": 0, "probable": 0, "unknown": 0}
    for entry in coords.values():
        confidence_counts[entry["confidence"]] = confidence_counts.get(entry["confidence"], 0) + 1

    sorted_entries = [coords[(r, c)] for r in range(ROWS) for c in range(COLS)]
    output = {
        "sourceAsset": "Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png",
        "sourceAddress": "sprites/ui/modern/16/style-2",
        "cellSize": 16,
        "rows": ROWS,
        "columns": COLS,
        "spriteCount": TOTAL,
        "generator": "Gemini Nano Banana (7 batches) + auto-fill unknowns",
        "policy": "Gemini wins on conflicts; missing cells auto-filled as unknown.",
        "confidenceCounts": confidence_counts,
        "entries": sorted_entries,
    }
    OUTPUT_PATH.write_text(
        json.dumps(output, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"\nWrote {OUTPUT_PATH}")
    print(f"  confidence: {confidence_counts}")

    # Lean output — drop unknown/empty cells. C# enum/Addressables consumers
    # don't need 1243 placeholder entries; they just won't be addressable.
    lean_entries = [e for e in sorted_entries if e["category"] != "unknown"]
    lean_output = {
        "sourceAsset": output["sourceAsset"],
        "sourceAddress": output["sourceAddress"],
        "cellSize": output["cellSize"],
        "rows": ROWS,
        "columns": COLS,
        "spriteCount": TOTAL,
        "generator": output["generator"] + " — lean (unknown cells excluded)",
        "policy": "Lean version: only cells with identified content are included. "
                  "Empty/transparent cells are intentionally omitted so they will "
                  "not be loaded by Addressables / referenced by C# enums.",
        "confidenceCounts": {
            "confirmed": sum(1 for e in lean_entries if e["confidence"] == "confirmed"),
            "probable": sum(1 for e in lean_entries if e["confidence"] == "probable"),
        },
        "entries": lean_entries,
    }
    OUTPUT_LEAN_PATH.write_text(
        json.dumps(lean_output, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"Wrote {OUTPUT_LEAN_PATH}")
    print(f"  lean entries: {len(lean_entries)} (dropped {TOTAL - len(lean_entries)} unknown cells)")

    # Write conflict report
    lines = ["# Style2 Catalog — Gemini vs Existing Conflicts", ""]
    lines.append(f"Generated by `docs/art/style2-gemini-batches/merge.py`.")
    lines.append(f"Policy: **Gemini wins**. Existing labels listed for reference.")
    lines.append("")
    lines.append(f"Total conflicts: **{len(conflicts)}**")
    lines.append("")
    if conflicts:
        lines.append("| Coord | Existing | Existing Conf | Gemini | Gemini Conf | Gemini Note |")
        lines.append("|---|---|---|---|---|---|")
        for c in conflicts:
            note = c["gemini_note"].replace("|", "\\|")
            lines.append(
                f"| {c['coord']} | `{c['existing']}` | {c['existing_conf']} | "
                f"`{c['gemini']}` | {c['gemini_conf']} | {note} |"
            )
    CONFLICT_REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {CONFLICT_REPORT}")


if __name__ == "__main__":
    main()
