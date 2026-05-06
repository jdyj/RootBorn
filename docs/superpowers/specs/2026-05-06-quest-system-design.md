# ROOTBORN Quest System Design

## Context

This spec follows `docs/superpowers/goals/2026-05-06-quest-system-goal.md`, `AGENTS.md`, and `.claude/rules/*`.

The project currently has no dedicated quest or dialogue domain. The relevant existing foundations are:
- `Rootborn.Game.Player.GatherInteractor` for player interaction, resource hits, inventory drops, and farming tool dispatch.
- `Rootborn.Game.Common.Inventory` and `PlayerInventory` for item storage and equipment.
- `Rootborn.Game.Knowledge.KnowledgeProgress` for data-driven unlock evaluation.
- `Rootborn.Game.Save.SaveService` for JSON persistence.
- `Rootborn.Game.Common.GameDataRegistry` and `Rootborn.Editor.DataValidators.GameDataRegistryValidator` for SO registration checks.
- `Rootborn.UI.HUD.FarmHudController` and related UGUI patterns for playable Farm scene UI.

The selected UI scope is playable Farm-scene integration: NPC dialogue choices, quest list, and reward claim button should be usable in scene, not only in tests.

## Non-Negotiable Rules

- C# under `Assets/**/*.cs` must be created or modified through Unity MCP `script-update-or-create` or an Editor-safe path, not direct disk writes.
- New behavior must follow TDD: failing tests first, failure confirmed, minimum implementation, related tests rerun.
- Quest, NPC, dialogue, reward, and story trigger definitions must be ScriptableObject data.
- No entity-ID branching: no questId/npcId/resourceId/toolId/cropId `if`, `switch`, or enum special cases in runtime code.
- Existing user changes in the worktree must not be reverted.
- Reward claim must preflight inventory capacity and duplicate-claim state before mutating inventory or quest state.
- Reward delivery must be all-or-nothing and idempotent across repeated calls and save/load.

## Recommended Architecture

Use a quest runtime core with thin Dialogue, Interaction, UI, and game-event adapters.

`Rootborn.Game.Quests` owns quest state, objective counting, reward preflight, reward claim, completion effects, and persistence DTOs. NPCs, interactable objects, farming/resource/combat events, and UI call into that core. This keeps the high-risk rules testable without needing a scene for every state transition.

The UI remains playable, but it does not own quest logic. It renders the current `QuestLog` state and sends user intents such as accept quest, complete dialogue choice, or claim reward.

## Runtime Domains

### Quests

`QuestDefinition : ScriptableObject`
- `_id` only as stable save/debug identity, never for runtime special branching.
- `_displayNameKey`, `_descriptionKey`.
- `_providerConditions : QuestConditionBase[]`.
- `_prerequisites : QuestConditionBase[]`.
- `_objectives : QuestObjectiveBase[]`.
- `_rewards : QuestRewardBase[]`.
- `_completionEffects : QuestCompletionEffectBase[]`.

`QuestLog`
- Owns all `QuestProgress` instances for a player/session.
- Accepts a `GameDataRegistry` or explicit quest set.
- Provides:
  - `CanOffer(QuestDefinition, QuestRuntimeContext)`.
  - `Accept(QuestDefinition)`.
  - `RecordEvent(QuestEvent)`.
  - `CanClaimReward(QuestDefinition, RewardRuntimeContext)`.
  - `ClaimReward(QuestDefinition, RewardRuntimeContext)`.
  - `ToSaveData()` / `LoadFromSaveData(...)`.

`QuestProgress`
- States: `NotStarted`, `Active`, `Completed`, `RewardClaimed`.
- Stores per-objective counters by objective index, not by entity-specific code paths.
- Stores processed event keys to prevent duplicate event application.
- Stops counting after `Completed` unless a future requirement explicitly introduces repeatable quests.

`QuestEvent`
- Deterministic value object for real gameplay events:
  - Gather resource node hit/break.
  - Defeat target.
  - Collect/acquire inventory item.
  - Harvest crop.
  - Talk to NPC.
  - Reach location or use tool events are reserved extension points and are out of the first implementation slice unless a QUEST test requires them.
