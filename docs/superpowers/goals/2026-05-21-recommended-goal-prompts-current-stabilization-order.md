# Recommended Goal Prompts: Current Stabilization Order

Date: 2026-05-21

This document lists the recommended goal prompts from the current workspace state. The priority is to finish the already-open dedicated server/client smoke, remove build and Addressables instability, then continue with a small player-facing Daily Plan feature and a broader dedicated gameplay matrix.

## Current Workspace Warning

The workspace is not clean. Before starting any later prompt, resolve or intentionally carry the current in-progress files:

- `Assets/Scripts/Game/Common/DirectValidationCareerDailyPlanAutoplayInstaller.cs`
- `Assets/Scripts/Game/Common/DirectValidationCareerDailyPlanAutoplayInstaller.cs.meta`
- `Assets/Tests/EditMode/Network/DedicatedCareerDailyPlanAutoplayContractTests.cs`
- `Assets/Tests/EditMode/Network/DedicatedCareerDailyPlanAutoplayContractTests.cs.meta`
- `Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1`

`Assets/AddressableAssetsData/link.xml` and `.meta` are currently deleted in git status. Restore or intentionally handle them before claiming any goal complete.

## Recommended Order

1. Finish Dedicated Career Daily Plan Route Smoke
2. Client and Server Build Artifact Hardening
3. Addressables `link.xml` Deletion Guard Hardening
4. Daily Plan Route Completion and Day Result Feedback
5. Dedicated Core Gameplay Matrix

Do not start Prompt 2 or later until Prompt 1 is complete, committed, or explicitly stopped with exact blocker evidence.

## Common Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior, regression, QA script, or build-script changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when scene, prefab, ScriptableObject, palette, tilemap, or serialized save state should persist.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.
- Run `git diff --check`, `Scripts/ci/check-addressables-linkxml-preserved.ps1`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 1: Finish Dedicated Career Daily Plan Route Smoke

Paste this prompt first.

### Objective

Complete the already-started dedicated server/client smoke where career practice unlocks a career-gated Daily Plan route, the route is selected through player-facing UI, screenshots/logs prove completion, and an observe-only reload pass verifies career hint and selected route state persisted.

### Why First

This work is already in progress and has uncommitted C# tests, runtime installer code, and a QA runner. Leaving it half-open makes every later recommendation harder to reason about.

### Source Context

- `docs/superpowers/goals/2026-05-21-dedicated-career-daily-plan-route-smoke-goal.md`
- `Assets/Scripts/Game/Common/DirectValidationCareerDailyPlanAutoplayInstaller.cs`
- `Assets/Tests/EditMode/Network/DedicatedCareerDailyPlanAutoplayContractTests.cs`
- `Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1`
- `Builds/Logs/build-server-windows-dedicated-career.log`

### Required Work

- Inspect the latest server build log and current client/server artifact timestamps.
- Restore deleted Addressables `link.xml` and `.meta` unless a later explicit goal owns a different generated state.
- If server or client artifacts are stale, rebuild through supported batchmode commands, not dynamic MCP build re-entry.
- Run the dedicated smoke script against fresh artifacts.
- If the smoke cannot run, stop with exact build artifact, log, or process evidence. Do not commit a partial smoke as complete.

### Required TDD

RED has already been observed through the missing installer/script contract tests. Preserve that evidence in the final summary.

GREEN requires:

- `Rootborn.Tests.EditMode.Network` passes.
- `Rootborn.Tests.PlayMode.Network` passes.
- The dedicated action pass logs `autoplay completed career-daily-plan-route`.
- The observe-only reload pass logs `autoplay completed career-daily-plan-route-observe`.
- Both screenshots exist and have non-zero length.
- Logs prove non-empty career hint and selected Daily Plan route state after reload.

### Required Verification

