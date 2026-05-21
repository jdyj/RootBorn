# Recommended Goal Prompts: After Dedicated Core Matrix

Date: 2026-05-21

This document gives the recommended next goal prompts in execution order from the current workspace state.

## Current State

- Latest committed foundation:
  - `fe5703ed8 [NETWORK][TEST][DOCS] Daily Plan completion dedicated smoke 추가`
  - `9c099ea7c [TEST][CHORE][NETWORK] Dedicated runner artifact freshness 가드 추가`
  - `03c56953f [TEST][CHORE] Addressables link.xml 삭제 가드 강화`
- Current uncommitted work appears to be the Dedicated Core Gameplay Matrix cycle:
  - `Assets/Tests/EditMode/Network/DedicatedCoreGameplayMatrixContractTests.cs`
  - `Assets/Tests/EditMode/Network/DedicatedCoreGameplayMatrixContractTests.cs.meta`
  - `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`
  - `docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-goal.md`
- `Assets/AddressableAssetsData/link.xml` and `.meta` are currently deleted in the working tree and must be restored before any completion claim or commit.
- Existing dedicated validation now covers:
  - route/relationship smoke,
  - career Daily Plan route smoke,
  - Daily Plan completion smoke,
  - runner artifact freshness checks,
  - Addressables `link.xml` preservation.

## Recommended Order

0. Finish and commit Dedicated Core Gameplay Matrix.
1. Discovery Clue Route Unlock Playable Slice.
2. Discovery Route Unlock Dedicated Smoke.
3. Student Life Scenario Registry and Coverage Gate.
4. Objective Journal Source Collision Regression.
5. First Resource/Recipe Town Activity Slice.

Do not start Prompt 1 until Prompt 0 is committed or explicitly blocked with exact test, log, and workspace evidence. Prompt 1 is the strongest next feature slice because it connects existing Discovery Clue, World State, Daily Plan, and Objective Journal systems into one visible player-facing loop.

## Common Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior, regression, QA script, or build-script changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, clue, world-state, recipe, or resource IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.
- Run `git diff --check`, `Scripts/ci/check-addressables-linkxml-preserved.ps1`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 0: Finish Dedicated Core Gameplay Matrix

Paste this prompt first.

### Objective

Finish the already-started Dedicated Core Gameplay Matrix cycle without mixing in new feature work.

### Why First

The matrix files are already uncommitted. Leaving them open while starting feature work would make verification and commits ambiguous.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Restore unintended `AddressableAssetsData/link.xml` deletion if still present.
- Inspect matrix evidence under:

```text
Builds/Logs/network-dedicated-core-gameplay-matrix
```

- Verify that the matrix includes these slices:
  - route/relationship,
  - career Daily Plan route,
  - Daily Plan completion.
- Verify every slice has:
  - action pass marker,
  - observe-only reload marker,
  - screenshot,
  - reload screenshot,
  - save or restored-state evidence,
  - no duplicate-state evidence where applicable,
  - cleanup/process-exit evidence.

### Required Verification

```powershell
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -Recurse -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If evidence is missing, rerun:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7901 -WaitSeconds 120
```

### Acceptance Criteria

- Network contract tests pass.
- Dedicated matrix passes all three slices.
- Evidence files are present and non-zero.
- `link.xml` and `.meta` are restored and preserved.
- Only matrix-cycle files are staged.

### Commit

```text
[NETWORK][TEST][DOCS] Dedicated core gameplay matrix 추가
```

---

## Prompt 1: Discovery Clue Route Unlock Playable Slice

Paste this prompt after Prompt 0 is committed.

### Objective

Implement the first player-facing Discovery Clue to Daily Plan unlock slice: a player discovers a clue in Town, interprets it through visible UI, activates a data-driven world-state flag, unlocks a new Daily Plan route, sees the hint in Objective Journal, and verifies the state after reload.

### Why Second

This is the next highest-value feature slice. It connects systems that already exist instead of adding another isolated mechanic: Discovery Clues, Clue Interpretation, World State, Daily Plan, Objective Journal, and save/load.

### Source Context

- `docs/superpowers/specs/2026-05-20-discovery-clue-daily-plan-route-unlock-design.md`
- `Assets/Scripts/Game/DiscoveryClues/`
- `Assets/Scripts/Game/WorldState/`
- `Assets/Scripts/Game/StudentLife/DailyPlan`
- `Assets/Scripts/UI/ObjectiveJournal`
- `Assets/Tests/PlayMode/EndToEnd/ClueInterpretationLoopE2EScenarioTests.cs`
- `Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteCompletionE2EScenarioTests.cs`

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Extend Daily Plan route availability with a context object that can read:
  - `StudentLifeProgress`,
  - `WorldStateProgress`,
  - `DiscoveryClueProgress`,
  - `ClueInterpretationProgress`.
