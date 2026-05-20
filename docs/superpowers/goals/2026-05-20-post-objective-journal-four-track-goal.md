# Goal: Post Objective Journal Four-Track Foundation Hardening

## Objective

Objective Journal legacy HUD migration is complete. Continue with the next four tracks in order, without starting isolated feature work before the foundation is audited, refactored, verified visually, and re-prioritized.

The four tracks are:

1. Objective Journal migration audit report
2. Objective Journal provider structure refactor
3. Objective Journal UI quality and scaling pass
4. Feature development inventory refresh

This goal is complete only when all four tracks are finished, documented, tested, and verified through player-facing PlayMode scenarios where applicable.

## Constitutional Constraints

- Do not create new always-visible objective HUDs.
- `ObjectiveJournalPanel` owns goals, quests, campaign, career hints, milestones, and similar "what should I do now?" UI.
- `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- All gameplay entity definitions remain ScriptableObject/data-driven.
- No entity ID-specific branching in production C#.
- Any visible or interactive work requires direct PlayMode user-flow verification and Game View or Camera screenshot evidence.
- Automated tests are required but not enough for visible UI or interaction changes.
- Do not edit `Assets/**/*.cs` by direct filesystem writes. Use Unity MCP script tools for C# changes.
- Keep unrelated dirty files untouched.

## Track 1: Objective Journal Migration Audit Report

### Purpose

Record the completed legacy HUD migration in an auditable form so future work does not reintroduce separate goal-like HUD ownership.

### Required Work

- Create an audit document under `docs/superpowers/audits/`.
- Summarize the removed legacy HUDs:
  - `CampaignHudPanel`
  - `MilestoneHudPanel`
  - `QuestChainHudWidget`
  - `CareerInterestHudWidget`
- List the current Objective Journal sources:
  - Student Day Guide
  - Quest Log
  - Quest Chain
  - Campaign
  - Milestone
  - Career Interest
- Record the committed migration evidence:
  - relevant commit hashes
  - EditMode test namespace
  - PlayMode E2E test methods
  - screenshot evidence paths under `Builds/Logs/`
- Record remaining unrelated dirty files explicitly and confirm they were not modified.

### Required Static Audit Commands

Run:

```powershell
rg -n "CampaignHudPanel|MilestoneHudPanel|QuestChainHudWidget|CareerInterestHudWidget|CampaignHudRoot|MilestoneHudRoot|QuestChainHudRoot|CareerInterestHudRoot" Assets/Scripts Assets/Tests docs -g "*.cs" -g "*.md"
git status --short
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Expected:

- No production `Assets/Scripts` references to deleted legacy HUD types.
- Remaining matches are limited to docs or name-based absence assertions in tests.
- `git diff --check` has no errors.
- Entity ID branching check passes.

### Track 1 Completion Criteria

- Audit document exists.
- Audit document has no vague placeholders.
- Audit confirms production code has no legacy objective HUD type/factory.
- Commit with prefix `[DOCS]`.

## Track 2: Objective Journal Provider Structure Refactor

### Purpose

Reduce `ObjectiveJournalRuntimeProvider` size and make objective sources independently testable without changing player-facing behavior.

### Required Work

Refactor nested source classes into focused files under `Assets/Scripts/UI/Objectives/Sources/` or an equivalent local pattern:

- `StudentDayGuideObjectiveSource`
- `QuestLogObjectiveSource`
- `QuestChainObjectiveSource`
- `CampaignObjectiveSource`
- `MilestoneObjectiveSource`
- `CareerInterestObjectiveSource`

Keep `ObjectiveJournalRuntimeProvider` responsible for:

- binding `ObjectiveJournalPanel`
- binding `TrackedObjectiveHud`
- resolving active registry/save metadata/player context
- composing source list
- refreshing panel and tracked HUD

Do not change item text semantics unless tests require preserving a known expected value.

### Required TDD

Before production refactor:

- Add or adjust EditMode tests proving each source can be instantiated and returns expected category IDs.
- Add a regression test proving source composition includes all six expected source IDs.
- Add a regression test proving tracked stable key restore still selects the correct row after aggregation.

Expected RED:

- A test should fail before extraction or before new source composition API exists.

Expected GREEN:

- All Objective Journal EditMode tests pass after extraction.

### Required PlayMode Scenario

Run a player-facing Objective Journal smoke scenario:

1. Start from MainMenu.
2. Click New Game.
3. If SPUM creator appears, click Confirm.
4. Enter Town.
5. Press Tab to open Objective Journal.
6. Click each category:
   - Goals
   - Quests
   - Campaign
   - Hints
7. Select at least one row in each non-empty category.
8. Click Track.
9. Verify `TrackedObjectiveHud` visibly changes.
10. Capture Game View screenshot.
11. Reload the save slot.
12. Verify the tracked objective restores after reload.

No direct source method calls, forced selected category state, or internal provider-only checks can replace this PlayMode scenario.

### Track 2 Completion Criteria

- Source classes are separated.
- Objective Journal runtime behavior is unchanged from player perspective.
- EditMode Objective tests pass.
- PlayMode smoke scenario passes with screenshot evidence.
- Static gates pass.
- Commit with prefix `[REFACTOR][UI][TEST]`.

## Track 3: Objective Journal UI Quality And Scaling Pass

### Purpose

Make the Objective Journal stable when objective rows grow beyond the small current set. Improve selection, refresh, row layout, and tracked restore without adding new panels.

