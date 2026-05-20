# Recommended Goal Prompts: Stabilization First Recommended Order

Date: 2026-05-21

This document lists the recommended goal prompts in execution order from the current workspace state. The order is intentionally stabilization-first: close the active client build and Addressables blocker, then complete the dedicated route/relationship smoke, then return to player-facing feature development.

## Current Recommendation

Run these goals in this order:

1. Client Build Addressables Loop Hardening
2. Complete Dedicated Route/Relationship Persistence Smoke
3. Dedicated Route/Relationship Long-Run Regression
4. Career/Job Daily Plan Route Expansion
5. Quest/Interior Reward Saved-State Regression Hardening
6. Second NPC Relationship Route Slice

Do not start Prompt 4 or later until Prompts 1 and 2 are complete or explicitly stopped with a documented blocker.

## Common Constraints For Every Prompt

- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior changes.
- Keep all gameplay/entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity, route, quest, activity, location, NPC, reward, crop, tool, furniture, career, job, or similar IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when scene, prefab, ScriptableObject, palette, tilemap, or serialized state should persist.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Run `git diff --check`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 1: Client Build Addressables Loop Hardening

Paste this prompt first.

### Objective

Finish the active client build hardening cycle so `BuildClientWindows64` produces fresh client artifacts without repeatedly re-entering Addressables bundle map or player script build loops, while preserving required Addressables runtime content and `link.xml`.

### Why First

The dedicated route/relationship smoke cannot be trusted until fresh client artifacts include current committed runtime code and Addressables runtime data is present in the built player.

### Required Work

- Confirm the current root cause from `Editor.log`, `BuildScript.cs`, and built player output.
- Keep one clear client build path that does not recurse through Addressables player-build processing.
- Ensure the built client contains:
  - fresh `Rootborn.Game.dll`,
  - fresh `Rootborn.UI.dll`,
  - `rootborn_Data/StreamingAssets/aa/settings.json`,
  - required Addressables catalog/bundle runtime files.
- Preserve direct validation runner stripping/linking requirements.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not claim dedicated smoke completion from EditMode tests alone.

### Required TDD

RED:

- Add or keep a source/EditMode contract test that fails if client build code can run both manual Addressables content generation and the Addressables player-build hook in one path.
- Add or keep a test that fails if the client build path does not explicitly guard or disable the recursive Addressables player-build hook.
- Add or keep a preservation test for direct validation runner types if stripping/linking evidence requires them.

GREEN:

- Client build source exposes one non-recursive Addressables strategy.
- Addressables preservation tests pass.
- Fresh player output contains current runtime assemblies and Addressables runtime content.

### Required Build And Smoke Scenario

Run the real build path:

```powershell
# Via Unity MCP or project-supported batchmode:
# Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64
Get-Item Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll,Builds\Client\Windows\rootborn_Data\Managed\Rootborn.UI.dll | Select-Object FullName,Length,LastWriteTime
Get-ChildItem Builds\Client\Windows\rootborn_Data\StreamingAssets\aa -Recurse -File | Select-Object -First 20 FullName,Length,LastWriteTime
```

