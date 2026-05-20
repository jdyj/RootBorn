# Objective Journal Legacy HUD Migration Audit - 2026-05-20

## Scope

This audit records the completed migration of legacy goal-like HUDs into `ObjectiveJournalPanel` and `TrackedObjectiveHud`.

The removed legacy HUD types are:

- `CampaignHudPanel`
- `MilestoneHudPanel`
- `QuestChainHudWidget`
- `CareerInterestHudWidget`

`ObjectiveJournalPanel` is now the owner for goals, quests, campaign, hints, milestones, and career-interest guidance. `TrackedObjectiveHud` remains the only always-visible objective HUD.

## Current Sources

`Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs` currently composes these sources:

- `StudentDayGuideObjectiveSource` with `SourceId` `student-day-guide`
- `QuestLogObjectiveSource` with `SourceId` `quest-log`
- `QuestChainObjectiveSource` with `SourceId` `quest-chain-log`
- `CampaignObjectiveSource` with `SourceId` `campaign-progress`
- `MilestoneObjectiveSource` with `SourceId` `milestones`
- `CareerInterestObjectiveSource` with `SourceId` `career-interests`

## Migration Commits

- `eae280e0d` `[REFACTOR][UI][TEST] Campaign HUD를 Objective Journal로 이관`
- `04832169a` `[DOCS] Objective Journal legacy HUD 이관 목표와 Milestone 계획 정리`
- `ba7041504` `[REFACTOR][UI][TEST] Milestone HUD를 Objective Journal로 이관`
- `d45c59d28` `[REFACTOR][UI][TEST] Quest Chain HUD를 Objective Journal로 이관`
- `017183e49` `[REFACTOR][UI][TEST] Career Interest HUD를 Objective Journal로 이관`
- `a85123457` `[BUGFIX][UI] Objective Journal provider 중복 설치 방지`

## Test Evidence

EditMode namespace:

- `Rootborn.Tests.EditMode.UI.Objectives`

Representative PlayMode E2E methods:

- `Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_TownFlowTracksStudentDayObjectiveRefreshesAfterActivityAndRestoresAfterReload`
- `Rootborn.Tests.PlayMode.EndToEnd.FirstThreeDaysCampaignE2EScenarioTests.CAMPAIGN_E2E_001_011_SaveSlotPlaysThreeDaysWithOutsideSchoolRouteReloadAndDedupe`
- `Rootborn.Tests.PlayMode.EndToEnd.OpenEndedMilestoneGrowthE2EScenarioTests.MILESTONE_E2E_001_010_PlayOutsideSchoolMilestoneResultReloadAndDedupe`
- `Rootborn.Tests.PlayMode.EndToEnd.CareerInterestQuestChainStateE2EScenarioTests.QUEST_CHAIN_E2E_001_014_SaveSlotMovementUiAcceptTrackProgressResultReload`
- `Rootborn.Tests.PlayMode.EndToEnd.CareerInterestQuestChainStateE2EScenarioTests.QUEST_CHAIN_E2E_009_010_BlockedChainReopensFromActualLocationActivity`
- `Rootborn.Tests.PlayMode.EndToEnd.CareerInterestSelectionLoopE2EScenarioTests.CAREER_INTEREST_E2E_001_013_SaveSlotMovementLocationUiInterestSelectionQuestResultReloadAndDedupe`

Screenshot evidence currently present:

- `Builds/Logs/objective-journal-playmode.png`
- `Builds/Logs/objective-journal-town-e2e.png`
- `Builds/Logs/campaign-objective-journal-e2e.png`
- `Builds/Logs/milestone-objective-journal-e2e.png`
- `Builds/Logs/quest-chain-objective-journal-e2e.png`
- `Builds/Logs/career-interest-objective-journal-e2e.png`

## Static Audit Results

Command:

```powershell
rg -n "CampaignHudPanel|MilestoneHudPanel|QuestChainHudWidget|CareerInterestHudWidget|CampaignHudRoot|MilestoneHudRoot|QuestChainHudRoot|CareerInterestHudRoot" Assets/Scripts Assets/Tests docs -g "*.cs" -g "*.md"
```

Result:

- No matches in `Assets/Scripts` production code.
- Remaining `Assets/Tests` matches are name-based runtime absence assertions and cleanup calls.
- Remaining `docs` matches are historical specs, migration plans, and older goal/audit references.

Command:

```powershell
git diff --check
```

Result:

- Passed with exit code 0.

Command:

```powershell
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Result:

- `OK: no entity-id branching in system code.`

## Workspace Notes

The following unrelated dirty files were present before this audit and were not modified or staged by this track:

- `D Assets/AddressableAssetsData/link.xml`
- `D Assets/AddressableAssetsData/link.xml.meta`
- `M production/qa/evidence/registry-provider-town-flow.png`

The new goal prompt file for the current four-track hardening run is:

- `docs/superpowers/goals/2026-05-20-post-objective-journal-four-track-goal.md`

## Conclusion

The legacy objective HUD migration is complete from a production-code ownership perspective. Current follow-up risk is not legacy HUD recreation, but maintainability: `ObjectiveJournalRuntimeProvider` still owns all source implementations as nested classes. The next track should split those sources into focused files while preserving the existing player-facing Objective Journal behavior.
