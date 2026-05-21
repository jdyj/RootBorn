# Recommended Goal Prompts: After Dedicated Freshness Guard

Date: 2026-05-21

This document lists the next recommended goal prompts from the current clean workspace state after the dedicated runner artifact freshness guard.

## Current State

- Latest committed foundation:
  - `9c099ea7c [TEST][CHORE][NETWORK] Dedicated runner artifact freshness 가드 추가`
  - `03c56953f [TEST][CHORE] Addressables link.xml 삭제 가드 강화`
  - `da4e4d39e [NETWORK][TEST][CHORE][DOCS] Dedicated career Daily Plan smoke 추가`
- Existing validated slices:
  - Dedicated route/relationship smoke.
  - Dedicated career Daily Plan route smoke.
  - Editor PlayMode Daily Plan route completion/result/reload E2E.
  - Addressables `link.xml` deletion guard.
  - Dedicated runner stale artifact guard.
- Recommended next move:
  - Add one dedicated Daily Plan completion smoke before attempting a broad dedicated gameplay matrix.

## Recommended Order

1. Dedicated Daily Plan Completion Smoke
2. Dedicated Core Gameplay Matrix
3. Discovery Clue Route Unlock Playable Slice
4. Student Life Scenario Registry and Coverage Gate

Do not start Prompt 2 until Prompt 1 is complete, committed, or explicitly blocked with exact process/log evidence. Prompt 3 is the next feature slice after dedicated QA breadth is reliable. Prompt 4 is a stabilization pass to keep the expanding scenario catalog from drifting.

## Common Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior, regression, QA script, or build-script changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.
- Run `git diff --check`, `Scripts/ci/check-addressables-linkxml-preserved.ps1`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 1: Dedicated Daily Plan Completion Smoke

Paste this prompt first.

### Objective

Add a dedicated server/client process smoke where a player selects a Daily Plan route through visible UI, completes it through the player-facing Town flow, sees result feedback, captures screenshots, and verifies the completed route state after observe-only reload.

### Why First

Daily Plan completion already has Editor PlayMode E2E coverage, but dedicated server/client process coverage still stops at route selection. This is the smallest prerequisite before a broad matrix.

### Source Context

- `docs/superpowers/goals/2026-05-21-daily-plan-route-completion-result-feedback-goal.md`
- `Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteCompletionE2EScenarioTests.cs`
- `Assets/Tests/PlayMode/EndToEnd/Support/DailyPlanE2EDriver.cs`
- `Assets/Tests/PlayMode/EndToEnd/Support/ObjectiveJournalE2EDriver.cs`
- `Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs`
- `Assets/Scripts/Game/Common/DirectValidationCareerDailyPlanAutoplayInstaller.cs`
- `Scripts/qa/run-dedicated-route-relationship-smoke.ps1`
- `Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1`

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Add a dedicated Daily Plan completion autoplay installer keyed by:

```text
-directValidationAutoplayDailyPlanCompletion
-directValidationAutoplayDailyPlanCompletionObserveOnly
```

- The action pass must:
  - wait for the local input player,
  - enter Town through the normal runtime flow,
  - open/select a Daily Plan route through visible UI,
  - complete the route through player-facing movement, interaction, or the existing location activity/day-end flow,
  - open Objective Journal with the player-facing input path,
  - verify no stale completed-route tracked HUD remains,
  - capture a screenshot when `-directValidationScreenshot` is provided,
  - log route completion, result feedback, Objective Journal, HUD, and save-state snapshots,
  - log `autoplay completed daily-plan-completion`.
- The observe-only reload pass must:
  - load the same save slot,
  - inspect restored Daily Plan completion state,
  - open Objective Journal through the player-facing path,
  - verify no duplicate route row, no duplicate HUD, and no re-applied completion,
  - capture a reload screenshot,
  - log `autoplay completed daily-plan-completion-observe`.
- Add repository runner:

```text
Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1
```

