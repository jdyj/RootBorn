# Recommended Goal Prompts: Current Next Sequence

Date: 2026-05-21

This document lists the recommended goal prompts from the current committed state. Earlier stabilization goals for Quest/Interior reward saved-state hardening and Career/Job Daily Plan saved-state hardening are already committed. The current workspace has an in-progress test edit for Second NPC relationship saved-state hardening, so the first prompt is to finish that cycle before starting new feature work.

## Recommended Order

1. Finish Second NPC Relationship Saved-State Regression Hardening
2. House Upgrade/Interior Construction Next Slice
3. Objective Journal Legacy HUD Final Ownership Audit
4. Broader Dedicated Multiplayer Gameplay Matrix

Do not start Prompt 2 or later until Prompt 1 is complete, committed, or explicitly stopped with a documented blocker.

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

## Prompt 1: Finish Second NPC Relationship Saved-State Regression Hardening

Paste this prompt first.

### Objective

Implement and complete `docs/superpowers/goals/2026-05-21-second-npc-relationship-saved-state-regression-goal.md`. Harden the existing Teacher relationship milestone slice so serialized relationship progress, milestone progress, Objective Journal rows, tracked HUD state, day-result behavior, and reload persistence cannot silently duplicate or drift.

### Why First

This work is already in progress in the workspace. Finishing it keeps the active TDD cycle coherent and closes the relationship saved-state risk before adding more relationship, housing, or multiplayer matrix coverage.

### Preflight

```powershell
git status --short
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
```

If `Assets/AddressableAssetsData/link.xml` or `.meta` are missing, restore them before continuing:

```powershell
git restore -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta
```

### Required Work

- Finish the RED/GREEN cycle in `Assets/Tests/EditMode/StudentLife/SecondNpcRelationshipRouteTests.cs`.
- Strengthen `Assets/Tests/PlayMode/EndToEnd/SecondNpcRelationshipMilestoneE2EScenarioTests.cs`.
- Verify serialized StudentLife save data contains the Teacher relationship record exactly once after the help action, day result, reload, and repeated result interaction.
- Verify serialized MilestoneProgress save data contains the Teacher milestone and Teacher objective exactly once after completion and reload.
- Verify Objective Journal rows for the Teacher milestone do not duplicate before completion, after completion, or after reload.
- Verify exactly one `TrackedObjectiveHud` exists before completion, after completion, and after reload.
- Verify repeated day-result interaction does not mutate milestone save data or reapply relationship growth.

### Required TDD

RED:

- Run the new EditMode source contract and confirm it fails because the E2E still uses containment-only save assertions.
- The expected RED failure should identify missing exact-once helper/assertion coverage in the Second NPC relationship E2E.

GREEN:

- Add exact-once helper assertions to the E2E.
- Count serialized relationship, milestone, and objective records using stable serialized values or field-specific patterns, not loose substring checks when raw IDs appear in logs.
- Add Objective Journal row-count assertions and tracked HUD uniqueness assertions.

### Required PlayMode Scenario

Strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Move to the Teacher NPC through player-facing movement.
4. Use the interact key or visible help/dialogue UI to perform the Teacher support action.
5. Verify visible relationship/milestone result text.
6. Open Objective Journal with `Tab`.
7. Verify the Teacher milestone row appears exactly once.
8. Track it and verify exactly one `TrackedObjectiveHud`.
9. Complete the day-result path.
10. Capture screenshot:

```text
Builds/Logs/npc-relationship-second-slice-e2e.png
```

11. Inspect serialized StudentLife and MilestoneProgress save files.
12. Reload the same save slot.
13. Verify relationship progress, completed milestone state, Journal row state, and tracked HUD state restore without duplication.
14. Reopen the day-result path and verify milestone save data and relationship value do not mutate.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
Get-ChildItem Builds/Logs/npc-relationship-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Existing Teacher relationship milestone remains player-facing and visible.
- Teacher NPC/help action is reached through actual movement, interact key, and visible UI.
- Serialized StudentLife save data contains the Teacher relationship record exactly once.
- Serialized MilestoneProgress save data contains the Teacher milestone and objective records exactly once.
- Objective Journal shows the Teacher milestone row exactly once before completion, after completion, and after reload.
- Exactly one tracked HUD exists and restores after reload.
- Repeated day-result interaction does not mutate milestone save data or relationship value.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

### Commit

```text
[TEST] Second NPC relationship saved-state regression hardening
```

---

## Prompt 2: House Upgrade/Interior Construction Next Slice

Paste this prompt after Prompt 1 is committed or explicitly stopped with a documented blocker.

### Objective

Implement one small, data-driven House upgrade or interior construction slice that connects progression requirements, upgrade availability, visible House/Interior result, Objective Journal guidance, and save/load persistence.

### Why Second

Quest/interior reward and saved-state tests are now stronger. The next player-facing value should build on that foundation with a narrow housing slice rather than broad housing content.

### Required Work

- Use the existing design docs:
  - `docs/superpowers/specs/2026-05-17-house-upgrade-and-interior-construction-design.md`
  - `docs/superpowers/specs/2026-05-19-house-expansion-interior-loop-design.md`
- Pick one upgrade or construction action small enough for a single vertical slice.
- Represent requirements and outcomes through ScriptableObject data or existing strategy arrays.
- Register all new data assets in `Assets/Data/Registry/GameDataRegistry.asset`.
- Surface upgrade guidance through `ObjectiveJournalPanel`, not a new permanent HUD.
- Apply the visible House/Interior change through saved scene, prefab, data, or save-state paths that survive reload.
- Verify old and new visual states do not overlap.

