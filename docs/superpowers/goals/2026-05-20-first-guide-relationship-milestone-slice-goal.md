# Goal: First Guide Relationship Milestone Slice

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Wire one small data-driven First Guide relationship milestone path so the existing reachable NPC routine, First Guide town help action, relationship progress, Milestone system, and Objective Journal Hints all participate in the same player-facing slice.

## Non-Negotiable Constraints

- Use existing systems where possible: `NpcDefinition`, `NpcScheduleDefinition`, `TownHelpActionDefinition`, `RelationshipMilestoneObjective`, `MilestoneRouteDefinition`, `MilestoneDefinition`, `MilestoneObjectiveSource`, and `ObjectiveJournalPanel`.
- All NPC, relationship, schedule, help action, milestone, and hint behavior must be ScriptableObject/data-driven.
- Do not branch production C# on specific NPC IDs, relationship IDs, schedule IDs, dialogue IDs, route IDs, quest IDs, location IDs, crop IDs, or tool IDs.
- Do not create a new relationship HUD, NPC hint HUD, floating objective panel, or permanent Canvas.
- `ObjectiveJournalPanel` owns relationship next-action guidance.
- `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD.
- Visible or interactive changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence.
- Save/load persistence must be verified after reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging` if any test or PlayMode flow fails unexpectedly
- `superpowers:verification-before-completion`

## Required Work

- Add or wire exactly one relationship milestone path for First Guide trust.
- Reuse existing data when possible:
  - NPC: `npc.first-guide`
  - schedule: `Schedule_FirstGuide`
  - relationship: `relationship.first-guide.trust`
  - action: `town.help.first-guide-errand`
- Create and register a `RelationshipMilestoneObjective` requiring First Guide trust progress.
- Create and register a `MilestoneRouteDefinition` whose recommended action is `town.help.first-guide-errand`.
- Create and register a `MilestoneDefinition` that owns that objective and route.
- Add EditMode data wiring tests proving the NPC schedule, help action, relationship objective, milestone route, and registry entries are connected without entity ID branching in production code.
- Add or extend strict PlayMode coverage so the player-facing flow proves:
  - MainMenu SaveSlot enters Town.
  - the scheduled First Guide NPC is reachable through keyboard movement and interact input.
  - the First Guide help action increases relationship progress.
  - day-end milestone processing reflects the relationship milestone and Objective Journal Hints/Completed state.
  - save/load restores relationship and milestone state without duplicate relationship or milestone application.
  - Game View screenshot evidence is written.

## Required TDD

RED:

- Add the EditMode data wiring test first and confirm it fails because the First Guide relationship milestone assets are not registered yet.
- Add or tighten the PlayMode scenario assertions before wiring the milestone data and confirm the missing milestone path fails at the expected assertion.

GREEN:

- Create the relationship objective, milestone route, and milestone assets through Unity editor/MCP APIs.
- Register the assets in `Assets/Data/Registry/GameDataRegistry.asset`.
- Re-run EditMode and PlayMode tests until the same player-facing path passes.

## Required Verification

Run:

```powershell
tests_run EditMode testMethod Rootborn.Tests.EditMode.StudentLife.FirstGuideRelationshipMilestoneSliceTests.NPC_RELATIONSHIP_EDIT_001_FirstGuideRelationshipMilestoneDataIsRegistered
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.FirstGuideRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_001_ScheduleInteractionMilestoneJournalReload
Get-ChildItem Builds/Logs/first-guide-relationship-milestone-e2e.png | Select-Object Name,Length,LastWriteTime
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- First Guide schedule data remains reachable in Town.
- First Guide help action references `npc.first-guide` and increases `relationship.first-guide.trust`.
- A registered relationship milestone objective evaluates First Guide trust progress.
- A registered milestone route recommends `town.help.first-guide-errand`.
- Objective Journal Hints surfaces the milestone route through the existing Milestone source, without new HUD/UI ownership.
- The strict PlayMode flow uses actual keyboard movement, interact key input, UI click/selection, day-end interaction, reload, and screenshot evidence.
- Relationship and milestone state survive reload and do not duplicate on repeated result interaction.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] First Guide 관계 milestone slice 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
