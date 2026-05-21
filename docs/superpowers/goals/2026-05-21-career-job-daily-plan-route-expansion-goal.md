# Goal: Career/Job Daily Plan Route Expansion

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Resource/Recipe feature, dedicated smoke, core gameplay matrix, and scenario catalog coverage were completed.

## Candidate Selection

Three next improvements were compared:

1. Career/Job Daily Plan route expansion
   - Value: builds directly on existing Career Practice, Part-Time Work, Daily Plan, Objective Journal, saved-state, and dedicated smoke foundations.
   - Risk: touches progression, route availability, Journal rows, and PlayMode UI, but existing tests and data patterns are strong.
   - Verification burden: EditMode domain/objective tests plus one player-facing PlayMode E2E screenshot/reload flow.
2. Relationship activity reward and commitment route
   - Value: deepens NPC relationship content.
   - Risk: relationship milestones and commitment semantics need more product judgment than career/job route wiring.
   - Verification burden: PlayMode NPC interaction, Journal, milestone, save/reload, duplicate prevention.
3. Knowledge/Status activity chain
   - Value: supports free-path growth.
   - Risk: broader design choice between Knowledge and Status chain should be made after one more route slice.
   - Verification burden: activity, day result, Journal, reload, and status/knowledge persistence.

Selected candidate: Career/Job Daily Plan route expansion. It is the smallest high-value player-facing slice because `CareerDailyPlanRouteE2EScenarioTests`, `CareerPracticeDailyPlanRouteAssetWiringTests`, `PartTimeWorkGrowthTests`, `WorkshopShiftRoute.asset`, `DailyPlanObjectiveSource`, and `DailyPlanRouteHintObjectiveSource` already provide most of the foundation.

## Objective

Add one small data-driven career/job route slice so a player-facing Town career or part-time work action unlocks or recommends a Daily Plan route, Objective Journal guides the next action, and save/load plus repeat interaction cannot duplicate progress or UI state.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests before implementation for behavior, regression, and PlayMode coverage.
- Keep career/job route availability data-driven through ScriptableObject definitions, requirements, outcomes, and registry references.
- Do not add production C# branching on career IDs, job IDs, route IDs, activity IDs, location IDs, NPC IDs, reward IDs, crop IDs, tool IDs, furniture IDs, room IDs, or similar entity identifiers.
- `ObjectiveJournalPanel` owns career/job guidance. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible and interactive verification must use the actual PlayMode player-facing flow: MainMenu save slot, Town entry, movement, interaction/UI selection, Daily Plan board, Objective Journal `Tab`, screenshot, save/reload.
- Runtime-only state checks are not enough. Serialized StudentLife, PartTimeWork or Career, DailyPlan, Objective Journal tracked state, and save files must be inspected when persistence is expected.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables churn before completion.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect and reuse existing patterns before adding code:
  - `Assets/Scripts/Game/StudentLife/PartTimeWorkDefinition.cs`
  - `Assets/Scripts/Game/StudentLife/PartTimeWorkRunner.cs`
  - `Assets/Scripts/Game/StudentLife/PartTimeWorkOutcomes.cs`
  - `Assets/Scripts/Game/StudentLife/DailyPlanRouteDefinition.cs`
  - `Assets/Scripts/Game/StudentLife/DailyPlanProgress.cs`
  - `Assets/Scripts/UI/Objectives/Sources/DailyPlanObjectiveSource.cs`
  - `Assets/Scripts/UI/Objectives/Sources/DailyPlanRouteHintObjectiveSource.cs`
