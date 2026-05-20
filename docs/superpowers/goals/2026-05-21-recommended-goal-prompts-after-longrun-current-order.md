# Recommended Goal Prompts: After Dedicated Long-Run Current Order

Date: 2026-05-21

This document lists the recommended goal prompts from the current project state. The dedicated route/relationship smoke and long-run regression are already committed, so the next order should protect saved-state correctness first, then return to small player-facing feature slices.

## Current State

- Completed foundation:
  - Client build Addressables loop hardening.
  - Dedicated route Daily Plan selection hardening.
  - Dedicated route/relationship smoke.
  - Dedicated route/relationship long-run regression.
- Current active next goal:
  - `docs/superpowers/goals/2026-05-21-quest-interior-reward-saved-state-regression-goal.md`
- Current worktree preflight:
  - `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta` are deleted in the worktree. Restore them before any implementation commit unless the current cycle explicitly owns that deletion.

## Recommended Order

1. Quest/Interior Reward Saved-State Regression Hardening
2. Career/Job Daily Plan Route Expansion
3. Second NPC Relationship Route Slice
4. House Upgrade/Interior Construction Next Slice
5. Objective Journal Legacy HUD Final Ownership Audit
6. Broader Dedicated Multiplayer Gameplay Matrix

Do not start Prompt 2 or later until Prompt 1 is complete, committed, or explicitly stopped with a documented blocker.

## Common Non-Negotiable Constraints

- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior or regression changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible or interactive changes require actual PlayMode player-facing flow, Game View or Camera screenshot evidence, runtime state inspection, and reload verification when persistence is expected.
- Runtime-only checks are not enough when scene, prefab, ScriptableObject, palette, tilemap, or serialized save state should persist.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Run `git diff --check`, `Scripts/ci/check-no-entity-id-branching.sh`, `Scripts/ci/check-addressables-linkxml-preserved.ps1`, and `git status --short` before completion.

---

## Prompt 1: Quest/Interior Reward Saved-State Regression Hardening

Paste this prompt first.

### Objective

Implement `docs/superpowers/goals/2026-05-21-quest-interior-reward-saved-state-regression-goal.md`. Harden the existing quest-to-interior reward slice so duplicate reward claims, reloads, and House placement saves cannot silently duplicate furniture unlock tokens, placed furniture tiles, occupancy cells, or saved layout records.

### Why First

The network foundation is now stable enough to move back to gameplay, but the quest/interior reward path crosses reward transaction rules, unlock persistence, placement UI, House visuals, and save/load. It is the highest-risk existing feature slice and should be protected before adding more content.

### Preflight

Restore Addressables preservation files if they are deleted:

```powershell
git restore -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git status --short
```

Do not stage unrelated generated Addressables churn.

### Required Work

- Tighten `Rootborn.Tests.EditMode.Interiors.QuestInteriorUnlockRewardTests`.
- Tighten `Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`.
- Verify duplicate unlock tokens collapse to exactly one saved token.
- Verify duplicate reward claim remains disabled and does not mutate the unlock save file.
- Verify serialized furniture unlock save data contains the unlock token exactly once.
- Verify serialized House furniture layout contains the reward furniture id and tile name exactly once after placement and reload.
- Verify runtime House object tilemap and occupancy tilemap contain exactly one restored placed furniture instance.

### Required TDD

RED:

- Add a failing EditMode contract that requires exact-once saved-state assertions in the quest/interior E2E.
- Add or tighten a test that fails if `FurnitureUnlockProgress.FromSaveData` permits duplicate unlock tokens to inflate saved-state count.
- Add or tighten PlayMode assertions that fail if duplicate claim changes the unlock file after the reward is already claimed.
- Add or tighten PlayMode assertions that fail if `house-furniture-layout*.json` contains duplicate reward furniture records after save/reload.

GREEN:

- Duplicate unlock tokens collapse to one progress token.
- Duplicate reward claim remains disabled and does not mutate the saved unlock file.
- Reloaded House layout has one reward furniture record, one visible object tile, and one occupancy cell.

### Required PlayMode Scenario

Strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town and claim the relevant quest reward through player-facing movement, interaction, and visible UI.
3. Confirm the claim button is disabled after the first claim.
4. Confirm a disabled duplicate claim attempt does not mutate the unlock save file.
5. Open Objective Journal through `Tab` and keep quest guidance under Objective Journal ownership.
6. Transition to House or Interior through the project-supported player-facing path.
7. Open placement UI.
8. Select and place the unlocked item visibly.
9. Inspect selected asset name, placement cell, object tilemap, occupancy tilemap, and overlay state.
10. Capture screenshot:

