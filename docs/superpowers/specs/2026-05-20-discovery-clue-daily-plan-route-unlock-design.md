# Discovery Clue Daily Plan Route Unlock Design

Date: 2026-05-20

## Purpose

Add a focused player-facing feature slice where exploration produces a concrete next action:

1. the player discovers or reveals a clue in Town,
2. the player completes a visible clue interpretation choice,
3. that interpretation activates data-driven world/knowledge state,
4. a new Daily Plan route becomes available from that state,
5. Objective Journal `Hints` points the player to the unlocked route,
6. save/load restores the clue, interpretation, route availability, and hint without duplication.

This is not a new quest UI, not a new persistent HUD, and not a new route-specific C# branch. It connects existing Discovery Clue, World State, Daily Plan, and Objective Journal systems through ScriptableObject definitions and strategy arrays.

## Current Code Reality

Relevant systems already exist:

- `DiscoveryClueDefinition`, `DiscoveryClueRunner`, `DiscoveryClueProgress`, `DiscoveryClueProgressPersistence`, `DiscoveryClueSourcePanel`, and `DiscoveryClueInteractor`.
- `ClueInterpretationDefinition`, `ClueInterpretationRunner`, `ClueInterpretationProgress`, `ClueInterpretationProgressPersistence`, `ClueInterpretationChoicePanel`, and `ClueInterpretationInteractor`.
- `ClueInterpretationWorldStateOutcome` can activate `WorldStateFlagDefinition` entries from interpretation completion.
- `ClueInterpretationWorldStateActiveCondition` can gate interpretations by world state.
- `DailyPlanRouteDefinition`, `DailyPlanRouteSetDefinition`, `DailyPlanProgress`, `DailyPlanProgressComponent`, `DailyPlanRouteRunner`, `DailyPlanPanel`, and `DailyPlanBoardInteractor`.
- `DailyPlanObjectiveSource` exposes selected Daily Plan route state in Objective Journal `Goals`.
- `ObjectiveJournalPanel` owns `Goals`, `Quests`, `Campaign`, and `Hints`; `TrackedObjectiveHud` is the only always-visible objective HUD.
- Existing E2E coverage proves clue reveal, clue interpretation, Daily Plan route selection, Objective Journal tracking, and save/load independently.

Important gap:

- `DailyPlanRouteRequirementBase` currently receives only `StudentLifeProgress` and `DailyPlanRouteDefinition`. It cannot directly inspect `WorldStateProgress` or `ClueInterpretationProgress`.
- Therefore the route unlock needs a small route-availability context extension rather than hardcoded route IDs or a parallel unlock store.

## Design Options Considered

### Option A: Route Requirement Context Reads World State

Extend Daily Plan route availability evaluation so requirements can read a route context containing `StudentLifeProgress`, `WorldStateProgress`, `DiscoveryClueProgress`, and `ClueInterpretationProgress`. Add a `DailyPlanWorldStateRequirement` that checks a referenced `WorldStateFlagDefinition`.

Pros:
- Keeps route unlock data-driven.
- Reuses existing `ClueInterpretationWorldStateOutcome`.
- Avoids route ID branching.
- Lets future route requirements read clue/knowledge state without adding new global stores.

Cons:
- Requires changing the Daily Plan route requirement API carefully while preserving existing requirement implementations.

### Option B: Interpretation Outcome Writes Directly To Daily Plan Progress

Add a clue interpretation outcome that writes an unlocked route ID into `DailyPlanProgress`, then Daily Plan reads that list.

Pros:
- Direct and simple for one route.

Cons:
- Couples DiscoveryClues to DailyPlan route IDs.
- Adds another persistence field that can drift from existing WorldState/Knowledge truth.
- Higher risk of entity ID-specific branching and duplicate unlock state.

### Option C: Objective Journal Hint Only, No Actual Route Availability

