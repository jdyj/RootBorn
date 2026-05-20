# Daily Plan Route Selection Design

Date: 2026-05-20

## Purpose

Add a player-facing daily plan route slice that connects the existing Student Life activity loop into one readable day flow:

1. choose a plan for today,
2. perform a real Town activity matching that plan,
3. end the day,
4. see the plan/action/result in the day result UI,
5. see the next hint in Objective Journal,
6. reload the save and keep the selected plan/result context.

This is not a new quest UI, not a new permanent HUD, and not a school-only path. It is a thin planning layer over the existing data-driven Student Life systems.

## Current Code Reality

Relevant existing systems:

- `StudentLifeProgress` already owns day state, today activity ids, today result logs, previous day result logs, next objective id, and next guide text.
- `LocationActivityDefinition` and `LocationIdentityDefinition` already model Town locations and growth routes.
- `LocationActivityRunner` already applies activity results and writes result log ids.
- `StudentDayResultPanel` already shows end-of-day results.
- `ObjectiveJournalPanel` and `TrackedObjectiveHud` now own goal-like player guidance.
- `StudentDayGuideObjectiveSource` already exposes the current day objective in the `Goals` category.
- Data exists for Library, Town Square, School, Store, Workshop, outside-school growth, campaign routes, milestones, career hints, and location activities.

Relevant constraints:

- All route definitions must be ScriptableObject/data-driven.
- No route id or activity id branching in production C#.
- No new always-visible objective HUD.
- Visible UI changes require real PlayMode user-flow verification plus Game View screenshot.
- C# files under `Assets/**/*.cs` must be edited only through Unity MCP script tools.

## Design Options Considered

### Option A: Minimal Plan State Over Existing Activities

Create daily plan route definitions and a small progress/persistence layer. The plan references existing locations, activity growth routes, and Objective Journal text. Existing location activity interactions satisfy the plan when their data matches the selected plan.

Pros:
- Smallest implementation.
- Reuses existing data and PlayMode flows.
- Keeps Objective Journal as the guidance owner.
- Avoids new activity mechanics.

Cons:
- The first version is a planning/feedback layer, not a full calendar system.

### Option B: Full Daily Planner UI With Time Blocks

Add a schedule planner with multiple time slots, route ordering, and full-day preview.

Pros:
- More expressive long-term.

Cons:
- Too large for the next slice.
- Risks duplicating campaign/day-result responsibilities.
- Requires more UI, persistence, and balancing before core value is proven.

### Option C: Campaign-Only Route Selection

Reuse campaign route selection and surface it more strongly as the daily plan.

Pros:
- Uses existing campaign data.

Cons:
- Makes the feature feel campaign-bound instead of a general daily-life system.
- Does not solve non-campaign daily growth planning.

Recommended: **Option A**.

## Player Flow

First target flow:

1. Player starts a new game and enters Town.
2. Player opens the daily plan UI from an actual player-facing input or Town board prompt.
3. UI shows at least two data-defined routes:
   - a non-school study/growth route such as Library self-study,
   - a relationship/help/work route such as Town Square help or Workshop shift.
4. Player selects one route.
5. Objective Journal `Goals` updates to show the selected plan and the target action.
6. Player walks to the route's target Town location using keyboard input.
7. Player interacts with the existing location prompt and applies a visible activity button or prompt path.
8. The matching activity marks the plan as progressed/completed.
9. Player ends the day at the day-end board.
10. Day result UI shows:
    - selected plan,
    - actual completed action,
    - changed stat/result logs,
    - next hint.
11. Objective Journal shows the next plan hint after the result.
12. Reloading the save slot restores plan completion/result context without duplication.

## Data Model

Add new ScriptableObject definitions under `Assets/Scripts/Game/StudentLife/` and instances under `Assets/Data/StudentLife/DailyPlans/`.

### `DailyPlanRouteDefinition`

Represents one selectable daily plan route.

Fields:

- `Id`
- `DisplayNameKey`
- `DescriptionKey`
- `CategoryKey`
- `Priority`
- `LocationDefinition TargetLocation`
- `LocationGrowthRoute GrowthRoute`
- `LocationActivityDefinition[] MatchingActivities`
- `MilestoneRouteDefinition[] RelatedMilestoneRoutes`
- `CareerCandidateRouteDefinition[] RelatedCareerRoutes`
- `DailyPlanRouteRequirementBase[] Requirements`
- `DailyPlanRouteOutcomeBase[] Outcomes`
- `ObjectiveTitleKey`
- `ObjectiveDetailKey`
- `NextHintKey`

Rules:

- Matching uses object references and data fields, not hardcoded route/activity ids.
- A route may match by explicit `MatchingActivities` or by `TargetLocation + GrowthRoute`.
- Requirements and outcomes are SO strategy arrays.
- First slice should ship two or three routes only.

### `DailyPlanRouteSetDefinition`

Groups routes available for a day or progression context.

Fields:

- `Id`
- `DisplayNameKey`
- `DailyPlanRouteDefinition[] Routes`
- `int MaxSelectionsPerDay` default 1
- optional requirement strategy array for the set

The first implementation can register one default route set in `GameDataRegistry`.

### Progress / Save Data

Add `DailyPlanProgress` and persistence data either as a dedicated save file or an extension of the existing Student Life save path. Prefer a dedicated file if it keeps `StudentLifeProgress` from growing too much.

Tracked values:

- save slot
- player id
- current day
- selected route id
- selected route stable key
- selected timestamp/day sequence
- completed route id for the day
- matched activity id
- result log ids already applied to plan summary
- last generated next hint key

Idempotency:

- Selecting the same route again on the same day is a no-op.
- Completing the same route from the same activity request cannot duplicate result logs.
- Reloading after day end must not reapply route outcomes.

## Runtime Components

### `DailyPlanProgressComponent`

Attach to the player from `StudentDayRuntimeInstaller` or a focused `DailyPlanRuntimeInstaller`.

Responsibilities:

- Ensure/load progress for active save slot/player.
- Expose selected route and completion state.
- Save after selection and completion.
- Publish an event when selection or completion changes.

### `DailyPlanRouteRunner`

Pure domain service.

Responsibilities:

- Determine available routes from registry/progress.
- Select a route if requirements pass.
- Match an applied `LocationActivityResult` to the selected route.
- Produce a summary for day result and Objective Journal.

This service should be EditMode-testable without scene objects.

### `DailyPlanPanel`

Player-facing selection panel.

Responsibilities:

- Show available routes.
- Let player select one route through real UI buttons.
- Show selected/current state.

UI constraints:

- Use Modern UI Style2 panel rules.
- Do not create a new permanent HUD.
- The panel can be opened from an interactable Town board or from an existing daily planning input, but it must not compete with `Tab = Objective Journal`.
- Route rows must be scrollable or bounded if data grows.

### `DailyPlanBoardInteractor`

Town scene interactable, similar to day-end board or location interactors.

Responsibilities:

- Expose a prompt such as `[E] Plan Day`.
- Open `DailyPlanPanel`.
- Use actual `PlayerInteractionRouter` path in PlayMode tests.

### Objective Journal Integration

Extend `StudentDayGuideObjectiveSource` or add a focused `DailyPlanObjectiveSource` under `Assets/Scripts/UI/Objectives/Sources/`.

Preferred first slice: extend `StudentDayGuideObjectiveSource` only if it remains small. If it becomes mixed, add `DailyPlanObjectiveSource` with source id `daily-plan`.

Journal behavior:

- Before selection: show "Choose today's route" in `Goals`.
- After selection: show selected route and target location/action.
- After matching activity: show completion state and next day-end hint.
- After day end: show next route suggestion.

Tracked HUD behavior:

- `TrackedObjectiveHud` can show the selected daily plan only when the player tracks it from Objective Journal.
- No separate plan HUD is allowed.

## Day Result Integration

`StudentDayResultPanel` should include a compact daily plan summary:

- planned route display name,
- actual matched activity display name,
- completion state,
- key stat/relationship/status/career result logs,
- next hint.

The panel must not become the planner. It only reports what happened.

## Save / Load

Save boundaries:

- plan selection saved immediately after route selection,
- route completion saved immediately after matching activity,
- day result state saved through existing Student Life day-end persistence,
- reload restores selected/completed route state before Objective Journal refresh.