```text
Builds/Logs/quest-interior-reward-e2e.png
```

11. Save layout and inspect serialized unlock/layout files.
12. Reload the same save slot.
13. Verify the reward cannot duplicate, the unlock persists, and the placed item remains visible exactly once.
14. Reinspect serialized unlock/layout files after reload.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
Get-ChildItem Builds/Logs/quest-interior-reward-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Existing quest interior reward feature remains player-facing and visible.
- Locked furniture is absent before reward claim and available after reward claim.
- Reward claimed state survives reload and cannot be claimed again.
- Serialized furniture unlock data contains the unlock token exactly once.
- Serialized House furniture layout contains the reward furniture id and tile name exactly once.
- Runtime House tilemaps contain exactly one visible reward furniture tile and matching occupancy after reload.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

### Commit

```text
[TEST] Quest interior reward 저장 회귀 강화
```

---

## Prompt 2: Career/Job Daily Plan Route Expansion

Paste this prompt after Prompt 1 is committed or explicitly stopped with a documented blocker.

### Objective

Add one small data-driven career or part-time job route slice so Daily Plan progression is not limited to discovery, quest, and relationship sources.

### Why Second

Career/job is the next high-value student-life pillar. It reuses Daily Plan, Objective Journal, save/load, and the newly stabilized route verification paths without needing a new UI surface.

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
- Registry wiring test fails if any new activity, route, career/job, or outcome asset is missing.

### Required PlayMode Scenario

Create or strengthen:

```text
CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Open the Daily Plan board and verify the route is locked or absent before progress.
4. Use actual movement and visible UI to perform the career/job action.
5. Open the Daily Plan board again and verify the route is visible/selectable.
6. Select the route.
7. Open Objective Journal with `Tab` and verify route guidance.
8. Track it and verify exactly one `TrackedObjectiveHud`.
9. Capture screenshot:

```text
Builds/Logs/career-job-route-e2e.png
```

10. Reload the same save slot.
11. Verify career/job progress, route availability, route selection, Journal state, and HUD state restore without duplication.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload
Get-ChildItem Builds/Logs/career-job-route-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
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

## Prompt 3: Second NPC Relationship Route Slice

Paste this prompt after Prompt 2, unless Prompt 1 or 2 exposes unresolved persistence issues.

### Objective

Add one second NPC relationship route slice so relationship progress starts to feel like a reusable Town system instead of a single First Guide sample.

### Why Third

Relationship content is still thin. This should follow the saved-state and career/job route work because relationship slices feed Objective Journal, Daily Plan, save/load, and multiplayer validation.

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
- Registry wiring test fails if any new relationship, NPC, schedule, dialogue, or route asset is missing.

### Required PlayMode Scenario

Create or strengthen:

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
7. Open Objective Journal with `Tab` and verify the relationship guidance row.
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
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

---

## Prompt 4: House Upgrade/Interior Construction Next Slice

Paste this prompt after Prompt 3, or earlier only if interior construction is the next explicit product priority.

### Objective

Add one small, data-driven House upgrade or interior construction slice that connects progression requirements, upgrade availability, visible House/Interior result, and save/load persistence.

### Why Fourth

Interior placement and quest rewards now have regression pressure. The next housing feature should build on that foundation with a narrow construction slice rather than broad housing content.

### Required Work

- Pick one upgrade or construction action from the existing House expansion/interior design docs.
- Represent upgrade requirements and outcomes through ScriptableObject data or existing strategy arrays.
- Register all new data assets in `GameDataRegistry.asset`.
- Surface upgrade guidance through Objective Journal, not a new permanent HUD.
- Apply the visible House/Interior change through saved scene/prefab/data paths that survive reload.
- Verify the old and new visual states do not overlap.

### Required TDD

- Upgrade availability is false before requirements are met.
- Player-facing construction action consumes or validates required state exactly once.
- Upgrade result is persisted and restored after reload.
- Objective Journal shows upgrade guidance through existing ownership rules.
- Registry wiring test fails if upgrade data or outcome asset is missing.

### Required PlayMode Scenario

Create:

```text
HOUSE_UPGRADE_E2E_001_UnlockConstructReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter the required Town/House flow through player-facing movement or UI.
3. Verify the upgrade is unavailable before requirements.
4. Complete the required progress path through player-facing action.
5. Trigger construction or upgrade through visible UI.
6. Observe the visible House/Interior change in Game View.
7. Inspect runtime scene/tilemap/prefab/state references.
8. Capture screenshot:

```text
Builds/Logs/house-upgrade-e2e.png
```

