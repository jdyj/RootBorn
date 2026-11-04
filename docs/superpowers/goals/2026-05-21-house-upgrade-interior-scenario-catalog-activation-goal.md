# Goal: House Upgrade/Interior Scenario Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Activate the existing House Upgrade/Interior Loop PlayMode evidence in the scenario registry catalog so visual placement, tilemap, and saved-state coverage is tracked as an active scenario.

## Non-Negotiable Constraints

- Treat unrelated dirty or untracked files as user-owned.
- Do not delete, revert, move, stage, or commit unrelated files.
- Do not edit `Assets/**/*.cs` directly through shell commands; use Unity MCP script tools for Unity C# files.
- Do not add production gameplay, UI, scene, prefab, or asset changes in this catalog activation unless verification proves the evidence is missing.
- Runtime-only visual checks do not replace saved-state evidence for House, tilemap, preset, or furniture placement work.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect the existing `HouseUpgradeInteriorLoopE2EScenarioTests` evidence and screenshot.
- Add a catalog gate test named `SCENARIO_REGISTRY_EDIT_015_CatalogActivatesHouseUpgradeInteriorScenario`.
- Add an active catalog row:
  - ID: `HOUSE-UPGRADE-INTERIOR-001`
  - Category: `LIFE`
  - Owner: `House Upgrade/Interior Loop`
  - Evidence Type: `PlayMode`
  - Evidence: `Assets/Tests/PlayMode/EndToEnd/HouseUpgradeInteriorLoopE2EScenarioTests.cs`
  - Player-facing verification: `HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload`, screenshot `Builds/Logs/house-upgrade-interior-loop-e2e.png`, stage/preset/furniture exact-once saved state, runtime object and occupancy tilemap checks, reload persistence, and overlay cleanup.

## Required TDD

- RED: add the catalog gate test first and run `StudentLifeScenarioRegistryCoverageGateTests`; it must fail because the row is missing.
- GREEN: add only the active catalog row needed for `HOUSE-UPGRADE-INTERIOR-001`.
- Re-run the focused EditMode test class and scenario sync gates.

## Required Verification

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
Get-ChildItem Builds/Logs/house-upgrade-interior-loop-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- `HOUSE-UPGRADE-INTERIOR-001` is active in the scenario coverage catalog.
- The catalog gate fails before the row exists and passes after it is added.
- The PlayMode evidence passes and screenshot evidence is present and non-zero.
- Scenario sync gates pass with the refreshed catalog.
- No production gameplay, UI, scene, prefab, or asset changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] House upgrade interior scenario catalog 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