Expected files:

- Either `daily-plan-progress.json` under the active save slot, or a clearly named section in student progress save.
- The implementation plan should choose one after inspecting current persistence helpers.

## Performance And Scaling

First version is small, but route lists can grow.

Requirements:

- Build available route list on demand or dirty refresh, not every `Update()`.
- No repeated full registry scans per frame.
- Cache route availability for the current day until progress/time changes.
- UI rows must be scrollable or bounded.
- Avoid runtime string allocation loops in `Update()`.

## Error Handling

- If no route set exists, the plan panel shows an explicit empty state and Objective Journal falls back to the current Student Day Guide.
- If a saved selected route is missing from registry, ignore the missing route, show "Choose today's route", and do not crash.
- If the player completes an activity without selecting a plan, no plan completion is recorded, but normal activity progress continues.
- If selected route requirements become invalid before completion, keep the selected route visible and mark it blocked until the next day rather than silently switching.

## Tests

### EditMode

Add or extend tests under `Assets/Tests/EditMode/StudentLife/` and `Assets/Tests/EditMode/UI/Objectives/`.

Required tests:

- route set returns available routes from SO data without id branching,
- route selection persists selected route for active save slot/player/day,
- duplicate route selection is idempotent,
- matching location activity completes the selected route,
- non-matching activity does not complete the selected route,
- completion does not duplicate after save/load,
- Objective Journal source shows before-selection, selected, completed, and next-hint states,
- no new always-visible objective HUD is introduced.

### PlayMode

Required player-facing E2E:

Scenario: Daily plan route survives real player day flow

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Walk to the daily plan board with keyboard input.
3. Interact with `[E] Plan Day`.
4. Select a non-school growth route through a visible UI button.
5. Open Objective Journal with Tab.
6. Verify `Goals` shows the selected route and target action.
7. Track the route from Objective Journal.
8. Close Journal.
9. Walk to the target Town location with keyboard input.
10. Interact with the location prompt.
11. Apply the matching activity through the visible UI button or prompt path.
12. Reopen Objective Journal.
13. Verify the tracked route shows completed/progressed state and did not jump to another row.
14. Walk to day-end board.
15. Interact with day-end board.
16. Verify result UI shows planned route, actual action, changed stat/result log, and next hint.
17. Capture Game View screenshot.
18. Load the same save slot.
19. Verify Objective Journal/tracked HUD restores the next route context.

Evidence:

- `Builds/Logs/daily-plan-route-e2e.png`
- EditMode and PlayMode test output.
- `git diff --check`
- `Scripts/ci/check-no-entity-id-branching.sh`

## Implementation Order

1. Add failing EditMode tests for route definition, progress, matching, and persistence.
2. Add minimal domain model and progress persistence.
3. Add Objective Journal source tests and integration.
4. Add Daily Plan panel and board tests.
5. Add player-facing PlayMode E2E.
6. Implement UI and runtime installers.
7. Run full relevant regression:
   - `Rootborn.Tests.EditMode.StudentLife`
   - `Rootborn.Tests.EditMode.UI.Objectives`
   - daily plan PlayMode E2E
   - Objective Journal Town E2E
   - Student Day Result E2E
8. Capture screenshot evidence.

## Non-Goals

- No multi-slot calendar.
- No hourly schedule planner.
- No new permanent objective HUD.
- No school-only route funnel.
- No hardcoded route/activity/career id branching.
- No dedicated server expansion in this slice.
- No housing/interior reward unlock in this slice, except as a future hint.

## Open Decisions For Implementation Plan

The implementation plan must choose:

- whether daily plan progress is a new save file or part of Student Life progress save,
- whether the first Objective Journal integration extends `StudentDayGuideObjectiveSource` or adds `DailyPlanObjectiveSource`,
- the exact first two or three route assets to wire in data,
- whether the panel opens from a new `DailyPlanBoardInteractor` or an existing Town board object.

Recommended defaults:

- dedicated `daily-plan-progress.json`,
- focused `DailyPlanObjectiveSource`,
- first routes: Library self-study, Town Square help, Workshop shift,
- new `DailyPlanBoardInteractor` placed in Town by runtime installer for testability.