9. Reload the same save slot.
10. Verify the upgraded visual state, serialized state, Objective Journal state, and absence of duplicate old/new objects.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UnlockConstructReload
Get-ChildItem Builds/Logs/house-upgrade-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One House upgrade/construction slice is fully player-facing.
- Requirements, outcomes, and registry wiring are data-driven.
- Visible and serialized state agree after reload.
- Objective Journal owns guidance and no new permanent objective HUD is introduced.

### Commit

```text
[FEATURE][ASSET][TEST] House upgrade construction slice 추가
```

---

## Prompt 5: Objective Journal Legacy HUD Final Ownership Audit

Paste this prompt after Prompt 4, or earlier if a new feature tries to create its own always-visible goal HUD.

### Objective

Run a final ownership audit for legacy objective-like HUDs and remove or gate any remaining always-visible goal, quest, campaign, career, milestone, or hint UI outside `ObjectiveJournalPanel` and `TrackedObjectiveHud`.

### Why Fifth

Multiple route, relationship, career, quest, and housing slices now converge on Objective Journal. A compact audit prevents future features from reintroducing scattered permanent HUD ownership.

### Required Work

- Audit production code for legacy HUD/widget factories and always-visible objective panels.
- Keep only `ObjectiveJournalPanel` and one `TrackedObjectiveHud` as persistent objective surfaces.
- Preserve modal/result feedback panels only when they are not persistent goal guidance.
- Add or tighten source ownership tests for forbidden HUD creation.
- Update affected PlayMode E2E assertions to open Objective Journal with `Tab` and track one item.

### Required TDD

- Test fails if forbidden HUD/widget types are referenced by runtime installers or factories.
- Test fails if more than one `TrackedObjectiveHud` exists in a player-facing scene.
- Test fails if a source emits duplicate stable keys across Goals, Quests, Hints, Campaign, Career, Relationship, and Housing guidance.

### Required PlayMode Scenario

Create or strengthen:

```text
OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Open Objective Journal with `Tab`.
4. Verify quest, route, relationship, career, and housing guidance appears in owned categories when available.
5. Track one item.
6. Verify exactly one `TrackedObjectiveHud`.
7. Perform one related player-facing progress action.
8. Verify Journal and tracked HUD update.
9. Capture screenshot:

```text
Builds/Logs/objective-journal-ownership-e2e.png
```

10. Reload the same save slot and verify no duplicate HUDs or rows.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
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
- Reload does not duplicate rows or HUDs.

### Commit

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 점검
```

---

## Prompt 6: Broader Dedicated Multiplayer Gameplay Matrix

Paste this prompt after Prompts 1 through 5 are complete, or earlier only if multiplayer validation becomes the release blocker.

### Objective

Expand dedicated server/client validation from the route/relationship slice into a broader gameplay matrix covering quest reward, career/job route, relationship, House/interior saved state, Objective Journal, and reload behavior.

### Why Sixth

The dedicated long-run regression proves one route/relationship path. After more slices are stable, the next infrastructure value is to prove those systems together under dedicated server/client process conditions.

### Required Work

- Extend the dedicated QA harness or add a new opt-in matrix script.
- Run separate server and client processes with explicit port and save slot.
- Exercise at least three slices:
  - route/relationship,
  - career/job route,
  - quest/interior reward or House/interior placement.
- Capture client screenshots and logs per slice.
- Verify save/reload restores each slice without duplicate Journal rows, HUDs, route states, relationship deltas, reward claims, or interior records.
- Keep the matrix opt-in if runtime is too long for standard PlayMode.

### Required TDD

- Log parser test fails if any matrix slice is missing server readiness, client readiness, screenshot, completion marker, and restored-state evidence.
- Cleanup test fails if launched server/client processes remain alive.
- Duplicate-state parser test fails if repeated reload creates duplicate objective rows, HUDs, route states, relationship deltas, reward claims, or interior records.

### Required Process Scenario

Create:

```text
NETWORK_DEDICATED_MATRIX_001_CoreGameplayReload
```

The scenario must:

1. Build or use fresh client/server artifacts.
2. Run dedicated server/client cycle for route/relationship.
3. Run dedicated server/client cycle for career/job route.
4. Run dedicated server/client cycle for quest/interior reward or House placement.
5. Capture one screenshot per client cycle.
6. Verify persisted state after reload for every slice.
7. Verify no launched process survives cleanup.

### Required Verification

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7891 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -File | Select-Object Name,Length,LastWriteTime
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated server/client validation covers multiple gameplay slices.
- Screenshots, logs, runtime state, and saved state agree for every slice.
- Reload does not duplicate Journal/HUD/gameplay state.
- No launched server/client process remains after cleanup.

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix 추가
```