### Required TDD

RED:

- Upgrade availability is false before data-defined requirements are met.
- Registry wiring fails if upgrade data, requirement data, or outcome data is missing.
- Objective Journal ownership test fails if housing guidance is emitted outside the owned source contract.
- Save/load test fails if upgrade state is not restored.

GREEN:

- Player-facing construction action validates or consumes required state exactly once.
- Upgrade result is persisted and restored after reload.
- Journal guidance appears through existing ownership rules.

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
7. Inspect runtime scene, tilemap, prefab, renderer, and saved-state references relevant to the chosen slice.
8. Open Objective Journal with `Tab`, verify housing guidance, and track one item if applicable.
9. Capture screenshot:

```text
Builds/Logs/house-upgrade-e2e.png
```

10. Reload the same save slot.
11. Verify the upgraded visual state, serialized state, Objective Journal state, and absence of duplicate old/new objects.

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

- One House upgrade or construction slice is fully player-facing.
- Requirements, outcomes, and registry wiring are data-driven.
- Visible runtime state and serialized saved state agree after reload.
- Objective Journal owns guidance.
- No new permanent objective HUD is introduced.
- No stale sample/debug tilemap, placeholder object, or overlapping old/new House visual remains visible.

### Commit

```text
[FEATURE][ASSET][TEST] House upgrade construction slice add
```

---

## Prompt 3: Objective Journal Legacy HUD Final Ownership Audit

Paste this prompt after Prompt 2, or earlier if a new feature tries to create its own always-visible goal HUD.

### Objective

Run a final ownership audit for legacy objective-like HUDs and remove, gate, or test-block any remaining always-visible goal, quest, campaign, career, milestone, hint, or housing UI outside `ObjectiveJournalPanel` and `TrackedObjectiveHud`.

### Why Third

Quest, career, relationship, and housing slices now converge on Objective Journal. A compact ownership audit prevents future features from reintroducing scattered permanent HUD surfaces.

### Required Work

- Audit production code for legacy HUD/widget factories and always-visible objective panels.
- Keep only `ObjectiveJournalPanel` and one `TrackedObjectiveHud` as persistent objective surfaces.
- Preserve modal/result feedback panels only when they are not persistent goal guidance.
- Add or tighten source ownership tests for forbidden HUD creation.
- Update affected PlayMode E2E assertions to open Objective Journal with `Tab` and track one item.
- Verify tracked rows disappear or update cleanly when their stable key is completed, hidden, or reloaded.

### Required TDD

RED:

- Test fails if forbidden HUD/widget types are referenced by runtime installers or factories.
- Test fails if more than one `TrackedObjectiveHud` exists in a player-facing scene.
- Test fails if a source emits duplicate stable keys across Goals, Quests, Hints, Campaign, Career, Relationship, and Housing guidance.

GREEN:

- Only Objective Journal and one tracked HUD own persistent objective guidance.
- Existing modal/result panels remain available for transient feedback.
- Save/load does not duplicate rows or HUDs.

### Required PlayMode Scenario

Create or strengthen:

```text
OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
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
9. Verify Journal and tracked HUD update.
10. Capture screenshot:

```text
Builds/Logs/objective-journal-ownership-e2e.png
```

11. Reload the same save slot and verify no duplicate HUDs or rows.

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
- Reload does not duplicate rows or HUDs.
- New feature prompts can point to this test as the ownership gate.

### Commit

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership final audit
```

---

## Prompt 4: Broader Dedicated Multiplayer Gameplay Matrix

Paste this prompt after Prompts 1 through 3 are complete, or earlier only if multiplayer validation becomes the release blocker.

### Objective

Expand dedicated server/client validation from the route/relationship slice into a broader gameplay matrix covering quest reward, career/job route, relationship, House/interior saved state, Objective Journal, and reload behavior.

### Why Fourth

The dedicated long-run regression proves one route/relationship path. After saved-state and UI ownership hardening, the next infrastructure value is proving those systems together under dedicated server/client process conditions.

### Required Work

- Extend the dedicated QA harness or add a new opt-in matrix script.
- Run separate server and client processes with explicit port and save slot.
- Exercise at least three slices:
  - route/relationship,
  - career/job route,
  - quest/interior reward or House upgrade/placement.
- Capture client screenshots and logs per slice.
- Verify save/reload restores each slice without duplicate Journal rows, HUDs, route states, relationship deltas, reward claims, upgrade states, or interior records.
- Keep the matrix opt-in if runtime is too long for standard PlayMode.

### Required TDD

RED:

- Log parser test fails if any matrix slice is missing server readiness, client readiness, screenshot, completion marker, and restored-state evidence.
- Cleanup test fails if launched server/client processes remain alive.
- Duplicate-state parser test fails if repeated reload creates duplicate objective rows, HUDs, route states, relationship deltas, reward claims, upgrade states, or interior records.

GREEN:

- Each matrix slice emits explicit server/client readiness, action, screenshot, saved-state, reload, and cleanup evidence.
- Process cleanup is verified after every cycle.

### Required Process Scenario

Create:

```text
NETWORK_DEDICATED_MATRIX_001_CoreGameplayReload
```

The scenario must:

1. Build or use fresh client/server artifacts.
2. Run dedicated server/client cycle for route/relationship.
3. Run dedicated server/client cycle for career/job route.
4. Run dedicated server/client cycle for quest/interior reward or House upgrade/placement.
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
- Reload does not duplicate Journal, HUD, or gameplay state.
- No launched server/client process remains after cleanup.
- Failures distinguish build artifact problems, network process problems, and gameplay saved-state problems.

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix add
```