- Contains SO references where possible: `ResourceNodeDefinition`, `ItemDefinition`, `CropDefinition`, `ToolDefinition`, `NpcDefinition`.
- Contains an optional event instance key for duplicate prevention. Tests can inject deterministic keys.

### Objectives

`QuestObjectiveBase : ScriptableObject`
- `RequiredCount`.
- `GetDisplayKey()`.
- `Matches(in QuestEvent, in QuestObjectiveRuntimeContext)`.
- `GetCountDelta(in QuestEvent, in QuestObjectiveRuntimeContext)`.

Initial objective SO types:
- `GatherQuestObjective`: target `ResourceNodeDefinition`, count from actual gather/break event.
- `DefeatQuestObjective`: target defeat definition, count from combat event. The defeat target definition can be introduced as a small SO if combat has no existing target SO.
- `CollectQuestObjective`: target `ItemDefinition`, count from item acquisition event. It should align with inventory additions, not raw UI state.
- `HarvestQuestObjective`: target `CropDefinition` or harvest `ItemDefinition`, count from `HarvestCropEffect`/`FarmGrid.TryHarvest` success path.
- `TalkQuestObjective`: target `NpcDefinition`, count from dialogue completion event.

Objectives do not inspect string IDs to decide behavior. Tests may assert IDs for fixture clarity, but runtime matching uses SO references and strategy methods.

### Rewards

`QuestRewardBase : ScriptableObject`
- `CanApply(in RewardRuntimeContext)`.
- `Apply(in RewardRuntimeContext)`.
- `DescribeKey`.

Initial reward SO types:
- `ItemQuestReward`: grants `ItemDefinition` and count.
- `KnowledgeQuestReward`: unlocks `KnowledgeNode` through `KnowledgeProgress.Seed` or a dedicated unlock interface.
- `StoryFlagQuestReward`: sets `StoryFlagDefinition`.
- `ToolUnlockQuestReward`: grants the tool item or unlocks tool availability through data-defined effect.

Reward claim flow:
1. Reject if quest is not `Completed`.
2. Reject if quest is already `RewardClaimed`.
3. Run `CanApply` for every reward and completion effect that mutates inventory/progression.
4. If any preflight fails, return a failure result and mutate nothing.
5. Apply all rewards.
6. Apply completion effects.
7. Mark quest `RewardClaimed`.
8. Persisted state records `RewardClaimed`, so reload cannot grant again.

Inventory safety:
- `Inventory` needs a capacity-aware preflight API or reward code needs an injected inventory gateway that can simulate/add atomically.
- Item rewards must not call `Inventory.Add` until all reward items fit.
- Multi-reward quests cannot partially grant.
- Repeated claim calls return an idempotent no-op result after `RewardClaimed`.

### Conditions And Story

`QuestConditionBase : ScriptableObject`
- Evaluates data-defined prerequisites.

Initial condition SO types:
- `QuestStateCondition`: requires another `QuestDefinition` to be Active/Completed/RewardClaimed.
- `StoryFlagCondition`: requires a `StoryFlagDefinition` state.
- `KnowledgeUnlockedCondition`: requires a `KnowledgeNode`.

`QuestCompletionEffectBase : ScriptableObject`
- Runs after reward preflight succeeds and reward application begins.
- Initial effects:
  - Set `StoryFlagDefinition`.
  - Unlock or offer next `QuestDefinition`.
  - Change NPC dialogue state through data-defined `DialogueDefinition`.
  - Unlock `KnowledgeNode`.

Story content is out of scope. The system only provides data-driven progression hooks.

### Dialogue And NPC

`Rootborn.Game.Dialogue` owns data, not quest state.

`NpcDefinition : ScriptableObject`
- `_id` as stable identity only.
- `_displayNameKey`.
- `_defaultDialogue`.
- `_availableQuests : QuestDefinition[]`.
- Optional `_dialogueRules : DialogueRuleDefinition[]`.

