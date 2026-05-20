# Recommended Goal Prompts: Stabilization-First Order

Date: 2026-05-21

This document lists the recommended next goal prompts in execution order. The ordering favors foundation stabilization, regression coverage, and reusable verification harnesses before adding broader player-facing content.

Recent completed baseline:

- Quest chain Daily Plan route reward is implemented and committed.
- Host route/relationship save smoke is implemented and committed.
- Quest interior unlock reward is implemented and committed.
- Second NPC relationship route is implemented and committed.
- Daily Plan route completion/result feedback is implemented and committed.
- Dedicated server route/relationship persistence was audited and deferred because the project does not yet have a true separate server/client process harness.

Current recommendation:

1. Objective Journal Route Source Regression Hardening
2. Dedicated Server Client Process Harness
3. Dedicated Server Route/Relationship Persistence Smoke
4. Career/Job Daily Plan Route Expansion
5. Quest/Interior Reward Saved-State Regression Hardening

Common non-negotiable constraints for every prompt:

- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior/code changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when scene, prefab, ScriptableObject, palette, tilemap, or serialized state should persist.
- Restore `Assets/AddressableAssetsData/link.xml` and its `.meta` immediately if Unity tooling deletes them.
- Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Run `git diff --check`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 1: Objective Journal Route Source Regression Hardening

Paste this prompt into a goal session and execute it first.

### Objective

Harden Objective Journal route/source regression coverage so career, quest reward, relationship, completed Daily Plan, reload, and HUD ownership behavior cannot regress silently.

### Why First

Recent feature slices now converge on Objective Journal: Daily Plan route selection/completion, quest rewards, relationship milestones, host smoke, and career/route hints. Before adding more sources, the aggregator and HUD ownership rules need a compact regression net.

### Required Work

- Add a compact EditMode regression matrix for Objective Journal source ownership and stable keys.
- Verify Daily Plan selected route goals, unlocked route hints, relationship milestone hints, quest-chain rows, and completed route filtering do not collide.
- Verify completed Daily Plan routes leave active Goals.
- Verify `TrackedObjectiveHud` is unique and hides stale tracked rows when the tracked stable key disappears.
- Verify legacy always-visible goal/quest/campaign/hint HUD objects do not reappear.
- Add PlayMode scenario:

```text
OBJECTIVE_JOURNAL_REGRESSION_PM_001_MultiSourceReloadNoDuplicateHud
```

### Required TDD

- Source matrix fails if multiple Objective Journal sources emit duplicate stable keys.
- Source matrix fails if completed Daily Plan route remains active in Goals.
- Source matrix fails if relationship milestone hints and Daily Plan route hints collide.
- Ownership gate fails if legacy goal-like HUD classes or prefab roots are present.
- PlayMode E2E fails if duplicate Objective rows/HUDs appear after reload.

### Required PlayMode Scenario

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Produce at least two different Journal source states through player-facing flow or existing validated fixtures.
4. Open Objective Journal with Tab.
5. Verify row categories, stable keys, and absence of legacy goal HUDs.
6. Track one goal and verify exactly one `TrackedObjectiveHud`.
7. Capture screenshot:

```text
Builds/Logs/objective-journal-regression-e2e.png
```

8. Reload the same save slot.
9. Verify no duplicate Journal rows or HUDs appear.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalRegressionE2EScenarioTests.OBJECTIVE_JOURNAL_REGRESSION_PM_001_MultiSourceReloadNoDuplicateHud
Get-ChildItem Builds/Logs/objective-journal-regression-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Objective Journal handles multiple source types without duplicate stable keys.
- Completed Daily Plan routes do not remain active Goals.
- Relationship and route hints coexist without stable-key collisions.
- Legacy goal-like HUD ownership does not regress.
- `TrackedObjectiveHud` count remains exactly one.
- SaveSlot reload preserves Journal/tracking state without duplicate rows or HUDs.

### Commit

```text
[TEST][REFACTOR] Objective Journal source 회귀망 강화
```

---

## Prompt 2: Dedicated Server Client Process Harness

Paste this prompt only after Prompt 1 is committed.

### Objective

Build a reusable automated harness that can launch a true dedicated server process and a separate client process for player-facing PlayMode or integration validation.

### Why Second

The dedicated route/relationship smoke is currently blocked. A server-only PlayMode test cannot prove client-visible gameplay, input, UI, persistence, or reload. The foundation harness must exist before any dedicated persistence claim is valid.

### Required Work

- Add a test utility or editor-side harness that can launch a dedicated server build/process with explicit `-mode server`, `-port`, `-saveSlot`, and isolated temp save root.
- Launch or coordinate a separate client process with explicit `-mode client`, `-joinIp`, `-port`, and player identity.
- Add readiness probes for server listening, client connected, Town scene synchronized, and clean shutdown.
- Collect server/client logs into `Builds/Logs`.
- Keep host-mode tests separate from dedicated server tests.
- Document unsupported paths or build prerequisites clearly in the test output or audit doc.

### Required TDD

- A harness test fails when no server process is launched.
- A harness test fails when no separate client connection is observed.
- A harness test fails when save slot or player identity is missing.
- A cleanup test fails if launched processes are left running.

Expected RED:

- Current infrastructure cannot prove a separate server/client process.

Expected GREEN:

- The project has a reusable server/client process harness that can start, connect, report readiness, collect logs, and shut down cleanly.

### Required Verification

Use exact test names chosen during implementation, but include at minimum:

```powershell
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 20 Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the harness requires built player artifacts, also run the smallest relevant build command and record the exact executable paths used.

### Acceptance Criteria

- The harness proves dedicated server and client are separate processes or explicitly reports the missing build/runtime prerequisite.
- Server/client readiness, save slot, player identity, logs, and cleanup are verified.
- No host-mode test is mislabeled as dedicated-server coverage.

### Commit

```text
[NETWORK][TEST] Dedicated server client 하네스 추가
```

---

## Prompt 3: Dedicated Server Route/Relationship Persistence Smoke

Paste this prompt only after Prompt 2 is committed and the separate server/client harness is verified.

### Objective

Add one dedicated-server smoke scenario that verifies route, relationship, Objective Journal, and save slot state survive a true server/client flow without host-only assumptions.

### Why Third

After the harness exists, the deferred dedicated persistence smoke becomes safe to implement without weakening the direct player-flow verification gate.

### Required Work

- Start the dedicated server through the harness with explicit save slot and port.
- Connect a separate client through the harness.
- Enter Town on the client.
- Advance one existing route or relationship state through player-facing movement, interact key, and visible UI where the harness supports it.
- Verify server/client player identity and save path.
- Persist and reload the server/client flow.
- Verify no duplicate network objects, Journal rows, relationship deltas, route states, or HUD entries are produced.

### Required PlayMode Scenario

Create:

```text
NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload
```

The scenario must:

1. Start a dedicated server process.
2. Start a separate client process.
3. Enter Town as the client.
4. Advance one existing route or relationship state through player-facing input/UI.
5. Verify visible client Game View state.
6. Verify server/client identity, save slot, route state, relationship state, Objective Journal state, and tracked HUD state.
7. Capture screenshot:

```text
Builds/Logs/network-dedicated-route-relationship-e2e.png
```

8. Restart or reload through the server/client path.
9. Verify state restores without duplication.

### Required Verification

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.NetworkDedicatedPersistenceE2EScenarioTests.NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e.png | Select-Object Name,Length,LastWriteTime
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 20 Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated server route/relationship persistence is verified through a true server/client path.
- Client-visible state, runtime state, save state, and reload state agree.
- Screenshot evidence exists and has non-zero length.
- Host-mode coverage is not used as a substitute.

### Commit

```text
[NETWORK][TEST] Dedicated server route relationship 저장 smoke 추가
```

---

## Prompt 4: Career/Job Daily Plan Route Expansion

Paste this prompt only after Prompt 3 is committed, or after Prompt 2 if the user explicitly chooses to defer dedicated persistence content.

### Objective

Add one small data-driven career or job route slice so Daily Plan progression is not limited to discovery, quest, and relationship sources.

### Why Fourth

Career/job data is already a strong ROOTBORN pillar, but Daily Plan needs another non-discovery source after the stabilization and network foundation work is protected.

### Required Work

- Add or reuse one career/job activity, condition, or outcome that unlocks or recommends a Daily Plan route.
- Register the relevant ScriptableObject assets in `GameDataRegistry.asset`.
- Surface the next action through Objective Journal Hints or Goals using existing source contracts.
- Allow the route to be selected from the Daily Plan board.
- Persist career/job progress, route availability, selected route state, and tracked Journal state.
- Keep all logic data-driven; do not branch production C# on career/job/route IDs.

### Required TDD

- Career/job route condition is false before the required progress.
- The player-facing career/job action changes progress exactly once.
- The route becomes available after progress.
- Objective Journal emits the correct hint/goal through existing sources.
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
4. Open Daily Plan board and verify the route is visible/selectable after progress.
5. Select the route.
6. Open Objective Journal with Tab and verify the route guidance.
7. Track it and verify exactly one `TrackedObjectiveHud`.
8. Capture screenshot:

```text
Builds/Logs/career-job-route-e2e.png
```

9. Reload the same save slot.
10. Verify career/job progress, route availability, selection, Journal state, and HUD state restore without duplication.

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

Paste this prompt only after Prompt 4 is committed, or earlier if a recent interior placement/reward regression appears.

### Objective

Harden saved-state and visual regression coverage for quest-to-interior rewards so unlocked furniture, placement, and reload behavior cannot silently drift.

### Why Fifth

Quest interior unlock is high value but high risk: it crosses quest rewards, unlock state, large furniture catalogs, placement UI, House/Interior visuals, and saved state. After core source and network stabilization, this should get dedicated regression coverage.

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
2. Enter Town and claim the relevant quest reward through movement/interact/UI.
3. Transition to House or Interior through the player-facing path.
4. Open placement UI.
5. Select and place the unlocked item visibly.
6. Inspect selected asset name and placement position.
7. Capture screenshot:

```text
Builds/Logs/quest-interior-reward-regression-e2e.png
```

8. Reload the same save slot.
9. Verify the reward cannot duplicate, the unlock persists, and the placed item remains visible.
10. Inspect serialized unlock and layout save data.

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