- The runner must:
  - use existing client/server build artifacts,
  - fail before launch if required artifacts are stale or missing,
  - run an action pass and observe-only reload pass with separate logs/screenshots,
  - fail closed on fatal logs, missing completion markers, missing screenshots, stale artifacts, duplicate-state evidence, or leaked processes.

### Required TDD

RED:

- Add an EditMode contract test that fails while the Daily Plan completion autoplay installer is missing.
- Add a script contract test that fails while `Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1` is missing.
- Tests must require both action and observe-only flags, screenshot support, artifact freshness checks, completion/failure logs, route/result/Journals/HUD snapshots, and no direct domain mutation calls such as `SelectRoute(`, `CompleteRoute(`, `MarkCompleted(`, or direct save-state writes inside the autoplay installer.

GREEN:

- Contract tests pass.
- The dedicated runner completes both action and observe-only reload passes.
- Logs and screenshots prove completion, result feedback, Objective Journal cleanup, HUD cleanup, and reload persistence.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1 -RunName network-dedicated-daily-plan-completion-e2e -Port 7897 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-daily-plan-completion-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated action pass completes through actual server/client processes and player-facing input/UI.
- Observe-only reload pass proves completed route state persisted.
- Result feedback is visible or logged from the player-facing UI flow.
- Objective Journal does not show completed route as an active stale Goal.
- `TrackedObjectiveHud` remains unique and does not keep completed-route guidance.
- Screenshots for both passes exist and are non-zero.
- Runner cleans up all launched processes.

### Commit

```text
[NETWORK][TEST] Daily Plan completion dedicated smoke 추가
```

---

## Prompt 2: Dedicated Core Gameplay Matrix

Paste this prompt after Prompt 1 is complete.

### Objective

Expand dedicated server/client validation from individual smoke scripts into an opt-in gameplay matrix that runs multiple player-facing slices with screenshots, reload checks, duplicate-state checks, and process cleanup.

### Why Second

The matrix should compose stable slices, not become the first place a new Daily Plan completion dedicated flow is debugged.

### Source Context

- `Scripts/qa/run-dedicated-route-relationship-smoke.ps1`
- `Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1`
- `Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1`
- `Assets/Tests/EditMode/Network/`
- `Assets/Tests/PlayMode/Network/`
- `docs/superpowers/goals/2026-05-21-dedicated-route-relationship-longrun-regression-goal.md`

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Add:

```text
Scripts/qa/run-dedicated-core-gameplay-matrix.ps1
```

- The matrix must run at least three dedicated slices:
  - route/relationship,
  - career Daily Plan route,
  - Daily Plan completion feedback.
- Each slice must have:
  - fresh artifact check,
  - isolated run name,
  - isolated save slot,
  - explicit port,
  - action pass,
  - observe-only reload pass,
  - screenshot assertion,
  - saved-state assertion,
  - restored-state assertion,
  - process cleanup assertion.
- Add contract tests that parse the matrix script and require every slice to include readiness, screenshot, action completion marker, observe completion marker, stale artifact guard, and cleanup guard.

### Required TDD

RED:

- A contract test fails while the matrix runner is missing.
- A contract test fails if any slice lacks screenshot, saved-state, restored-state, or cleanup evidence.

GREEN:

