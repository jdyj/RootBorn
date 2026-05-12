"""Generate ROOTBORN Modern UI Style2 catalog artifacts.

Inputs:
- docs/art/modern-ui-style2-icon-catalog-gemini-lean.json
- docs/art/modern-ui-style2-icon-catalog-gemini-merged.json

Outputs:
- patched lean JSON in place
- docs/art/modern-ui-style2-icon-catalog.json
- docs/art/modern-ui-style2-icon-catalog.md
- Builds/Generated/Style2/ModernUiStyle2Icon.cs
- Builds/Generated/Style2/ModernUiStyle2IconCatalog.cs

The generated C# files are staged outside Assets on purpose. Apply them to
Assets/Scripts/Game/Common via Unity MCP script-update-or-create.
"""

from __future__ import annotations

import json
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
LEAN_PATH = ROOT / "docs/art/modern-ui-style2-icon-catalog-gemini-lean.json"
MERGED_PATH = ROOT / "docs/art/modern-ui-style2-icon-catalog-gemini-merged.json"
CATALOG_JSON_PATH = ROOT / "docs/art/modern-ui-style2-icon-catalog.json"
CATALOG_MD_PATH = ROOT / "docs/art/modern-ui-style2-icon-catalog.md"
GENERATED_DIR = ROOT / "Builds/Generated/Style2"

PANEL_COMMON = {
    "r2_c0": ("panel.common.topLeft", "PanelCommonTopLeft", "panel", None, None, "confirmed", "ROOTBORN CommonPanel top-left tile per repository UI standard."),
    "r2_c1": ("panel.common.top", "PanelCommonTop", "panel", None, None, "confirmed", "ROOTBORN CommonPanel top edge tile per repository UI standard."),
    "r2_c2": ("panel.common.topRight", "PanelCommonTopRight", "panel", None, None, "confirmed", "ROOTBORN CommonPanel top-right tile per repository UI standard."),
    "r3_c0": ("panel.common.left", "PanelCommonLeft", "panel", None, None, "confirmed", "ROOTBORN CommonPanel left edge tile per repository UI standard."),
    "r3_c1": ("panel.common.fill", "PanelCommonFill", "panel", None, None, "confirmed", "ROOTBORN CommonPanel repeated fill tile per repository UI standard."),
    "r3_c2": ("panel.common.right", "PanelCommonRight", "panel", None, None, "confirmed", "ROOTBORN CommonPanel right edge tile per repository UI standard."),
    "r4_c0": ("panel.common.bottomLeft", "PanelCommonBottomLeft", "panel", None, None, "confirmed", "ROOTBORN CommonPanel bottom-left tile per repository UI standard."),
    "r4_c1": ("panel.common.bottom", "PanelCommonBottom", "panel", None, None, "confirmed", "ROOTBORN CommonPanel bottom edge tile per repository UI standard."),
    "r4_c2": ("panel.common.bottomRight", "PanelCommonBottomRight", "panel", None, None, "confirmed", "ROOTBORN CommonPanel bottom-right tile per repository UI standard."),
}

BUTTON_FIXES = {
    "r3_c40": ("button.plus.normal", "ButtonPlusNormal", "button", "button.plus.state", "normal", "confirmed", "Brightest plus button state."),
    "r3_c41": ("button.plus.hover", "ButtonPlusHover", "button", "button.plus.state", "hover", "confirmed", "Intermediate plus button state."),
    "r3_c42": ("button.plus.pressed", "ButtonPlusPressed", "button", "button.plus.state", "pressed", "confirmed", "Darkest plus button state."),
    "r3_c43": ("button.minus.normal", "ButtonMinusNormal", "button", "button.minus.state", "normal", "confirmed", "Brightest minus button state."),
    "r3_c44": ("button.minus.hover", "ButtonMinusHover", "button", "button.minus.state", "hover", "confirmed", "Intermediate minus button state."),
    "r3_c45": ("button.minus.pressed", "ButtonMinusPressed", "button", "button.minus.state", "pressed", "confirmed", "Darkest minus button state."),
}