After interpretation, only show a hint in Objective Journal telling the player to use an existing route.

Pros:
- Smallest implementation.

Cons:
- Does not deliver the core player value: a new route opening from discovery.
- The Daily Plan board would not visibly change.

Recommended: **Option A**.

## Architecture

Introduce a small `DailyPlanRouteContext` value and route availability overloads:

```csharp
public readonly struct DailyPlanRouteContext
{
    public readonly StudentLifeProgress StudentProgress;
    public readonly WorldStateProgress WorldStateProgress;
    public readonly DiscoveryClueProgress DiscoveryClueProgress;
    public readonly ClueInterpretationProgress InterpretationProgress;
}
```

Daily Plan route availability should flow through context-aware methods:

- `DailyPlanRouteDefinition.IsAvailable(in DailyPlanRouteContext context)`
- `DailyPlanRouteSetDefinition.GetAvailableRoutes(in DailyPlanRouteContext context)`
- `DailyPlanRouteRunner.GetAvailableRoutes(DailyPlanRouteSetDefinition[] routeSets, in DailyPlanRouteContext context)`
- `DailyPlanRouteRunner.TrySelectRoute(DailyPlanProgress dailyProgress, DailyPlanRouteDefinition route, in DailyPlanRouteContext context)`

Keep compatibility overloads that accept `StudentLifeProgress` only. They should construct a context with just `StudentProgress` so existing tests and simple requirements keep working.

Add a context-aware requirement base:

```csharp
public abstract class DailyPlanRouteRequirementBase : ScriptableObject
{
    public virtual bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route)
        => IsSatisfied(new DailyPlanRouteContext(progress), route);

    public abstract bool IsSatisfied(in DailyPlanRouteContext context, DailyPlanRouteDefinition route);
}
```

Existing requirement classes move their logic into the context overload and use `context.StudentProgress`.

Add one new requirement:

- `DailyPlanWorldStateRequirement`
  - serialized `WorldStateFlagDefinition _flag`
  - serialized `bool _expectedActive = true`
  - returns `context.WorldStateProgress != null && context.WorldStateProgress.IsActive(_flag) == _expectedActive`

This keeps route unlock logic data-driven. The clue interpretation outcome activates the world state flag; the route requirement reads that flag.

## Data Flow

Target flow:

1. Registered clue interpretation has `ClueInterpretationWorldStateOutcome` referencing `world.discovery.route.library-archive` or equivalent.
2. Player completes the interpretation through `ClueInterpretationChoicePanel` or `ClueInterpretationInteractor`.
3. `ClueInterpretationRunner.TryComplete` applies the outcome and saves:
   - `ClueInterpretationProgress`
   - `DiscoveryClueProgress`, if the flow reveals a clue first
   - `WorldStateProgress`
4. Daily Plan board opens and builds route availability with a full `DailyPlanRouteContext`.
5. A route with `DailyPlanWorldStateRequirement` becomes visible.
6. Player selects that route through `DailyPlanPanel`.
7. Objective Journal refreshes:
   - `Goals` shows selected Daily Plan route through `DailyPlanObjectiveSource`.
   - `Hints` shows an unlocked-route hint through a new source or an existing source extension.

Preferred Objective Journal hint implementation:

- Add a focused `DailyPlanRouteHintObjectiveSource` under `Assets/Scripts/UI/Objectives/Sources/`, or extend `DailyPlanObjectiveSource` only if it can remain simple.
- The source reads available-but-not-selected routes from the same route context.
- It emits one stable hint item per unlocked route:
  - stable key: `daily-plan-route-hint:<route.Id>`
  - category: `Hints`
  - title: route `ObjectiveTitleKey`
  - body: route `ObjectiveDetailKey`
  - next hint: route `NextHintKey`
- It must not create a new HUD or panel.

If extending the existing source would mix selected route `Goals` state with route-unlock `Hints`, create the new source. That keeps source responsibilities small.