- Add a data-driven route requirement that checks a referenced `WorldStateFlagDefinition`.
- Keep compatibility for existing route requirements.
- Add or wire ScriptableObject data so one clue interpretation activates one world-state flag and that flag unlocks one Daily Plan route.
- Add Objective Journal hint/source coverage for the newly unlocked route.
- Ensure save/load restores:
  - discovered clue,
  - completed interpretation,
  - active world-state flag,
  - route availability,
  - Objective Journal hint,
  - tracked HUD state without duplication.

### Required TDD

RED:

- Route availability test fails before context-aware requirements exist.
- World-state route requirement test fails before the requirement asset/type exists.
- Registry/wiring test fails before the new clue, interpretation, world-state flag, route, and hint are registered.
- PlayMode E2E fails before the player can unlock the route through the visible clue/interpretation path.

GREEN:

- Existing Daily Plan route tests still pass.
- New context-aware route availability tests pass.
- Player-facing PlayMode E2E passes through movement, interaction, UI click/selection, Objective Journal open, save, reload, and re-open.

### Required PlayMode Scenario

The PlayMode scenario must reproduce the player path:

1. Start from the normal Town playable flow.
2. Move the player to the clue source using player movement or the existing input driver.
3. Trigger the clue source using the real interaction path.
4. Verify the clue source panel is visible.
5. Click the clue reveal or source option through UI.
6. Open the interpretation UI through the visible path.
7. Select the interpretation option through UI.
8. Verify the world-state flag is active.
9. Open Daily Plan through the visible board/UI path.
10. Verify the newly unlocked route appears.
11. Open Objective Journal with Tab.
12. Verify a Hint points to the unlocked route.
13. Capture a Game View or Camera screenshot.
14. Save and reload the same slot.
15. Reopen Daily Plan and Objective Journal.
16. Verify no duplicate route rows, hint rows, tracked HUDs, or repeated interpretation rewards.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.DiscoveryClues
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.DiscoveryClueDailyPlanRouteUnlockE2EScenarioTests
Get-ChildItem Builds/Logs/discovery-clue-route-unlock-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Unlock is fully data-driven through ScriptableObject references and strategy arrays.
- No production clue, route, world-state, or location ID branching is introduced.
- The new route is unavailable before interpretation and available after interpretation.
- Objective Journal owns all guidance.
- Save/load restores the unlocked state exactly once.
- Screenshot evidence proves the visible player-facing flow.

### Commit

```text
[FEATURE][TEST] Discovery clue route unlock 추가
```

---

## Prompt 2: Discovery Route Unlock Dedicated Smoke

Paste this prompt after Prompt 1 is committed.

### Objective

Add a dedicated server/client smoke for the Discovery Clue route unlock slice so the new feature is proven outside Editor-only PlayMode.

### Why Third

Prompt 1 adds a player-facing feature. Prompt 2 immediately protects it in the same dedicated process validation layer used for route/relationship and Daily Plan.

### Required Work

- Add a direct validation autoplay installer or extend an existing safe installer with explicit flags:

```text
-directValidationAutoplayDiscoveryRouteUnlock
-directValidationAutoplayDiscoveryRouteUnlockObserveOnly
```

- Add:

```text
Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1
```

- The runner must:
  - use fresh client/server artifact checks,
  - run action and observe-only reload passes,
  - capture screenshots for both passes,
  - assert clue, interpretation, world-state, route, Journal, HUD, and save-state markers,
  - fail on fatal logs, duplicate state, stale artifacts, missing screenshots, missing markers, or leaked processes.

### Required TDD

RED:

- Network contract test fails while the dedicated runner is missing.
- Contract test fails if flags, screenshots, observe-only mode, artifact freshness, cleanup, or duplicate-state assertions are missing.
- Contract test fails if the autoplay installer directly mutates clue/route completion state instead of using the player-facing path.

GREEN:

- Network contract tests pass.
- Dedicated smoke action pass and observe-only reload pass complete.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1 -RunName network-dedicated-discovery-route-unlock-e2e -Port 7907 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-discovery-route-unlock-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated action pass unlocks the route through real server/client processes.
- Observe-only reload proves state persisted and does not replay the unlock.
- Screenshots for both passes exist and are non-zero.
- Logs prove no duplicate clue, interpretation, route, Journal, or HUD state.

### Commit

```text
[NETWORK][TEST] Discovery route unlock dedicated smoke 추가
```

---

## Prompt 3: Student Life Scenario Registry and Coverage Gate

Paste this prompt after Prompt 2 is committed.

### Objective

Add a small scenario registry and coverage gate so growing Town, Student Life, Discovery, Daily Plan, relationship, career, and dedicated validation scenarios do not drift from the documented scenario catalog.

### Why Fourth

The project now has many E2E and dedicated slices. A registry gate prevents new features from becoming untracked one-off tests.

### Required Work

- Audit existing scenario IDs and E2E classes.
- Add or update a scenario catalog document under:

```text
docs/superpowers/audits/
```

- Add a CI/script gate that verifies named scenario IDs map to at least one test or runner.
- Include categories:
  - `TOWN`,
  - `LIFE`,
  - `CAREER`,
  - `REL`,
  - `JOB`,
  - `KNOW`,
  - `STATUS`,
  - `NET`,
  - legacy `GEN`, `HEIR`, `CROP`.

### Required TDD

RED:

- Add a test or script assertion that fails on one intentionally missing or unmapped scenario entry.
- Add a source scan that fails if a new scenario category is undocumented.

GREEN:

- All documented active scenarios map to tests or runners.
- Deferred scenarios are explicitly marked deferred with a reason and owner domain.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Scenario catalog exists and is current.
- CI fails if scenario documentation and executable coverage drift.
- No gameplay behavior changes are introduced.
- Deferred coverage is explicit rather than hidden.

### Commit

```text
[TEST][DOCS][CHORE] Student Life scenario coverage gate 추가
```

---

## Prompt 4: Objective Journal Source Collision Regression

Paste this prompt after Prompt 3 is committed.

### Objective

Harden Objective Journal source aggregation so Quest, Campaign, Daily Plan, Discovery Hint, Relationship, Career, and completed-route entries cannot collide, duplicate, or leak into separate HUD ownership.

### Why Fifth

The Discovery route unlock will add another source feeding Objective Journal. Before adding more content, the source collision and ownership rules need a compact regression net.

### Required Work

- Add or tighten tests for Objective Journal source aggregation.
- Verify source rows have stable keys and categories.
- Verify completed Daily Plan entries do not remain active goals.
- Verify discovery hints, career hints, relationship hints, and quest rows can coexist.
- Verify `TrackedObjectiveHud` remains unique.
- Add a static gate if a goal-like UI creates a separate always-visible Canvas/HUD.

### Required TDD

RED:

- Aggregation test fails when two sources emit the same key/category incorrectly.
- Ownership test fails if a source creates a separate persistent HUD.
- Completed route filtering test fails if completed Daily Plan route remains active.

GREEN:

- Objective Journal source matrix tests pass.
- HUD uniqueness tests pass.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.ObjectiveJournal
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalSourceCollisionE2EScenarioTests
Get-ChildItem Builds/Logs/objective-journal-source-collision-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Multiple objective sources coexist without duplicate rows.
- Completed routes are filtered from active guidance.
- Discovery and career hints remain visible in the correct section.
- Only `ObjectiveJournalPanel` and `TrackedObjectiveHud` own objective UI.

### Commit

```text
[TEST][UI] Objective Journal source collision 회귀 추가
```

---

## Prompt 5: First Resource/Recipe Town Activity Slice

Paste this prompt after Prompt 4 is committed.

### Objective

Add the first small Town resource/recipe activity slice that connects exploration or activity output to a data-driven recipe/resource progression path without reviving legacy Farm as the main loop.

### Why Sixth

After Discovery, Daily Plan, and Objective Journal are stable, the next player value is a tangible activity reward loop: find or earn a resource, unlock or progress a recipe, and see next-step guidance.

### Required Work

- Choose one existing Town activity or location as the source.
- Add ScriptableObject resource/recipe data under the established `Assets/Data/Resources` and `Assets/Data/Recipes` style.
- Add one data-driven outcome strategy that awards or reveals the resource/recipe.
- Add Objective Journal hint guidance for the next step.
- Do not use legacy crop-specific or tool-specific production branches.
- Save/load must restore resource/recipe progress exactly once.

### Required PlayMode Scenario

1. Start from Town through the normal player flow.
2. Move to the activity or location source.
3. Use the visible interaction/UI path.
4. Receive or reveal the resource/recipe.
5. Open Objective Journal with Tab.
6. Verify the next-step hint.
7. Capture a screenshot.
8. Save and reload.
9. Verify the resource/recipe state and Journal hint restore once.
10. Repeat the same interaction and verify no duplicate reward or invalid partial transaction.

### Required TDD

RED:

- Data registry test fails before the resource/recipe assets are registered.
- Transaction/idempotency test fails before duplicate reward prevention exists.
- PlayMode E2E fails before the visible Town activity path grants/reveals the resource/recipe.

GREEN:

- Registry and transaction tests pass.
- Player-facing PlayMode E2E passes with screenshot and reload evidence.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Resources
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Recipes
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.TownResourceRecipeActivityE2EScenarioTests
Get-ChildItem Builds/Logs/town-resource-recipe-activity-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Resource/recipe progression is data-driven.
- Reward transaction is atomic and idempotent.
- Player-facing Town activity grants or reveals the resource/recipe.
- Objective Journal owns the next-step guidance.
- Save/load and repeat interaction do not duplicate state.

### Commit

```text
[FEATURE][TEST] Town resource recipe activity slice 추가
```
