# Recommended Goal Prompts: Current Execution Order

Date: 2026-05-21

This document lists the recommended goal prompts from the current project state. The ordering keeps the foundation-stabilization priority: finish reusable network verification tools first, then add the deferred dedicated persistence smoke, then return to feature slices and saved-state hardening.

Already completed before this sequence:

- Objective Journal route/source regression hardening.
- Dedicated server/client process harness.
- Dedicated client autoplay evidence contract.

Current recommended order:

1. Dedicated Route/Relationship Client Autoplay Runner
2. Dedicated Server Route/Relationship Persistence Smoke
3. Addressables `link.xml` Preservation Guard
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

## Prompt 1: Dedicated Route/Relationship Client Autoplay Runner

Paste this prompt first.

### Objective

Add a dedicated client-side route/relationship autoplay runner for future dedicated server smoke tests. It must use a route/relationship-specific command-line flag and must not run together with the existing generic direct-validation autoplay runner.

### Why First

The dedicated server/client harness now exists, but the future persistence smoke still needs exactly one client automation loop that can drive the route, relationship, Daily Plan, and screenshot evidence path. Running generic `-directValidationAutoplay` at the same time would create conflicting input automation.

### Required Work

- Add a runtime installer keyed by:

```text
-directValidationAutoplayRouteRelationship
```

- Add screenshot path parsing through:

```text
-directValidationScreenshot <path>
```

- Update the harness route/relationship preset so it includes:

```text
-directValidationTrace
-directValidationAutoplayRouteRelationship
-directValidationScreenshot <path>
```

- Ensure the preset does not include generic:

```text
-directValidationAutoplay
```

- The runner should wait for the local input player, drive player-facing input/UI where possible, capture a screenshot when a path is provided, and log explicit route/relationship evidence.

### Required TDD

- Harness preset test fails if generic `-directValidationAutoplay` appears.
- Harness preset test passes only when route/relationship flag and screenshot path are present.
- Source contract test fails until the runtime installer exists.
- Source contract test fails unless the installer contains:

```text
autoplay completed route-relationship
autoplay failed
ScreenCapture.CaptureScreenshot
```

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Future dedicated route/relationship smoke can launch exactly one client autoplay runner.
- Success, failure, and screenshot evidence contracts are present.
- This cycle does not claim full dedicated persistence coverage.

### Commit

```text
[NETWORK][TEST] Dedicated route relationship client autoplay 추가
```

---

## Prompt 2: Dedicated Server Route/Relationship Persistence Smoke

Paste this prompt only after Prompt 1 is committed.

### Objective

Add one true dedicated-server smoke scenario that verifies route, relationship, Objective Journal, tracked HUD, save slot, and reload state through a separate server process and separate client process.

### Why Second

The deferred dedicated persistence smoke should run only after the separate process harness and route/relationship client autoplay runner are both in place.

### Required Work

- Start a dedicated server process through the harness with explicit port and save slot.
- Start a separate client process through the harness with explicit join IP and port.
- Use the route/relationship autoplay runner to enter Town and advance one route/relationship state.
- Verify server/client identity, save slot, route state, relationship state, Objective Journal state, and tracked HUD state.
- Capture client screenshot evidence.
- Restart or reload the same save slot and verify state restores without duplicate Journal rows, HUDs, network objects, route deltas, or relationship deltas.

### Required PlayMode Scenario

Create:

```text
NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload
```

The scenario must:

1. Start a dedicated server process.
2. Start a separate client process.
3. Enter Town as the client.
4. Advance one route or relationship state through player-facing input/UI automation.
5. Verify visible client state and runtime state.
6. Capture screenshot:

```text
Builds/Logs/network-dedicated-route-relationship-e2e.png
```

7. Restart or reload the server/client flow.
8. Verify saved state restores without duplication.

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

- Dedicated route/relationship persistence is verified through a true server/client path.
- Client-visible state, runtime state, save state, and reload state agree.
- Screenshot evidence exists and has non-zero length.
- Host-mode coverage is not used as a substitute.

### Commit

```text
[NETWORK][TEST] Dedicated server route relationship 저장 smoke 추가
```

---

## Prompt 3: Addressables `link.xml` Preservation Guard

Paste this prompt after Prompt 2, or earlier if Unity tooling deletes `Assets/AddressableAssetsData/link.xml` again.

### Objective

Add a regression guard that detects and prevents accidental deletion of `Assets/AddressableAssetsData/link.xml` and its `.meta` during Unity MCP test or asset refresh workflows.

### Why Third

The file has been repeatedly removed by Unity tooling during test runs. Manual restoration is currently required, which makes verification noisy and creates a risk of committing accidental asset deletion.

### Required Work

- Add a lightweight repository or Unity-side guard test that fails if `Assets/AddressableAssetsData/link.xml` or `.meta` is missing.
- Document the restoration expectation in the relevant goal or test output.
- Ensure the guard is cheap enough to run in standard EditMode verification.
- Do not change Addressables configuration unless a failing test proves it is required.

### Required TDD

- Test fails when `link.xml` is absent.
- Test fails when `link.xml.meta` is absent.
- Test passes when both files exist.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Accidental `link.xml` deletion is caught by automated verification.
- The guard does not require broad PlayMode coverage.
- No unrelated Addressables asset churn is introduced.

### Commit

```text
[TEST][CHORE] Addressables link.xml 보존 가드 추가
```

---

## Prompt 4: Career/Job Daily Plan Route Expansion

Paste this prompt after Prompt 3, or after Prompt 2 if the `link.xml` guard is already complete.

### Objective

Add one small data-driven career or job route slice so Daily Plan progression is not limited to discovery, quest, and relationship sources.

### Why Fourth

After the verification foundation is stabilized, the next feature value should expand the student-life loop through career/job progression while reusing existing Daily Plan and Objective Journal contracts.

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
- Objective Journal emits guidance through existing sources.
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

Paste this prompt after Prompt 4, or earlier if a recent interior placement/reward regression appears.

### Objective

Harden saved-state and visual regression coverage for quest-to-interior rewards so unlocked furniture, placement, and reload behavior cannot silently drift.

### Why Fifth

Quest interior unlock crosses quest rewards, unlock state, large furniture catalogs, placement UI, House/Interior visuals, and saved state. It should be protected after the core verification and career/job route expansion work.

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