## Data Assets

Create or update assets under existing data folders:

- `Assets/Data/WorldState/...`
  - one `WorldStateFlagDefinition` for the unlocked route state.
- `Assets/Data/DiscoveryClues/...`
  - one clue or existing registered play clue source suitable for Town.
  - one `ClueInterpretationDefinition` with:
    - visible interpretation choice text,
    - existing clue-seen condition if needed,
    - `ClueInterpretationWorldStateOutcome`.
- `Assets/Data/StudentLife/DailyPlans/...`
  - one `DailyPlanRouteDefinition` for the new route.
  - route requirement array includes `DailyPlanWorldStateRequirement`.
  - route target points to an existing `LocationDefinition` and matching `LocationActivityDefinition`.
  - route text fields provide Objective Journal title/detail/next hint.
- `Assets/Data/Registry/GameDataRegistry.asset`
  - register new/updated clue, interpretation, world state flag, route, and route set references.

The first slice should use one route only. It should target an existing Town activity already reachable in PlayMode, such as Library self-study or another existing location activity. Do not create a new scene, new permanent UI, or new route category system.

## Player Flow

Strict player-facing flow:

1. Player starts at MainMenu.
2. Player clicks New Game through SaveSlot UI.
3. Player confirms SPUM character creation.
4. Player enters Town.
5. Player uses keyboard movement to reach the clue source or clue interpretation point.
6. Player presses interact key.
7. Player clicks the visible clue/interpretation choice button.
8. Game View shows clue/interpretation text confirming the action.
9. Player moves to the Daily Plan board.
10. Player presses interact key.
11. Daily Plan panel shows the newly unlocked route.
12. Player selects the unlocked route through the visible button.
13. Player opens Objective Journal with Tab.
14. Objective Journal shows the route guidance without adding any new HUD.
15. Player performs the matching activity.
16. Player ends the day.
17. Save/load restores the same route availability and does not duplicate clue or interpretation records.

## UI Ownership

- Clue reveal remains owned by `DiscoveryClueSourcePanel` or existing clue interactor UI.
- Interpretation choice remains owned by `ClueInterpretationChoicePanel`.
- Route selection remains owned by `DailyPlanPanel`, opened by `DailyPlanBoardInteractor`.
- Goal-like guidance remains owned by `ObjectiveJournalPanel`.
- Always-visible objective guidance remains only `TrackedObjectiveHud`, one tracked objective maximum.
- No new Canvas-level permanent objective widget is allowed.

## Persistence And Idempotency

Save/load must prove:

- The clue reveal/completion record is saved once.
- The interpretation completion record is saved once.
- The world state flag is active after reload.
- The route appears after reload because its requirement reads saved world state.
- Selecting the route saves Daily Plan progress once.
- Re-clicking the same interpretation or reloading does not duplicate records, outcomes, hints, or tracked HUD state.

Do not persist a separate "unlocked route IDs" list in the first slice. The source of truth is the world state flag and the route requirement.

## Error Handling

- If `WorldStateProgress` is missing, `DailyPlanWorldStateRequirement` returns false for routes requiring active world state.
- If registry data is missing, Daily Plan falls back to existing route behavior and does not throw.
- If the route is available but no matching activity exists, EditMode registry tests must fail before PlayMode.
- If Objective Journal source context cannot resolve route context, it returns no hint items rather than creating a broken row.
- Duplicate interpretation completion returns the existing idempotent result and does not reapply world state or route side effects.

## Performance

- Route availability should be computed when Daily Plan panel opens, Objective Journal refreshes, or relevant progress changes.
- Do not evaluate route requirements in `Update()`.
- Objective Journal hint source should cap initial output to route rows from registered route sets and should not scan unrelated asset folders.
- As route count grows, Daily Plan panel row list must remain bounded/scrollable as already required by UI standards.

## Testing Strategy

### EditMode

