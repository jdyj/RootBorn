# Recommended Goal Prompts: After Objective Journal Source Collision Regression

Date: 2026-05-21

This document lists the recommended goal prompts from the current project state. The order is stabilization-first but now returns to player-facing feature delivery: finish the first Resource/Recipe Town activity slice, prove it in dedicated server/client smoke, then fold it into the core gameplay matrix.

## Current State

Already completed and committed:

- `998d915a3 [TEST][DOCS][CHORE] Student Life scenario coverage gate`
- `a804db655 [TEST][UI] Objective Journal source collision regression`

Currently in progress:

- First Resource/Recipe Town Activity Slice
- Goal file already exists:

```text
docs/superpowers/goals/2026-05-21-town-resource-recipe-activity-slice-goal.md
```

Recommended remaining order:

1. Finish First Resource/Recipe Town Activity Slice.
2. Add Resource/Recipe Dedicated Smoke.
3. Expand Core Gameplay Matrix.
4. Add Resource/Recipe/Discovery Scenario Catalog Follow-up.
5. Start the next player-facing Student Life feature slice.

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

## Prompt 1: Finish First Resource/Recipe Town Activity Slice

Paste this prompt first.

### Objective

Finish the first small Town resource/recipe activity slice: a player-facing Town activity grants or reveals a resource/recipe progression state, Objective Journal shows the next-step hint, and save/load plus repeat interaction cannot duplicate the progression.

### Why First

This is already the active feature slice. Finish it before starting new smoke or matrix work so dedicated validation has a real player-facing loop to prove.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Preserve existing uncommitted Resource/Recipe work unless a failing test proves it must change.
- Restore `Assets/AddressableAssetsData/link.xml` and `.meta` if Unity tooling deleted them.
- Complete recipe registration support in `GameDataRegistry`.
- Complete resource/recipe progress persistence by save slot and player identity.
- Complete one `LocationActivityOutcomeBase` resource/recipe reveal outcome.
- Create or wire one recipe asset under:

```text
Assets/Data/Recipes/
```

- Wire one Town activity to the resource/recipe outcome through ScriptableObject data.
- Register all relevant assets in:

```text
Assets/Data/Registry/GameDataRegistry.asset
```

- Complete Objective Journal source integration for resource/recipe next-step hints.
- Add or complete player-facing PlayMode E2E coverage.

### Required TDD

RED:

- Data registry test fails before recipes are exposed and registered.
- Progress persistence test fails before save slot and player identity isolation exists.
- Outcome idempotency test fails before repeated interaction is duplicate-safe.
- Objective Journal source test fails before resource/recipe hints are emitted.
- PlayMode scenario fails before the visible Town activity path and screenshot evidence exist.

GREEN:

- Registry, persistence, idempotency, Objective Journal, and PlayMode tests pass.
- The player-facing E2E produces screenshot evidence:

```text
Builds/Logs/town-resource-recipe-activity-e2e.png
```

### Required PlayMode Scenario

Create or complete:

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
8. Capture a Game View or Camera screenshot.
9. Save and reload the same slot.
10. Verify the resource/recipe state and Journal hint restore once.
11. Repeat the same interaction and verify no duplicate reward, partial transaction, or repeated unlock side effect.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Crafting
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.TownResourceRecipeActivityE2EScenarioTests
Get-ChildItem Builds/Logs/town-resource-recipe-activity-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Recipe data is registered through `GameDataRegistry`.
- Resource/recipe progress is persisted by save slot and player ID.
- A Town activity can reveal or progress a recipe through SO references, not production ID branching.
- Repeating the same request does not duplicate progression.
- Objective Journal shows resource/recipe next-step guidance and remains the only guidance owner.
- PlayMode E2E verifies visible player-facing flow, screenshot evidence, reload, and duplicate prevention.

### Commit

```text
[FEATURE][ASSET][TEST] Town resource recipe activity slice
```

---

## Prompt 2: Resource/Recipe Dedicated Smoke

Paste this prompt after Prompt 1 is committed.

### Objective

Add a dedicated server/client smoke for the first Resource/Recipe Town activity slice so the new reward loop is proven outside Editor-only PlayMode.

