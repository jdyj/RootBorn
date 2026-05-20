# Goal: Career/Job Daily Plan Saved-State Regression Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after quest/interior reward saved-state regression hardening passed.

## Objective

Harden the existing career/job Daily Plan slices so career hint unlocks, workshop/job routes, selected Daily Plan route state, Objective Journal guidance, tracked HUD state, and save/load behavior cannot silently duplicate, disappear, or drift.

## Current Evidence

- `CareerInteriorPracticeRoute.asset` already exists with route id `daily.route.career.interior-practice`.
- `WorkshopShiftRoute.asset` already exists with route id `daily.workshop.shift`.
- `CareerDailyPlanRouteE2EScenarioTests.CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload` already proves a player-facing career practice route flow.
- `CareerPracticeDailyPlanRouteAssetWiringTests` already proves career-hint route gating and Objective Journal hint-to-goal movement.

This is therefore not a broad new feature slice. Treat it as regression hardening for existing career/job Daily Plan behavior.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior or regression changes.
- Do not add production C# branching on career IDs, job IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, crop IDs, tool IDs, or similar identifiers.
- Keep career/job route availability data-driven through ScriptableObject definitions and requirement/outcome arrays.
- Visible and interactive verification must use the player-facing PlayMode flow and capture Game View screenshot evidence.
- Runtime-only checks are not enough; serialized StudentLife, DailyPlan, Objective Journal tracked state, and relevant save files must also be inspected.
- `ObjectiveJournalPanel` owns career/job guidance; do not create new career/job HUDs.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables file churn before commit.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Tighten existing EditMode coverage in `Rootborn.Tests.EditMode.StudentLife.CareerPracticeDailyPlanRouteAssetWiringTests`.
- Tighten existing PlayMode coverage in `Rootborn.Tests.PlayMode.EndToEnd.CareerDailyPlanRouteE2EScenarioTests.CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload`.
- Verify serialized StudentLife save data contains the career hint exactly once after repeated career-practice interactions and reload.
- Verify serialized DailyPlan save data contains the selected career route exactly once after selection and reload.
- Verify the career route is absent/locked before the hint and visible/selectable after the hint.
- Verify the Objective Journal moves the unlocked unselected career route from `Hints` to `Goals` after selection without duplicate stable keys.
- Verify exactly one `TrackedObjectiveHud` exists before and after reload.
- Add job/workshop route asset wiring coverage so the existing `WorkshopShiftRoute` remains registered, reachable, selectable, and represented by Objective Journal text fields.

## Required TDD

RED:

- Add or tighten a test that fails if `CareerDailyPlanRouteE2EScenarioTests` only checks save-file containment instead of exact-once serialized career route and career hint assertions.
- Add or tighten a test that fails if the registered workshop/job Daily Plan route is missing from `GameDataRegistry.asset`, lacks a reachable target location, or lacks Objective Journal text keys.
- Add or tighten a test that fails if Objective Journal career route hint/goal stable keys duplicate after route selection.

GREEN:

- Career hint save data and Daily Plan route save data contain the expected values exactly once.
- Reloaded career Daily Plan route state has one selected route, one tracked HUD, and no duplicate Journal source rows.
- Workshop/job route asset wiring remains registered and route-ready.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerDailyPlanRouteE2EScenarioTests.CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload
Get-ChildItem Builds/Logs/career-daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Existing career Daily Plan route remains player-facing and visible.
- Career route is locked before career hint unlock and visible/selectable after the player-facing career practice path.
- Serialized StudentLife save data contains the career hint exactly once.
- Serialized DailyPlan save data contains the selected career route exactly once.
- Objective Journal shows the unselected unlocked route as a hint, then the selected route as a goal, without duplicate stable keys.
- Exactly one tracked HUD exists and restores after reload.
- Existing workshop/job route asset wiring is covered by registry/route readiness tests.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[TEST] Career job Daily Plan 저장 회귀 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
