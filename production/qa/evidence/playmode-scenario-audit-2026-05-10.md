# PlayMode Scenario Audit - 2026-05-10

## Summary

- Audit scope: `Assets/Tests/PlayMode`, `Assets/Tests/EditMode/Quests`, `Assets/Tests/EditMode/Dialogue`.
- Goal focus: quest/dialogue/reward/save flows that must prove actual PlayMode user paths, not only direct method calls.
- New coverage added: `QuestPlayUserFlowScenarioTests` covers `QUEST-PLAY-001` through `QUEST-PLAY-005` on the Town scene with `GatherInteractor.TriggerInteract()` and `EventSystem` pointer-clicked `Button` objects.
- Remaining risk: several older E2E and slot-isolation quest tests still use `DialoguePanel.Choose(...)`, `NpcInteractor.Interact()`, `QuestLog.RecordEvent(...)`, or `Button.onClick.Invoke()` directly. They remain useful regression/unit-style checks, but are not accepted as user-scenario evidence.

## Whole Suite Classification

| Scope | Tests/classes audited | Classification | Quest/dialogue/reward/save scenario gap |
|---|---|---|---|
| PlayMode quest/user-flow tests | `QuestPlayUserFlowScenarioTests`, `QuestDialogueScenarioTests` | Actual PlayMode path for the named quest scenarios after this pass | None for `QUEST-PLAY-001` through `QUEST-PLAY-005` |
| PlayMode older quest/E2E tests | `BootToGameEndToEndTests`, `NpcQuestSaveSlotIsolationTests`, `QuestProviderScenarioTests` | Mixed: scene/runtime smoke plus direct method calls | Useful regression coverage, but direct `Choose`/`Interact`/`RecordEvent` paths are documented below as non-evidence for user scenarios |
| PlayMode non-quest user flows | `TownKeyboardInteractionPlayModeTests`, `TownCareerPracticeInteractionTests`, `TownStudentLifeInteractionTests`, `PlayerWorldInteractionScenarioTests`, `WorldPortalTriggerTests`, `InventoryEquipmentUiScenarioTests`, `GlobalSceneUiPlayModeTests`, `FarmingScenarioTests`, `FarmFlowTests`, `SaveSlotUiFlowTests` | Mostly actual PlayMode interaction paths using input, interactors, scene loading, or UI buttons | Outside quest reward scope; no blocking gap for `QUEST-PLAY-*` |
| PlayMode visual/runtime smoke | `CharacterPartCompositionPlayModeTests`, `FarmCharacterHudOverlayPlayModeTests`, `FarmCharacterHudReferenceExactMatchPlayModeTests`, `FarmCharacterHudOverlayPlayModeTests`, `ModernUiPanelAutoInstallerPlayModeTests`, `ModernUiReconstructionScenarioTests`, `TownSceneBootTests`, `TownStyle2SpriteExhaustivePlayModeTests`, `TownStyle2UiSmokeTests`, `SeededFarmGenerationFlowTests` | PlayMode smoke, screenshot/pixel, installer, or rendering validation | Not quest-flow evidence; missing/broken sprites are tracked separately |
| EditMode quest/dialogue tests | `DialogueQuestTests`, `QuestProgressTests`, `QuestObjectiveTests`, `QuestEventAdapterTests`, `QuestRewardTests`, `QuestSaveTests`, `QuestConditionTests`, `QuestRegistryTests`, `QuestUiTests`, `NpcQuestNetworkSyncTests`, `QuestHudAutoFillerSourceTests` | Domain/unit/source/UI-structure tests using direct calls | Retained for model correctness; not counted as user-scenario completion evidence |
| EditMode non-quest tests | All other EditMode classes found under `Assets/Tests/EditMode` | Unit/source/data/asset wiring tests | Outside `QUEST-PLAY-*`; previously inventoried in `full-code-test-gap-audit-2026-05-06.md` and re-scanned by class during this pass |

## Scenario Coverage Matrix

