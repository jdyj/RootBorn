# Town Concept Audit

Date: 2026-05-08

## Farm Residue Classes

| Class | Examples | First action |
| --- | --- | --- |
| User-facing text | HUD labels, quest text, status text | Replace with town-life labels. |
| Scene path | `Assets/Scenes/Farm.unity` | Move default playable flow to `Assets/Scenes/Town.unity` after smoke test. |
| Runtime type name | `FarmAutoFillerRuntimeInstaller`, `SeededFarmWorldApplier` | Isolate from default path first; rename in the explicit technical rename phase. |
| Data asset | `Crop_Wheat.asset`, `Effect_WaterCrop.asset` | Remove from first-scope registry or mark legacy. |
| Tests | `Farm` or `Crop` scenario names | Keep only when testing legacy isolation. |

## Style1 To Style2 UI Decision

The first town UI baseline uses `sprites/ui/modern/16/style-2`.

Style1 remains available only for legacy comparison and migration tests.

## Deferred Technical Renames

Technical renames are deferred until the default town path is green. This avoids broad scene, prefab, asmdef, and serialized reference churn before behavior is verified.

## Town Visual Smoke

- Town scene loaded in PlayMode.
- Visual roots were present.
- Modern UI common panel recipe uses `sprites/ui/modern/16/style-2`.
- PlayMode smoke tests passed:
  - `Rootborn.Tests.PlayMode.TownConcept.TownStyle2UiSmokeTests.TownScene_HasNonEmptyVisualRoots`
  - `Rootborn.Tests.PlayMode.TownConcept.TownStyle2UiSmokeTests.TownScene_ModernUiCommonPanelUsesStyle2`
- Screenshot artifact: `Builds/Logs/town-concept/town-style2-smoke.png`

## Final Verification

| Gate | Result | Evidence |
| --- | --- | --- |
| TownConcept EditMode | PASS | `Rootborn.Tests.EditMode.TownConcept`: 5 passed, 0 failed. |
| ModernSociety EditMode | PASS | `Rootborn.Tests.EditMode.ModernSociety`: 1 passed, 0 failed. |
| TownConcept PlayMode | PASS | `Rootborn.Tests.PlayMode.TownConcept`: 3 passed, 0 failed. |
| Entity ID branching gate | PASS | `Scripts/ci/check-no-entity-id-branching.sh`: `OK: no entity-id branching in system code.` |
| Forbidden runtime lookup audit | PASS | No matches for `Resources.Load`, `GameObject.Find`, `FindObjectOfType`, or `FindObjectsOfType` in the town conversion files. |

## Remaining Farm Residue

| Residue | Classification | Next action |
| --- | --- | --- |
| `Assets/Scenes/Farm.unity` | legacy scene | Keep until all boot/save/world regression tests are migrated to Town. |
| `Assets/Scripts/Game/Bootstrap/FarmAutoFillerRuntimeInstaller.cs` | technical rename deferred | Isolate from default Town path first, then rename in a dedicated serialized-reference-safe pass. |
| `Assets/Scripts/Game/Bootstrap/FarmModernUiPanelBridge.cs` | technical rename deferred | Replace with town-aware bridge after Town boot is the default runtime path. |
| `Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs` | technical rename deferred | Migrate world generation naming after Town scene/world data is authoritative. |
| `Assets/Data/Crops/` and crop tool effects | data migration deferred | Remove from first-scope registry or mark legacy when town activity SOs replace the crop loop. |
| `Farm` and `Crop` terms in `docs/art/town-concept-*.md` | audit vocabulary | Allowed because these documents explicitly classify removed/legacy farm concepts. |