FURNITURE_FIXES = {
    "r1_c13": ("item.furnitureChair", "FurnitureChair", "furniture", None, None, "probable", "Visual audit shows a chair-like furniture icon."),
    "r1_c14": ("item.furnitureBed", "FurnitureBed", "furniture", None, None, "probable", "Visual audit shows a bed-like furniture icon."),
}

PATCHES = {**PANEL_COMMON, **BUTTON_FIXES, **FURNITURE_FIXES}


def coordinate(row: int, column: int) -> str:
    return f"r{row}_c{column}"


def sprite_name(row: int, column: int) -> str:
    return f"ModernUI_16_Style2_{coordinate(row, column)}"


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def entry_for(row: int, column: int, patch: tuple[str, str, str, str | None, str | None, str, str]) -> dict:
    semantic_id, enum_name, category, state_group_id, state_role, confidence, note = patch
    return {
        "row": row,
        "column": column,
        "coordinate": coordinate(row, column),
        "spriteName": sprite_name(row, column),
        "semanticId": semantic_id,
        "enumName": enum_name,
        "category": category,
        "stateGroupId": state_group_id,
        "stateRole": state_role,
        "confidence": confidence,
        "note": note,
    }


def apply_patches(data: dict, include_unknowns: bool) -> dict:
    entries_by_coordinate = {}
    for source_entry in data["entries"]:
        entry = dict(source_entry)
        entry["coordinate"] = coordinate(entry["row"], entry["column"])
        entry["spriteName"] = sprite_name(entry["row"], entry["column"])
        entries_by_coordinate[entry["coordinate"]] = entry
    for coord, patch in PATCHES.items():
        row = int(coord.split("_c", 1)[0][1:])
        column = int(coord.split("_c", 1)[1])
        entries_by_coordinate[coord] = entry_for(row, column, patch)

    entries = sorted(
        entries_by_coordinate.values(),
        key=lambda entry: (entry["row"], entry["column"]),
    )
    if not include_unknowns:
        entries = [entry for entry in entries if entry["category"] != "unknown" and entry["confidence"] != "unknown"]

    result = dict(data)
    result["entries"] = entries
    counts = Counter(entry["confidence"] for entry in entries)
    result["confidenceCounts"] = {key: counts.get(key, 0) for key in ("confirmed", "probable", "unknown")}
    if include_unknowns:
        result["spriteCount"] = len(entries)
    return result


