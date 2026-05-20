# Objective Journal Migration Audit - 2026-05-20

## Scope

This audit records the current post-migration state of objective-like UI ownership after the legacy HUD migration and the Daily Plan integration.

The audited legacy HUD/widget types are:

- `CampaignHudPanel`
- `MilestoneHudPanel`
- `QuestChainHudWidget`
- `CareerInterestHudWidget`

The ownership rule is unchanged: `ObjectiveJournalPanel` owns goal-like guidance, and `TrackedObjectiveHud` is the only always-visible compact objective HUD.

## Current Objective Journal Sources

`ObjectiveJournalSourceCatalog.CreateDefaultSources()` currently composes these sources:

| Source ID | Class | Category |
| --- | --- | --- |
| `student-day-guide` | `StudentDayGuideObjectiveSource` | `Goals` |
| `daily-plan` | `DailyPlanObjectiveSource` | `Goals` |
| `quest-log` | `QuestLogObjectiveSource` | `Quests` |
| `quest-chain-log` | `QuestChainObjectiveSource` | `Quests` |
| `campaign-progress` | `CampaignObjectiveSource` | `Campaign` |
| `milestones` | `MilestoneObjectiveSource` | `Hints` |
| `career-interests` | `CareerInterestObjectiveSource` | `Hints` |

`ObjectiveJournalRuntimeProvider` binds the panel/HUD pair, resolves the active registry/save/player context, and refreshes the panel through `ObjectiveJournalAggregator`.

## Evidence Commits

Recent relevant commits:

- `610ad4135` `[DOCS] Objective Journal 이관 감사와 후속 목표 정리`
- `c6c3db6ad` `[REFACTOR][UI][TEST] Objective Journal source 구조 분리`
- `b509646d0` `[UI][TEST] Objective Journal 선택과 목록 품질 안정화`
- `73ec830a6` `[TEST] Objective Journal E2E 게이트 별칭 추가`
- `215fc82fd` `[FEATURE][UI][TEST] Daily Plan Objective Journal 연동`
- `935e09682` `[FEATURE][TEST] Daily Plan 플레이 흐름 검증 추가`
- `fbf610a1b` `[DOCS] Objective Journal 4트랙 강화 목표 프롬프트 작성`

## Test And Screenshot Evidence

EditMode namespace:

- `Rootborn.Tests.EditMode.UI.Objectives`

Representative PlayMode E2E methods:

- `Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload`
- `Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalUiQualityE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_002_SelectionTrackRefreshReloadAndScaling`
- `Rootborn.Tests.PlayMode.EndToEnd.FirstThreeDaysCampaignE2EScenarioTests.CAMPAIGN_E2E_001_011_SaveSlotPlaysThreeDaysWithOutsideSchoolRouteReloadAndDedupe`
- `Rootborn.Tests.PlayMode.EndToEnd.OpenEndedMilestoneGrowthE2EScenarioTests.MILESTONE_E2E_001_010_PlayOutsideSchoolMilestoneResultReloadAndDedupe`
- `Rootborn.Tests.PlayMode.EndToEnd.CareerInterestQuestChainStateE2EScenarioTests.QUEST_CHAIN_E2E_001_014_SaveSlotMovementUiAcceptTrackProgressResultReload`
- `Rootborn.Tests.PlayMode.EndToEnd.CareerInterestSelectionLoopE2EScenarioTests.CAREER_INTEREST_E2E_001_013_SaveSlotMovementLocationUiInterestSelectionQuestResultReloadAndDedupe`
- `Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload`

Screenshot evidence currently present:

| Screenshot | Size |
| --- | ---: |
| `Builds/Logs/objective-journal-playmode.png` | 46719 bytes |
| `Builds/Logs/objective-journal-town-e2e.png` | 35201 bytes |
| `Builds/Logs/objective-journal-ui-quality-e2e.png` | 48822 bytes |
| `Builds/Logs/campaign-objective-journal-e2e.png` | 34376 bytes |
| `Builds/Logs/milestone-objective-journal-e2e.png` | 44017 bytes |
| `Builds/Logs/quest-chain-objective-journal-e2e.png` | 47867 bytes |
| `Builds/Logs/career-interest-objective-journal-e2e.png` | 36421 bytes |
| `Builds/Logs/daily-plan-route-e2e.png` | 48883 bytes |

## Static Audit Results

Command:

```powershell
rg -n "CampaignHudPanel|MilestoneHudPanel|QuestChainHudWidget|CareerInterestHudWidget|CampaignHudRoot|MilestoneHudRoot|QuestChainHudRoot|CareerInterestHudRoot" Assets/Scripts Assets/Tests docs -g "*.cs" -g "*.md"
```

Result summary:

- No matches in production `Assets/Scripts` code.
- `Assets/Tests` matches are name-based absence assertions and cleanup checks in E2E tests.
- `docs` matches are historical specs, goals, plans, and audits that describe the migration.

Command:

```powershell
rg -n "new GameObject\\(.*Hud|AddComponent<.*Hud|TrackedObjectiveHud|ObjectiveJournalPanel" Assets/Scripts Assets/Tests -g "*.cs"
```

Result summary:

- `TrackedObjectiveHud` and `ObjectiveJournalPanel` are created through the reviewed Objective Journal installer path.
- Other HUD matches are non-objective surfaces such as time/status/resource/farm test HUDs, or tests that instantiate the Objective Journal pair.
- No legacy objective HUD factory remains in production code.

Command:

```powershell
git diff --check
```

Result:

- Exit code 0.

Command:

```powershell
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Result:

- `OK: no entity-id branching in system code.`

## Workspace State

The following unrelated dirty files were present before this audit and were not staged, reverted, or modified by Track 1:

- `D Assets/AddressableAssetsData/link.xml`
- `D Assets/AddressableAssetsData/link.xml.meta`
- `M production/qa/evidence/registry-provider-town-flow.png`

## Findings

- Production code no longer owns or creates `CampaignHudPanel`, `MilestoneHudPanel`, `QuestChainHudWidget`, or `CareerInterestHudWidget`.
- Current source composition includes the Daily Plan source in addition to the previous Student Day, quest, campaign, milestone, and career-interest sources.
- `TrackedObjectiveHud` remains the single compact objective HUD.
- The remaining risk is scale and maintainability, not legacy HUD recreation: Objective Journal source composition and UI refresh behavior need strict regression coverage as row counts and source count grow.

## Next Track

Proceed to Track 2: Objective Journal provider/source structure refactor. Because the source classes are already extracted, Track 2 should focus on tightening tests, adding strict PlayMode provider composition evidence, and making only the smallest production changes required by failing tests.