```powershell
Get-Content Builds/Logs/build-server-windows-dedicated-career.log -Tail 200
Get-Item Builds\Server\Windows\rootborn-server_Data\Managed\Rootborn.Game.dll,Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll | Select-Object FullName,Length,LastWriteTime
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1 -RunName network-dedicated-career-daily-plan-route-e2e -Port 7893 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-career-daily-plan-route-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated career Daily Plan route smoke completes through real server/client processes.
- Action and observe-only reload evidence are present in logs.
- Screenshots exist for both passes.
- Runner cleans up launched processes.
- Addressables `link.xml` and `.meta` are preserved.
- Only the owned smoke files are committed.

### Commit

```text
[NETWORK][TEST] Dedicated career Daily Plan route smoke 추가
```

---

## Prompt 2: Client and Server Build Artifact Hardening

Paste this prompt after Prompt 1 is complete, or immediately if Prompt 1 is blocked by stale artifacts or repeated Addressables/player build loops.

### Objective

Make client and server Windows build artifact generation reliable enough that dedicated QA scripts can trust `Rootborn.Game.dll` and related assemblies are fresh before running process-level smoke tests.

### Why Second

Dedicated smoke tests are only meaningful if the executable artifacts include current code. Recent evidence showed stale server artifacts and repeated client Addressables/player build re-entry.

### Source Context

- `docs/superpowers/goals/2026-05-21-client-build-addressables-loop-hardening-goal.md`
- `Assets/Scripts/Editor/BuildScripts/BuildScript.cs`
- `Assets/Tests/EditMode/Editor/`
- `Scripts/qa/`
- `Builds/Logs/`

### Required Work

- Confirm whether client build, server build, or both can produce fresh `Rootborn.Game.dll`.
- Add source/contract tests for build-script behavior before modifying build code.
- Prevent duplicate Addressables player content build paths for client builds.
- Add or tighten QA artifact freshness checks so dedicated runners fail before launch if artifacts are older than the source/test changes they require.
- Keep server build behavior unchanged unless evidence shows server artifacts are also stale.

### Required TDD

RED:

- A test fails while client build code can run both manual Addressables content build and Addressables player-build hook in the same path.
- A test or script contract fails while dedicated runners can launch with stale `Rootborn.Game.dll` artifacts.

GREEN:

- Build script exposes one Addressables build route for client builds.
- Dedicated runners fail closed on stale artifacts.
- Fresh client and server artifacts can be produced or an exact remaining blocker is documented.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Editor
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

Then verify fresh artifacts:

```powershell
Get-Item Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll,Builds\Server\Windows\rootborn-server_Data\Managed\Rootborn.Game.dll | Select-Object FullName,Length,LastWriteTime
```

### Acceptance Criteria

- Fresh client/server artifacts can be identified before dedicated scripts run.
- Client build no longer repeatedly re-enters Addressables map/player script build loops.
- Dedicated scripts fail closed on stale artifacts.
- Addressables preservation guard passes after build attempts.

### Commit

```text
[CHORE][TEST] Client/server build artifact 안정화
```

---

## Prompt 3: Addressables `link.xml` Deletion Guard Hardening

Paste this prompt after Prompt 2, or earlier if `link.xml` deletion keeps recurring during Unity tests/builds.

### Objective

Harden the repository guard so accidental deletion or generated mutation of `Assets/AddressableAssetsData/link.xml` and `.meta` is caught early and restored before any goal can be reported complete.

### Why Third

The current workspace shows `link.xml` and `.meta` deleted. This is a recurring Unity tooling side effect and should be blocked by a small, reliable guard before more feature work.

### Source Context

- `docs/superpowers/goals/2026-05-20-addressables-linkxml-preservation-guard-goal.md`
- `docs/superpowers/goals/2026-05-21-addressables-linkxml-deletion-guard-hardening-goal.md`
- `Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `Assets/AddressableAssetsData/link.xml`
- `Assets/AddressableAssetsData/link.xml.meta`

### Required Work

- Restore the deleted tracked Addressables files first.
- Add or tighten tests for the preservation script.
- Ensure the script fails if `link.xml` or `.meta` is missing, deleted in git status, or replaced with generated empty content.
- Add clear remediation text to the script output.
- Add the guard to relevant QA runners if they do not already call it.

### Required TDD

RED:

- A test fails when `link.xml` or `.meta` deletion is not detected.
- A test fails when QA runners omit the preservation guard after Unity tests/builds.

GREEN:

- Missing, deleted, or mutated `link.xml` state fails with clear output.
- Existing QA runners either call the guard or document why they do not run Unity tooling.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Editor
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Addressables `link.xml` and `.meta` deletion is caught before completion.
- Remediation instructions are visible in script output.
- Guard coverage exists for Unity-test/build QA paths.

### Commit

```text
[CHORE][TEST] Addressables link.xml 삭제 가드 강화
```

---

## Prompt 4: Daily Plan Route Completion and Day Result Feedback

Paste this prompt after Prompts 1-3 are complete.

### Objective

Connect one selected Daily Plan route to a completion and day-result feedback loop so selecting a route becomes part of the daily gameplay loop, not just a saved choice.

### Why Fourth

After dedicated smoke and build/guard stability are stronger, the next best player-facing slice is small and builds directly on the Daily Plan route foundation.

### Source Context

- `docs/superpowers/goals/2026-05-21-daily-plan-route-completion-result-feedback-goal.md`
- `docs/superpowers/specs/2026-05-20-daily-plan-route-selection-design.md`
- `docs/superpowers/specs/2026-05-20-discovery-clue-daily-plan-route-unlock-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/UI/Objectives/`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

### Required Work

