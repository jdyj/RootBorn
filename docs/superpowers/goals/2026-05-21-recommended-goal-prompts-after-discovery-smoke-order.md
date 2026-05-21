# Recommended Goal Prompts: After Discovery Dedicated Smoke

Date: 2026-05-21

This document gives the recommended goal prompts in execution order from the current workspace state. The order is stabilization-first: use the completed dedicated Discovery smoke as the new baseline, add a scenario coverage gate, harden Objective Journal source ownership, then return to the next player-facing feature slice.

## Current State

- Latest committed foundation:
  - `6ed50b238 [NETWORK][TEST] Discovery route unlock dedicated smoke`
  - `9e9a0483e [DOCS] Recommended goal order after core matrix`
  - `8baf7ba7c [NETWORK][TEST][DOCS] Dedicated core gameplay matrix`
- Dedicated Discovery Route Unlock Smoke was the most recent implementation cycle:
  - `Assets/Scripts/Game/Common/DirectValidationDiscoveryRouteUnlockAutoplayInstaller.cs`
  - `Assets/Tests/EditMode/Network/DedicatedDiscoveryRouteUnlockAutoplayContractTests.cs`
  - `Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1`
  - `docs/superpowers/goals/2026-05-21-dedicated-discovery-route-unlock-smoke-goal.md`
- Existing dedicated validation now covers:
  - route/relationship persistence,
  - career Daily Plan route,
  - Daily Plan completion,
  - dedicated core gameplay matrix,
  - Discovery route unlock smoke.

## Recommended Order

0. Completed prerequisite: Dedicated Discovery Route Unlock Smoke.
1. Student Life Scenario Registry and Coverage Gate.
2. Objective Journal Source Collision Regression.
3. First Resource/Recipe Town Activity Slice.
4. Resource/Recipe Dedicated Smoke.
5. Core Gameplay Matrix Expansion.

Start with Prompt 1 from the current branch state. Prompt 0 is retained only as a prerequisite audit prompt if this document is used from an older checkout that does not include the Discovery dedicated smoke commit. Prompt 1 is the best next stabilization task because the project now has enough EditMode, PlayMode, and dedicated process scenarios that undocumented drift is becoming a real maintenance risk.

## Common Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior, regression, QA script, build-script, or gate changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, clue, world-state, recipe, resource, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when the requested result should persist in a scene, prefab, ScriptableObject, palette, tilemap, or serialized asset.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.
- Run `git diff --check`, `Scripts/ci/check-addressables-linkxml-preserved.ps1`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 0: Dedicated Discovery Route Unlock Smoke Prerequisite Audit

Use this prompt only if the current checkout does not include `6ed50b238 [NETWORK][TEST] Discovery route unlock dedicated smoke`.

### Objective

Verify or finish the Dedicated Discovery Route Unlock Smoke prerequisite without mixing in new feature work.

### Why First

The next stabilization task depends on the Discovery route unlock dedicated smoke being committed and reproducible. If the prerequisite is missing, close it before starting Prompt 1.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Inspect the current staged and unstaged state.
- Confirm the dedicated runner exists:

```text
Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1
```

- Confirm the direct validation installer supports:

```text
-directValidationAutoplayDiscoveryRouteUnlock
-directValidationAutoplayDiscoveryRouteUnlockObserveOnly
-directValidationScreenshot
```

- Confirm `link.xml` preserves all direct validation and progress types needed by the built player.
- Confirm the runner performs both:
  - first action pass,
  - observe-only reload pass.
- Confirm logs include:
  - clue seen,
  - interpretation completed,
  - world-state active,
  - Daily Plan selected route,
  - Objective Journal duplicate row count,
  - HUD duplicate count,
  - completion markers for both passes.
- If save snapshot logs are misleading file-existence booleans, either:
  - improve the snapshot to report actual progress-state booleans, then rebuild and rerun the smoke, or
  - explicitly document that reload evidence, not file booleans, proves persistence.

### Required Verification

