# Goal: Daily Plan Route Selection And Result Integration

## Objective

Implement the first player-facing Daily Plan route slice from `docs/superpowers/specs/2026-05-20-daily-plan-route-selection-design.md`.

The player must be able to choose a data-defined daily route in Town, perform a matching real Town activity, see the selected plan and result reflected in Objective Journal and day result UI, then reload the save slot and keep the plan/result context without duplicate application.

This goal is complete only when the feature is implemented, tested with TDD, verified through actual PlayMode player flow, visually evidenced with a Game View screenshot, and committed in small reviewable units.

## Constitutional Constraints

- Do not create a new always-visible objective HUD.
- `ObjectiveJournalPanel` owns all "what should I do now?" guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- All Daily Plan routes, requirements, outcomes, and route sets must be ScriptableObject/data-driven.
- No production C# may branch on specific route/activity/location/career ids.
- Do not use enums for route ids or activity ids.
- Do not edit `Assets/**/*.cs` by direct filesystem writes. Use Unity MCP `script-update-or-create` / `script-delete` for C#.
- Visible UI and interaction changes require direct PlayMode user-flow verification plus Game View or Camera screenshot evidence.
- Keep unrelated dirty files untouched:
  - `D Assets/AddressableAssetsData/link.xml`
  - `D Assets/AddressableAssetsData/link.xml.meta`
  - `M production/qa/evidence/registry-provider-town-flow.png`

## Required Design Reference

Read first:

- `docs/superpowers/specs/2026-05-20-daily-plan-route-selection-design.md`
- `docs/superpowers/audits/2026-05-20-feature-development-inventory-refresh.md`
- `.claude/rules/testing-discipline.md`
- `.claude/rules/objective-journal-ui.md`
- `.claude/rules/path-based/assets-data.md`
- `.claude/rules/ui-standards.md`

## Required Skills / Workflow

- Use `superpowers:test-driven-development` before production implementation.
- Use `superpowers:systematic-debugging` for any failed test or unexpected PlayMode behavior.
- Use `superpowers:verification-before-completion` before claiming completion.
- If creating an implementation plan first, use `superpowers:writing-plans`.

## Track 1: Daily Plan Domain And Persistence

### Purpose

Create the data-driven Daily Plan domain without scene/UI dependencies.

### Required Work

Add focused runtime code under `Assets/Scripts/Game/StudentLife/`:

- `DailyPlanRouteDefinition`
- `DailyPlanRouteSetDefinition`
- `DailyPlanRouteRequirementBase`
- `DailyPlanRouteOutcomeBase`
- `DailyPlanProgress`
- `DailyPlanProgressSaveData`
- `DailyPlanProgressPersistence`
- `DailyPlanRouteRunner`

Recommended defaults:

- Use dedicated save file `daily-plan-progress.json`.
- A route matches by explicit `MatchingActivities` or by `TargetLocation + GrowthRoute`.
- Selection and completion are idempotent for the same save slot/player/day.
- Missing saved route data must not crash; it should degrade to "choose today's route".

### Required TDD

Before production code:

- Add failing EditMode tests under `Assets/Tests/EditMode/StudentLife/`.
- Prove route set returns available routes from SO data.
- Prove route selection persists selected route for active save slot/player/day.
- Prove duplicate route selection is a no-op.
- Prove matching location activity completes selected route.
- Prove non-matching activity does not complete selected route.
- Prove completion does not duplicate after save/load.
- Prove missing saved route is handled without exception.

Expected RED:

- Tests fail because Daily Plan domain classes or persistence APIs do not exist yet.

Expected GREEN:

- `Rootborn.Tests.EditMode.StudentLife` passes.

### Completion Criteria

- Domain is data-driven and EditMode tested.
- No scene/UI code required for tests.
- Commit with prefix `[FEATURE][TEST]` or `[FEATURE][TEST][DATA]`.

## Track 2: Data Assets And Registry Wiring

### Purpose

