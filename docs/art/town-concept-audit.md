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