`DialogueDefinition : ScriptableObject`
- `_lineKeys`.
- `_choices : DialogueChoiceDefinition[]`.

`DialogueChoiceDefinition : ScriptableObject`
- `_labelKey`.
- Optional quest action:
  - Accept quest.
  - Claim quest reward.
  - Continue/close dialogue.
- Optional conditions using `QuestConditionBase[]`.

`NpcInteractor : MonoBehaviour`
- References `NpcDefinition`.
- Opens the dialogue UI through a scene service or serialized UI reference.
- Emits `Talk` quest events when a dialogue branch completes.

`QuestProvider : MonoBehaviour`
- Can be attached to NPCs or non-NPC interactable objects.
- References one or more `QuestDefinition`.
- Delegates offer/accept checks to `QuestLog`.
- This satisfies QUEST-003 without forcing every provider to be an NPC.

## Game Event Integration

The first implementation should add narrow event hooks at existing success points:
- `GatherInteractor` or `ResourceNode` success path emits Gather events after actual resource hit/break.
- Combat defeat emits Defeat events when combat exists. If combat is not present yet, provide deterministic test injection and a thin future adapter seam.
- `Inventory.Add` path or an inventory gateway emits Collect events for actual item acquisition. Collection counting must match real item increases.
- `HarvestCropEffect` emits Harvest events only after `FarmGrid.TryHarvest` succeeds and inventory reward is applied or confirmed.
- `NpcInteractor` emits Talk events after dialogue completion.

Events should be injectable in EditMode tests without scene state. Scene adapters should be thin enough that PlayMode tests validate integration without duplicating all unit cases.

## UI Design

Playable UI scope:
- `DialoguePanel`
  - Shows NPC name, localized dialogue lines, and choices.
  - Choices can accept quests, claim rewards, or close dialogue.
  - Quest-related choices are hidden/disabled based on `QuestLog.CanOffer`, completion state, and reward preflight result.
- `QuestLogPanel`
  - Shows active and completed quests.
  - Shows objective current/required progress.
  - Shows completion readiness and reward claim state.
- `QuestRewardButton`
  - Calls `CanClaimReward` before enabling.
  - Shows failure state when inventory cannot accept rewards.

UI text uses localization keys from data. Hardcoded runtime display strings are limited to test fixtures or temporary diagnostics.

## Data And Registry

New data folders:
- `Assets/Data/Quests/`
- `Assets/Data/Quests/Objectives/`
- `Assets/Data/Quests/Rewards/`
- `Assets/Data/Quests/Conditions/`
- `Assets/Data/Quests/Effects/`
- `Assets/Data/NPCs/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Story/`

`GameDataRegistry` should add arrays for:
- `QuestDefinition[]`.
- `QuestObjectiveBase[]` if standalone objective assets must be globally validated.
- `QuestRewardBase[]`.
- `QuestCompletionEffectBase[]`.
- `QuestConditionBase[]`.
- `NpcDefinition[]`.
- `DialogueDefinition[]`.
- `StoryFlagDefinition[]`.

`GameDataRegistryValidator` should validate:
- Non-null entries.
- Unique `_id` where present.
- All quest references have registered dependencies.
- Quest definitions have at least one objective unless explicitly marked as dialogue-only.
- Reward definitions are registered and valid.

`GenerateDefaultData` can create a small starter quest data set for tests and Farm scene smoke:
- One NPC provider.
- One gather objective.
- One item reward.
- One story flag completion effect.

## Persistence

Quest save data should be JSON-serializable:
- Quest stable identity.
- Quest state.
- Objective counters by index.
- Processed event keys for active quests.
- Claimed rewards.
- Story flags.

Load behavior:
- Resolve saved identities through registry data.
- Unknown or missing quest data is ignored with a warning result, not a crash.
- `RewardClaimed` quests never apply rewards during load.
- Active objective progress resumes from saved counters and does not recalculate from inventory totals.

## Test Plan

All C# tests must be written with Unity MCP `script-update-or-create`.

### EditMode Unit Tests

