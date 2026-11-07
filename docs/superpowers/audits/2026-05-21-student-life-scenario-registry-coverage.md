# Student Life Scenario Registry Coverage

Date: 2026-05-21

This catalog maps active ROOTBORN player-flow, domain, and dedicated validation scenarios to concrete executable evidence. Active rows must map to an existing test file or QA runner. Deferred rows must state why they are not executable yet and what validation type should cover them later.

## Active Scenarios

| Status | ID | Category | Owner | Evidence Type | Evidence | Player-Facing Verification | Deferred Reason | Future Validation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Active | TOWN-FLOW-001 | TOWN | Town/Core Flow | PlayMode | Assets/Tests/PlayMode/EndToEnd/TownExplorationDiscoveryE2EScenarioTests.cs | `TOWN_POINT_001_AuthoredTown_EveryPlacedPointIsReachableAndPromptsItself` walks the player with the keyboard to every placed authored Town point and checks its own prompt |  |  |
| Active | TOWN-FLOW-002 | TOWN | Town/Core Flow | PlayMode | Assets/Tests/PlayMode/EndToEnd/SpumCharacterCreatorNewGameE2ETests.cs | `CREATOR_003_ConfirmSavesNameAndStartingOutfitThatTheTownCharacterWears` starts a new game through the creator UI and enters Town wearing the saved outfit |  |  |
| Active | LIFE-STUDENT-001 | LIFE | Student Life Activities | EditMode | Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs | Domain behavior only; no visible UI changed by this gate |  |  |
| Active | LIFE-HELP-001 | LIFE | Town Help Action | PlayMode | Assets/Tests/PlayMode/EndToEnd/TownHelpActionGrowthE2EScenarioTests.cs | `HELP_001_AuthoredTown_ErrandForMomGrowsResponsibilityAndTrustOncePerHelp` walks to the errand, presses E and checks growth applies once per help |  |  |
| Active | LIFE-FREE-PATH-001 | LIFE | Free-Path Growth | PlayMode | Assets/Tests/PlayMode/EndToEnd/SelfStudyLibraryGrowthE2EScenarioTests.cs | `FREE_PATH_001_FocusGrowsFromLibrarySelfStudyAndFromSchoolClass` grows the same trait through library self-study outside school and through a school class |  |  |
| Active | CAREER-CANDIDATE-001 | CAREER | Career Candidate | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerCandidateDeepeningLoopE2EScenarioTests.cs | Library/candidate route progression and reload |  |  |
| Active | REL-FIRST-GUIDE-001 | REL | Relationships | PlayMode | Assets/Tests/PlayMode/EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs | First Guide relationship milestone through Town interaction |  |  |
| Active | JOB-PART-TIME-001 | JOB | Part-Time Work | PlayMode | Assets/Tests/PlayMode/EndToEnd/PartTimeWorkGrowthE2EScenarioTests.cs | Town part-time work growth route and reload |  |  |
| Active | KNOW-001 | KNOW | Knowledge/Discovery | PlayMode | Assets/Tests/PlayMode/EndToEnd/EncyclopediaDiscoveryUiE2EScenarioTests.cs | Discovery state appears in player-facing encyclopedia UI |  |  |
| Active | KNOW-STATUS-ACTIVITY-001 | KNOW | Knowledge/Status Activity Chain | PlayMode | Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs | `KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate` verifies Workshop activity status growth, Objective Journal hint, tracked HUD, screenshot `Builds/Logs/knowledge-status-activity-chain-e2e.png`, reload persistence, and duplicate prevention |  |  |
| Active | STATUS-002 | STATUS | Day Result State | PlayMode | Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs | Day result state appears and survives reload |  |  |
| Active | RESOURCE-RECIPE-001 | LIFE | Resources/Recipes | PlayMode | Assets/Tests/PlayMode/EndToEnd/TownResourceRecipeActivityE2EScenarioTests.cs | Town resource recipe activity reveals recipe/resource, Objective Journal hint, screenshot, reload, and duplicate prevention |  |  |
| Active | QUEST-DIALOGUE-001 | LIFE | Quest Dialogue | PlayMode | Assets/Tests/PlayMode/EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs | `QUEST_DIALOGUE_E2E_001_AuthoredHouse_ClickMomsAcceptChoiceThenJournalShowsItAcrossScenesAndReload` clicks Mom's accept choice with the mouse, then checks the Objective Journal across scenes and after reload |  |  |
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

2026-11-07 (phase 4): the quarantined PlayMode E2E files that waited for prototype Town/House objects were deleted. TOWN-FLOW-001/002 now point at live authored-Town tests; LIFE-DAILY-PLAN-001/002 and QUEST-INTERIOR-REWARD-001 were replaced by LIFE-HELP-001, LIFE-FREE-PATH-001 and QUEST-DIALOGUE-001; CAREER-DAILY-PLAN-001, REL-SECOND-NPC-001, REL-COMMITMENT-001, JOB-CAREER-BRIDGE-001 and STATUS-001 were removed because no live test covers them. Each category keeps at least one live row.