| Scenario ID | Test name | Current verification method | Actual PlayMode path covered | Gap | Fix/additional scenario | Status |
|---|---|---|---|---|---|---|
| QUEST-PLAY-001 | `QuestPlayUserFlowScenarioTests.QUEST_PLAY_001_TownGuideDialogueCloseButtonClickClosesWithoutQuestMutation` | Town scene load, player moved near `GuideNpc`, `GatherInteractor.TriggerInteract()`, real `CloseButton` clicked through `EventSystem` | Yes | None for close flow | Added close button to `DialoguePanel` | Done |
| QUEST-PLAY-002 | `QuestPlayUserFlowScenarioTests.QUEST_PLAY_002_AcceptChoiceButtonClickActivatesQuestAndQuestLogUi` | Town scene, prompt visible, real `ChoiceButton_0` clicked through `EventSystem`, `QuestLogPanel` text checked | Yes | Uses direct position placement near NPC rather than walking there | Keep as PlayMode interaction test; keyboard walking is covered separately by Town keyboard tests | Done |
| QUEST-PLAY-003 | `QuestPlayUserFlowScenarioTests.QUEST_PLAY_003_004_TalkObjectiveCompletesAndClaimChoiceButtonPaysOnce` | Quest accepted by real choice button, objective completed by talking to NPC again through `GatherInteractor.TriggerInteract()` | Yes | Covers Talk objective path, not gather/resource objectives | Add a later resource-gather quest PlayMode path if default quest data adds one | Done |
| QUEST-PLAY-004 | `QuestPlayUserFlowScenarioTests.QUEST_PLAY_003_004_TalkObjectiveCompletesAndClaimChoiceButtonPaysOnce` | Claim reward through real `ClaimReward` choice button, verifies trait delta/career hint and duplicate click idempotency | Yes | Reward type is StudentLife trait/career hint, not inventory item | Inventory reward duplicate path remains covered by EditMode and older E2E direct-call tests | Done |
| QUEST-PLAY-005 | `QuestPlayUserFlowScenarioTests.QUEST_PLAY_005_RewardClaimedPersistsAfterTownReloadAndCannotPayTwice` | Accept/complete/claim via PlayMode UI path, save file existence, Town reload, reward state remains claimed, second claim click does not mutate trait | Yes | StudentLife reward persistence is inferred from quest state; trait progress persistence is not separately saved/loaded here | Add StudentLife save/load assertion when that persistence format is formalized | Done |

## Existing Quest/Dialog Tests Classification

