# Goal: Discovery Clue Unlocks A Daily Plan Route

Paste this prompt into a goal session and execute it as written.

## Objective

Implement the first Discovery Clue -> Daily Plan route unlock slice from:

- `docs/superpowers/specs/2026-05-20-discovery-clue-daily-plan-route-unlock-design.md`

The player must be able to complete a visible clue interpretation in Town, activate data-driven world/knowledge state, see a new Daily Plan route become available from that state, select the route through the Daily Plan board, see Objective Journal guidance, and reload the save with no duplicate clue, interpretation, route, hint, or tracked HUD state.

This is a focused feature slice. Do not add house rewards, NPC relationship milestones, new permanent HUDs, dedicated-server validation, or broad Daily Plan redesign work.

## Non-Negotiable Constraints

- All route unlock behavior must be ScriptableObject/data-driven.
- Do not branch production C# on specific clue, interpretation, route, location, activity, world-state, NPC, quest, or career IDs.
- Do not create a separate unlocked-route save list in the first slice. The unlock source of truth is `WorldStateProgress` plus route requirements.
- `ObjectiveJournalPanel` owns route guidance. `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not create a new permanent HUD, floating route panel, duplicate Canvas objective widget, or source-specific tracked widget.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools, not direct filesystem writes.
- Every behavior change follows TDD: failing test first, then implementation, then regression verification.
- Visible or interactive changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence.
- Internal method calls, forced state setup, direct runner calls, JSON-only checks, or asset-name checks do not replace player-facing PlayMode verification.
- Keep unrelated dirty files untouched and unstaged:
  - `D Assets/AddressableAssetsData/link.xml`
  - `D Assets/AddressableAssetsData/link.xml.meta`
  - `M production/qa/evidence/registry-provider-town-flow.png`

## Required Skills

Use these skills in order where applicable:

- `superpowers:executing-plans` or `superpowers:subagent-driven-development`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`
- `superpowers:finishing-a-development-branch` only after all work and verification are complete

## Required References

Read before modifying code or assets:

- `docs/superpowers/specs/2026-05-20-discovery-clue-daily-plan-route-unlock-design.md`
- `docs/superpowers/audits/2026-05-20-feature-development-inventory-strict-refresh.md`
- `.claude/rules/testing-discipline.md`
- `.claude/rules/objective-journal-ui.md`
- `.claude/rules/path-based/assets-data.md`
- `.claude/rules/ui-standards.md`
- `.claude/rules/game-design.md`

## Track 1: Daily Plan Route Context And World-State Requirement

### Purpose

Let Daily Plan route availability read world/clue/interpretation state without hardcoded route IDs or a parallel unlock store.

### Required Work

Modify focused runtime code under `Assets/Scripts/Game/StudentLife/`:

- Add `DailyPlanRouteContext`.
- Add context-aware availability overloads:
  - `DailyPlanRouteDefinition.IsAvailable(in DailyPlanRouteContext context)`
  - `DailyPlanRouteSetDefinition.GetAvailableRoutes(in DailyPlanRouteContext context)`
  - `DailyPlanRouteRunner.GetAvailableRoutes(DailyPlanRouteSetDefinition[] routeSets, in DailyPlanRouteContext context)`
  - `DailyPlanRouteRunner.TrySelectRoute(DailyPlanProgress dailyProgress, DailyPlanRouteDefinition route, in DailyPlanRouteContext context)`
- Keep compatibility overloads that accept `StudentLifeProgress`.
- Update `DailyPlanRouteRequirementBase` so requirements can evaluate `DailyPlanRouteContext`.
- Update existing route requirement implementations to use `context.StudentProgress`.
- Add `DailyPlanWorldStateRequirement`.

### Required TDD

Write failing EditMode tests before production code:

- `DailyPlanWorldStateRequirement_ReturnsFalseWithoutWorldStateProgress`
- `DailyPlanWorldStateRequirement_ReturnsTrueWhenFlagActive`
- `DailyPlanRouteRunner_GetAvailableRoutes_UsesContextRequirements`
- `DailyPlanRouteRunner_TrySelectRoute_RejectsLockedWorldStateRoute`
- `DailyPlanRouteRunner_TrySelectRoute_AcceptsUnlockedWorldStateRoute`
- compatibility test proving existing `StudentLifeProgress`-only route requirements still work.

Expected RED:

- tests fail because `DailyPlanRouteContext` or `DailyPlanWorldStateRequirement` does not exist, or because runner overloads ignore world state.

Expected GREEN:

- `Rootborn.Tests.EditMode.StudentLife` passes.

### Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Commit

Commit with:

```text
[FEATURE][TEST] Daily Plan route context 추가
```

## Track 2: Objective Journal Route Hint Source

### Purpose

Show unlocked Daily Plan route guidance in Objective Journal `Hints` without creating another HUD or mixing selected route `Goals` behavior with route-unlock hint behavior.

### Required Work

Prefer adding a focused source:

- `Assets/Scripts/UI/Objectives/Sources/DailyPlanRouteHintObjectiveSource.cs`

Register it in `ObjectiveJournalSourceCatalog` if a new source is added.

Source behavior:

- Source id: `daily-plan-route-hints`
- Category: `Hints`
- Reads route sets from `GameDataRegistry`.
- Builds a full `DailyPlanRouteContext` from the active player/save context.
- Emits one hint item for available but unselected routes.
- Stable key: `daily-plan-route-hint:<route.Id>`
- Title/body/hint from route `ObjectiveTitleKey`, `ObjectiveDetailKey`, and `NextHintKey`.
- If context or registry is missing, returns no items and does not throw.

Do not modify `TrackedObjectiveHud` ownership or create any permanent UI surface.

### Required TDD

Write failing EditMode tests before source implementation:

- `DailyPlanRouteHintObjectiveSource_ExposesExpectedSourceIdAndCategory`
- `DailyPlanRouteHintObjectiveSource_DoesNotEmitHintForLockedRoute`
- `DailyPlanRouteHintObjectiveSource_EmitsHintForUnlockedUnselectedRoute`
- `DailyPlanRouteHintObjectiveSource_DoesNotEmitSelectedRouteAsHint`
- `ObjectiveJournalSourceCatalog_DefaultSourcesContainsDailyPlanRouteHintsInDeterministicOrder`
- `ObjectiveJournalProvider_ComposesDailyPlanRouteHintIntoHintsCategory`

Expected RED:

- tests fail because the source/catalog entry does not exist or because unlocked route hints are not composed.

Expected GREEN:

- `Rootborn.Tests.EditMode.UI.Objectives` passes.

### Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Commit

Commit with:

```text
[FEATURE][UI][TEST] Daily Plan route hint source 추가
```

## Track 3: Data Assets And Registry Wiring

### Purpose

Wire exactly one real route unlock through existing clue interpretation world-state outcome and Daily Plan route data.

### Required Work

Create or update assets under existing data folders:

- `Assets/Data/WorldState/`
  - one `WorldStateFlagDefinition` for the route unlock.
- `Assets/Data/DiscoveryClues/`
  - one clue or existing registered play clue suitable for Town.
  - one `ClueInterpretationDefinition` with visible choice text and `ClueInterpretationWorldStateOutcome`.
- `Assets/Data/StudentLife/DailyPlans/`
  - one `DailyPlanRouteDefinition` gated by `DailyPlanWorldStateRequirement`.
  - route references an existing reachable `LocationDefinition`.
  - route references at least one existing reachable `LocationActivityDefinition` or matches by `TargetLocation + GrowthRoute`.
  - route text fields are non-empty and suitable for Objective Journal.
- `Assets/Data/Registry/GameDataRegistry.asset`
  - register the world state flag, clue/interpretation, route, and route set references.

Use Unity asset APIs or existing project asset factory/test helpers where appropriate. Include `.meta` files for new assets.

### Required TDD

