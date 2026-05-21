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
| Active | LIFE-DAILY-PLAN-002 | LIFE | Daily Plan | PlayMode | Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteCompletionE2EScenarioTests.cs | Selected route completion result and reload persistence |  |  |
| Active | CAREER-DAILY-PLAN-001 | CAREER | Career Daily Plan | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerDailyPlanRouteE2EScenarioTests.cs | Career interest unlocks practice route through player-facing flow |  |  |
| Active | CAREER-CANDIDATE-001 | CAREER | Career Candidate | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerCandidateDeepeningLoopE2EScenarioTests.cs | Library/candidate route progression and reload |  |  |
| Active | REL-FIRST-GUIDE-001 | REL | Relationships | PlayMode | Assets/Tests/PlayMode/EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs | First Guide relationship milestone through Town interaction |  |  |
| Active | REL-SECOND-NPC-001 | REL | Relationships | PlayMode | Assets/Tests/PlayMode/EndToEnd/SecondNpcRelationshipMilestoneE2EScenarioTests.cs | Second NPC relationship milestone, Journal guidance, reload |  |  |
| Active | JOB-PART-TIME-001 | JOB | Part-Time Work | PlayMode | Assets/Tests/PlayMode/EndToEnd/PartTimeWorkGrowthE2EScenarioTests.cs | Town part-time work growth route and reload |  |  |
| Active | KNOW-001 | KNOW | Knowledge/Discovery | PlayMode | Assets/Tests/PlayMode/EndToEnd/EncyclopediaDiscoveryUiE2EScenarioTests.cs | Discovery state appears in player-facing encyclopedia UI |  |  |
| Active | KNOW-002 | KNOW | Discovery Route Unlock | PlayMode | Assets/Tests/PlayMode/EndToEnd/DiscoveryRouteUnlockE2EScenarioTests.cs | Clue interpretation unlocks Daily Plan route and reloads |  |  |
| Active | STATUS-001 | STATUS | Student Condition | PlayMode | Assets/Tests/PlayMode/EndToEnd/StudentRelationshipConditionDayLoopE2EScenarioTests.cs | Relationship and condition day loop through player-facing route |  |  |
| Active | STATUS-002 | STATUS | Day Result State | PlayMode | Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs | Day result state appears and survives reload |  |  |
| Active | RESOURCE-RECIPE-001 | LIFE | Resources/Recipes | PlayMode | Assets/Tests/PlayMode/EndToEnd/TownResourceRecipeActivityE2EScenarioTests.cs | Town resource recipe activity reveals recipe/resource, Objective Journal hint, screenshot, reload, and duplicate prevention |  |  |
| Active | NET-ROUTE-REL-001 | NET | Dedicated Route Relationship | QA runner | Scripts/qa/run-dedicated-route-relationship-smoke.ps1 | Built client/server action and observe-only reload screenshots |  |  |
| Active | NET-CAREER-DAILY-001 | NET | Dedicated Career Daily Plan | QA runner | Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1 | Built client/server route unlock and reload screenshots |  |  |
| Active | NET-DAILY-COMPLETE-001 | NET | Dedicated Daily Plan Completion | QA runner | Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1 | Built client/server route completion and reload screenshots |  |  |
| Active | NET-DISCOVERY-ROUTE-001 | NET | Dedicated Discovery Route Unlock | QA runner | Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1 | Built client/server clue route unlock and observe-only reload screenshots |  |  |
| Active | NET-RESOURCE-RECIPE-001 | NET | Dedicated Resource/Recipe | QA runner | Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1 | Built client/server resource recipe action and observe-only reload screenshots |  |  |
| Active | NET-CORE-MATRIX-001 | NET | Dedicated Core Matrix | QA runner | Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 | Matrix runner composes dedicated route/relationship, career, completion, discovery, and Resource/Recipe slices |  |  |
| Active | NET-CORE-MATRIX-RESOURCE-001 | NET | Dedicated Core Matrix Resource/Recipe Slice | QA runner | Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 | Resource/Recipe slice is included in matrix action/reload evidence with recipe/resource, save, Journal, HUD, and cleanup markers |  |  |
| Active | GEN-001 | GEN | Legacy Generation | EditMode | Assets/Tests/EditMode/GenerationManagerTests.cs | Legacy domain regression; no player-facing change in this gate |  |  |
| Active | HEIR-001 | HEIR | Legacy Heir | EditMode | Assets/Tests/EditMode/HeirGeneratorTests.cs | Legacy domain regression; no player-facing change in this gate |  |  |
| Active | CROP-001 | CROP | Legacy Crops | PlayMode | Assets/Tests/PlayMode/Farming/FarmingScenarioTests.cs | Legacy Farm/Crop regression path retained for migration safety |  |  |

## Deferred Scenarios

| Status | ID | Category | Owner | Evidence Type | Evidence | Player-Facing Verification | Deferred Reason | Future Validation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Deferred | JOB-CAREER-BRIDGE-001 | JOB | Part-Time Work/Career Bridge | doc-only deferred |  |  | No committed player-facing job-to-career bridge slice yet | PlayMode plus dedicated smoke after feature slice |

## Gate Rules

- Active rows must have a non-empty `Evidence` value.
- File-backed active evidence must exist in the repository.
- Deferred rows must have a non-empty `Deferred Reason`.
- Deferred rows must have a non-empty `Future Validation`.
- Required categories are `TOWN`, `LIFE`, `CAREER`, `REL`, `JOB`, `KNOW`, `STATUS`, `NET`, `GEN`, `HEIR`, and `CROP`.
- Goal-like UI scenarios remain owned by `ObjectiveJournalPanel` and `TrackedObjectiveHud`.
