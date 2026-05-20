# Goal: Daily Plan Route Completion And Day Result Feedback

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Connect one selected Daily Plan route to a completion and day-result feedback loop so selecting a route becomes part of the daily gameplay loop, not just a saved choice.

## Non-Negotiable Constraints

- Treat the current clean workspace as shared; do not stage or commit unrelated dirty/untracked files if they appear.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- All route, activity, result, and Objective Journal behavior must remain data-driven.
- Do not add production C# branching on route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, crop IDs, tool IDs, or similar entity identifiers.
- `ObjectiveJournalPanel` owns goal-like guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive behavior must be verified through the actual player-facing PlayMode flow, including keyboard movement, interact key, visible UI clicks, Game View screenshot evidence, and reload.
- If `Assets/AddressableAssetsData/link.xml` or its `.meta` is removed by Unity tooling, restore it immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add or tighten tests proving completed Daily Plan routes are no longer active Goals in Objective Journal.
- Keep unselected available routes in Hints and selected incomplete routes in Goals.
- Complete one selected route through an existing player-facing location activity.
- Ensure `StudentDayResultPanel` shows the completed Daily Plan route and matched activity.
- Ensure tracked HUD state does not duplicate and does not keep a completed route as an active tracked goal.
- Verify route completion state persists after SaveSlot reload and does not re-complete or duplicate result state.

## Required TDD

Write failing tests first:

- `DailyPlanObjectiveSource` does not emit an active Goal item for a completed route.
- Completed route state still produces day-result feedback through `StudentDayEndInteractor.BuildDailyPlanSummaryForTests`.
- Repeated activity completion after route completion does not duplicate completion keys or result logs.
- PlayMode E2E `DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload` fails before the implementation/coverage change because the completed route remains active or the scenario is missing.

Expected RED:

- Existing `DailyPlanObjectiveSource` still emits a completed route as a Goals item, or the E2E scenario is missing.

Expected GREEN:

- The selected route appears as a Goal before completion, disappears from active Goals after completion, the day result shows completion feedback, and reload preserves completion without duplicate HUD or route state.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
Get-ChildItem Builds/Logs/daily-plan-route-completion-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

Inspect the screenshot evidence:

```text
Builds/Logs/daily-plan-route-completion-e2e.png
```

## Acceptance Criteria

- One Daily Plan route can be selected and completed through actual player-facing Town input/UI flow.
- Objective Journal shows the selected route as a Goal before completion and removes or transitions it out of active Goals after completion.
- `TrackedObjectiveHud` remains unique and does not keep stale completed-route guidance.
- Day result feedback visibly includes the completed route and matched activity.
- Save/load restores route completion, Journal state, and HUD state without duplicate completion.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[FEATURE][TEST] Daily Plan route 완료 피드백 연결
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
