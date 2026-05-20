# Goal: Second NPC Relationship Route

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add one second data-driven NPC relationship route so Town social progression is proven as a repeatable system beyond the First Guide sample, then verify the route through actual Town movement, interaction, Objective Journal guidance, save/load, and screenshot evidence.

## Candidate Selection

Considered candidates:

- Second NPC Relationship Route
  - Why now: career route, quest route reward, host save smoke, and quest-to-interior reward are complete, so the next highest-value gameplay slice is proving relationship content can scale past one NPC.
  - Value: makes Town social progression feel like a reusable system instead of a single fixture.
  - Risk: touches NPC data, schedule/reachability, relationship milestone idempotency, Objective Journal source behavior, and save/load.
  - Verification burden: EditMode data/Journaling tests plus strict PlayMode movement/interact/UI/reload screenshot.
  - Category: feature, asset, test.
- Daily Plan Route Completion And Day Result Feedback
  - Why now: route sources are now broad enough to need completion feedback.
  - Value: improves the daily loop after route selection.
  - Risk: result semantics may need product judgment and could grow broader than one cycle.
  - Verification burden: route completion, result UI, Journal filtering, reload.
  - Category: feature, UI, test.
- Objective Journal Route Source Regression Hardening
  - Why now: many sources now feed Journal.
  - Value: reduces regression risk across recent slices.
  - Risk: primarily test/foundation value, less immediate player-facing content.
  - Verification burden: multi-source regression matrix and PlayMode no-duplicate HUD screenshot.
  - Category: foundation, test.

Selected candidate: Second NPC Relationship Route.

## Non-Negotiable Constraints

- All NPC, relationship, schedule, dialogue/help/action, milestone, route, and Objective Journal behavior must be ScriptableObject-driven.
- Do not branch production C# on NPC IDs, relationship IDs, schedule IDs, dialogue IDs, route IDs, quest IDs, activity IDs, location IDs, crop IDs, or tool IDs.
- Do not create a relationship HUD, NPC hint HUD, floating objective panel, permanent Canvas, or goal-like UI outside `ObjectiveJournalPanel`.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Save/load persistence must be verified after player-facing reload.
- Keep unrelated dirty or untracked files untouched and unstaged.
- If `Assets/AddressableAssetsData/link.xml` or `.meta` is deleted by Unity tooling, restore it immediately and keep it out of this cycle unless explicitly changed.

## Required Skills

- `superpowers:brainstorming` for candidate selection and scope shaping.
- `superpowers:test-driven-development` for all behavior changes.
- `superpowers:systematic-debugging` for test, Unity, UI, save/load, or PlayMode failures.
- `superpowers:verification-before-completion` before claiming completion or committing.

## Required Work

Create one second relationship route:

- one existing or new registered NPC definition suitable for Town
- one schedule/routine or runtime placement path that makes the NPC reachable through real Town player movement
- one relationship progress or milestone definition
- one data-driven dialogue/help/action outcome that advances the relationship milestone exactly once
- one Objective Journal Hint or Goal item through existing milestone/source contracts
- save/load support using the existing relationship/milestone save path
- no duplicate relationship delta or Journal/HUD state after repeated action or reload

Prefer existing NPC, location, schedule, help action, and milestone infrastructure. Add new ScriptableObject assets only where current data cannot express the slice.

## Required TDD

Add failing tests first:

- the selected second NPC data is registered and distinct from the First Guide relationship sample
- the selected second NPC can be reached in the expected Town context through registered data
- the relationship/help/action outcome advances the configured milestone exactly once
- repeated action or reload does not double-apply relationship progress
- `ObjectiveJournalPanel` source emits the relationship next action from data
- the relationship hint disappears or changes after the milestone is resolved
- save/load restores relationship milestone and Journal state without duplicates

Expected RED:

- tests fail because the second relationship route data path does not exist yet, or the Journal/save contract only proves the First Guide sample.

Expected GREEN:

- a second NPC relationship route advances and persists through data-driven assets without production entity-ID branching.

## Required PlayMode Scenario

Create or extend:

```text
NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual keyboard movement to reach the second NPC at the scheduled/reachable location.
4. Press the interact key.
5. Click the visible dialogue/help/action button.
6. Verify visible dialogue/result text in Game View.
7. Verify relationship milestone runtime state.
8. Open Objective Journal with Tab.
9. Verify the relationship next action appears in Hints or Goals according to the source contract.
10. Track it if applicable and verify exactly one `TrackedObjectiveHud`.
11. Capture screenshot:

```text
Builds/Logs/npc-relationship-second-slice-e2e.png
```

12. Reload the same save slot through player-facing SaveSlot UI.
13. Verify NPC relationship milestone, Journal hint/goal state, and tracked HUD state restore without duplicates.

Forbidden shortcuts:

- direct relationship or milestone mutation as the only proof
- direct scene loading as the only PlayMode proof
- direct Objective Journal source calls as the only PlayMode proof
- JSON-only save verification without player-facing reload

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
Get-ChildItem Builds/Logs/npc-relationship-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- A second data-driven NPC relationship route exists.
- The selected NPC route is distinct from the First Guide relationship sample.
- The player can reach and complete it through actual Town input/UI flow.
- Objective Journal owns next-action guidance.
- Save/load restores milestone and Journal state without duplicate relationship progress.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] 두 번째 NPC 관계 route 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
