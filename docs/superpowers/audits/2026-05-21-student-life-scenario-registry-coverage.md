# Student Life Scenario Registry Coverage

Date: 2026-05-21

This catalog maps active ROOTBORN player-flow, domain, and dedicated validation scenarios to concrete executable evidence. Active rows must map to an existing test file or QA runner. Deferred rows must state why they are not executable yet and what validation type should cover them later.

## Active Scenarios

| Status | ID | Category | Owner | Evidence Type | Evidence | Player-Facing Verification | Deferred Reason | Future Validation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Active | TOWN-FLOW-001 | TOWN | Town/Core Flow | PlayMode | Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs | MainMenu to Town, Objective Journal, day result, reload screenshot evidence |  |  |
| Active | TOWN-FLOW-002 | TOWN | Town/Core Flow | PlayMode | Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs | New player Town flow keeps player-facing route observable |  |  |
| Active | LIFE-STUDENT-001 | LIFE | Student Life Activities | EditMode | Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs | Domain behavior only; no visible UI changed by this gate |  |  |
| Active | LIFE-DAILY-PLAN-001 | LIFE | Daily Plan | PlayMode | Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteSelectionE2EScenarioTests.cs | Daily Plan board route selection, activity result, reload |  |  |
| Active | LIFE-DAILY-PLAN-002 | LIFE | Daily Plan | PlayMode | Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteCompletionE2EScenarioTests.cs | `DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload` verifies selected route completion result feedback, Objective Journal transition out of active Goals, TrackedObjectiveHud cleanup, screenshot `Builds/Logs/daily-plan-route-completion-e2e.png`, reload persistence, and duplicate prevention |  |  |
| Active | CAREER-DAILY-PLAN-001 | CAREER | Career Daily Plan | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerDailyPlanRouteE2EScenarioTests.cs | Career interest unlocks practice route through player-facing flow |  |  |
| Active | CAREER-CANDIDATE-001 | CAREER | Career Candidate | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerCandidateDeepeningLoopE2EScenarioTests.cs | Library/candidate route progression and reload |  |  |
| Active | REL-FIRST-GUIDE-001 | REL | Relationships | PlayMode | Assets/Tests/PlayMode/EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs | First Guide relationship milestone through Town interaction |  |  |
| Active | REL-SECOND-NPC-001 | REL | Relationships | PlayMode | Assets/Tests/PlayMode/EndToEnd/SecondNpcRelationshipMilestoneE2EScenarioTests.cs | Second NPC relationship milestone, Journal guidance, reload |  |  |
| Active | REL-COMMITMENT-001 | REL | Relationship Commitment | PlayMode | Assets/Tests/PlayMode/EndToEnd/RelationshipActivityCommitmentE2EScenarioTests.cs | `REL_COMMITMENT_E2E_001_TeacherHelpCommitmentReload` verifies Teacher help commitment guidance, Objective Journal row, tracked HUD, screenshot `Builds/Logs/relationship-activity-commitment-e2e.png`, reload persistence, and duplicate prevention |  |  |
| Active | JOB-PART-TIME-001 | JOB | Part-Time Work | PlayMode | Assets/Tests/PlayMode/EndToEnd/PartTimeWorkGrowthE2EScenarioTests.cs | Town part-time work growth route and reload |  |  |
| Active | JOB-CAREER-BRIDGE-001 | JOB | Part-Time Work/Career Bridge | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerJobRouteE2EScenarioTests.cs | `CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload` verifies Corner Store work progress unlocks/selects Workshop Daily Plan route, Objective Journal guidance, tracked HUD, screenshot, and reload persistence |  |  |
| Active | KNOW-001 | KNOW | Knowledge/Discovery | PlayMode | Assets/Tests/PlayMode/EndToEnd/EncyclopediaDiscoveryUiE2EScenarioTests.cs | Discovery state appears in player-facing encyclopedia UI |  |  |
| Active | KNOW-STATUS-ACTIVITY-001 | KNOW | Knowledge/Status Activity Chain | PlayMode | Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs | `KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate` verifies Workshop activity status growth, Objective Journal hint, tracked HUD, screenshot `Builds/Logs/knowledge-status-activity-chain-e2e.png`, reload persistence, and duplicate prevention |  |  |
| Active | STATUS-001 | STATUS | Student Condition | PlayMode | Assets/Tests/PlayMode/EndToEnd/StudentRelationshipConditionDayLoopE2EScenarioTests.cs | Relationship and condition day loop through player-facing route |  |  |
| Active | STATUS-002 | STATUS | Day Result State | PlayMode | Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs | Day result state appears and survives reload |  |  |
| Active | RESOURCE-RECIPE-001 | LIFE | Resources/Recipes | PlayMode | Assets/Tests/PlayMode/EndToEnd/TownResourceRecipeActivityE2EScenarioTests.cs | Town resource recipe activity reveals recipe/resource, Objective Journal hint, screenshot, reload, and duplicate prevention |  |  |
| Active | QUEST-INTERIOR-REWARD-001 | LIFE | Quest/Interior Reward | PlayMode | Assets/Tests/PlayMode/EndToEnd/QuestInteriorRewardE2EScenarioTests.cs | `QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload` verifies exact-once furniture reward unlock, visible interior placement, screenshot `Builds/Logs/quest-interior-reward-e2e.png`, reload persistence, and duplicate prevention |  |  |
| Active | NET-SMOKE-001 | NET | Dedicated Multiplayer Smoke | QA runner | Scripts/qa/run-dedicated-multiplayer-smoke.ps1 | Built dedicated server and client connect and move in Town |  |  |
| Active | NET-HOST-001 | NET | Network Session Host | PlayMode | Assets/Tests/PlayMode/Network/NetworkSessionHostPlayModeTests.cs | Host session starts and spawns the player |  |  |
| Active | OBJECTIVE-JOURNAL-REGRESSION-001 | UI | Objective Journal Regression | PlayMode | Assets/Tests/PlayMode/EndToEnd/ObjectiveJournalRegressionE2EScenarioTests.cs | `OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001_MultiSourceReloadNoDuplicateHud` verifies Tab-opened Objective Journal multi-source stable keys, legacy HUD absence, single TrackedObjectiveHud, screenshot `Builds/Logs/objective-journal-source-collision-e2e.png`, and reload row/HUD persistence |  |  |
| Active | HEIR-001 | HEIR | Legacy Heir | EditMode | Assets/Tests/EditMode/HeirGeneratorTests.cs | Legacy domain regression; no player-facing change in this gate |  |  |

## Deferred Scenarios

| Status | ID | Category | Owner | Evidence Type | Evidence | Player-Facing Verification | Deferred Reason | Future Validation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Gate Rules

- Active rows must have a non-empty `Evidence` value.
- File-backed active evidence must exist in the repository.
- Deferred rows must have a non-empty `Deferred Reason`.
- Deferred rows must have a non-empty `Future Validation`.
- Required categories are `TOWN`, `LIFE`, `CAREER`, `REL`, `JOB`, `KNOW`, `STATUS`, `NET`, `UI`, `GEN`, `HEIR`, and `CROP`.
- Goal-like UI scenarios remain owned by `ObjectiveJournalPanel` and `TrackedObjectiveHud`.


2026-11-07: Farm/Crop/Generation, DirectValidation autoplay runners and the clue-route UI were deleted (owner decision). Their rows (GEN-001, CROP-001, KNOW-002, eight dedicated autoplay NET rows) were removed; NET now maps to the remaining multiplayer smoke runner and host PlayMode test.