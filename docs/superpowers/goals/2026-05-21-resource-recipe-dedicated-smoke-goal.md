# Goal: Resource/Recipe Dedicated Smoke

## Objective

Add a dedicated server/client smoke for the Town Resource/Recipe activity slice so the recipe reveal, Objective Journal hint, screenshot evidence, save/load, and duplicate prevention are proven in built player processes.

## Current Baseline

- Completed prerequisite:
  - `2ec5d8675 [FEATURE][ASSET][TEST] Town resource recipe activity slice`
- Player-facing Editor PlayMode coverage:
  - `TOWN_RESOURCE_RECIPE_E2E_001_ActivityRewardHintReloadNoDuplicate`
  - screenshot: `Builds/Logs/town-resource-recipe-activity-e2e.png`
- Existing dedicated smoke patterns:
  - `DirectValidationDiscoveryRouteUnlockAutoplayInstaller`
  - `run-dedicated-discovery-route-unlock-smoke.ps1`
  - `DedicatedDiscoveryRouteUnlockAutoplayContractTests`

## Required Work

- Add a dedicated autoplay installer:

```text
Assets/Scripts/Game/Common/DirectValidationResourceRecipeActivityAutoplayInstaller.cs
```

- Add explicit flags:

```text
-directValidationAutoplayResourceRecipeActivity
-directValidationAutoplayResourceRecipeActivityObserveOnly
-directValidationScreenshot
```

- Add a QA runner:

```text
Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1
```

- Run first action pass and observe-only reload pass.
- Capture screenshots:

```text
network-dedicated-resource-recipe-activity-e2e.png
network-dedicated-resource-recipe-activity-e2e-reload.png
```

- Assert log evidence for:
  - recipe revealed,
  - resource seen,
  - Objective Journal duplicate rows = 0,
  - TrackedObjectiveHud duplicates = 0,
  - saved progress exists,
  - observe-only reload restored the same state,
  - repeated/direct duplicate grant did not occur.
- Preserve `link.xml` so the installer survives built-player stripping.

## Required TDD

RED:

- Network contract test fails while the installer and runner are missing.
- Addressables preservation test fails while the new installer type is absent from `link.xml`.
- Contract test fails if the runner does not check action pass, observe-only reload pass, screenshots, stale artifacts, fatal logs, and duplicate evidence.

GREEN:

- Network contract tests pass.
- Dedicated smoke passes with action and observe-only reload screenshots.

## Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1 -RunName network-dedicated-resource-recipe-activity-e2e -Port 7911 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-resource-recipe-activity-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Dedicated action pass reveals the recipe through the player-facing Town Square activity path.
- Observe-only reload proves state persisted and does not replay the reward.
- Screenshots for both passes exist and are non-zero.
- Logs prove no duplicate recipe, Journal row, HUD, transaction, or fatal client/server state.
- Runner cleans up launched processes.

## Commit

```text
[NETWORK][TEST] Resource recipe activity dedicated smoke
```