| Test name | Current verification method | Actual PlayMode path covered | Gap | Additional/fixed scenario ID | Status |
|---|---|---|---|---|---|
| `QuestDialogueScenarioTests.QUEST_PM_UI_001_TownNpcChoiceButtonClickAcceptsQuest` | `GatherInteractor.TriggerInteract()` plus `EventSystem` click on `ChoiceButton_0` | Yes | Does not verify close, completion, reward, or save | Superseded by `QUEST-PLAY-002` for acceptance | Updated |
| `QuestDialogueScenarioTests.QUEST_STUDENT_PM_001_TownNpcPromptEQuestClaimRaisesAllCareerTraits` | Prompt visible, `GatherInteractor.TriggerInteract()`, real accept/claim buttons, second NPC interaction completes Talk objective | Yes | Loops through all career routes but does not reload save | Covered by `QUEST-PLAY-003/004`; reload covered by `QUEST-PLAY-005` | Updated |
| `QuestDialogueScenarioTests.QUEST_001_NpcInteraction_OpensAndClosesDialogue` | Direct `NpcInteractor.Interact()` and `Close()` on constructed object | No | Unit-style session check only | Not accepted as user scenario | Intentionally retained |
| `QuestDialogueScenarioTests.QUEST_015_NpcQuest_FullFlow_ClaimReward_SetsStoryFlag` | Direct `QuestLog.Accept`, `RecordEvent`, `ClaimReward` | No | Code-level reward transaction only | Not accepted as user scenario | Intentionally retained |
| `BootToGameEndToEndTests.QUEST_E2E_001_NpcInteractionOpensDialogueChoiceAndAcceptsQuest` | Direct `npc.Interact()` and `DialoguePanel.Choose(0)` | Partial | Does not use Button/EventSystem path | `QUEST-PLAY-002` now supplies accepted user-flow evidence | Needs later cleanup |
| `BootToGameEndToEndTests.QUEST_E2E_003_RewardClaimedPersistsAcrossSaveLoadAndCannotPayTwice` | Direct `DialoguePanel.Choose`, direct `QuestLog.RecordEvent` helper | Partial | Save/reward regression is useful, but not user-flow evidence | `QUEST-PLAY-005` now supplies Town UI save/reload evidence | Needs later cleanup |
| `BootToGameEndToEndTests.QUEST_E2E_004_AcceptedQuestPersistsAcrossFarmReload` | Direct `npc.Interact()` and `DialoguePanel.Choose(0)` | Partial | Accept persistence is direct panel method, not Button/EventSystem | `QUEST-PLAY-005` covers post-claim persistence; active-only user-flow persistence remains optional | Open |
| `NpcQuestSaveSlotIsolationTests.NPC_QUEST_006_SaveSlotAQuestAcceptanceDoesNotBleedIntoSaveSlotB` | Direct `npc.Interact()` and `DialoguePanel.Choose(0)` | Partial | Slot isolation is valid, but acceptance path is not real button click | Add Button/EventSystem variant if slot bleed becomes high-risk | Open |
| `DialogueQuestTests.*` | Direct dialogue/quest model calls | No | EditMode unit coverage only | Not accepted as PlayMode user-scenario evidence | Intentionally retained |
| `QuestProgressTests`, `QuestObjectiveTests`, `QuestRewardTests`, `QuestSaveTests` | Direct quest model calls | No | Domain unit coverage only | Not accepted as PlayMode user-scenario evidence | Intentionally retained |
| `QuestUiTests.*` | EditMode UI structure/component assertions | No | Verifies Button/structure existence, not EventSystem click path | `QUEST-PLAY-001/002/004` cover click path | Intentionally retained |

## Empty Or Weak Scenario Areas

| Area | Current state | Risk | Follow-up |
|---|---|---|---|
| Resource-gather quest PlayMode completion | Default Town quest path covered here is Talk objective based | Gather objective user path can still regress separately | Add a data-backed Town/Farm gather quest scenario when a default gather quest is promoted to the main loop |
| Active-only save/reload | Reward-claimed save/reload is covered by `QUEST-PLAY-005`; older active reload test uses direct `Choose` | Active quest may regress before reward claim | Convert `BootToGameEndToEndTests.QUEST_E2E_004` or add `QUEST-PLAY-006` with real accept button and reload before completion |
| Farm older quest E2E | Several Farm E2E tests still call `DialoguePanel.Choose` and `QuestLog.RecordEvent` | Tests can pass while UI buttons break | Keep as regression tests, but do not count as user-scenario completion evidence |
| Inventory item reward through real UI | StudentLife reward path is covered; item reward atomicity mostly EditMode/direct E2E | Item reward UI claim could break independently | Add real Button/EventSystem item reward scenario if default quest data includes an item reward route |

## Evidence From This Pass

