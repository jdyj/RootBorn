# Goal: Relationship Activity Commitment Route

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Career/Job, dedicated smoke, Relationship saved-state, and Quest/Interior saved-state gates were verified.

## Objective

Add one small player-facing relationship commitment route on top of the existing Teacher relationship slice. A player must be able to perform the Teacher help activity, complete the Teacher support milestone, see a follow-up commitment guidance row in Objective Journal, track it with the single `TrackedObjectiveHud`, reload the same save slot, and repeat the relevant interaction without duplicate relationship growth, milestone state, Journal rows, HUDs, or reward/progress records.

## Candidate Selection

Three next improvements were compared:

1. Relationship activity commitment route
   - Value: deepens an existing NPC relationship into a readable next step without inventing a new UI owner.
   - Risk: medium; touches relationship data, milestone route data, Objective Journal source output, saved-state exact-once checks, and PlayMode interaction flow.
   - Verification burden: EditMode data/Journal tests plus one player-facing PlayMode E2E screenshot/reload flow.
2. Knowledge/Status activity chain
   - Value: supports free-path growth, but requires choosing between Knowledge and Status semantics first.
   - Risk: medium; broader domain direction than a relationship follow-up slice.
   - Verification burden: activity, day result, Journal, reload, and persistence checks.
3. New quest/interior feature slice
   - Value: strong visual payoff, but the last cycle already focused on quest/interior saved-state hardening.
   - Risk: higher because visible House/interior changes require stricter saved scene/runtime tilemap checks.
   - Verification burden: PlayMode House placement, screenshot, save files, tilemap/occupancy inspection.

Selected candidate: Relationship activity commitment route. It advances current Student Life content while reusing the strongest existing route: `npc.teacher`, `relationship.teacher.support`, `town.help.teacher-study-prep`, and `milestone.teacher-support`.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests before implementation for behavior, regression, and PlayMode coverage.
- Keep NPC, relationship, milestone, route, activity, reward, and objective behavior data-driven through ScriptableObject definitions and strategy arrays.
- Do not add production C# branching on NPC IDs, relationship IDs, milestone IDs, help-action IDs, route IDs, activity IDs, location IDs, quest IDs, reward IDs, crop IDs, tool IDs, furniture IDs, room IDs, or similar entity identifiers.
- `ObjectiveJournalPanel` owns all relationship commitment guidance. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Visible and interactive verification must use the actual PlayMode player-facing flow: MainMenu save slot, Town entry, movement, interact key, Objective Journal `Tab`, tracking click, screenshot, save/reload.
- Runtime-only state checks are not enough. Serialized StudentLife, MilestoneProgress, Objective Journal tracked state, and relevant save files must be inspected when persistence is expected.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables churn before completion.
- Do not broaden this into a general relationship system rewrite.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect and reuse existing patterns before adding code:
  - `Assets/Scripts/Game/StudentLife/TownHelpActionDefinition.cs`
  - `Assets/Scripts/Game/StudentLife/TownHelpActionRunner.cs`
  - `Assets/Scripts/Game/StudentLife/HelpActionRelationshipDeltaOutcome.cs`
  - `Assets/Scripts/Game/StudentLife/MilestoneDefinition.cs`
  - `Assets/Scripts/Game/StudentLife/MilestoneRouteDefinition.cs`
  - `Assets/Scripts/UI/Objectives/Sources/MilestoneObjectiveSource.cs`
  - `Assets/Tests/PlayMode/EndToEnd/SecondNpcRelationshipMilestoneE2EScenarioTests.cs`
- Reuse the existing Teacher relationship surface:
  - `Assets/Data/StudentLife/TownHelpActions/TownHelp_TeacherStudyPrep.asset`
  - `Assets/Data/StudentLife/Relationships/Relationship_TeacherSupport.asset` if present, otherwise the registered Teacher relationship asset referenced by `MilestoneObjective_TeacherSupport.asset`
  - `Assets/Data/StudentLife/Milestones/Milestone_TeacherSupport.asset`
  - `Assets/Data/StudentLife/Milestones/Routes/MilestoneRoute_TeacherSupportHelp.asset`
- Add one follow-up commitment route or equivalent relationship commitment guidance asset after the Teacher support milestone is completed.
- Register all new or modified ScriptableObject assets through `Assets/Data/Registry/GameDataRegistry.asset`.
- Surface the commitment guidance through Objective Journal `Hints`; after it is tracked or completed, it must not duplicate across `Hints` and `Goals`.
- Persist relationship value, milestone completion, commitment route visibility, selected/tracked Journal state, and day-result state.
- Ensure repeated help action, repeated day-result interaction, repeated tracking, and reload do not duplicate relationship deltas, milestone objective records, Journal rows, HUDs, or route records.
- If the route affects dedicated server/client gameplay state, write a follow-up dedicated smoke goal instead of expanding this slice beyond one reviewable cycle.