- Add or tighten tests proving completed Daily Plan routes are no longer active Goals in Objective Journal.
- Keep unselected available routes in Hints and selected incomplete routes in Goals.
- Complete one selected route through an existing player-facing location activity.
- Ensure `StudentDayResultPanel` shows completed Daily Plan route and matched activity feedback.
- Ensure tracked HUD state does not duplicate and does not keep completed-route guidance.
- Verify route completion persists after SaveSlot reload and does not re-complete or duplicate result state.

### Required TDD

RED:

- `DailyPlanObjectiveSource` still emits a completed route as a Goals item, or the E2E scenario is missing.
- Result feedback does not include the completed route.
- Repeated completion can duplicate completion keys or result logs.

GREEN:

- Selected route appears as a Goal before completion.
- Completed route leaves active Goals or transitions to the correct non-active category.
- Day result feedback visibly includes completion.
- Reload preserves completion without duplicate HUD or route state.

### Required PlayMode Scenario

Create or strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Select a Daily Plan route through visible UI.
4. Complete the route through player-facing movement, interaction, or supported route flow.
5. Verify visible day-result or transient feedback.
6. Open Objective Journal with `Tab`.
7. Verify selected/completed route row state and one tracked HUD if tracked.
8. Capture `Builds/Logs/daily-plan-route-completion-e2e.png`.
9. Inspect serialized route/progress save state.
10. Reload the same save slot.
11. Verify route completion restores without duplicate rows, duplicate HUDs, or re-applied outcomes.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
Get-ChildItem Builds/Logs/daily-plan-route-completion-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One Daily Plan route can be selected and completed through actual player-facing Town input/UI flow.
- Objective Journal shows selected route before completion and removes or transitions it after completion.
- `TrackedObjectiveHud` remains unique and does not keep stale completed-route guidance.
- Day result feedback visibly includes completed route and matched activity.
- Save/load restores route completion without duplicate completion.
- Screenshot evidence exists and has non-zero length.

### Commit

```text
[FEATURE][TEST] Daily Plan route 완료 피드백 연결
```

---

## Prompt 5: Dedicated Core Gameplay Matrix

Paste this prompt after Prompt 4, or after Prompt 2 if the project needs QA breadth before more gameplay.

### Objective

Expand dedicated server/client validation from single-slice smoke tests into an opt-in gameplay matrix covering route/relationship, career/job route, Daily Plan completion, Objective Journal tracking, screenshots, reload, and cleanup.

### Why Fifth

After individual smoke and Daily Plan completion are stable, a matrix gives better confidence that the same player-facing loops survive dedicated server/client process conditions.

### Source Context

- `docs/superpowers/goals/2026-05-21-dedicated-route-relationship-longrun-regression-goal.md`
- `docs/superpowers/goals/2026-05-21-dedicated-career-daily-plan-route-smoke-goal.md`
- `docs/superpowers/goals/2026-05-21-daily-plan-route-completion-result-feedback-goal.md`
- `Scripts/qa/`
- `Assets/Tests/EditMode/Network/`
- `Assets/Tests/PlayMode/Network/`

### Required Work

- Add an opt-in QA script:

```text
Scripts/qa/run-dedicated-core-gameplay-matrix.ps1
```

- Run separate server/client cycles with explicit port, run name, save slot, timeout, and screenshot paths.
- Exercise at least three slices:
  - route/relationship,
  - career/job Daily Plan route,
  - Daily Plan completion feedback or House upgrade/interior loop.
- Capture one client screenshot and isolated logs per slice.
- Verify save/reload restores each slice without duplicate Journal rows, HUDs, route states, relationship deltas, reward claims, upgrade states, or interior records.
- Verify all launched server/client processes are cleaned up after every cycle, including failed cycles.

### Required TDD

RED:

- A parser/contract test fails if any matrix slice lacks server readiness, client readiness, screenshot, completion marker, saved-state evidence, restored-state evidence, and cleanup evidence.
- A cleanup test fails if launched server/client processes can remain alive after a failed cycle.
- A duplicate-state parser test fails if logs show duplicate Objective Journal rows, HUDs, route state, reward claims, or placement records.

GREEN:

- Each matrix slice emits explicit server/client readiness, player-flow action, screenshot, saved-state, reload, and cleanup evidence.
- Process cleanup is verified after every cycle.
- Missing screenshots or failed autoplay logs fail closed.

### Required Verification

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7891 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -File | Select-Object Name,Length,LastWriteTime
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated server/client validation covers at least three gameplay slices.
- Screenshots, logs, runtime state, and saved state agree for every slice.
- Reload does not duplicate Journal, HUD, route, reward, relationship, House, or interior state.
- No launched server/client process remains after cleanup.
- Failures distinguish build artifact problems, network process problems, and gameplay saved-state problems.

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix 추가
```