Add or tighten registry/data tests before asset wiring:

- route unlock world-state flag exists in registry.
- clue interpretation outcome references the same flag used by the route requirement.
- locked route is unavailable before interpretation/world-state activation.
- route is available after activating the registered flag.
- unlocked route has a reachable target location and matching activity.
- route has Objective Journal title/detail/next hint text.
- registered clue/interpretation can be found without `Resources.Load` fallback.

Expected RED:

- tests fail because the registered assets or matching references do not exist.

Expected GREEN:

- relevant EditMode tests pass after asset wiring.

### Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.DiscoveryClues
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Commit

Commit with:

```text
[ASSET][TEST] Discovery route unlock 데이터 연결
```

## Track 4: Strict Player-Facing PlayMode E2E

### Purpose

Prove the feature through the actual player path, not direct method calls or saved JSON only.

### Required PlayMode Test

Create:

`Assets/Tests/PlayMode/EndToEnd/DiscoveryRouteUnlockE2EScenarioTests.cs`

Method:

`Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload`

### Required Scenario

The test must:

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Wait for player, clue/interpretion runtime, Daily Plan board, Objective Journal, tracked HUD, provider, and EventSystem.
3. Use actual keyboard movement to reach the named clue source or interpretation point.
4. Press interact key.
5. Click the visible clue interpretation choice button.
6. Verify visible clue/interpretation UI text in Game View.
7. Inspect runtime state after the user action:
   - clue record seen/completed as applicable,
   - interpretation record completed,
   - world state flag active.
8. Move to the Daily Plan board using keyboard input.
9. Press interact key.
10. Verify Daily Plan panel shows the newly unlocked route.
11. Click the unlocked route row/button.
12. Press Tab.
13. Verify Objective Journal shows route guidance through `Goals` or `Hints`.
14. Click Track on the route guidance row if needed and verify `TrackedObjectiveHud` still has exactly one tracked objective.
15. Capture screenshot:
    - `Builds/Logs/discovery-route-unlock-e2e.png`
16. Clear active context.
17. Load the same save slot through SaveSlot UI.
18. Verify world state flag and route availability restore.
19. Verify clue and interpretation records were not duplicated.
20. Verify Objective Journal still shows the route guidance without duplicate tracked HUDs.

Forbidden shortcuts:

- Directly calling `ClueInterpretationRunner.TryComplete` as the only PlayMode proof.
- Directly setting world-state active or route selected state.
- Passing only by checking save JSON.
- Inspecting route availability without opening the player-facing Daily Plan board.
- Skipping Game View or camera screenshot evidence.

### Required Runtime Inspection

The PlayMode test must inspect:

- selected save slot/player id,
- clue id and interpretation id used by the registered feature,
- interpretation completion record count before/after duplicate attempt or reload,
- world state active flag,
- Daily Plan visible route row text,
- selected route id after UI click,
- Objective Journal visible text snapshot,
- tracked HUD count and visible text,
- screenshot file length.

### Verification

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Commit

Commit with:

```text
[TEST] Discovery route unlock PlayMode 검증 추가
```

## Full Goal Verification

After all tracks are complete, run:

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
```

Verify screenshots:

```powershell
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png,Builds/Logs/daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Expected:

- all listed tests pass,
- screenshots exist and have non-zero length,
- no static gate errors,
- no production entity ID-specific branching,
- no new permanent objective HUD or duplicate tracked HUD,
- unrelated dirty files remain untouched and unstaged.

## Done Means

- Discovery clue interpretation unlocks one Daily Plan route through world state.
- Daily Plan route availability is data/context-driven.
- Objective Journal shows route guidance without a new HUD.
- Save/load restores clue, interpretation, world state, route availability, route selection, and Journal guidance without duplication.
- The strict PlayMode scenario uses real keyboard movement, interact key, visible UI clicks, SaveSlot reload, and screenshot evidence.
- Work is split into small reviewable commits.
- No unrelated dirty files are staged, reverted, or modified.