## Required TDD

RED:

- Add or tighten an EditMode test in `Rootborn.Tests.EditMode.StudentLife.SecondNpcRelationshipRouteTests` that fails while the Teacher commitment route is missing, not registered, lacks a recommended action, lacks Objective Journal text keys, or is visible before its prerequisite Teacher milestone state.
- Add or tighten an EditMode Objective Journal test that fails if the Teacher commitment guidance emits duplicate stable keys or appears in both `Hints` and `Goals`.
- Add or tighten a PlayMode E2E test that fails before the player can complete Teacher help, see/track the follow-up commitment guidance, capture screenshot evidence, reload, and verify exact-once persistence.

GREEN:

- Teacher help remains player-facing and data-driven.
- Teacher support milestone completion unlocks one commitment guidance route.
- Objective Journal shows the commitment row exactly once in the expected category and exactly one `TrackedObjectiveHud` after tracking.
- Save/load restores relationship value, milestone completion, commitment guidance, and tracked HUD state without duplication.
- Repeating day-result or tracking does not mutate saved milestone/relationship state or create duplicate rows/HUDs.

## Required PlayMode Scenario

Create or strengthen:

```text
Rootborn.Tests.PlayMode.EndToEnd.RelationshipActivityCommitmentE2EScenarioTests.REL_COMMITMENT_E2E_001_TeacherHelpCommitmentReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Create or load the test save slot through visible SaveSlot UI.
3. Enter Town.
4. Open Objective Journal with `Tab` and verify the Teacher milestone guidance appears once before play.
5. Move the player to the Teacher NPC using keyboard input and interact through the visible prompt.
6. Move the player to the Teacher help action source using keyboard input and trigger it through the visible prompt.
7. Verify runtime Teacher relationship progress changed exactly once.
8. Move to the day-end board and open the day-result panel through visible interaction.
9. Verify Teacher milestone completion appears and saved MilestoneProgress contains the Teacher milestone/objective exactly once.
10. Open Objective Journal with `Tab` and verify the follow-up commitment guidance row appears exactly once.
11. Track the commitment row and verify exactly one `TrackedObjectiveHud`.
12. Capture Game View or Camera screenshot:

```text
Builds/Logs/relationship-activity-commitment-e2e.png
```

13. Inspect serialized save files for exact-once Teacher relationship, Teacher milestone, commitment route/guidance, and tracked objective state.
14. Reload the same save slot through SaveSlot UI.
15. Reopen Objective Journal.
16. Verify relationship value, milestone completion, commitment guidance, and tracked HUD restore without duplication.
17. Repeat day-result interaction or tracking and verify no duplicate relationship delta, milestone record, commitment row, tracked HUD, or reward/progress record is created.

Forbidden shortcuts:

- Do not directly mutate `StudentLifeProgress`, `MilestoneProgress`, or Objective Journal tracked state as the only PlayMode proof.
- Do not directly call runner methods as the only PlayMode proof.
- Do not pass only by checking JSON files.
- Do not create a standalone relationship HUD or a new always-visible commitment panel.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.RelationshipActivityCommitmentE2EScenarioTests.REL_COMMITMENT_E2E_001_TeacherHelpCommitmentReload
Get-ChildItem Builds/Logs/relationship-activity-commitment-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the slice creates network-visible relationship or milestone behavior, write a dedicated follow-up goal with exact evidence instead of bundling it here.

## Acceptance Criteria

- One Teacher relationship commitment route or equivalent guidance asset is implemented as a data-driven vertical slice.
- The player reaches Teacher NPC/help through actual Town movement and visible interaction.
- Teacher support milestone completion unlocks the follow-up commitment guidance.
- Objective Journal owns all commitment guidance and does not duplicate rows across `Hints` and `Goals`.
- Exactly one `TrackedObjectiveHud` exists when the commitment row is tracked.
- Save/load restores Teacher relationship value, milestone completion, commitment guidance, and tracked HUD state exactly once.
- Repeating day-result interaction or tracking does not duplicate relationship deltas, milestone records, Journal rows, HUDs, or reward/progress records.
- Screenshot evidence exists at `Builds/Logs/relationship-activity-commitment-e2e.png` and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[FEATURE][TEST] Relationship activity commitment route 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

