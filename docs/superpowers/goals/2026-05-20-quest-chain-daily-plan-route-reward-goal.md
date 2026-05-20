# Goal: Quest Chain Rewards A Daily Plan Route

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add one data-driven quest-chain completion reward that activates a world-state flag, unlocks one Daily Plan route, and is visible through the existing Daily Plan board and Objective Journal source contract.

## Non-Negotiable Constraints

- All quest-chain, reward, world-state, route, activity, and Journal behavior must be ScriptableObject-driven.
- Do not branch production C# on quest IDs, chain IDs, reward IDs, world-state IDs, route IDs, activity IDs, location IDs, NPC IDs, career IDs, crop IDs, or tool IDs.
- Reward application must be atomic and idempotent.
- Failed reward validation must not mutate inventory, world-state, route state, quest-chain reward state, or Journal state.
- `ObjectiveJournalPanel` owns quest and route guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for Unity C# files.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming` to select and scope the improvement from the current audit.
- `superpowers:test-driven-development` for reward context, route unlock data, and UI flow changes.
- `superpowers:verification-before-completion` before committing or reporting completion.

## Required Work

- Add or wire one quest-chain completion `WorldStateChangeQuestReward`.
- Ensure `QuestChainRuntimeInstaller` claim flow passes `WorldStateProgress` in `RewardRuntimeContext`.
- Ensure world-state reward changes are persisted after quest-chain reward claim.
- Add one world-state-gated Daily Plan route asset unlocked by that quest-chain reward.
- Register the route through the default Daily Plan route set and `GameDataRegistry` world-state surfaces.
- Ensure Objective Journal route hint behavior uses the existing `DailyPlanRouteHintObjectiveSource`.

## Required TDD

Add failing tests first:

- quest-chain reward claim receives a non-null `WorldStateProgress` in the runtime reward context.
- a registered quest-chain completion reward references the same world-state flag used by the new Daily Plan route requirement.
- the new route is unavailable before the quest reward flag and available after the flag is activated.
- claiming completion rewards is idempotent and cannot reactivate or duplicate the route unlock after reload.
- Objective Journal emits the unlocked unselected route as a hint and removes the hint once the route is selected.

Expected RED:

- tests fail because the quest-chain runtime reward context does not provide/persist `WorldStateProgress`, and because the quest-chain route reward data does not exist yet.

Expected GREEN:

- claiming the quest-chain completion reward activates and persists one world-state flag that unlocks one Daily Plan route.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestRouteRewardE2EScenarioTests.QUEST_ROUTE_REWARD_E2E_001_CompleteChainUnlockRouteReload
Get-ChildItem Builds/Logs/quest-route-reward-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the existing quest-chain UI cannot complete a chain through actual player-facing UI without a broad new quest authoring flow, keep this cycle to claim/reward/route unlock through the existing Quest Chain panel and report the player-flow limitation explicitly.

## Acceptance Criteria

- One quest-chain completion reward unlocks one Daily Plan route through a world-state flag.
- Reward claim is idempotent and persists world-state progress.
- Objective Journal owns the next-action guidance through existing quest-chain and route sources.
- The player can observe the unlocked route through actual Town UI flow.
- Screenshot evidence exists at `Builds/Logs/quest-route-reward-e2e.png` and has non-zero length.
- No production entity ID branching is introduced.

## Commit

Use:

```text
[FEATURE][TEST] Quest chain Daily Plan route 보상 연결
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