Wire the first small Daily Plan route set into real project data.

### Required Work

Create SO assets under:

- `Assets/Data/StudentLife/DailyPlans/`

Minimum route set:

- Library self-study route
- Town Square help route
- Workshop shift route

Each route must reference existing data where possible:

- `LocationDefinition`
- `LocationActivityDefinition`
- `LocationGrowthRoute`
- optional milestone/career route references

Register the route set in `Assets/Data/Registry/GameDataRegistry.asset`.

### Required TDD

Add or extend registry tests proving:

- `GameDataRegistry` exposes Daily Plan route sets.
- Default registry includes the first Daily Plan route set.
- All route assets have stable ids, target location/activity references, and Objective Journal text keys.

### Completion Criteria

- Assets exist with `.meta` files.
- Registry wiring is verified by tests.
- No Resources.Load fallback is added.
- Commit with prefix `[ASSET][TEST]` or `[FEATURE][ASSET][TEST]`.

## Track 3: Runtime Installer, Plan Board, And Plan Panel

### Purpose

Expose Daily Plan selection to the player through real Town interaction and a visible UI panel.

### Required Work

Add focused UI/runtime code:

- `DailyPlanProgressComponent`
- `DailyPlanRuntimeInstaller`
- `DailyPlanBoardInteractor`
- `DailyPlanPanel`

Player-facing behavior:

- Town contains a runtime board/interactable with prompt `[E] Plan Day`.
- Interacting through `PlayerInteractionRouter` opens `DailyPlanPanel`.
- Panel lists available routes and lets the player select one through a visible button.
- Route rows must be scrollable or bounded.
- Panel uses Modern UI Style2 common panel rules.
- Panel is not permanent HUD and does not replace `Tab = Objective Journal`.

### Required TDD

Add or extend EditMode/PlayMode tests proving:

- runtime installer adds exactly one board/panel path in Town,
- panel shows an explicit empty state when no routes are available,
- panel rows are bounded or scrollable,
- clicking a route button selects and saves the route,
- repeated install does not duplicate panel or board objects.

Expected RED:

- Tests fail because board/panel/installer do not exist.

### Completion Criteria

- Player can select a route through actual UI button path.
- No new always-visible objective HUD is created.
- Commit with prefix `[UI][TEST]` or `[FEATURE][UI][TEST]`.

## Track 4: Objective Journal And Tracked HUD Integration

### Purpose

Make the selected Daily Plan visible through the existing Objective Journal ownership model.

### Required Work

Add either:

- focused `DailyPlanObjectiveSource` under `Assets/Scripts/UI/Objectives/Sources/`, source id `daily-plan`, or
- a carefully bounded extension to `StudentDayGuideObjectiveSource`.

Recommended default:

- Add `DailyPlanObjectiveSource` and register it in `ObjectiveJournalSourceCatalog`.

Journal states:

- Before selection: `Goals` shows "Choose today's route".
- After selection: `Goals` shows selected route and target action/location.
- After matching activity: `Goals` shows completed/progressed state and day-end hint.
- After day end/reload: `Goals` shows next route context.

Tracked HUD:

- Only updates when player tracks the Daily Plan row from Objective Journal.
- Must restore from saved stable key after reload.

### Required TDD

Add EditMode tests under `Assets/Tests/EditMode/UI/Objectives/` proving:

- source is standalone and registered in catalog if new source is added,
- before-selection state appears in `Goals`,
- selected state appears in `Goals`,
- completed state appears in `Goals`,
- tracked stable key restore still selects the Daily Plan row after refresh/reorder.

### Completion Criteria

- Objective Journal owns Daily Plan guidance.
- `TrackedObjectiveHud` remains the only compact persistent surface.
- Existing Objective Journal tests still pass.
- Commit with prefix `[FEATURE][UI][TEST]`.

## Track 5: Day Result And Save/Load Integration

### Purpose

Show the selected plan and completed matching action in day result UI and ensure save/load is idempotent.

