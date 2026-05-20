# Recommended Goal Prompts: After House E2E Current Order

Date: 2026-05-21

This document lists the recommended next goal prompts from the current committed state after:

- Second NPC relationship saved-state regression hardening
- Career/job Daily Plan saved-state regression hardening
- Quest/interior reward saved-state regression hardening
- House upgrade/interior loop E2E hardening

Recommended priority is stabilization first, then broader multiplayer validation, then repeatable QA automation, then the next player-facing feature slice.

## Recommended Order

1. Objective Journal Legacy HUD Final Ownership Audit
2. Dedicated Core Gameplay Matrix
3. House Upgrade Interior Loop QA Runner
4. Daily Plan Result Feedback and Route Completion Slice

Do not start Prompt 2 or later until Prompt 1 is complete, committed, or explicitly stopped with a documented blocker. If a later prompt exposes a direct blocker in an earlier prompt, return to the earlier prompt and close the gap first.

## Common Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior or regression changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when scene, prefab, ScriptableObject, palette, tilemap, or serialized save state should persist.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.
- Run `git diff --check`, `Scripts/ci/check-addressables-linkxml-preserved.ps1`, `Scripts/ci/check-no-entity-id-branching.sh`, and `git status --short` before completion.

---

## Prompt 1: Objective Journal Legacy HUD Final Ownership Audit

Paste this prompt first.

### Objective

Perform the final ownership audit for legacy objective-like HUDs and block any persistent goal, quest, campaign, career, relationship, housing, milestone, route, or hint UI outside `ObjectiveJournalPanel` and `TrackedObjectiveHud`.

### Why First

Recent Quest, Career, Relationship, and House loops now depend on Objective Journal as the single guidance surface. Before adding more visible gameplay, close the regression risk where a new or old installer creates a second always-visible objective HUD.

### Source Context

- `docs/superpowers/specs/2026-05-19-objective-journal-legacy-hud-migration-design.md`
- `docs/superpowers/specs/2026-05-20-objective-journal-legacy-hud-full-migration-goal.md`
- `Assets/Scripts/UI/Objectives/`
- `Assets/Tests/EditMode/UI/Objectives/`
- `Assets/Tests/PlayMode/EndToEnd/`

### Required Work

- Audit production UI code for legacy HUD, widget, installer, factory, or canvas creation paths.
- Keep only `ObjectiveJournalPanel` and one `TrackedObjectiveHud` as persistent objective guidance.
- Classify transient result, reward, dialogue, and log panels as allowed only when they are not persistent objective guidance.
- Add or tighten source ownership tests for forbidden HUD creation.
- Add a stable-key duplicate gate across Goals, Quests, Hints, Campaign, Career, Relationship, Housing, and route guidance.
- Update or create the player-facing E2E ownership scenario.

### Required TDD

RED:

- Add or tighten a test that fails if forbidden legacy HUD/widget types are referenced by runtime installers or factories.
- Add or tighten a test that fails if more than one `TrackedObjectiveHud` exists in a player-facing scene.
- Add or tighten a test that fails if Objective Journal sources emit duplicate stable keys across categories.

GREEN:

- Only `ObjectiveJournalPanel` and one `TrackedObjectiveHud` own persistent objective guidance.
- Existing transient feedback panels remain available.
- Save/load does not duplicate rows, stable keys, or HUDs.

### Required PlayMode Scenario

Create or strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalOwnershipE2EScenarioTests.OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Produce at least two guidance source states through existing player-facing flows.
4. Open Objective Journal with `Tab`.
5. Verify quest, route, relationship, career, and housing guidance appears in owned categories when available.
6. Track one item.
7. Verify exactly one `TrackedObjectiveHud`.
8. Perform one related player-facing progress action.
9. Verify Journal rows and tracked HUD update.
10. Capture screenshot:

```text
Builds/Logs/objective-journal-ownership-e2e.png
```

11. Reload the same save slot.
12. Verify no duplicate HUDs, rows, stable keys, or stale legacy roots appear.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalOwnershipE2EScenarioTests.OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
Get-ChildItem Builds/Logs/objective-journal-ownership-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Persistent objective-like UI is owned only by Objective Journal and one tracked HUD.
- No legacy goal HUD factory or always-visible objective panel remains active.
- Existing player-facing guidance still appears through Objective Journal.
- Reload does not duplicate rows, stable keys, or HUDs.
- New feature prompts can point to this test as the ownership gate.

