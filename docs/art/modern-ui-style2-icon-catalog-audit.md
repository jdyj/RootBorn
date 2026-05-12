# Modern UI Style2 Icon Catalog Audit

Date: 2026-05-11

## Objective

Build a semantic catalog for `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png` so Style2 UI sprites can be referenced by meaning instead of raw row/column strings. The catalog must preserve all 34x49 sliced coordinates, classify confirmed/probable icons, keep unknown entries explicit, model multi-frame button states, and expose a C# UI-only enum/catalog without becoming a gameplay entity enum.

## Prompt-To-Artifact Checklist

| Requirement | Evidence | Status |
|---|---|---|
| Directly inspect Style2 sheet | `docs/art/modern-ui-style2-contact-sheet-4x.png` generated from the source sheet with row/column grid overlay | Done |
| Full JSON catalog | `docs/art/modern-ui-style2-icon-catalog.json` | Done |
| Human-readable catalog | `docs/art/modern-ui-style2-icon-catalog.md` | Done |
| 34 rows, 49 columns, 1666 sprites | JSON metadata: rows=34, columns=49, spriteCount=1666, entries=1666 | Done |
| Unknown entries preserved | Unknown entries use `unknown.r{row}.c{column}` / `UnknownR{row}C{column}` | Done |
| Representative icon semantics | JSON/Markdown include panel, ribbon, slot, button, glyph, direction, cursor, toggle, status, item, and furniture categories | Done |
| `r1_c13` / `r1_c14` examples | `r1_c13=furniture.chair`, `r1_c14=furniture.bed`, both probable | Done |
| `r3_c42/r3_c43/r3_c44` state group | `button.icon.heightFrame` with `frame0`, `frame1`, `frame2` | Done |
| C# UI-only enum | `Assets/Scripts/Game/Common/ModernUiStyle2Icon.cs` includes a comment that it is not a gameplay entity id enum | Done |
| C# resolver/catalog | `Assets/Scripts/Game/Common/ModernUiStyle2IconCatalog.cs` maps enum keys to `ModernUiSpriteKey` | Done |
| TDD RED | New test first produced expected missing-type compile failure for `ModernUiStyle2Icon` / `ModernUiStyle2IconCatalog` | Done |
| TDD GREEN | Fresh `ModernUiStyle2IconCatalogTests` run passed: 5/5 selected tests, 0 failed | Done |
| Existing Style2 recipe regression | Fresh `ModernUiStyle2RecipeTests` run passed while unrelated generated StudentDay test files were temporarily quarantined: 7/7 selected tests, 0 failed | Done |
| Runtime source audit regression | Unity MCP test call timed out after the recipe/icon verification; equivalent source assertions were re-run directly against the same files and passed | Done |
| Entity ID branching gate | `Scripts/ci/check-no-entity-id-branching.sh` passed with `OK: no entity-id branching in system code.` | Done |
| Final focused Unity test run | Fresh focused Style2 icon catalog and recipe Unity tests passed after temporary quarantine of unrelated generated StudentDay test files | Done |
| Full project Unity test run | Blocked by unrelated untracked StudentDay test files that reference missing day-progress/UI APIs | Blocked outside scope |

## Current Catalog Counts

- Total entries: 1666
- Confirmed: 465
- Probable: 39
- Unknown: 1162
- Known semantic entries: 504
- Coordinate duplicates: 0
- C# enum duplicates: 0
- Button category entries: 240
- Direction category entries: 74
- Cursor category entries: 9
- Toggle category entries: 10
- State groups in JSON: 76
- Incomplete JSON state groups: 0

## Verification Notes

The Unity project can be blocked before tests run when unrelated untracked StudentDay test files are present, because they reference APIs that are not present in the current compiled assemblies:

- `Assets/Tests/EditMode/StudentLife/StudentDayProgressTests.cs` references missing day-progress APIs such as `StudentLifeProgress.CurrentDay`, `StudentDayState`, `TryEndDay`, and day-end rule types.
- `Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs` references missing `Rootborn.UI.StudentLife` and `StudentDayEndInteractor`.

These files are outside the Style2 icon catalog scope and were not modified.

For final Style2 verification, those unrelated generated test files were moved temporarily to `Builds/Temp/style2-icon-catalog-quarantine`, AssetDatabase was refreshed, and the relevant Style2 tests were run. The files were then restored to their original paths. During restoration Unity had regenerated `StudentDayResultLoopE2EScenarioTests.cs`; because the regenerated file differed from the quarantined copy, the quarantined copy was preserved in `Builds/Temp/style2-icon-catalog-quarantine` rather than overwriting the current untracked file.

The Style2 icon catalog artifacts themselves were verified by shell inspection against the JSON and C# files, and the no-entity-id branching CI gate passed after the UI enum/catalog was added.

## Remaining Risk

The catalog intentionally leaves 1162 sprites as `unknown` rather than inventing semantics. The right-side button/glyph regions and common control icons were mapped from the 4x contact sheet, but every unknown sprite still needs human art review before being used as a named UI icon.
