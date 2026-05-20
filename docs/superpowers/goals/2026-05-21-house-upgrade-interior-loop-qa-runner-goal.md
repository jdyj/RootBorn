# Goal: House Upgrade Interior Loop QA Runner

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Objective Journal legacy HUD ownership auditing was committed.

## Objective

Add a repeatable opt-in QA runner for the strict House upgrade/interior E2E so future housing, UI, camera, tilemap, placement, or build changes can rerun the same player-facing visual/persistence gate with one command.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior or script contract changes.
- Do not change player-facing House, placement, tilemap, UI, or save behavior in this cycle unless the runner exposes a real regression.
- Keep House stage, preset, furniture, and construction behavior data-driven.
- Do not add production C# branching on stage IDs, preset IDs, furniture IDs, quest IDs, route IDs, or similar entity IDs.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add an opt-in QA script:

```text
Scripts/qa/run-house-upgrade-interior-loop.ps1
```

- The script must run:

```text
Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
```

- The script must fail closed if the Unity test command fails, screenshot evidence is missing or empty, Addressables `link.xml` preservation fails, or required evidence markers are missing from its own output.
- The script must print a compact evidence summary including:
  - test method name,
  - screenshot path and byte length,
  - stage marker,
  - room preset marker,
  - furniture layout marker,
  - runtime tilemap marker,
  - overlay cleanup marker,
  - Addressables preservation marker.
- Add an EditMode script contract test in the existing style that fails if the runner is missing required command, screenshot, preservation, and evidence marker checks.

## Required TDD

RED:

- Add an EditMode contract test that fails because `Scripts/qa/run-house-upgrade-interior-loop.ps1` is missing.
- The test must require the strict E2E method name, screenshot path, `check-addressables-linkxml-preserved.ps1`, and all required evidence marker strings.

GREEN:

- Add the runner script.
- The contract test passes.
- The runner executes the strict House E2E and verifies screenshot and Addressables preservation.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Housing
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
```

Run the new QA runner:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-house-upgrade-interior-loop.ps1 -RunName house-upgrade-interior-loop
Get-ChildItem Builds/Logs/house-upgrade-interior-loop-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If Unity test runs delete `Assets/AddressableAssetsData/link.xml` or `.meta`, restore both and rerun the preservation check before committing.

## Acceptance Criteria

- One command reruns the strict House upgrade/interior visual and persistence E2E.
- Missing screenshot, missing Addressables preservation, or missing evidence markers fails the runner.
- Contract tests prevent weakening the runner.
- The runner leaves no unrelated dirty state except expected log/screenshot outputs.
- No production gameplay code changes are required unless a real regression is found and fixed test-first.

## Commit

Use:

```text
[TEST][CHORE] House upgrade interior QA runner 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