- QUEST-002: accepting a quest through the dialogue/quest command changes state to Active.
- QUEST-004: incomplete objectives do not complete the quest.
- QUEST-005: completed quest reward grants exactly once.
- QUEST-006: repeated reward claim is idempotent and grants no duplicate items.
- QUEST-007: Gather objective counts actual gather events.
- QUEST-008: Defeat objective counts actual defeat events.
- QUEST-009: Collect objective matches actual item acquisition.
- QUEST-010: completion effect unlocks next quest or StoryFlag through data.
- QUEST-011: prerequisites block unavailable follow-up quests.
- QUEST-012: active objective progress survives save/load.
- QUEST-013: RewardClaimed survives save/load and cannot pay again.
- Reward inventory safety: full inventory or invalid reward causes no item/state mutation.

### EditMode Integration Tests

- QuestDefinition + Objective + Reward + CompletionEffect composition.
- GameDataRegistry registration and validator coverage.
- Inventory/Knowledge/StoryFlag integration through reward and completion effect strategies.
- Static usage gate extended to detect quest/npc/resource/tool/crop ID branching.

### PlayMode Scenario Tests

- QUEST-001: NPC interaction opens and closes dialogue.
- QUEST-002: NPC dialogue choice accepts quest and activates it.
- QUEST-003: non-NPC interactable object can provide a QuestDefinition.
- Full scenario: NPC dialogue -> accept quest -> real gather/harvest/collect event -> completion -> reward claim -> follow-up story trigger.
- QUEST-015 regression: run existing KNOW/CROP/TOOL/GEN/STATUS/NET and inventory-related tests.

### Required Regression Runs

- `KnowledgeProgressTests`
- `KnowledgeTriggerTests`
- `GatherInteractorTests`
- `InventoryUiTests`
- `InventoryEquipmentScenarioTests`
- `FarmingScenarioTests`
- `CropGrowthTests`
- `ToolDefinitionStrategyTests`
- `GameDataRegistryValidator` tests or validator command
- Relevant EditMode suite after each TDD slice
- Relevant PlayMode scenario suite after scene/UI integration

## Implementation Slices After Spec Approval

1. Add QUEST scenario IDs and failing QuestProgress state tests.
2. Implement minimal `QuestDefinition`, `QuestProgress`, and `QuestLog`.
3. Add objective counting tests and implement Gather/Defeat/Collect/Harvest/Talk objective strategies.
4. Add reward preflight/idempotency tests and implement reward gateway.
5. Add completion effect and prerequisite tests.
6. Add save/load tests and persistence DTOs.
7. Add registry tests and registry/validator fields.
8. Add dialogue/NPC/provider tests and thin runtime components.
9. Add playable UGUI panels and PlayMode scenario tests.
10. Add default SO data and registry registration.
11. Run targeted regressions, then broader EditMode/PlayMode checks.

## Completion Criteria

- QUEST-001 through QUEST-015 tests exist and pass.
- Quest accept, progress, completion, reward claim, and story trigger are data-driven.
- Reward claim preflights inventory capacity and duplicate state before mutation.
- Reward claim is atomic and idempotent across repeated calls and save/load.
- No runtime entity-ID branching or entity enum special cases exist.
- All new SOs are under the approved `Assets/Data/**` folders and registered in `GameDataRegistry`.
- Existing KNOW/CROP/TOOL/GEN/STATUS/NET and inventory tests still pass.
- Final report lists modified files, added SO/data, added tests, executed tests, pass/fail result, and remaining risks.

## Risks And Constraints

- Current `Inventory` has `Add`, `Remove`, and `CountOf`, but no capacity or transaction API. Reward safety likely requires a small inventory gateway or capacity-aware API before item reward implementation.
- `StatusHud.cs` currently contains multiple UI classes. Quest UI should be separated into `Rootborn.UI.Quests` to avoid increasing that file further.
- Combat defeat events may not have an existing combat domain. Defeat objective should support deterministic injected events first and connect to real combat when combat exists.
- The worktree already contains unrelated user changes. Implementation must read current files before editing and only touch necessary paths.
