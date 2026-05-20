# Recommended Goal Prompts: Next Order

Date: 2026-05-21

This document lists the recommended goal prompts from the current workspace state. The order is stabilization-first: finish the active dedicated route/relationship smoke, close generated-artifact risk, then return to small player-facing feature slices.

## Current State

- Recent committed foundation:
  - Dedicated server/client process harness.
  - Dedicated route/relationship client autoplay runner.
  - Addressables `link.xml` deletion guard.
- Current active worktree signal:
  - Dedicated route/relationship persistence smoke is in progress.
  - `Assets/AddressableAssetsData/link.xml` and `.meta` are currently deleted in the worktree and must be restored before any commit unless the current cycle intentionally owns them.
  - The latest dedicated smoke reached the client route/relationship autoplay path, but Daily Plan state was logged as `dailyPlan=missing`.
- Recommended priority:
  - Do not start a new gameplay feature before the active dedicated smoke cycle is either completed or explicitly stopped with evidence.

## Common Non-Negotiable Constraints

- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for all behavior changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity, route, quest, activity, location, NPC, reward, crop, tool, furniture, career, job, or similar IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when scene, prefab, ScriptableObject, palette, tilemap, or serialized state should persist.
- Restore `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta` immediately if Unity tooling deletes them.
- Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Run `git diff --check`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 1: Complete Dedicated Route/Relationship Persistence Smoke

Paste this prompt first.

### Objective

Finish the active dedicated route/relationship persistence smoke cycle by fixing the missing Daily Plan runtime player discovery, proving the first-pass action runner and reload observe-only runner through tests, and running the true server/client smoke when fresh build artifacts are available.

### Why First

This work is already in progress and blocks truthful dedicated-server persistence claims. Starting a new feature while this remains half-complete would leave the network verification foundation ambiguous.

### Required Work

- Restore `Assets/AddressableAssetsData/link.xml` and `.meta` if they are deleted by Unity tooling, and keep unrelated Addressables generated churn out of the commit.
- Update `DailyPlanRuntimeInstaller` so it can discover a network-spawned local player when the explicit `Player` root is absent.
- The fallback discovery must prefer a runtime local player with:
  - `PlayerIdentity`,
  - enabled `PlayerController`,
  - enabled `PlayerInteractionRouter`.
- Keep the existing root `"Player"` path working for single-player and test scenes.
- Keep the route/relationship autoplay observe-only contract:
  - first pass uses `-directValidationAutoplayRouteRelationship`,
  - reload pass uses `-directValidationAutoplayRouteRelationshipObserveOnly`,
  - reload pass logs snapshots and does not replay route/relationship actions.
- Keep the smoke script at:

```text
Scripts/qa/run-dedicated-route-relationship-smoke.ps1
```

### Required TDD

RED:

- Add or keep a failing EditMode test proving `EnsureSceneForTests` does not attach `DailyPlanProgressComponent` when the only valid player is `NetworkPlayer(Clone)` with local player components.
- Add or keep source contract tests for the observe-only flag and smoke script.

GREEN:

- `DailyPlanRuntimeInstaller` finds the network-named local player.
- The Daily Plan route snapshot in dedicated client logs is no longer `dailyPlan=missing`.
- Observe-only smoke script contracts pass.

### Required PlayMode And Process Scenario

Run the dedicated smoke as an external player-facing process flow:

1. Start `Builds/Server/Windows/rootborn-server.exe` with `-mode server`, explicit port, and explicit save slot.
2. Start `Builds/Client/Windows/rootborn.exe` with `-mode client`, `-joinIp 127.0.0.1`, same port, same save slot, and `-directValidationAutoplayRouteRelationship`.
3. Verify the client enters Town and drives the route/relationship path through the autoplay runner.
4. Verify logs include selected Daily Plan route state, relationship state, and `autoplay completed route-relationship`.
5. Capture first-pass screenshot in:

```text
Builds/Logs/network-dedicated-route-relationship-e2e/network-dedicated-route-relationship-e2e.png
```