| Verification | Result |
|---|---|
| RED, initial `QuestPlayUserFlowScenarioTests` | Failed on missing `CloseButton`, stale `QuestLogPanel` state text, and Talk objective not progressing through NPC re-interaction |
| GREEN, `QuestPlayUserFlowScenarioTests` | Passed 4/4 after fixes |
| `QuestDialogueScenarioTests` | Passed 4/4 after replacing direct choice calls in scenario tests with real button clicks |
| `BootToGameEndToEndTests` | Passed 10/10 |
| `NpcQuestSaveSlotIsolationTests` | Passed 1/1 |
| `QuestUiTests` | Passed 5/5 |
| `Rootborn.Tests.EditMode.Quests` | Passed 32/32 |
| `Rootborn.Tests.EditMode.Dialogue` | Passed 3/3 |
| Full PlayMode suite via MCP | Attempted; MCP `tests-run` timed out at 120 seconds before returning a complete result |
| Full PlayMode suite via Unity CLI `-batchmode -nographics` | Attempted; Unity crashed in URP/TilemapRenderer before writing result XML. Log: `Builds/Logs/playmode.log` |
| Full PlayMode suite via Unity CLI `-batchmode -force-d3d11` | Completed with result XML: 84 total, 75 passed, 9 failed, 0 skipped, duration 112.2956419s. Log: `Builds/Logs/playmode-graphics.log`; results: `Builds/Logs/playmode-results.xml` |
| `Scripts/ci/check-no-entity-id-branching.sh` via Git Bash | Passed: `OK: no entity-id branching in system code.` |
| Missing image references | Existing evidence file: `production/qa/evidence/missing-image-references-2026-05-10.md`; no new missing sprite/texture reference was isolated during this quest pass |

## Implementation Notes

- `DialoguePanel` now creates a real `CloseButton`.
- `QuestLogPanel` subscribes to `QuestLog.OnStateChanged` and refreshes displayed state/claim binding.
- `QuestRewardButton` now wires its Unity `Button.onClick` to the reward transaction path.
- `NpcInteractor.Interact()` records a data-driven `QuestEventKind.Talk` event for the bound `NpcDefinition`, so talking to the NPC again can complete Talk objectives through the same interaction route the player uses.

## Full PlayMode CLI Failures

| Test name | Failure reason | Scenario impact |
|---|---|---|
| `CharacterPartCompositionPlayModeTests.CHAR_PART_005_FarmPlayerLayeredRenderScreenshot_WritesEvidence` | `WaitForEndOfFrame` is not evoked in batchmode | Screenshot evidence test is not CLI-batchmode compatible |
| `FarmCharacterHudOverlayPlayModeTests.FarmScene_TopLeftHudReferenceComparison_WritesReport` | `WaitForEndOfFrame` is not evoked in batchmode | Screenshot/reference report test is not CLI-batchmode compatible |
| `FarmCharacterHudOverlayPlayModeTests.FarmScene_TopLeftHudScreenshotAudit_WritesEvidence` | `WaitForEndOfFrame` is not evoked in batchmode | Screenshot evidence test is not CLI-batchmode compatible |
| `FarmCharacterHudReferenceExactMatchPlayModeTests.FarmScene_TopLeftHudCropMatchesReferencePixelsStrictly` | `WaitForEndOfFrame` is not evoked in batchmode | Pixel crop assertion is not CLI-batchmode compatible |
| `ModernUiReconstructionScenarioTests.InventoryAndStatusPreviewWindows_UseTiledModernUiPanels` | `WaitForEndOfFrame` is not evoked in batchmode | Screenshot/pixel wait path is not CLI-batchmode compatible |
| `ModernUiReconstructionScenarioTests.SettingsPanel_ShowAndHide_UsesTiledModernUiPanel` | `WaitForEndOfFrame` is not evoked in batchmode | Screenshot/pixel wait path is not CLI-batchmode compatible |
| `TownKeyboardInteractionPlayModeTests.LIFE_CAREER_PRACTICE_PM_008_PlayerKeyboardWalksToCareerBoardAndEProgressesAllRoutes` | InputSystem `Key:/Keyboard/w` has no associated state during `InputTestFixture.Release` | Keyboard fixture cleanup is unstable in full CLI suite |
| `TownKeyboardInteractionPlayModeTests.LIFE_STUDENT_PM_005_PlayerKeyboardWalksToSchoolAndEAppliesActivity` | InputSystem `Key:/Keyboard/w` has no associated state during `InputTestFixture.Release` | Keyboard fixture cleanup is unstable in full CLI suite |
| `TownKeyboardInteractionPlayModeTests.LIFE_STUDENT_PM_006_PlayerKeyboardWalksToStudyBasicsAndEAppliesActivity` | InputSystem `Key:/Keyboard/w` has no associated state during `InputTestFixture.Release` | Keyboard fixture cleanup is unstable in full CLI suite |