### Required Work

Evaluate and implement only the smallest necessary UI quality improvements from this list:

- Preserve selected category and selected row across refresh when the same stable key still exists.
- Avoid selecting an unrelated row when a category refresh reorders items.
- Add predictable empty-category text.
- Ensure row text does not overflow its button at 1920x1080.
- Ensure repeated refreshes do not leave stale row GameObjects.
- Add scroll, paging, or capped rows if the current row area cannot handle larger data safely.
- Keep category IDs stable: `Goals`, `Quests`, `Campaign`, `Hints`.

Do not add decorative UI work. Do not create cards inside cards. Do not create another permanent objective panel.

### Required TDD

Add EditMode tests for:

- stable key selection survives refresh
- tracked item restore still works after item reorder
- empty category shows explicit empty text
- refreshing with many items does not duplicate rows
- row count is bounded or scrollable if the source has more entries than visible slots

### Required PlayMode Scenario

Create or extend a PlayMode E2E scenario that proves UI quality as a player:

1. Start from MainMenu.
2. Enter Town through New Game and SPUM Confirm if needed.
3. Open Objective Journal with Tab.
4. Navigate to a category with multiple rows.
5. Select a non-first row.
6. Track it.
7. Perform a real action that refreshes objective state:
   - location activity
   - quest progress
   - day-end interaction
   - campaign route progress
8. Reopen Objective Journal with Tab.
9. Verify the selected/tracked objective did not jump to an unrelated row.
10. Verify visible text is still readable in Game View.
11. Capture screenshot.
12. Reload the save slot.
13. Verify tracked HUD restores the same stable key.

The test must inspect visible UI text and runtime state. It must not pass only by checking saved JSON.

### Track 3 Completion Criteria

- UI quality tests pass.
- PlayMode scenario captures screenshot evidence.
- No new always-visible objective HUD is created.
- Static gates pass.
- Commit with prefix `[UI][TEST]` or `[REFACTOR][UI][TEST]`.

## Track 4: Feature Development Inventory Refresh

### Purpose

Update the feature-development inventory after Objective Journal stabilization so the next feature goal is chosen from current code reality, not stale assumptions.

### Required Work

Create a new document under `docs/superpowers/specs/` or `docs/superpowers/audits/` that inventories the next feature candidates.

Must inspect current code, data, tests, and docs for at least these domains:

- Student Life daily activity loop
- Career interest/candidate/job loop
- Quest and quest-chain loop
- NPC relationship and schedule loop
- Exploration, discovery clue, and knowledge loop
- Housing/interior reward loop
- Network/dedicated server save/load loop
- Legacy Farm/Crop/Generation role

For each domain, record:

- current production code signal
- current ScriptableObject/data signal
- current PlayMode/EditMode coverage signal
- player-facing gap
- risk if implemented next
- recommended next slice
- minimum PlayMode scenario required

### Required Output Ranking

Rank at least five next feature candidates.

Each candidate must include:

- goal title
- player value
- affected systems
- data assets needed
- tests needed
- direct PlayMode scenario
- screenshot/evidence requirement
- why it should or should not be next

### Required PlayMode Scenario Templates

The inventory document must include concrete scenario templates, not vague test labels.

Example level of detail expected:

```md
Scenario: Daily plan route survives real player day flow
1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Open planning UI through actual UI/input.
3. Select a non-school growth route.
4. Walk to the target Town location with keyboard input.
5. Interact with the location prompt using the interact key.
6. Apply the activity through the visible UI button.
7. End the day through the day-end board.
8. Verify day result UI shows planned route, actual action, changed stat, and next hint.
9. Capture Game View screenshot.
10. Load same save slot.
11. Verify journal/tracked HUD restores the next route.
```

### Track 4 Completion Criteria

- Inventory document exists.
- At least five candidates are ranked.
- Each candidate has a concrete PlayMode scenario template.
- Recommendation names the single best next goal.
- Static gates pass if files changed.
- Commit with prefix `[DOCS]`.

## Full-Goal Verification Gate

After all four tracks:

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.FirstThreeDaysCampaignE2EScenarioTests.CAMPAIGN_E2E_001_011_SaveSlotPlaysThreeDaysWithOutsideSchoolRouteReloadAndDedupe
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.OpenEndedMilestoneGrowthE2EScenarioTests.MILESTONE_E2E_001_010_PlayOutsideSchoolMilestoneResultReloadAndDedupe
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerInterestQuestChainStateE2EScenarioTests.QUEST_CHAIN_E2E_001_014_SaveSlotMovementUiAcceptTrackProgressResultReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerInterestSelectionLoopE2EScenarioTests.CAREER_INTEREST_E2E_001_013_SaveSlotMovementLocationUiInterestSelectionQuestResultReloadAndDedupe
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Also verify screenshot files exist and have non-zero length:

```powershell
Get-ChildItem Builds/Logs/*objective-journal*.png | Select-Object Name,Length,LastWriteTime
```

## Done Means

- Four tracks are complete in order.
- Each track has its own commit.
- All changed C# was edited through Unity MCP script tools.
- All visible/interactive changes have PlayMode user-flow verification.
- Screenshots exist for UI-visible flows.
- Objective Journal remains the single owner of goal-like UI.
- `TrackedObjectiveHud` remains the only always-visible objective HUD.
- No unrelated dirty files were reverted or staged.