Add or tighten tests before implementation:

- `DailyPlanWorldStateRequirement_ReturnsFalseWithoutWorldStateProgress`
- `DailyPlanWorldStateRequirement_ReturnsTrueWhenFlagActive`
- `DailyPlanRouteRunner_GetAvailableRoutes_UsesContextRequirements`
- `DailyPlanRouteRunner_TrySelectRoute_RejectsLockedWorldStateRoute`
- `DailyPlanRouteRunner_TrySelectRoute_AcceptsUnlockedWorldStateRoute`
- `DailyPlanRouteHintObjectiveSource_EmitsHintForUnlockedUnselectedRoute`
- `DailyPlanRouteHintObjectiveSource_DoesNotEmitHintForLockedRoute`
- registry wiring test proving the clue interpretation outcome flag and route requirement flag reference the same `WorldStateFlagDefinition`
- registry wiring test proving the unlocked route has at least one reachable matching activity

### PlayMode

Create:

`Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload`

Required steps:

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Wait for player, clue/interpretion runtime, Daily Plan board, Objective Journal, tracked HUD, and EventSystem.
3. Use actual keyboard movement to reach the named clue source or interpretation point.
4. Press interact key.
5. Click the visible interpretation choice button.
6. Verify visible clue/interpretation UI text in Game View.
7. Inspect runtime state:
   - clue record seen/completed as applicable,
   - interpretation record completed,
   - world state flag active.
8. Move to Daily Plan board using keyboard input.
9. Press interact key.
10. Verify Daily Plan panel shows the unlocked route.
11. Click the unlocked route row/button.
12. Press Tab and verify Objective Journal shows either the selected route in `Goals` or the route hint in `Hints`.
13. Capture screenshot:
   - `Builds/Logs/discovery-route-unlock-e2e.png`
14. Clear active context.
15. Load the same save slot through SaveSlot UI.
16. Verify the world state flag and route availability restore.
17. Verify clue and interpretation records were not duplicated.
18. Verify Objective Journal still shows the route guidance without duplicate tracked HUDs.

Forbidden shortcuts:

- Do not directly call `ClueInterpretationRunner.TryComplete` as the only PlayMode proof.
- Do not manually set route selected state.
- Do not pass only by checking JSON files.
- Do not inspect route availability without opening the player-facing Daily Plan board.

## Static Gates

Run in the implementation goal:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.DiscoveryClues
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ClueInterpretationInteractorE2EScenarioTests.CLUE_INTERPRET_E2E_012_PlayerMovesToInterpretationInteractorAndCompletesViaPromptAndChoiceButton
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png | Select-Object Name,Length,LastWriteTime
```

Expected:

- all listed tests pass,
- screenshot exists and has non-zero length,
- no entity ID-specific branching is introduced,
- unrelated dirty files remain untouched unless explicitly requested.

## Implementation Slice

Keep the first implementation slice narrow:

1. Add context-aware Daily Plan route requirements.
2. Add `DailyPlanWorldStateRequirement`.
3. Add Objective Journal route hint source only if needed for `Hints` ownership.
4. Wire one route unlock via existing clue interpretation world-state outcome.
5. Add strict EditMode and PlayMode coverage.

Do not add:

- multi-step route trees,
- a route unlock inventory,
- new permanent HUDs,
- new dedicated server validation,
- house/interior rewards,
- NPC relationship milestones,
- broad Daily Plan UI redesign.

## Open Decisions Locked For Implementation

- Source of truth for unlock: `WorldStateProgress`.
- First unlock mechanism: `ClueInterpretationWorldStateOutcome`.
- First route gate: `DailyPlanWorldStateRequirement`.
- First UI proof: Daily Plan board plus Objective Journal.
- First screenshot path: `Builds/Logs/discovery-route-unlock-e2e.png`.
- Recommended next implementation goal name: `Discovery Clue Unlocks A Daily Plan Route`.