```powershell
git status --short
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1 -RunName network-dedicated-discovery-route-unlock-e2e -Port 7907 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-discovery-route-unlock-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If Unity MCP returns null for tests, run the equivalent Unity CLI command and inspect the XML result before claiming completion.

### Acceptance Criteria

- Network contract tests pass.
- Addressables/link preservation tests or guards pass.
- Dedicated action pass and observe-only reload pass both pass.
- Screenshots for both passes exist and are non-zero.
- Logs prove route unlock, reload persistence, Objective Journal ownership, HUD uniqueness, and no fatal client/server errors.
- Only this smoke cycle's files are staged.

### Commit

```text
[NETWORK][TEST] Discovery route unlock dedicated smoke
```

---

## Prompt 1: Student Life Scenario Registry and Coverage Gate

Paste this prompt after Prompt 0 is committed.

### Objective

Add a scenario registry and coverage gate so Town, Student Life, Discovery, Daily Plan, relationship, career, Objective Journal, and dedicated validation scenarios cannot drift away from documented scenario coverage.

### Why Second

The project now has many named validation flows across EditMode, PlayMode, and dedicated process runners. Before adding more gameplay slices, create a small gate that makes scenario ownership explicit.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Audit existing scenario IDs, E2E test classes, and QA runners.
- Add a scenario catalog under:

```text
docs/superpowers/audits/
```

- Add a CI/script gate that verifies documented active scenarios map to at least one test or runner.
- Cover categories:
  - `TOWN`
  - `LIFE`
  - `CAREER`
  - `REL`
  - `JOB`
  - `KNOW`
  - `STATUS`
  - `NET`
  - legacy `GEN`, `HEIR`, `CROP`
- Mark deferred scenarios explicitly with:
  - reason,
  - owner domain,
  - expected future validation type.
- Keep this task non-gameplay unless the audit exposes a broken scenario reference that must be fixed for the gate to be meaningful.

### Required TDD

RED:

- Add a gate test/script assertion that fails when a documented active scenario has no matching test or runner.
- Add a gate test/script assertion that fails when a scenario category appears in tests but is undocumented.
- Add a fixture or temporary failing sample only if it is removed before GREEN.

GREEN:

- All active documented scenarios map to tests or QA runners.
- Deferred scenarios are explicitly excluded by status, not by omission.
- Existing tests and runners remain unchanged unless the audit finds stale names.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the full EditMode suite is too slow or blocked by unrelated failures, run the new gate test/script plus the nearest affected namespaces and document the remaining blocker precisely.

### Acceptance Criteria

- Scenario catalog exists and is current.
- CI fails on scenario documentation and executable coverage drift.
- No gameplay behavior changes are introduced.
- Deferred coverage is explicit rather than hidden.
- The gate can be run locally with one command.

### Commit

```text
[TEST][DOCS][CHORE] Student Life scenario coverage gate
```

---

## Prompt 2: Objective Journal Source Collision Regression

Paste this prompt after Prompt 1 is committed.

### Objective

Harden Objective Journal source aggregation so Quest, Campaign, Daily Plan, Discovery Hint, Relationship, Career, and completed-route entries cannot collide, duplicate, or leak into separate HUD ownership.

### Why Third

Discovery and Daily Plan now feed Objective Journal from multiple sources. Before adding another content loop, protect the source aggregation contract and the one-HUD ownership rule.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Add or tighten Objective Journal source aggregation tests.
- Verify source rows have stable keys and categories.
- Verify completed Daily Plan entries do not remain active goals.
- Verify discovery hints, career hints, relationship hints, and quest rows can coexist.
- Verify `TrackedObjectiveHud` remains unique.
- Add a static gate if any goal-like UI creates a separate always-visible Canvas/HUD.
- Keep all goal-like guidance routed through `ObjectiveJournalPanel` and `TrackedObjectiveHud`.

### Required TDD

RED:

- Aggregation test fails when two sources emit the same key/category incorrectly.
- Ownership test fails if a source creates a separate persistent HUD.
- Completed route filtering test fails if a completed Daily Plan route remains active guidance.
- Discovery/career/relationship coexistence test fails if one source hides or overwrites another.

GREEN:

- Objective Journal source matrix tests pass.
- HUD uniqueness tests pass.
- Completed route filtering is deterministic.

### Required PlayMode Scenario

Create or extend:

```text
OBJECTIVE_JOURNAL_E2E_002_SourceCollisionNoDuplicateReload
```

The scenario must:

1. Start from normal player-facing Town flow.
2. Produce at least two objective sources through visible interactions or existing scenario setup where direct setup is already part of the test harness.
3. Open Objective Journal with Tab.
4. Verify Quest, Daily Plan, Discovery Hint, Career, and Relationship sections do not collide.
5. Track one item and verify exactly one `TrackedObjectiveHud`.
6. Complete or mark one Daily Plan route complete through the supported flow.
7. Reopen Objective Journal and verify completed route is no longer active guidance.
8. Capture a Game View or Camera screenshot.
9. Save and reload.
10. Verify rows and HUD remain unique after reload.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.ObjectiveJournal
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalSourceCollisionE2EScenarioTests
Get-ChildItem Builds/Logs/objective-journal-source-collision-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the exact namespace differs, use `rg "ObjectiveJournal" Assets/Tests` to find the nearest current namespace and record the substitution.

### Acceptance Criteria

- Multiple objective sources coexist without duplicate rows.
- Completed routes are filtered from active guidance.
- Discovery, career, relationship, quest, and Daily Plan rows keep stable ownership.
- Only `ObjectiveJournalPanel` and `TrackedObjectiveHud` own objective UI.
- Screenshot and reload evidence exist for the player-facing scenario.

### Commit

```text
[TEST][UI] Objective Journal source collision regression
```

---

## Prompt 3: First Resource/Recipe Town Activity Slice

Paste this prompt after Prompt 2 is committed.

### Objective

Add the first small Town resource/recipe activity slice that connects exploration or activity output to a data-driven recipe/resource progression path without reviving legacy Farm as the main loop.

### Why Fourth

After Discovery, Daily Plan, and Objective Journal are stable, the next player value is a tangible reward loop: find or earn a resource, unlock or progress a recipe, and see next-step guidance.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Choose one existing Town activity or location as the source.
- Add ScriptableObject resource data under:

```text
Assets/Data/Resources/
```

- Add ScriptableObject recipe data under:

```text
Assets/Data/Recipes/
```

- Register all new SOs in:

```text
Assets/Data/Registry/GameDataRegistry.asset
```

- Add one data-driven outcome strategy that awards or reveals the resource/recipe.
- Add Objective Journal hint guidance for the next step.
- Do not use legacy crop-specific or tool-specific production branches.
- Save/load must restore resource/recipe progress exactly once.

### Required TDD

RED:

- Data registry test fails before the resource/recipe assets are registered.
- Transaction/idempotency test fails before duplicate reward prevention exists.
- Objective Journal hint test fails before next-step guidance is wired.
- PlayMode E2E fails before the visible Town activity path grants or reveals the resource/recipe.

GREEN:

- Registry, transaction, idempotency, and Objective Journal tests pass.
- Player-facing PlayMode E2E passes with screenshot and reload evidence.

### Required PlayMode Scenario

Create:

```text
TOWN_RESOURCE_RECIPE_E2E_001_ActivityRewardHintReloadNoDuplicate
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Move to the activity or location source using player movement.
4. Use the visible interaction/UI path.
5. Receive or reveal the resource/recipe.
6. Open Objective Journal with Tab.
7. Verify the next-step hint.
8. Capture a Game View or Camera screenshot:

```text
Builds/Logs/town-resource-recipe-activity-e2e.png
```

9. Save and reload the same slot.
10. Verify the resource/recipe state and Journal hint restore once.
11. Repeat the same interaction and verify no duplicate reward, partial transaction, or repeated unlock side effect.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Resources
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Recipes
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.TownResourceRecipeActivityE2EScenarioTests
Get-ChildItem Builds/Logs/town-resource-recipe-activity-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Resource/recipe progression is data-driven through SO assets and strategy arrays.
- Reward transaction is atomic and idempotent.
- Player-facing Town activity grants or reveals the resource/recipe.
- Objective Journal owns the next-step guidance.
- Save/load and repeated interaction do not duplicate state.
- Screenshot evidence proves the visible flow.

### Commit

```text
[FEATURE][ASSET][TEST] Town resource recipe activity slice
```

---

## Prompt 4: Resource/Recipe Dedicated Smoke

Paste this prompt after Prompt 3 is committed.

### Objective

Add a dedicated server/client smoke for the first Resource/Recipe Town activity slice so the new reward loop is proven outside Editor-only PlayMode.

### Why Fifth

Prompt 3 adds a player-facing reward/progression loop. The project standard now requires real dedicated server/client smoke for core loops that touch save/load and network persistence.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Add a direct validation autoplay installer or extend an existing safe installer with explicit flags:

```text
-directValidationAutoplayResourceRecipeActivity
-directValidationAutoplayResourceRecipeActivityObserveOnly
```

- Add:

```text
Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1
```

- The runner must:
  - use fresh client/server artifact checks,
  - run action and observe-only reload passes,
  - capture screenshots for both passes,
  - assert resource, recipe, Journal, HUD, transaction, and save-state markers,
  - fail on fatal logs, duplicate reward state, stale artifacts, missing screenshots, missing markers, or leaked processes.
- The autoplay flow must drive the player-facing interaction/UI path as far as the dedicated harness supports.
- The installer must not directly mutate resource/recipe/save state as its proof of completion.

### Required TDD

RED:

- Network contract test fails while the dedicated runner is missing.
- Contract test fails if flags, screenshots, observe-only mode, artifact freshness, cleanup, or duplicate-state assertions are missing.
- Contract test fails if the autoplay installer directly calls resource/recipe grant or save-state mutation APIs as a shortcut.

GREEN:

- Network contract tests pass.
- Dedicated smoke action pass and observe-only reload pass complete.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1 -RunName network-dedicated-resource-recipe-activity-e2e -Port 7911 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-resource-recipe-activity-e2e -File | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated action pass grants or reveals the resource/recipe through real server/client processes.
- Observe-only reload proves state persisted and does not replay the reward.
- Screenshots for both passes exist and are non-zero.
- Logs prove no duplicate resource, recipe, Journal, HUD, transaction, or fatal client/server state.
- Runner cleans up all launched processes.

### Commit

```text
[NETWORK][TEST] Resource recipe activity dedicated smoke
```

---

## Prompt 5: Core Gameplay Matrix Expansion

Paste this prompt after Prompt 4 is committed.

### Objective

Expand the dedicated core gameplay matrix so it includes the newest Discovery and Resource/Recipe slices alongside the existing route/relationship, career Daily Plan, and Daily Plan completion slices.

### Why Sixth

The matrix should remain the compact "does the player-facing core still work in built dedicated processes" gate. Expanding it after each new dedicated slice keeps regression coverage aligned with the actual playable surface.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Extend the matrix runner to include:
  - Discovery route unlock dedicated smoke,
  - Resource/Recipe activity dedicated smoke.
- Preserve existing slices:
  - route/relationship persistence,
  - career Daily Plan route,
  - Daily Plan completion.
- Keep per-slice ports, run names, logs, and screenshots isolated.
- Add or update matrix contract tests so a missing slice fails.
- Keep the matrix opt-in if runtime is too long for normal local iteration.

### Required TDD

RED:

- Matrix contract test fails when the Discovery slice is absent.
- Matrix contract test fails when the Resource/Recipe slice is absent.
- Matrix contract test fails if a slice lacks action pass, observe-only reload pass, screenshot, reload screenshot, and log marker checks.

GREEN:

- Matrix contract tests pass.
- Matrix runner completes all included slices.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7921 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -Recurse -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Matrix includes every current core dedicated slice.
- Every slice has first-pass and observe-only reload evidence.
- Every slice has non-zero screenshot evidence.
- Matrix logs prove cleanup and no fatal client/server errors.
- The matrix remains runnable as one local QA command.

### Commit

```text
[NETWORK][TEST] Core gameplay matrix coverage expansion
```