Then run the dedicated smoke once fresh artifacts exist:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-e2e -Port 7871 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e -File | Select-Object Name,Length,LastWriteTime
```

If the smoke fails because gameplay state is missing, continue with Prompt 2. If it fails because build output lacks Addressables runtime data, finish that blocker inside Prompt 1.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Client build no longer loops through Addressables player script build recursion.
- Fresh client artifacts include current runtime assemblies.
- Built client has Addressables runtime settings/catalog content.
- `link.xml` and `.meta` remain present.
- Dedicated smoke either passes or reaches a real gameplay/runtime blocker that is documented accurately.

### Commit

```text
[CHORE][TEST] Client build Addressables loop 안정화
```

---

## Prompt 2: Complete Dedicated Route/Relationship Persistence Smoke

Paste this prompt after Prompt 1 is committed or explicitly blocked with evidence.

### Objective

Complete the dedicated route/relationship persistence smoke through a real server process and a real client process, including first-pass action, screenshot evidence, reload observe-only verification, and no duplicate state.

### Why Second

This is the verification foundation for multiplayer persistence. Feature work that touches Daily Plan, relationship, Objective Journal, or save/load should not proceed while this remains ambiguous.

### Required Work

- Use fresh `Builds/Server/Windows/rootborn-server.exe` and `Builds/Client/Windows/rootborn.exe` artifacts.
- Run first pass with `-directValidationAutoplayRouteRelationship`.
- Run reload pass with `-directValidationAutoplayRouteRelationshipObserveOnly`.
- Verify Daily Plan route state, relationship state, Objective Journal state, tracked HUD state, and save slot state.
- Ensure observe-only reload logs snapshots and does not replay the action runner.
- Capture screenshot evidence from the built client.
- Keep `Scripts/qa/run-dedicated-route-relationship-smoke.ps1` as the canonical smoke entry point.

### Required TDD

RED:

- Add or keep a test that fails if the smoke preset omits route/relationship autoplay, trace, screenshot, or observe-only arguments.
- Add or keep a test that fails if observe-only mode can replay route/relationship actions.
- Add or keep a Daily Plan runtime installer test that fails when the local dedicated client player cannot be discovered.

GREEN:

- The dedicated client local player receives Daily Plan runtime state.
- The first pass logs route and relationship evidence.
- The reload pass logs restored state without replaying deltas.

### Required Player-Facing Process Scenario

1. Start dedicated server:

```powershell
Builds/Server/Windows/rootborn-server.exe -mode server -batchmode -nographics -port 7871 -maxPlayers 4 -saveSlot network-dedicated-route-relationship-e2e
```

2. Start client:

```powershell
Builds/Client/Windows/rootborn.exe -mode client -joinIp 127.0.0.1 -port 7871 -saveSlot network-dedicated-route-relationship-e2e -directValidationTrace -directValidationAutoplayRouteRelationship -directValidationScreenshot Builds/Logs/network-dedicated-route-relationship-e2e/network-dedicated-route-relationship-e2e.png
```

3. Verify the client enters Town.
4. Verify the runner drives the route/relationship path through player-facing input/UI where possible.
5. Verify logs include Daily Plan route snapshot, relationship snapshot, and `autoplay completed route-relationship`.
6. Verify screenshot exists and has non-zero length.
7. Restart server and client with the same save slot.
8. Start client with observe-only flag.
9. Verify restored route/relationship/Journal/HUD state without replaying actions.
10. Verify no duplicate Journal rows, HUDs, route deltas, relationship deltas, network objects, or fatal logs.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-e2e -Port 7871 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- First-pass dedicated client completes route/relationship autoplay.
- Reload observe-only pass verifies persisted state without replay.
- Screenshot evidence exists and has non-zero length.
- Logs prove server/client process flow, route state, relationship state, Journal/HUD state, and cleanup.

### Commit

```text
[NETWORK][TEST] Dedicated route relationship persistence smoke 완료
```

---

## Prompt 3: Dedicated Route/Relationship Long-Run Regression

Paste this prompt after Prompt 2 passes.

### Objective

Add a longer dedicated server/client regression that repeats route/relationship action or observe, reload, and shutdown cycles to catch process cleanup, save-slot drift, duplicate state, and intermittent network persistence failures.

### Why Third

One smoke proves the path once. A repeated regression protects the foundation before feature work expands more Daily Plan and relationship sources.

### Required Work

- Add or extend a QA script for repeated dedicated cycles.
- Use unique run names, ports, and save slots per run, or a controlled same-slot reload pattern per scenario.
- Collect server logs, client logs, and screenshots per cycle.
- Verify no launched processes survive cleanup.
- Verify repeated reloads do not duplicate route, relationship, Journal, HUD, or save state.
- Keep it opt-in if it is too slow for standard PlayMode verification.

### Required TDD

- Log parser test fails if a cycle is missing server readiness, client readiness, route snapshot, relationship snapshot, screenshot, or completion marker.
- Cleanup test fails if launched server/client processes remain alive.
- Duplicate-state test fails if repeated observe-only reload adds duplicate Journal rows, HUDs, route deltas, or relationship deltas.

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
5. Verify the same save slot state restores without duplication.
6. Verify no launched process survives cleanup.

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

- Three dedicated cycles complete with isolated logs and screenshots.
- Save slot state remains stable across repeated observe-only reloads.
- No duplicate Journal/HUD/route/relationship state appears.
- No launched process is left running.

### Commit

```text
[NETWORK][TEST] Dedicated route relationship long-run 회귀 추가
```

---

## Prompt 4: Career/Job Daily Plan Route Expansion

Paste this prompt after Prompts 1 and 2 are complete.

### Objective

Add one small data-driven career or part-time job route slice so Daily Plan progression is not limited to discovery, quest, and relationship sources.

### Why Fourth

Career/job data is already mature enough to become the next non-discovery Daily Plan source without creating a new UI surface.

### Required Work

- Add or reuse one career/job activity, condition, or outcome that unlocks or recommends a Daily Plan route.
- Register all relevant ScriptableObject assets in `GameDataRegistry.asset`.
- Surface next-action guidance through existing Objective Journal source contracts.
- Allow the route to be selected from the Daily Plan board.
- Persist career/job progress, route availability, selected route state, Objective Journal state, and tracked HUD state.
- Keep all route unlock logic data-driven.

### Required TDD

- Career/job route condition is false before required progress.
- Player-facing career/job action changes progress exactly once.
- Route becomes available after progress.
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
4. Open the Daily Plan board and verify the route is locked or absent before progress.
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

## Prompt 5: Quest/Interior Reward Saved-State Regression Hardening

Paste this prompt after Prompt 4, or earlier if a quest/interior reward regression appears.

### Objective

Harden saved-state and visual regression coverage for quest-to-interior rewards so unlocked furniture, placement, and reload behavior cannot silently drift.

### Why Fifth

Quest interior unlock crosses reward transaction rules, furniture catalog filtering, placement UI, House/Interior visuals, and saved layout state. It should be protected before adding broader housing reward content.

### Required Work

- Add EditMode regression coverage for quest reward idempotency, furniture unlock persistence, catalog filtering, and placement save data.
- Add PlayMode regression coverage that verifies unlocked furniture remains visible after reload.
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
[TEST][REFACTOR] Quest interior reward 저장 회귀 강화
```

---

## Prompt 6: Second NPC Relationship Route Slice

Paste this prompt after Prompt 5, unless Prompt 2 exposes unresolved relationship persistence issues.

### Objective

Add one second NPC relationship route slice so relationship progress starts to feel like a reusable Town system instead of a single First Guide sample.

### Why Sixth

Relationship content is still thin, but it should come after dedicated persistence and saved-state hardening because relationship slices feed Objective Journal, Daily Plan, save/load, and network validation.

### Required Work

- Add one data-driven relationship definition or milestone using existing ScriptableObject strategy patterns.
- Add or reuse one NPC schedule, dialogue, or help action path.
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
- Player-facing Town interaction advances relationship state.
- Objective Journal owns the guidance.
- Save/load restores state without duplicate deltas or duplicate HUDs.

### Commit

```text
[FEATURE][ASSET][TEST] 두 번째 NPC 관계 route 확장
```