def generate_markdown(data: dict) -> str:
    lines = [
        "# Modern UI Style2 Icon Catalog",
        "",
        "Generated from Gemini lean catalog plus ROOTBORN visual audit corrections.",
        "",
        "Unknown policy: full-grid JSON keeps empty cells as `unknown.r{row}.c{column}`; C# excludes unknown entries.",
        "",
        "| coordinate | spriteName | semanticId | enumName | category | stateGroupId | stateRole | confidence | note |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    for entry in data["entries"]:
        lines.append(
            "| {coordinate} | {spriteName} | {semanticId} | {enumName} | {category} | {stateGroupId} | {stateRole} | {confidence} | {note} |".format(
                coordinate=entry["coordinate"],
                spriteName=entry["spriteName"],
                semanticId=entry["semanticId"],
                enumName=entry["enumName"],
                category=entry["category"],
                stateGroupId=entry["stateGroupId"] or "",
                stateRole=entry["stateRole"] or "",
                confidence=entry["confidence"],
                note=str(entry["note"]).replace("|", "/"),
            )
        )
    return "\n".join(lines) + "\n"


def cs_string(value: str | None) -> str:
    if value is None:
        return "null"
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def generate_icon_enum(entries: list[dict]) -> str:
    names = [entry["enumName"] for entry in entries]
    body = "\n".join(f"        {name}," for name in names)
    return f"""namespace Rootborn.Game.Common
{{
    /// <summary>
    /// Generated from lean JSON; do not edit by hand.
    /// Source: docs/art/modern-ui-style2-icon-catalog-gemini-lean.json
    /// UI asset keys only. These are not gameplay entity ids.
    /// </summary>
    public enum ModernUiStyle2Icon
    {{
{body}
    }}
}}
"""


def generate_catalog(entries: list[dict]) -> str:
    rows = []
    for entry in entries:
        rows.append(
            "            EntryFor(ModernUiStyle2Icon.{enumName}, {row}, {column}, {semanticId}, {category}, {confidence}, {stateGroupId}, {stateRole}),".format(
                enumName=entry["enumName"],
                row=entry["row"],
                column=entry["column"],
                semanticId=cs_string(entry["semanticId"]),
                category=cs_string(entry["category"]),
                confidence=cs_string(entry["confidence"]),
                stateGroupId=cs_string(entry["stateGroupId"]),
                stateRole=cs_string(entry["stateRole"]),
            )
        )
    body = "\n".join(rows)
    return f"""using System;
using System.Collections.Generic;

namespace Rootborn.Game.Common
{{
    /// <summary>
    /// Generated from lean JSON; do not edit by hand.
    /// Source: docs/art/modern-ui-style2-icon-catalog-gemini-lean.json
    /// </summary>
    public static class ModernUiStyle2IconCatalog
    {{
        private const string Prefix = "ModernUI_16_Style2_";

        public static readonly IReadOnlyList<Entry> Entries = new[]
        {{
{body}
        }};

        public static ModernUiSpriteKey GetSpriteKey(ModernUiStyle2Icon icon)
        {{
            foreach (Entry entry in Entries)
            {{
                if (entry.Icon == icon)
                {{
                    return entry.SpriteKey;
                }}
            }}

            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown Modern UI Style2 icon.");
        }}

        private static Entry EntryFor(ModernUiStyle2Icon icon, int row, int column, string semanticId, string category, string confidence, string stateGroupId, string stateRole)
        {{
            string coord = "r" + row + "_c" + column;
            string sprite = Prefix + coord;
            return new Entry(icon, row, column, coord, sprite, semanticId, category, stateGroupId, stateRole, confidence);
        }}

        public readonly struct Entry
        {{
            public Entry(ModernUiStyle2Icon icon, int row, int column, string coordinate, string spriteName, string semanticId, string category, string stateGroupId, string stateRole, string confidence)
            {{
                Icon = icon;
                Row = row;
                Column = column;
                Coordinate = coordinate;
                SpriteName = spriteName;
                SemanticId = semanticId;
                Category = category;
                StateGroupId = stateGroupId;
                StateRole = stateRole;
                Confidence = confidence;
                SpriteKey = new ModernUiSpriteKey(ModernUiStyle2Sprites.SheetAddress, spriteName, row, column, semanticId);
            }}

            public ModernUiStyle2Icon Icon {{ get; }}
            public int Row {{ get; }}
            public int Column {{ get; }}
            public string Coordinate {{ get; }}
            public string SpriteName {{ get; }}
            public string SemanticId {{ get; }}
            public string Category {{ get; }}
            public string StateGroupId {{ get; }}
            public string StateRole {{ get; }}
            public string Confidence {{ get; }}
            public ModernUiSpriteKey SpriteKey {{ get; }}
        }}
    }}
}}
"""


def validate_unique_enum(entries: list[dict]) -> None:
    counts = Counter(entry["enumName"] for entry in entries)
    duplicates = sorted(name for name, count in counts.items() if count > 1)
    if duplicates:
        raise ValueError("Duplicate enum names: " + ", ".join(duplicates))


def main() -> None:
    lean = apply_patches(read_json(LEAN_PATH), include_unknowns=False)
    merged = apply_patches(read_json(MERGED_PATH), include_unknowns=True)
    validate_unique_enum(lean["entries"])

    write_json(LEAN_PATH, lean)
    write_json(CATALOG_JSON_PATH, merged)
    CATALOG_MD_PATH.write_text(generate_markdown(merged), encoding="utf-8")

    GENERATED_DIR.mkdir(parents=True, exist_ok=True)
    (GENERATED_DIR / "ModernUiStyle2Icon.cs").write_text(generate_icon_enum(lean["entries"]), encoding="utf-8")
    (GENERATED_DIR / "ModernUiStyle2IconCatalog.cs").write_text(generate_catalog(lean["entries"]), encoding="utf-8")

    print(f"lean entries: {len(lean['entries'])}")
    print(f"merged entries: {len(merged['entries'])}")
    print(f"generated: {GENERATED_DIR}")


if __name__ == "__main__":
    main()
