# Goal Prompt Sequence: Recommended Next ROOTBORN Work

Date: 2026-05-20

Use these prompts in order. The sequence assumes the workspace dirty-state cleanup has already been resolved and the recent Discovery route unlock second slice is committed.

Recommended order:

1. Addressables `link.xml` Preservation Guard
2. Discovery Route Unlock E2E Diagnostics Hardening
3. Objective Journal Route Source Contract Hardening
4. NPC Routine Relationship Milestone Slice

Do not start prompt 2 until prompt 1 is committed. Do not start prompt 4 until prompt 2 and 3 have passed their verification gates.

---

## Prompt 1: Addressables `link.xml` Preservation Guard

Paste this prompt into a goal session and execute it as written.

### Objective

Add a small foundation guard that prevents critical runtime ScriptableObject and registry types from silently dropping out of `Assets/AddressableAssetsData/link.xml`.

This is the recommended first task because `link.xml` was recently restored from an unsafe dirty workspace state, while current gameplay direction keeps adding data-driven route, clue, world-state, and Objective Journal integrations.

### Non-Negotiable Constraints

- This is a foundation/test cycle, not gameplay behavior work.
- Do not change player-facing UI, scenes, prefabs, or gameplay behavior.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for C# tests.
- Keep all gameplay/entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity, clue, route, location, activity, quest, world-state, crop, tool, or NPC IDs.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create an EditMode test under a suitable foundation or addressables test folder.

The test must parse `Assets/AddressableAssetsData/link.xml` as XML and assert that critical runtime data types are preserved, including:

- `Rootborn.Game.Common.GameDataRegistry`
- current Discovery Clue definition/outcome types used by route unlocks
- current Daily Plan route definition/requirement types used by route unlocks
- current World State flag/progress types used by route unlocks
- current Student Life location/activity definition types used by Daily Plan routes

Keep the required type list focused. The test should protect current registry-driven flows without becoming a broad mirror of every type in the project.

If the RED run shows that a required current type is missing from `link.xml`, update only `Assets/AddressableAssetsData/link.xml` and keep the XML valid.

### Required TDD

1. Add the failing EditMode test first.
2. Run the new test and confirm the RED failure is either missing test type or missing `link.xml` entry.
3. Implement the smallest `link.xml` or test adjustment needed.
4. Re-run the test and confirm GREEN.

Expected RED:

- the new test fails because the guard does not exist yet, or because a current route-unlock support type is not preserved.

Expected GREEN:

- the new preservation guard passes and fails with actionable messages if a critical type is absent.

### Required Verification

Run:

```powershell
tests_run EditMode testClass Rootborn.Tests.EditMode.Addressables.AddressablesLinkXmlPreservationTests
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the test class name is adjusted to match repository conventions, update the command to the exact final class name.

### Acceptance Criteria

- `link.xml` exists and is valid XML.
- The guard covers current registry, route unlock, clue interpretation, Daily Plan route, world-state, and location/activity data types needed by recent flows.
- Failure messages name the missing type and the file being checked.
- No gameplay behavior or UI behavior changes are mixed into this cycle.

### Commit

Use:

```text
[TEST] Addressables link.xml 보존 가드 추가
```

---

## Prompt 2: Discovery Route Unlock E2E Diagnostics Hardening

Paste this prompt into a goal session and execute it as written.

### Objective

Harden the strict player-facing Discovery route unlock PlayMode tests so future failures explain which visible step, runtime state, save/load state, or screenshot evidence failed.

This should not change gameplay behavior. It should make the existing route unlock E2E coverage cheaper to debug and safer to extend.

### Non-Negotiable Constraints

- This is a PlayMode test diagnostics/refactor cycle.
- Do not replace real player-facing movement, interact key, UI click, scene transition, Objective Journal Tab flow, Daily Plan board selection, or screenshot evidence with direct method calls.
- Do not reduce existing assertions.
- Do not create a new permanent HUD or UI ownership surface.
- `ObjectiveJournalPanel` remains the owner of route guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for C# tests.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Improve diagnostics in the existing Discovery route unlock E2E tests and shared test support.

Cover both current strict route unlock flows:

- first Discovery Clue to Daily Plan route unlock
- second Discovery Clue to Daily Plan route unlock

Improve assertions around:

- MainMenu SaveSlot entry into Town
- keyboard movement to the clue or interpretation source
- interact key response
- visible choice button click
- world-state flag activation
- Daily Plan board row visibility
- route selection
- Objective Journal category and row visibility
- tracked HUD single-instance state
- screenshot file existence and non-zero length
- reload persistence for clue, interpretation, world-state, route availability, route selection, Journal state, and tracked HUD state

### Required TDD

Add or tighten diagnostics-focused tests first. If the existing tests already pass behaviorally, the RED can be a deliberately missing diagnostic contract in a helper test, such as:

- screenshot verifier reports the expected file path when missing
- UI button helper reports visible button text candidates when the target button is missing
- Journal row selector reports available row titles when target row selection fails

Expected RED:

- the new diagnostics contract fails before helper implementation.

Expected GREEN:

- existing first and second Discovery route unlock E2E tests pass with stronger diagnostic helpers.

### Required PlayMode Verification

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockSecondSliceE2EScenarioTests.DISCOVERY_ROUTE_E2E_002_SaveSlotSecondClueInterpretationUnlocksSecondDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png,Builds/Logs/discovery-route-unlock-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Both route unlock E2E tests still use actual player-facing input/UI flow.
- Both screenshot evidence files are freshly produced or freshly inspected with non-zero length.
- Failure output identifies the exact missing player-facing step or state.
- No production behavior changes are included unless a real blocking bug is found and fixed in the same TDD cycle.

### Commit

Use:

```text
[TEST] Discovery route unlock E2E 진단 강화
```

---

## Prompt 3: Objective Journal Route Source Contract Hardening

Paste this prompt into a goal session and execute it as written.

### Objective

Lock down the contract between Daily Plan route sources, Discovery route unlock hints, Objective Journal aggregation, and `TrackedObjectiveHud` after the first two route unlock slices.

This protects against duplicate rows, duplicate tracked HUD state, unstable ordering, and source ownership drift as more route unlocks are added.

### Non-Negotiable Constraints

- `ObjectiveJournalPanel` owns all goal-like UI.
- `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Do not create feature-specific permanent HUDs, route hint canvases, quest hint canvases, or source-owned tracked widgets.
- Do not branch production C# on route, clue, world-state, location, activity, quest, campaign, NPC, crop, or tool IDs.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Add or tighten EditMode contract tests for:

- unlocked but unselected Daily Plan routes appear in `Hints`
- selected Daily Plan routes appear in `Goals`
- the same route cannot appear as both selected goal and unlocked hint
- source catalog ordering is deterministic
- repeated source refresh does not duplicate rows
- repeated tracking or reload does not duplicate `TrackedObjectiveHud`
- null or incomplete registry/save/player context degrades without throwing
- first and second route unlock data both participate through the same source contract

Only change production UI/source code if the tightened tests reveal a real contract bug.

### Required TDD

1. Add or tighten the contract tests first.
2. Confirm at least one RED failure if current behavior is incomplete.
3. Implement the smallest fix needed.
4. Confirm GREEN for Objective Journal and related route tests.

Expected RED:

- duplicate Hints/Goals behavior, unstable ordering, null-context exception, or missing multi-route contract coverage is exposed.

Expected GREEN:

- Objective Journal source contract is deterministic across first and second route unlock data.

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.DiscoveryClues
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockSecondSliceE2EScenarioTests.DISCOVERY_ROUTE_E2E_002_SaveSlotSecondClueInterpretationUnlocksSecondDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Objective Journal route source behavior is deterministic and covered by tests.
- Selected routes are not duplicated in Hints.
- Repeated refresh/reload does not duplicate tracked HUD state.
- The strict second route unlock PlayMode flow still passes with screenshot evidence.
- No new goal-like UI ownership surface is introduced.