### Why Second

The feature touches progression, save/load, Objective Journal guidance, and likely server/client state. It should be validated through the same dedicated-process standard as Discovery and Daily Plan flows.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Add a direct validation autoplay installer or extend an existing safe installer with explicit flags:

```text
-directValidationAutoplayResourceRecipeActivity
-directValidationAutoplayResourceRecipeActivityObserveOnly
-directValidationScreenshot
```

- Add:

```text
Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1
```

- The runner must:
  - verify fresh client/server artifacts,
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

## Prompt 3: Core Gameplay Matrix Expansion

Paste this prompt after Prompt 2 is committed.

### Objective

Expand the dedicated core gameplay matrix so it includes Discovery and Resource/Recipe slices alongside the existing route/relationship, career Daily Plan, and Daily Plan completion slices.

### Why Third

The matrix should remain the compact "does the player-facing core still work in built dedicated processes" gate. Expanding it after the new dedicated smoke keeps regression coverage aligned with the playable surface.

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

---

## Prompt 4: Resource/Recipe and Discovery Scenario Catalog Follow-up

Paste this prompt after Prompt 3 is committed.

### Objective

Update the Student Life scenario catalog and coverage gate so the Discovery and Resource/Recipe dedicated smoke flows are explicitly represented as active scenarios.

### Why Fourth

The scenario gate was added before the Resource/Recipe slice. After the new feature and dedicated smoke land, the catalog should describe the current playable and network-verified surface.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

### Required Work

- Update the scenario catalog under:

```text
docs/superpowers/audits/
```

- Add active scenario rows for:
  - Discovery route unlock dedicated smoke,
  - Resource/Recipe Town activity PlayMode E2E,
  - Resource/Recipe dedicated smoke,
  - Core gameplay matrix Resource/Recipe slice.
- Ensure every active row maps to a test class, test method, or QA runner.
- Keep deferred scenarios explicit with reason and expected validation type.

### Required TDD

RED:

- Scenario registry sync fails while the new Resource/Recipe scenarios are absent or unmapped.

GREEN:

- Scenario registry sync passes with the new active scenarios documented.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Scenario catalog matches the actual active test and QA runner surface.
- Resource/Recipe coverage is no longer implicit.
- Deferred coverage remains explicit rather than hidden by omission.

### Commit

```text
[TEST][DOCS] Resource recipe scenario catalog update
```

---

## Prompt 5: Next Player-Facing Student Life Feature Slice

Paste this prompt after Prompt 4 is committed.

### Objective

Select and implement the next small player-facing Student Life feature slice using the stabilized Daily Plan, Objective Journal, scenario gate, and dedicated smoke patterns.

### Why Fifth

After Resource/Recipe is implemented, network-smoked, matrix-covered, and cataloged, the foundation is stable enough to add another vertical slice without increasing validation debt.

### Recommended Candidate Order

1. Career/Job Daily Plan route expansion.
2. Relationship activity reward or commitment route.
3. Knowledge/Status activity chain.
4. Quest/Interior reward saved-state regression hardening.

Choose the first candidate unless current code inspection shows a higher-risk regression in another candidate.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Inspect current code and scenario catalog before selecting the slice.
- Write or update one focused goal file under:

```text
docs/superpowers/goals/
```

- Keep the slice data-driven through ScriptableObject assets and strategy arrays.
- Add EditMode coverage for data, transactions, persistence, and Objective Journal integration.
- Add player-facing PlayMode E2E with screenshot and reload verification.
- Add dedicated smoke only if the slice touches core progression, persistence, or network-visible state.

### Required Verification

Use the narrow tests for the selected domain plus:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- The selected slice is small enough to complete in one implementation cycle.
- It has named EditMode and PlayMode coverage.
- Visible changes are verified through the actual player-facing flow.
- Persistence and duplicate prevention are tested when state changes.
- Objective Journal remains the owner for goal-like guidance.

### Commit

Use the prefix matching the selected slice, for example:

```text
[FEATURE][ASSET][TEST] Career job Daily Plan route expansion
```
