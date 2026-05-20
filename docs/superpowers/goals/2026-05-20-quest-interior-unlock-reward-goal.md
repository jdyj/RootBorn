# Goal: Quest Chain Rewards An Interior Unlock

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add one data-driven quest-chain reward that unlocks one furniture palette option, then verify the unlocked furniture can be selected, placed visibly in the House scene, saved, and restored without duplicate reward or placement state.

## Candidate Selection

Considered candidates:

- Quest Chain Rewards An Interior Unlock
  - Why now: quest reward route wiring and host save smoke are complete, so the next highest-value slice is making a quest reward unlock something visible in the home loop.
  - Value: connects Town quest progress to a tangible House/interior reward.
  - Risk: touches reward idempotency, furniture palette filtering, visual placement, and save/load persistence.
  - Verification burden: EditMode reward/catalog tests, strict PlayMode player flow, Game View screenshot, and saved layout inspection.
  - Category: feature, asset, test.
- Second NPC Relationship Route
  - Why now: relationship content is still thin after the First Guide sample.
  - Value: expands repeatable Town social gameplay.
  - Risk: less foundational than proving reward-to-home integration.
  - Verification burden: data wiring, Journal hint, player interaction, save/load.
  - Category: feature, asset, test.
- Daily Plan Route Completion And Day Result Feedback
  - Why now: routes are selectable but need stronger completion feedback.
  - Value: improves the daily loop.
  - Risk: result semantics may need product judgment.
  - Verification burden: route completion, Journal filtering, result UI, reload persistence.
  - Category: feature, UI, test.

Selected candidate: Quest Chain Rewards An Interior Unlock.

## Design

Use the existing `FurnitureDefinition.UnlockToken` field as the data-driven gate for palette availability.

Implementation surface:

- Add `FurnitureUnlockProgress` and `FurnitureUnlockProgressPersistence` under placement/game runtime code.
- Add a `FurnitureUnlockQuestReward` strategy that grants one unlock token to `FurnitureUnlockProgress`.
- Extend `RewardRuntimeContext` with `FurnitureUnlockProgress`.
- Extend quest-chain reward context wiring so claim UI can apply and save furniture unlock progress.
- Update `InteriorFurnitureCatalog` to filter out furniture with a non-empty unlock token unless the active save slot/player has that token.
- Set one existing furniture asset's unlock token to the quest reward token.
- Add the reward asset to the existing test quest chain completion rewards.

This avoids quest ID, reward ID, furniture ID, or room ID branching in production code. The only furniture-specific value lives in asset data.

## Non-Negotiable Constraints

- All quest, reward, unlock, furniture, placement, and Journal behavior must be data-driven.
- Do not branch production C# on quest IDs, reward IDs, furniture IDs, room IDs, route IDs, location IDs, NPC IDs, crop IDs, or tool IDs.
- Reward application must be atomic and idempotent.
- Visible placement work must verify both runtime Game View and saved placement state.
- If tilemap/sprite/prefab/scene state should persist, inspect the serialized save state after placement.
- `ObjectiveJournalPanel` owns quest guidance; do not create new quest HUDs.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Every behavior change follows TDD.
- Keep unrelated dirty or untracked files untouched and unstaged.
- If `Assets/AddressableAssetsData/link.xml` or `.meta` is deleted by Unity tooling, restore it immediately and keep it out of this cycle unless the cycle explicitly changes it.

## Required Skills

- `superpowers:brainstorming` for candidate selection and scope shaping.
- `superpowers:test-driven-development` for all behavior changes.
- `superpowers:systematic-debugging` for test, Unity, placement, or save/load failures.
- `superpowers:verification-before-completion` before claiming completion or committing.

## Required Work

Create one quest-to-interior reward slice:

- one furniture unlock progress model and persistence file
- one quest-chain reward strategy that grants one furniture unlock token
- one existing furniture asset gated by that token
- one existing quest-chain completion reward list containing the unlock reward
- palette filtering that hides locked furniture before reward claim and shows it after reward claim
- placement UI flow that can select and place the unlocked furniture
- save/load behavior that restores quest state, reward claim, unlock state, and placed furniture
- repeated claim or reload must not duplicate unlocks or placed furniture

## Required TDD

Add failing tests first:

- `FurnitureUnlockQuestReward` cannot apply without unlock progress.
- the reward unlocks its token exactly once and remains idempotent.
- the registered test quest-chain completion rewards include a furniture unlock reward.
- the selected furniture asset is hidden from `InteriorFurnitureCatalog` before unlock.
- the selected furniture asset appears in `InteriorFurnitureCatalog` after unlock.
- unlock progress persists per save slot/player.

Expected RED:

- tests fail because reward-to-furniture unlock progress, reward asset wiring, and palette filtering do not exist yet.

Expected GREEN:

- one quest reward unlocks one furniture palette option exactly once and the catalog respects the unlock state without entity-ID production branching.

## Required PlayMode Scenario

Create or extend:

```text
QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town and complete/claim the quest-chain reward through actual movement, interact key, and visible UI buttons.
3. Verify visible quest or Objective Journal state in Game View.
4. Verify the furniture unlock token runtime/save state.
5. Transition to House through the project-supported player-facing path. If no Town-to-House portal exists in the current scene, use the closest existing project-supported scene transition and state the limitation in the test failure messages.
6. Open placement UI through the actual input/UI flow.
7. Verify the locked furniture is absent before reward and visible/selectable after reward.
8. Select the unlocked furniture option.
9. Place it visibly.
10. Inspect runtime placement state, including selected furniture asset name and placement position.
11. Capture screenshot:

```text
Builds/Logs/quest-interior-reward-e2e.png
```

12. Save the furniture layout.
13. Reload the same save slot through SaveSlot UI or the project-supported House reload path.
14. Verify reward cannot be claimed twice and the placed item persists.
15. Inspect serialized save state, including the unlock file and `house-furniture-layout*.json`.

Forbidden shortcuts:

- direct furniture unlock mutation as the only proof
- direct placement registry mutation as the only proof
- direct scene loading as the only visual proof when a project-supported player-facing transition exists
- JSON-only save verification without Game View placement evidence

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
Get-ChildItem Builds/Logs/quest-interior-reward-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If `Rootborn.Tests.EditMode.Interiors` has no selected tests in this repo version, run the relevant placement/interior EditMode tests by class name and record that substitution in the final cycle report.

## Acceptance Criteria

- One quest-chain reward unlocks one interior/furniture option.
- The locked furniture is unavailable before reward claim and available after reward claim.
- The unlock and placement are visible through actual player flow.
- Reward claim, unlock persistence, and placement persistence are idempotent.
- Runtime and saved-state evidence are both checked.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

## Commit

Use:

```text
[FEATURE][ASSET][TEST] Quest interior unlock 보상 연결
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
