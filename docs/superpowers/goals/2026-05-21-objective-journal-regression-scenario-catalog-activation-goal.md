# Goal: Objective Journal Regression Scenario Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add the existing Objective Journal regression PlayMode scenario to the active scenario catalog so goal-like UI ownership, source stable-key uniqueness, tracked HUD uniqueness, screenshot evidence, and reload persistence are tracked by the same catalog gate as the gameplay slices.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is a catalog and test-gate cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI layout, save formats, or PlayMode scenario behavior.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity IDs, route IDs, quest IDs, activity IDs, location IDs, NPC IDs, UI row IDs, or similar identifiers.
- `ObjectiveJournalPanel` remains the owner of goal-like UI. `TrackedObjectiveHud` remains the only always-visible objective HUD.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Tighten `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs` so it fails while the catalog lacks an active `UI` category row for Objective Journal regression coverage.
- Update `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md` with an active Objective Journal regression row.
- Map the row to existing executable evidence:
  - `Assets/Tests/PlayMode/EndToEnd/ObjectiveJournalRegressionE2EScenarioTests.cs`
  - `OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001_MultiSourceReloadNoDuplicateHud`
  - `Builds/Logs/objective-journal-source-collision-e2e.png`
- Update `Scripts/ci/check-scenario-registry-sync.ps1` so `UI` is a required catalog category and Objective Journal test signals cannot drift out of the catalog.
- Do not rerun or modify the PlayMode scenario unless the evidence path is invalid.

## Required TDD

RED:

- Add an EditMode catalog contract test that fails while the Objective Journal regression row is missing.
- Add a gate contract assertion that fails while the PowerShell sync gate does not require `UI`.
- Confirm the test fails against the current catalog/gate.

GREEN:

- Add the active `UI` catalog row.
- Add `UI` to the PowerShell scenario sync required categories and known test signal map.
- Confirm the EditMode contract and scenario sync gates pass.

## Required Verification

Run:

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

This cycle does not require a new PlayMode rerun or screenshot capture because it only catalogs an already-existing PlayMode scenario and screenshot evidence contract. If the mapped scenario file or screenshot contract is missing, stop and report that the Objective Journal PlayMode evidence needs a separate verification cycle.

## Acceptance Criteria

- Scenario catalog includes active `OBJECTIVE-JOURNAL-REGRESSION-001` in category `UI`.
- The row maps to `ObjectiveJournalRegressionE2EScenarioTests.cs`.
- The row references `OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001_MultiSourceReloadNoDuplicateHud`.
- The row records the canonical screenshot path `Builds/Logs/objective-journal-source-collision-e2e.png`.
- PowerShell scenario sync requires `UI` and passes.
- Scenario EditMode tests and baseline gates pass.
- No runtime gameplay or UI behavior changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Objective Journal 회귀 시나리오 카탈로그 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