- Reuse or extend one existing route asset, preferably `Assets/Data/StudentLife/DailyPlans/WorkshopShiftRoute.asset`, unless inspection proves a smaller registered career/job route exists.
- Reuse or extend one existing player-facing Town action, preferably the Workshop or part-time work path, so the action changes career/job progress through SO outcome data.
- Register all new or modified ScriptableObject assets through `Assets/Data/Registry/GameDataRegistry.asset`.
- Make the Daily Plan route unavailable before the required career/job progress and available afterward.
- Surface the unlocked or recommended route through existing Objective Journal sources, not a new HUD.
- Persist career/job progress, route availability source state, selected Daily Plan route, and tracked Journal state.
- Ensure repeated action, repeated route selection, reload, and replay do not duplicate result logs, career hints, work logs, Journal rows, HUDs, or route selections.
- If the route affects dedicated server/client gameplay state, write a follow-up dedicated smoke goal instead of expanding this slice beyond one reviewable cycle.

## Required TDD

RED:

- Add or tighten an EditMode test that fails while the selected career/job route is available before the required progress.
- Add or tighten an EditMode test that fails while the player-facing career/job action does not change progress exactly once.
- Add or tighten an EditMode test that fails while the selected route lacks registry wiring, reachable location/activity references, or Objective Journal title/detail/next-hint keys.
- Add or tighten an Objective Journal test that fails if the career/job route appears in both `Hints` and `Goals` after selection or emits duplicate stable keys.
- Add a PlayMode E2E test that fails before the player can perform the career/job action, see/select the route, open Objective Journal, capture screenshot evidence, reload, and verify no duplicates.

GREEN:

- Career/job route condition is false before the required progress and true after the player-facing action.
- Career/job action changes progress once for a request key and remains idempotent after reload/replay.
- Daily Plan board shows the route only after progress and allows selection through visible UI.
- Objective Journal shows one correct route row in the expected category and exactly one tracked HUD when tracked.
- Save/load restores career/job progress, route availability, selected route, Journal state, and HUD state without duplication.

## Required PlayMode Scenario

Create or strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Create or load the test save slot through visible SaveSlot UI.
3. Enter Town.
4. Confirm the selected career/job route is absent or locked before progress.
5. Move the player to the career/job action source using keyboard input.
6. Trigger the action through the visible prompt, panel, or UI button path.
7. Verify runtime career/job progress changed exactly once.
8. Open Daily Plan board through the visible player-facing route.
9. Verify the career/job route is visible/selectable after progress.
10. Select the route through the visible Daily Plan UI.
11. Open Objective Journal with `Tab`.
12. Verify route guidance appears in the expected `Goals` or `Hints` category without duplicate stable keys.
13. Track the route and verify exactly one `TrackedObjectiveHud`.
14. Capture Game View or Camera screenshot:

```text
Builds/Logs/career-job-route-e2e.png
```

15. Inspect serialized save files for exact-once career/job progress and selected route state.
16. Reload the same save slot through SaveSlot UI.
17. Reopen Daily Plan and Objective Journal.
18. Verify progress, route availability, selection, Journal row, and tracked HUD restore without duplication.
19. Repeat the career/job action or route selection and verify no duplicate reward, progress, result log, route state, or HUD is created.

Forbidden shortcuts:

- Do not directly call runner methods as the only PlayMode proof.
- Do not manually set career/job progress or selected route state in the scenario.
- Do not pass only by checking JSON files.
- Do not inspect route availability without opening the player-facing Daily Plan board.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
Get-ChildItem Builds/Logs/career-job-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If C# or asset changes affect the dedicated core surface, also run the existing matrix before completion or write a dedicated follow-up goal with exact blocker evidence:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7921 -WaitSeconds 120
```

## Acceptance Criteria

- One career/job route source is implemented as a data-driven vertical slice.
- The player unlocks or recommends the route through actual Town movement and visible interaction/UI.
- Daily Plan board exposes the route after progress and lets the player select it.
- Objective Journal owns all route guidance and does not duplicate rows across `Hints` and `Goals`.
- Exactly one `TrackedObjectiveHud` exists when the route is tracked.
- Save/load restores career/job progress, route availability, route selection, Journal guidance, and HUD state exactly once.
- Repeating the action or selection does not duplicate reward, progress, result logs, route records, Journal rows, or HUDs.
- Screenshot evidence exists at `Builds/Logs/career-job-route-e2e.png` and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] Career job Daily Plan route 확장
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