### Commit

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 감사
```

---

## Prompt 2: Dedicated Core Gameplay Matrix

Paste this prompt after Prompt 1 is committed or explicitly stopped with a documented blocker.

### Objective

Expand dedicated server/client validation from the existing route/relationship foundation into a broader opt-in gameplay matrix covering route/relationship, career/job route, quest/interior reward or House upgrade, Objective Journal tracking, screenshots, reload, and cleanup.

### Why Second

Saved-state and Objective Journal ownership risks are the highest local regressions. Once those are locked, the next risk is whether the same loops survive dedicated server/client process conditions instead of only Editor PlayMode.

### Source Context

- `docs/superpowers/goals/2026-05-21-dedicated-route-relationship-longrun-regression-goal.md`
- `docs/superpowers/goals/2026-05-21-dedicated-client-autoplay-evidence-contract-goal.md`
- `Scripts/qa/`
- `Assets/Tests/EditMode/Network/`
- `Assets/Tests/PlayMode/Network/`

### Required Work

- Extend the dedicated QA harness or add a new opt-in matrix script:

```text
Scripts/qa/run-dedicated-core-gameplay-matrix.ps1
```

- Run separate server and client processes with explicit port, run name, save slot, timeout, and screenshot paths.
- Exercise at least three slices:
  - route/relationship,
  - career/job route,
  - quest/interior reward or House upgrade/interior placement.
- Capture one client screenshot and isolated logs per slice.
- Verify save/reload restores each slice without duplicate Journal rows, HUDs, route states, relationship deltas, reward claims, upgrade states, or interior records.
- Keep the matrix opt-in if runtime is too long for normal PlayMode.

### Required TDD

RED:

- Add a parser/contract test that fails if any matrix slice lacks server readiness, client readiness, screenshot, completion marker, saved-state evidence, restored-state evidence, and cleanup evidence.
- Add a cleanup test that fails if launched server/client processes can remain alive after a failed cycle.
- Add a duplicate-state parser test that fails if logs show duplicate Objective Journal rows, HUDs, route state, reward claims, or placement records.

GREEN:

- Each matrix slice emits explicit server/client readiness, player-flow action, screenshot, saved-state, reload, and cleanup evidence.
- Process cleanup is verified after every cycle.
- Missing screenshots or failed autoplay logs fail closed.

### Required Process Scenario

Create:

```text
NETWORK_DEDICATED_MATRIX_001_CoreGameplayReload
```

The scenario must:

1. Build or use fresh client/server artifacts that include current committed code.
2. Run a dedicated server/client cycle for route/relationship.
3. Run a dedicated server/client cycle for career/job route.
4. Run a dedicated server/client cycle for quest/interior reward or House upgrade/placement.
5. Capture one screenshot per client cycle.
6. Verify persisted state after reload for every slice.
7. Verify no launched process survives cleanup.

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

---

## Prompt 3: House Upgrade Interior Loop QA Runner

Paste this prompt after Prompt 2 is committed, or earlier only if House loop regressions start recurring during feature work.

### Objective

Turn the House upgrade/interior E2E into a repeatable QA runner that can execute the player-facing stage-1 upgrade, preset, furniture placement, screenshot, saved-state, reload, and overlay-cleanup checks outside one Unity test method.

### Why Third

The House E2E is now a high-value visual/persistence gate. A runner makes the same strict scenario easier to repeat after future housing, UI, camera, tilemap, or build changes.

### Source Context

- `docs/superpowers/goals/2026-05-21-house-upgrade-interior-loop-e2e-hardening-goal.md`
- `Assets/Tests/PlayMode/EndToEnd/HouseUpgradeInteriorLoopE2EScenarioTests.cs`
- `Assets/Tests/PlayMode/Interiors/`
- `Scripts/qa/`

### Required Work

- Add an opt-in QA script:

```text
Scripts/qa/run-house-upgrade-interior-loop.ps1
```

- The script must run the strict House E2E test method and then inspect screenshot and log evidence.
- The script must preserve Addressables `link.xml` and `.meta` after Unity tooling.
- The script must print a compact evidence summary:
  - test result,
  - screenshot path and byte length,
  - House stage,
  - selected room preset,
  - placed furniture id,
  - anchor cell,
  - direction,
  - object tile count,
  - occupancy tile count,
  - overlay cleanup status.
- Add contract tests for the script if this repository already has script contract tests; otherwise add the smallest existing-pattern test that verifies the required script text and expected evidence markers.

### Required TDD

RED:

- Add a test that fails because `Scripts/qa/run-house-upgrade-interior-loop.ps1` is missing or does not reference the strict EndToEnd method.
- Add a test that fails if the script does not require screenshot evidence and Addressables preservation.
- Add a test that fails if the script does not expose saved-state and runtime tilemap evidence markers.

GREEN:

- The script runs the target E2E test and fails closed when screenshot or evidence markers are missing.
- Script contract tests pass.
- The underlying House E2E remains passing.

### Required QA Scenario

The runner must execute:

```text
Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
```

The scenario must still prove:

1. Town House upgrade UI path.
2. Stage-1 upgrade.
3. House scene entry or supported player-facing House flow.
4. Room preset application.
5. Furniture select, rotate, place, move, and save.
6. Runtime tilemap/object/occupancy state.
7. Screenshot:

```text
Builds/Logs/house-upgrade-interior-loop-e2e.png
```

8. Serialized House and furniture layout exact-once checks.
9. Reloaded visual/runtime/saved-state agreement.
10. No stale preview/sample/debug/construction overlay remains visible.

### Required Verification

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-house-upgrade-interior-loop.ps1 -RunName house-upgrade-interior-loop
Get-ChildItem Builds/Logs/house-upgrade-interior-loop-e2e.png | Select-Object Name,Length,LastWriteTime
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Housing
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One command repeats the strict House E2E visual/persistence scenario.
- Missing screenshot, missing saved-state marker, missing runtime tilemap marker, or missing overlay cleanup marker fails the runner.
- The script leaves no unrelated dirty state except expected logs.
- Addressables `link.xml` and `.meta` remain preserved.

### Commit

```text
[TEST][CHORE] House upgrade interior QA runner 추가
```

---

## Prompt 4: Daily Plan Result Feedback and Route Completion Slice

Paste this prompt after Prompts 1-3 are complete, or earlier only if the next milestone must be a player-facing feature rather than QA infrastructure.

### Objective

Improve the player-facing Daily Plan route completion loop so completed route results show clear transient feedback, update Objective Journal rows, preserve selected/completed route state, and restore correctly after reload without adding a new persistent HUD.

### Why Fourth

After ownership, multiplayer, and QA gates are stronger, the next small feature value is to make Daily Plan route completion feel complete to the player. This builds on the current route, relationship, career/job, and Objective Journal foundations without adding a large new system.

### Source Context

- `docs/superpowers/specs/2026-05-20-daily-plan-route-selection-design.md`
- `docs/superpowers/specs/2026-05-20-discovery-clue-daily-plan-route-unlock-design.md`
- `docs/superpowers/goals/2026-05-21-daily-plan-route-completion-result-feedback-goal.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/UI/Objectives/`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

### Required Work

- Add or tighten Daily Plan completion result feedback through an existing transient result panel or existing UI pattern.
- Do not create a new always-visible route HUD.
- Ensure Objective Journal shows the selected route, completed route, and next recommendation state through owned categories.
- Ensure completion state saves exactly once and reloads without replaying rewards or relationship/job/skill deltas.
- Keep route behavior data-driven through existing ScriptableObject definitions and strategy arrays.

### Required TDD

RED:

- Add a test that fails if completed route state is not saved exactly once.
- Add a test that fails if repeated result interaction re-applies route outcomes.
- Add a test that fails if route completion guidance appears outside `ObjectiveJournalPanel` or `TrackedObjectiveHud`.
- Add a source/contract test that fails if the route E2E lacks visible result, Journal update, screenshot, saved-state, and reload assertions.

GREEN:

- Route completion feedback is visible as transient feedback.
- Objective Journal updates through the owned source.
- Save/reload restores selected and completed route state without duplication or reapplication.

### Required PlayMode Scenario

Create or strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_001_CompleteResultReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Select a Daily Plan route through visible UI.
4. Complete the route through player-facing movement, interaction, or supported route flow.
5. Verify visible transient result feedback.
6. Open Objective Journal with `Tab`.
7. Verify selected/completed route row state and one tracked HUD if tracked.
8. Capture screenshot:

```text
Builds/Logs/daily-plan-route-completion-e2e.png
```

9. Inspect serialized route/progress save state.
10. Reload the same save slot.
11. Verify route completion state restores without duplicate rows, duplicate HUDs, or re-applied outcomes.
12. Reopen result feedback if supported and verify it does not mutate completed state.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_001_CompleteResultReload
Get-ChildItem Builds/Logs/daily-plan-route-completion-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Daily Plan route completion is visible to the player through transient feedback.
- Objective Journal owns route guidance and completion state.
- Exactly one tracked HUD exists when tracking is used.
- Save/load restores selected and completed route state without duplication.
- Repeated result interaction does not reapply route rewards, relationship deltas, job/career deltas, skill deltas, or milestone progress.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

### Commit

```text
[FEATURE][UI][TEST] Daily Plan route result feedback 추가
```