6. Restart server and client with the same save slot.
7. Start the client with `-directValidationAutoplayRouteRelationshipObserveOnly`.
8. Verify the observe-only pass logs restored Daily Plan and relationship state without replaying the action runner.
9. Verify no duplicate Journal rows, HUDs, route deltas, relationship deltas, or fatal logs appear.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If fresh client/server build artifacts can be produced:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-e2e -Port 7871 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e -File | Select-Object Name,Length,LastWriteTime
```

If fresh build artifacts cannot be produced, do not claim full dedicated persistence coverage. Commit only the contract/script/fallback foundation if tests pass, and report the exact blocked build command.

### Acceptance Criteria

- Daily Plan runtime state attaches to the local dedicated client player.
- First-pass dedicated client logs route and relationship state.
- Reload observe-only pass verifies restored state without replaying actions.
- Screenshot evidence exists and has non-zero length when the external smoke runs.
- Addressables `link.xml` and `.meta` are preserved.

### Commit

```text
[NETWORK][TEST] Dedicated route relationship persistence smoke 추가
```

---

## Prompt 2: Career/Job Daily Plan Route Expansion

Paste this prompt only after Prompt 1 is committed or explicitly stopped with a documented blocker.

### Objective

Add one small data-driven career or part-time job route slice so Daily Plan progression is not limited to discovery, quest, and relationship sources.

### Why Second

After the dedicated verification foundation is closed, the highest-value feature slice is a non-discovery Daily Plan source. Career/job data is already rich enough to support a small vertical slice without introducing a new UI surface.

### Required Work

- Add or reuse one career/job activity, condition, or outcome that unlocks or recommends a Daily Plan route.
- Register all relevant ScriptableObject assets in `GameDataRegistry.asset`.
- Surface the next action through existing Objective Journal Hints or Goals source contracts.
- Allow the route to be selected from the Daily Plan board.
- Persist career/job progress, route availability, selected route state, Objective Journal state, and tracked HUD state.
- Keep all route unlock logic data-driven.

### Required TDD

- Career/job route condition is false before the required progress.
- The player-facing career/job action changes progress exactly once.
- The route becomes available after progress.
- Objective Journal emits guidance through existing source contracts.
- Save/load restores career/job progress and route availability without duplicate effects.

### Required PlayMode Scenario

Create:

```text
CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual movement and visible UI to perform the career/job action.
4. Open the Daily Plan board and verify the route is locked/absent before progress.
5. Perform the required career/job progress action.
6. Open the Daily Plan board again and verify the route is visible/selectable.
7. Select the route.
8. Open Objective Journal with Tab and verify route guidance.
9. Track it and verify exactly one `TrackedObjectiveHud`.
10. Capture screenshot:

```text
Builds/Logs/career-job-route-e2e.png
```

11. Reload the same save slot.
12. Verify career/job progress, route availability, route selection, Journal state, and HUD state restore without duplication.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
Get-ChildItem Builds/Logs/career-job-route-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One career/job route source is implemented as a data-driven vertical slice.
- The player can unlock or recommend it through actual Town flow.
- Daily Plan and Objective Journal expose it through existing UI ownership.
- Save/load restores all relevant state without duplication.

### Commit

```text
[FEATURE][ASSET][TEST] Career job Daily Plan route 확장
```

---

## Prompt 3: Quest/Interior Reward Saved-State Regression Hardening

Paste this prompt after Prompt 2, or earlier if a recent interior placement/reward regression appears.

### Objective

Harden saved-state and visual regression coverage for quest-to-interior rewards so unlocked furniture, placement, and reload behavior cannot silently drift.

### Why Third

Quest interior unlock crosses reward transaction rules, furniture catalog filtering, placement UI, House/Interior visuals, and saved layout state. It should be protected before adding broader housing reward content.

### Required Work

- Add EditMode regression coverage for quest reward idempotency, furniture unlock persistence, catalog filtering, and placement save data.
- Add PlayMode regression coverage that verifies the unlocked furniture remains visible after reload.
- Inspect runtime selected asset name, placement position, and serialized saved layout/unlock state.
- Verify locked items stay absent before reward and available after reward.
- Verify duplicate reward claim is blocked without mutating unlock or placement state.

### Required TDD

- Reward claim test fails if duplicate claim mutates unlock state.
- Catalog test fails if locked furniture appears before unlock.
- Save/load test fails if unlock or placement state is not restored.
- PlayMode test fails if visible placement disappears after reload.

### Required PlayMode Scenario

Create:

