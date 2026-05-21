# Goal: Daily Plan Route Completion Result Feedback Catalog Refresh

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Refresh the Student Life scenario registry catalog so `LIFE-DAILY-PLAN-002` explicitly tracks the stricter Daily Plan route completion result-feedback PlayMode evidence.

## Non-Negotiable Constraints

- Treat unrelated dirty or untracked files as user-owned.
- Do not delete, revert, move, stage, or commit unrelated files.
- Do not edit `Assets/**/*.cs` directly through shell commands; use Unity MCP script tools for Unity C# files.
- Do not add production gameplay or UI code in this catalog refresh unless verification proves the evidence is missing.
- Goal-like UI remains owned by `ObjectiveJournalPanel`, with only one `TrackedObjectiveHud`.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect the current `DailyPlanRouteCompletionE2EScenarioTests` evidence and screenshot.
- Add a catalog gate test named `SCENARIO_REGISTRY_EDIT_013_CatalogReferencesDailyPlanCompletionResultFeedbackEvidence`.
- Update the active `LIFE-DAILY-PLAN-002` catalog row so it references:
  - `Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteCompletionE2EScenarioTests.cs`
  - `DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload`
  - `Builds/Logs/daily-plan-route-completion-e2e.png`
  - selected route completion result feedback, Objective Journal transition, tracked HUD cleanup, reload persistence, and duplicate prevention.

## Required TDD

- RED: add the catalog gate test first and run `StudentLifeScenarioRegistryCoverageGateTests`; it must fail because the row does not yet reference the stricter method and screenshot evidence.
- GREEN: update only the catalog row needed for `LIFE-DAILY-PLAN-002`.
- Re-run the focused EditMode test class and scenario sync gates.

## Required Verification

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
Get-ChildItem Builds/Logs/daily-plan-route-completion-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- `LIFE-DAILY-PLAN-002` remains active in the scenario coverage catalog.
- The row explicitly references the stricter completion result-feedback method and screenshot evidence.
- The catalog gate fails before the row refresh and passes after it.
- Scenario sync gates pass with the refreshed catalog.
- No production gameplay or UI code changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Daily Plan completion scenario catalog 증거 갱신
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