### Commit

Use:

```text
[UI][TEST] Objective Journal route source 계약 강화
```

---

## Prompt 4: NPC Routine Relationship Milestone Slice

Paste this prompt into a goal session and execute it as written.

### Objective

Add one small data-driven NPC routine and relationship milestone slice that surfaces a clear next action through Objective Journal Hints.

This is the recommended first feature slice after the foundation tasks because relationship data is currently thin, NPC schedules already have PlayMode coverage, and the player value is easy to observe without introducing another permanent UI surface.

### Non-Negotiable Constraints

- All NPC, relationship, schedule, dialogue/help, milestone, and hint behavior must be ScriptableObject/data-driven.
- Do not branch production C# on specific NPC IDs, relationship IDs, schedule IDs, dialogue IDs, route IDs, quest IDs, or location IDs.
- Do not create a new relationship HUD, NPC hint HUD, floating objective panel, or permanent Canvas.
- `ObjectiveJournalPanel` owns relationship next-action guidance.
- `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD.
- Visible or interactive changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence.
- Save/load persistence must be verified after reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create or wire exactly one small relationship milestone path:

- one existing or new registered NPC definition suitable for Town
- one schedule/routine state that makes the NPC reachable through a real Town player flow
- one relationship progress or milestone definition
- one data-driven dialogue/help/action outcome that advances the relationship milestone
- one Objective Journal Hint item that appears when the milestone creates a next action
- save/load support using the existing save path, without duplicate relationship delta application

Prefer existing NPC, location, schedule, and dialogue infrastructure. Add new assets only when current data cannot express the slice.

### Required TDD

Add or tighten tests before wiring data:

- NPC routine makes the selected NPC reachable in the expected Town context.
- relationship action advances the milestone exactly once.
- repeated action or reload does not double-apply the relationship delta.
- Objective Journal Hints emits the relationship next action from data.
- relationship hint disappears or changes after the milestone is resolved.
- save/load restores relationship milestone and hint state without duplication.

Expected RED:

- tests fail because the new milestone data path does not exist yet or current sources do not expose it.

Expected GREEN:

- NPC schedule, relationship, Objective Journal, and save/load tests pass for the new slice.

### Required PlayMode Scenario

Create or extend a strict player-facing PlayMode test:

```text
NPC_RELATIONSHIP_E2E_001_ScheduleInteractionMilestoneJournalReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual keyboard movement to reach the NPC's reachable scheduled location.
4. Press the interact key.
5. Click the visible dialogue/help/action button.
6. Verify visible result text or state in Game View.
7. Verify relationship milestone runtime state.
8. Open Objective Journal with Tab.
9. Verify the relationship next action appears in Hints or Goals according to the source contract.
10. Track it if applicable and verify exactly one `TrackedObjectiveHud`.
11. Capture screenshot:

```text
Builds/Logs/npc-relationship-milestone-e2e.png
```

12. Reload the same save slot through the player-facing SaveSlot UI.
13. Verify NPC relationship milestone, Journal hint/goal state, and tracked HUD state restore without duplicates.

Forbidden shortcuts:

- direct relationship progress mutation as the only proof
- direct scene-only setup as the only proof
- direct Objective Journal source calls as the only PlayMode proof
- JSON-only save verification without player-facing reload

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.NpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_001_ScheduleInteractionMilestoneJournalReload
Get-ChildItem Builds/Logs/npc-relationship-milestone-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One data-driven NPC relationship milestone path exists.
- The player can reach and complete it through actual Town input/UI flow.
- Objective Journal owns the next-action guidance.
- Save/load restores milestone and Journal state without duplicate relationship progress.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][TEST] NPC 관계 마일스톤 세로 슬라이스 추가
```