- Network EditMode contract tests pass.
- The matrix runner completes all included slices or fails with a precise slice-level reason.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7901 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Matrix covers at least three dedicated gameplay slices.
- Every slice emits action, reload, screenshot, save-state, and cleanup evidence.
- Failures identify the slice and category: artifact, process, gameplay, visual, persistence, or cleanup.
- No launched server/client process remains after success or failure.

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix 추가
```

---

## Prompt 3: Discovery Clue Route Unlock Playable Slice

Paste this prompt after Prompt 2, unless QA stability needs another dedicated cleanup pass first.

### Objective

Implement the next small player-facing feature slice from the discovery clue design: a Town activity or interaction reveals a Daily Plan route unlock hint, exposes it in Objective Journal as a Hint, and allows the route to become selectable without hardcoded entity-ID branching.

### Why Third

After route selection and completion are validated, the next useful gameplay layer is discoverability: the player should learn about routes through activities, relationships, or exploration, not only through pre-existing availability.

### Source Context

- `docs/superpowers/specs/2026-05-20-discovery-clue-daily-plan-route-unlock-design.md`
- `docs/superpowers/specs/2026-05-20-daily-plan-route-selection-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/UI/Objectives/`
- `Assets/Data/StudentLife/`
- `Assets/Data/LifeActivities/`
- `Assets/Data/Registry/GameDataRegistry.asset`

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Add or reuse ScriptableObject data/strategy configuration for one discovery clue path.
- Do not add C# `if`/`switch` branches for specific route, activity, location, NPC, or clue IDs.
- Objective Journal must show discovered-but-not-selected route guidance as a Hint, not as a separate always-visible HUD.
- Route selection remains owned by the Daily Plan UI flow.
- Add one PlayMode E2E scenario proving:
  - player enters Town,
  - performs the discovery interaction through input/UI,
  - opens Objective Journal with `Tab`,
  - sees the route hint,
  - selects the route through visible Daily Plan UI,
  - saves/reloads,
  - sees the discovered/selected state persist without duplicate rows.

### Required TDD

RED:

- EditMode tests fail while discovery clue data cannot unlock route availability through data/strategy configuration.
- UI Objective tests fail while discovered route hints appear in the wrong category or duplicate existing Goal rows.
- PlayMode E2E fails while the player-facing discovery-to-selection flow is absent.

GREEN:

- Data-driven discovery unlock tests pass.
- Objective Journal Hint/Goal category behavior passes.
- PlayMode E2E captures screenshot and reload evidence.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.EndToEnd
Get-ChildItem Builds/Logs/*discovery* -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One discovery clue path unlocks one Daily Plan route through SO data and strategy arrays.
- Objective Journal shows the route as a Hint before selection and a Goal after selection.
- No extra always-visible goal UI is introduced.
- Save/load preserves discovered and selected state without duplicates.
- Player-facing PlayMode screenshot evidence exists.

### Commit

```text
[FEATURE][TEST] Discovery clue Daily Plan route unlock 추가
```

---

## Prompt 4: Student Life Scenario Registry and Coverage Gate

Paste this prompt after Prompt 3 or whenever scenario IDs begin drifting from tests.

### Objective

Tighten the Student Life scenario registry so named scenarios for Town, Life, Career, Relationship, Job, Knowledge, Status, Network, and legacy regression slices stay synchronized with automated tests and QA runners.

### Why Fourth

The project is adding many player-facing and dedicated validation slices. Without a registry sync gate, docs, scenario IDs, tests, and QA runners can drift.

### Source Context

- `.claude/rules/testing-discipline.md`
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/`
- `Scripts/ci/check-scenario-registry-sync.sh`
- `Scripts/qa/`
- `docs/superpowers/specs/2026-05-18-player-flow-scenario-simulation-design.md`

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Inspect current scenario IDs in docs, test method names, and QA runner names.
- Add or tighten a CI script/test that fails when a registered scenario lacks an automated test or QA runner.
- Register only scenarios that are already covered or are introduced in this goal with tests.
- Do not invent broad scenario IDs without runnable coverage.
- Include dedicated process scenarios for route/relationship, career route, and Daily Plan completion.

### Required TDD

RED:

- A sync test fails while a known scenario ID lacks matching test/runner evidence.
- A sync test fails if a test method claims a scenario ID not present in the registry.

GREEN:

- Registry, tests, and QA runners agree.
- CI gate produces clear remediation output listing missing registry entries, missing tests, or orphaned tests.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Scenario registry, automated tests, and QA runners are synchronized.
- Missing or orphaned scenario IDs fail CI with clear output.
- Existing dedicated gameplay smokes are represented in the registry.
- No gameplay behavior changes are introduced in this stabilization pass.

### Commit

```text
[TEST][CHORE] Student Life scenario registry sync 가드 추가
```
