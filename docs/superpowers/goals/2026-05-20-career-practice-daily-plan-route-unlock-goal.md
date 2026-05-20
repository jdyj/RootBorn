# Goal: Career Practice Unlocks A Daily Plan Route

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add one small data-driven career practice slice where unlocking a career hint through existing career practice makes one Daily Plan route available and visible through the existing Objective Journal source contract.

## Non-Negotiable Constraints

- All career, practice, route, activity, Journal, and unlock behavior must be ScriptableObject-driven.
- Do not branch production C# on career IDs, practice IDs, route IDs, activity IDs, location IDs, quest IDs, NPC IDs, crop IDs, or tool IDs.
- Do not create a career-specific HUD, route hint HUD, permanent Canvas, or goal-like UI outside `ObjectiveJournalPanel`.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for Unity C# files.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Save/load persistence must be verified after reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming` to select and scope the improvement from the current audit.
- `superpowers:test-driven-development` for the new requirement and route behavior.
- `superpowers:verification-before-completion` before committing or reporting completion.

## Required Work

- Add `DailyPlanCareerHintRequirement`, a reusable `DailyPlanRouteRequirementBase` that references a `CareerDefinition` and checks `StudentLifeProgress.IsCareerHintUnlocked`.
- Add one route requirement asset for the existing interior career hint.
- Add one Daily Plan route asset that is unavailable before the interior career hint is unlocked and available afterward.
- Register the route through the default Daily Plan route set and `GameDataRegistry`.
- Ensure Objective Journal route hint behavior uses the existing `DailyPlanRouteHintObjectiveSource`; do not add a new Journal source unless tests prove the existing contract cannot cover the slice.
- Keep route completion through a matching activity out of this cycle unless existing route matching supports it without extra production behavior.

## Required TDD

Add failing tests first:

- `DailyPlanCareerHintRequirement_ReturnsFalseBeforeCareerHintUnlock`
- `DailyPlanCareerHintRequirement_ReturnsTrueAfterCareerHintUnlock`
- `CareerPracticeDailyPlanRouteAssetWiringTests` verifies registered data:
  - the interior career exists in the registry,
  - the new route exists in a registered route set,
  - the route has a `DailyPlanCareerHintRequirement` referencing the same career,
  - the route is unavailable before the career hint and available after the career hint,
  - the route has target location/objective/next-hint text and registered matching activity or location path,
  - Objective Journal emits the unlocked unselected route as a hint and removes the hint once the route is selected.

Expected RED:

- the new tests fail because `DailyPlanCareerHintRequirement` and the career-gated route asset do not exist yet.

Expected GREEN:

- the requirement and registered route data make the route available only after the career hint is unlocked.

## Required Verification

Run:

```powershell
tests_run EditMode testClass Rootborn.Tests.EditMode.StudentLife.DailyPlanRouteContextTests
tests_run EditMode testClass Rootborn.Tests.EditMode.StudentLife.CareerPracticeDailyPlanRouteAssetWiringTests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerDailyPlanRouteE2EScenarioTests.CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload
Get-ChildItem Builds/Logs/career-daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the PlayMode test uncovers that a full route selection/reload path needs broader UI harness work, stop after the EditMode/data slice and report the exact blocker. Do not claim the visible slice complete without Game View evidence.

## Acceptance Criteria

- One career hint unlocks one Daily Plan route through data.
- Objective Journal owns the next-action guidance through the existing route hint/source contract.
- The route is unavailable before career practice state and available after career hint unlock.
- Save/load or persisted player context used by Objective Journal restores the availability state without duplicate hints.
- Player-facing PlayMode evidence is captured at `Builds/Logs/career-daily-plan-route-e2e.png`, or the blocker is explicitly reported.
- No production entity ID branching is introduced.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] Career practice Daily Plan route 연결
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
