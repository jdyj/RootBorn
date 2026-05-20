# Goal: House Upgrade Interior Loop E2E Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Second NPC relationship saved-state regression hardening passed and was committed.

## Objective

Harden the existing House upgrade and interior loop into one player-facing, saved-state-backed E2E slice: upgrade to stage 1, enter the expanded House, apply a room preset, place furniture, reload, and verify visible/runtime/saved state agree without stale overlays or duplicate records.

## Current Evidence

- `docs/superpowers/specs/2026-05-17-house-upgrade-and-interior-construction-design.md` defines the House upgrade and construction architecture.
- `docs/superpowers/specs/2026-05-17-house-upgrade-next-slice-design.md` defines the first stage-1 player-facing slice.
- `docs/superpowers/specs/2026-05-19-house-expansion-interior-loop-design.md` defines the integrated House expansion, room preset, and furniture placement loop.
- Stage-1 data assets already exist under `Assets/Data/Housing/`.
- Existing tests already cover isolated pieces:
  - `Rootborn.Tests.EditMode.Housing.HouseUpgradeDefinitionTests`
  - `Rootborn.Tests.EditMode.Housing.HouseUpgradeServiceTests`
  - `Rootborn.Tests.PlayMode.Housing.HouseUpgradeFlowPlayModeTests`
  - `Rootborn.Tests.PlayMode.Interiors.HouseExpansionInteriorLoopPlayModeTests`
  - `Rootborn.Tests.PlayMode.Interiors.HomeDesignRoomPresetPlayModeTests`

This is therefore not a broad new housing feature. Treat it as integration and verification hardening for the already-created House upgrade, room preset, placement, and persistence pieces.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior or regression changes.
- Keep stage, preset, furniture, and construction behavior data-driven through ScriptableObject definitions and reusable strategies.
- Do not add production C# branching on stage IDs, blueprint IDs, preset IDs, furniture IDs, route IDs, career IDs, quest IDs, NPC IDs, or similar identifiers.
- Visible House, tilemap, placement, camera, UI, and persistence work is not complete without actual PlayMode player-facing verification.
- Runtime-only checks are not enough; saved House state, preset identity, furniture layout records, tile names/counts, occupancy cells, and overlay/debug visibility must also be inspected.
- `ObjectiveJournalPanel` owns any future goal-like housing guidance. Do not add a new permanent House objective HUD.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables file churn before commit.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Tighten existing EditMode housing/interior asset coverage so the stage-1 data assets are registered, valid, and still data-driven.
- Tighten or add one PlayMode E2E scenario that connects the full loop instead of validating isolated pieces only.
- The preferred scenario name is:

```text
Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
```

- If creating a new EndToEnd test is too broad, strengthen the existing `HOUSE_LOOP_PM_*` and `HOUSE_UPGRADE_PM_*` tests and add a source contract test that requires the integrated full-loop assertions.
- Verify saved `HouseStateSaveData.CurrentStageIndex` equals `1` after upgrade and remains `1` after reload.
- Verify saved `SelectedRoomPresetId` matches the applied preset exactly once.
- Verify saved furniture layout contains the selected furniture id, anchor cell, and direction exactly once.
- Verify runtime House ground, wall, collision, furniture object, and occupancy tilemaps reflect the saved state after reload.
- Verify placement surface bounds match the expanded stage bounds after stage 1 generation.
- Verify no stale preview, sample, debug, or construction overlay tilemap remains visible after placement and reload.
- Capture Game View screenshot evidence after the visible result.

## Required TDD

RED:

- Add or tighten a test that fails if the integrated House E2E does not assert exact saved stage, selected preset, saved furniture record, tilemap object cell, occupancy cell, and overlay cleanup.
- Add or tighten a test that fails if stage-1 housing assets are missing from `GameDataRegistry.asset` or have null profile/blueprint/effect references.
- Add or tighten a test that fails if preset application can clear furniture/object/occupancy tilemaps during the integrated loop.
- Add or tighten a test that fails if reload duplicates saved furniture records or visible furniture cells.

GREEN:

- The player-facing flow produces one saved stage-1 House state, one selected preset, and one placed furniture record.
- Reloaded House visibly reflects the same stage, preset, furniture tile, occupancy tile, and placement bounds.
- Overlay/debug/sample tilemaps are absent or inactive after reload.
- The tests prove exact-once saved-state behavior, not only containment.

## Required PlayMode Scenario

Create or strengthen:

```text
HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow when practical; if the current housing flow cannot enter from MainMenu yet, document the exact blocker and use the nearest supported player-facing Town/House entry flow.
2. Open the House upgrade provider or panel through player-facing UI.
3. Verify stage 1 upgrade is available and insufficient/locked states are represented when applicable.
4. Select the hire route or the already-supported route that upgrades to stage 1.
5. Save the stage and enter or reload House through the supported player-facing scene flow.
6. Verify the expanded House is visible in Game View.
7. Open the room preset toolbar and apply one valid room preset.
8. Open furniture placement UI.
9. Select one furniture item, rotate it if rotation is supported in the current UI, and place it visibly.
10. Inspect selected furniture asset name, anchor cell, direction, object tilemap, occupancy tilemap, placement bounds, and overlay state.
11. Capture screenshot:

```text
Builds/Logs/house-upgrade-interior-loop-e2e.png
```

12. Inspect serialized House state and furniture layout files.
13. Reload the same save slot or House scene.
14. Verify stage, preset, furniture record, tilemap cells, occupancy cells, and overlay cleanup restore without duplicates.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Housing
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Housing
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
Get-ChildItem Builds/Logs/house-upgrade-interior-loop-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the exact EndToEnd namespace or method does not exist at the start, create it test-first. If creating it exposes that the MainMenu-to-House player path is not yet available, keep the cycle scoped to the nearest supported player-facing House flow and state the blocker in the final evidence.

## Acceptance Criteria

- Stage-1 House upgrade remains data-driven and uses existing stage assets.
- The integrated flow proves expanded House generation, preset application, furniture placement, save, and reload together.
- Runtime visible state and serialized state agree after reload.
- Saved House stage, preset id, furniture id, anchor cell, direction, object tile, and occupancy tile are exact and non-duplicated.
- No stale construction, preview, sample, or debug overlay remains visible after reload.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[TEST] House upgrade interior loop E2E 강화
```

If the cycle adds missing data assets or UI glue in addition to tests, use:

```text
[FEATURE][ASSET][TEST] House upgrade interior loop 연결
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