### Required Work

Extend day result summary flow so `StudentDayResultPanel` can show:

- planned route display name,
- actual matched activity display name,
- completion state,
- changed stat/result log summary,
- next hint.

Persistence requirements:

- route selection saved immediately,
- route completion saved immediately after matching activity,
- day result reload does not duplicate plan completion,
- loading a save restores the plan state before Objective Journal refresh.

### Required TDD

Add tests proving:

- day result model includes selected/completed Daily Plan summary,
- no selected route produces no false Daily Plan completion,
- completion survives reload,
- repeated day-end or repeated load does not duplicate summary/completion logs.

### Completion Criteria

- Day result UI displays Daily Plan summary in real flow.
- Save/load idempotency is covered.
- Commit with prefix `[FEATURE][UI][TEST]`.

## Required Player-Facing PlayMode E2E

Create or extend a PlayMode E2E under `Assets/Tests/PlayMode/EndToEnd/`.

Suggested method:

```csharp
Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload
```

Scenario: Daily plan route survives real player day flow

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Walk to the daily plan board with keyboard input.
3. Interact with `[E] Plan Day`.
4. Select a non-school growth route through a visible UI button.
5. Open Objective Journal with Tab.
6. Verify `Goals` shows the selected route and target action.
7. Track the route from Objective Journal.
8. Close Objective Journal.
9. Walk to the selected route's target Town location with keyboard input.
10. Interact with the location prompt.
11. Apply the matching activity through the visible UI button or prompt path.
12. Reopen Objective Journal.
13. Verify the tracked route shows completed/progressed state and did not jump to another row.
14. Walk to the day-end board.
15. Interact with the day-end board.
16. Verify result UI shows planned route, actual action, changed stat/result log, and next hint.
17. Capture Game View screenshot to `Builds/Logs/daily-plan-route-e2e.png`.
18. Load the same save slot.
19. Verify Objective Journal and tracked HUD restore the next route context.
20. Verify saved Daily Plan state did not duplicate completion/result logs.

This E2E must use real input/UI flow:

- keyboard movement,
- `PlayerInteractionRouter`,
- visible UI buttons,
- `Tab` Objective Journal flow,
- save slot load path.

Do not replace this scenario with direct method calls, forced scene-only setup, source method calls, or saved JSON checks only.

## Required Regression Gates

Run before completion:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.StudentDayResultLoopE2EScenarioTests.STUDENT_DAY_E2E_001_011_PlayedActivityEndDayResultReloadAndNextDayLoop
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Also verify screenshot evidence:

```powershell
Get-ChildItem Builds/Logs/daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Expected:

- all tests pass,
- `daily-plan-route-e2e.png` exists and has non-zero length,
- static gates pass,
- no production route/activity id-specific branching is introduced.

## Done Means

- Daily Plan route selection works from real Town player flow.
- Route definitions and route sets are ScriptableObject/data-driven.
- Player selects a non-school route through UI.
- Matching activity progresses/completes the selected plan.
- Objective Journal `Goals` reflects unselected, selected, completed, and reload states.
- `TrackedObjectiveHud` restores selected Daily Plan when tracked.
- Day result UI shows planned route, actual action, stat/result log, and next hint.
- Save/load preserves plan context without duplication.
- Game View screenshot evidence exists.
- All changed C# was edited through Unity MCP script tools.
- Unrelated dirty files were not reverted or staged.

## Suggested Commit Breakdown

1. `[FEATURE][TEST] Daily Plan 도메인과 저장 모델 추가`
2. `[FEATURE][ASSET][TEST] Daily Plan 기본 루트 데이터 등록`
3. `[FEATURE][UI][TEST] Daily Plan 보드와 선택 패널 추가`
4. `[FEATURE][UI][TEST] Daily Plan Objective Journal 연동`
5. `[FEATURE][UI][TEST] Daily Plan 하루 결과와 E2E 검증 추가`

