# Goal: Second NPC Relationship Saved-State Regression Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after career/job Daily Plan saved-state regression hardening passed.

## Objective

Harden the existing second NPC relationship slice so Teacher relationship progress, milestone completion, Objective Journal guidance, tracked HUD state, day-result behavior, and reload persistence cannot silently duplicate or drift.

## Current Evidence

- `SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload` already proves a player-facing Teacher NPC/help/milestone flow.
- `SecondNpcRelationshipRouteTests` already covers the data-driven Teacher route slice.
- `MilestoneRoute_TeacherSupportHelp.asset` exists under `Assets/Data/StudentLife/Milestones/Routes/`.
- The E2E already captures `Builds/Logs/npc-relationship-second-slice-e2e.png`.

This is therefore not a new relationship feature. Treat it as regression hardening for existing relationship/milestone persistence and Objective Journal ownership.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior or regression changes.
- Do not add production C# branching on NPC IDs, relationship IDs, milestone IDs, help-action IDs, route IDs, activity IDs, location IDs, quest IDs, reward IDs, crop IDs, tool IDs, or similar identifiers.
- Keep relationship/milestone route behavior data-driven through ScriptableObject definitions and strategy arrays.
- Visible and interactive verification must use the player-facing PlayMode flow and capture Game View screenshot evidence.
- Runtime-only checks are not enough; serialized StudentLife, MilestoneProgress, Objective Journal tracked state, and relevant save files must also be inspected.
- `ObjectiveJournalPanel` owns relationship/milestone guidance; do not create new relationship HUDs.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables file churn before commit.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Tighten existing EditMode coverage in `Rootborn.Tests.EditMode.StudentLife.SecondNpcRelationshipRouteTests`.
- Tighten existing PlayMode coverage in `Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload`.
- Verify serialized StudentLife save data contains the Teacher relationship entry exactly once after the help action, day result, reload, and repeated result interaction.
- Verify serialized MilestoneProgress save data contains the Teacher milestone and objective exactly once after completion and reload.
- Verify Objective Journal Hints contains the Teacher milestone row exactly once before completion and after reload.
- Verify exactly one `TrackedObjectiveHud` exists before completion, after completion, and after reload.
- Verify repeated day-result interaction does not mutate milestone save data or reapply relationship growth.

## Required TDD

RED:

- Add or tighten a test that fails if `SecondNpcRelationshipMilestoneE2EScenarioTests` only checks save-file containment instead of exact-once serialized relationship/milestone assertions.
- Add or tighten a test that fails if the Teacher milestone route asset is missing, not registered, lacks a relationship objective, or lacks Objective Journal text keys.
- Add or tighten a test that fails if Objective Journal Teacher milestone rows duplicate across active, completed, and reloaded states.

GREEN:

- Teacher relationship save data and milestone save data contain expected records exactly once.
- Reloaded Teacher milestone state has one completed row, one tracked HUD, and no duplicate relationship delta.
- Repeated day-result interaction leaves milestone save data and relationship value unchanged.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
Get-ChildItem Builds/Logs/npc-relationship-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Existing Teacher relationship milestone remains player-facing and visible.
- Teacher NPC/help action is reached through actual movement, interact key, and visible UI.
- Serialized StudentLife save data contains the Teacher relationship record exactly once.
- Serialized MilestoneProgress save data contains the Teacher milestone and objective records exactly once.
- Objective Journal shows the Teacher milestone row exactly once before completion, after completion, and after reload.
- Exactly one tracked HUD exists and restores after reload.
- Repeated day-result interaction does not mutate milestone save data or relationship value.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[TEST] Second NPC relationship 저장 회귀 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