```text
QUEST_INTERIOR_REWARD_REGRESSION_E2E_001_UnlockPlaceReloadNoDuplicate
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town and claim the relevant quest reward through movement, interact key, and visible UI.
3. Transition to House or Interior through the project-supported player-facing path.
4. Open placement UI.
5. Verify the locked furniture is unavailable before reward and available after reward.
6. Select and place the unlocked item visibly.
7. Inspect selected asset name and placement position.
8. Capture screenshot:

```text
Builds/Logs/quest-interior-reward-regression-e2e.png
```

9. Reload the same save slot.
10. Verify the reward cannot duplicate, the unlock persists, and the placed item remains visible.
11. Inspect serialized unlock and layout save data.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardRegressionE2EScenarioTests.QUEST_INTERIOR_REWARD_REGRESSION_E2E_001_UnlockPlaceReloadNoDuplicate
Get-ChildItem Builds/Logs/quest-interior-reward-regression-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Quest interior reward unlock, placement, save, and reload paths are regression-covered.
- Runtime visual state and serialized saved state agree.
- Duplicate reward claim does not mutate state.
- Screenshot evidence exists and has non-zero length.

### Commit

```text
[TEST][REFACTOR] Quest interior reward 저장 회귀망 강화
```

---

## Prompt 4: Second NPC Relationship Route Slice

Paste this prompt after Prompt 3, unless network or save-state regressions are still open.

### Objective

Add one second NPC relationship route slice so relationship progress starts to feel like a reusable Town system instead of a single First Guide sample.

### Why Fourth

Relationship content is still thin compared with career and discovery. This should come after the dedicated smoke and saved-state hardening because relationship slices feed Objective Journal, Daily Plan, save/load, and network validation.

### Required Work

- Add one data-driven relationship definition or milestone using existing ScriptableObject strategy patterns.
- Add or reuse one NPC schedule/dialogue/help action path.
- Connect the relationship outcome to Daily Plan route guidance or Objective Journal milestone guidance through existing source contracts.
- Register all new assets in `GameDataRegistry.asset`.
- Persist relationship progress, milestone state, route guidance state, and tracked Journal state.
- Do not add NPC-ID or relationship-ID production branching.

### Required TDD

- Relationship action is unavailable before its data-defined condition.
- Player-facing action applies the relationship delta exactly once.
- Milestone or route guidance appears through Objective Journal without stable-key collision.
- Save/load restores relationship progress and milestone/route state without duplicate deltas.
- Registry wiring test fails if any new relationship/NPC/schedule asset is missing.

### Required PlayMode Scenario

Create:

```text
NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual movement to reach the second NPC at a scheduled or data-defined location.
4. Use actual interact key or visible help/dialogue UI to perform the relationship action.
5. Verify visible dialogue/result text in Game View.
6. Verify runtime relationship progress and milestone/route guidance state.
7. Open Objective Journal with Tab and verify the relationship guidance row.
8. Track it and verify exactly one `TrackedObjectiveHud`.
9. Capture screenshot:

```text
Builds/Logs/npc-relationship-second-slice-e2e.png
```

10. Reload the same save slot.
11. Verify relationship progress, milestone/route guidance, Journal state, and HUD state restore without duplication.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Relationships
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.NpcRelationshipE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
Get-ChildItem Builds/Logs/npc-relationship-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the exact namespace differs, use the nearest existing relationship/NPC test namespace and record the substitution in the final report.

### Acceptance Criteria

- One second relationship slice is implemented through data.
- Player-facing Town interaction advances the relationship state.
- Objective Journal owns the guidance.
- Save/load restores state without duplicate deltas or duplicate HUDs.

### Commit

```text
[FEATURE][ASSET][TEST] 두 번째 NPC 관계 route 확장
```

---

## Prompt 5: Dedicated Route/Relationship Long-Run Regression

Paste this prompt after Prompt 4, or earlier if Prompt 1 exposes intermittent dedicated process instability.

### Objective

Add a longer dedicated server/client regression that repeats route/relationship connect, action or observe, reload, and shutdown cycles to catch process cleanup, save slot, network identity, and duplicate-state drift.

### Why Fifth

The first dedicated smoke should prove one path. A longer regression should come later, after the core feature slices are stable enough that failures are likely infrastructure regressions rather than unfinished gameplay wiring.

### Required Work

- Extend the dedicated smoke harness or add a separate QA script that can run multiple isolated cycles.
- Use unique run names, ports, and save slots per cycle.
- Collect server/client logs and screenshots per cycle under `Builds/Logs`.
- Verify no server/client processes remain after cleanup.
- Verify route, relationship, Journal, HUD, and save state do not duplicate across repeated reloads.
- Keep the test opt-in if it is too slow for standard PlayMode runs.

### Required TDD

- Cleanup test fails if launched processes remain alive.
- Log parser test fails if a cycle is missing server readiness, client readiness, route snapshot, relationship snapshot, screenshot, or completion marker.
- Save-state parser test fails if a repeated reload creates duplicate route/relationship deltas.

### Required Process Scenario

Create:

```text
NETWORK_DEDICATED_LONGRUN_001_RouteRelationshipRepeatedReload
```

The scenario must:

1. Run at least three dedicated server/client cycles.
2. In cycle 1, apply the route/relationship action through the client runner.
3. In cycles 2 and 3, use observe-only reload paths.
4. Capture one screenshot per client cycle.
5. Verify the same save slot state restores without duplicate Journal rows, HUDs, route deltas, or relationship deltas.
6. Verify no launched server/client process survives cleanup.

### Required Verification

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun -Cycles 3 -StartPort 7881 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun -File | Select-Object Name,Length,LastWriteTime
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Three dedicated server/client cycles complete with isolated logs and screenshots.
- Save slot state remains stable across repeated observe-only reloads.
- No duplicate Journal/HUD/route/relationship state appears.
- No launched process is left running.

### Commit

```text
[NETWORK][TEST] Dedicated route relationship long-run 회귀 추가
```
