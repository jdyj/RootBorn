# Multistep branching QuestChain implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax. This assigned worker stages files only; root imports scripts through Unity MCP, runs RED/GREEN, and performs actual player verification.

**Goal:** Execute existing quest-step data with ordered conditional branches, aggregate reward preflight, deterministic persistence and a current-step Journal display, without authoring unconfirmed story canon.

**Architecture:** Keep the existing `QuestChainProgress`/`QuestChainLog` and old single-step entry points. Add distinctly named context-aware APIs because existing reflection tests select methods by name. A concrete transition strategy references a target step SO plus existing condition strategies. Reuse `InventoryGrant` and coordinate a shared atomic Inventory exchange with the adult-facility worker; root owns applying that method to `GameDataRegistry.cs`.

**Tech Stack:** Unity 6000.3.13f1, C#, ScriptableObject strategies, NUnit EditMode, existing PlayMode E2E infrastructure.

## File map and API contract

- Test `Assets/Tests/EditMode/StudentLife/QuestChainBranchingTests.cs`: behavioral tests staged FIRST, only existing types or reflection for new APIs.
- Modify `Assets/Scripts/Game/Quests/QuestChainCore.cs`: sequential/branch advancement, terminal state only at final node, completed-step/reward/history persistence, conditions, invalid destinations, no repeated-event advancement, current-step accessor.
- Modify `Assets/Scripts/Game/Quests/QuestChainTransitionBase.cs`: add `TryResolveNextStep(QuestChainDefinition, QuestChainProgress, in QuestRuntimeContext, out string nextStepId)` while retaining legacy `ResolveNextStepId`.
- Add `Assets/Scripts/Game/Quests/ConditionalQuestChainTransition.cs`: `_targetStep`, `_completeChain`, `_conditions`; first satisfied strategy wins; successful empty target means explicit terminal only.
- Add `Assets/Scripts/Game/Quests/QuestRewardBatch.cs`: validate every reward and aggregate item grants before mutation; commit state inside coordinated Inventory transaction before notification; use in-flight guard for callbacks.
- Modify `QuestRewardBase.cs`/`Rewards/ItemQuestReward.cs`: optional pure item-grant extraction contract, without changing existing rewards' Apply API.
- UI targets `UI/Quests/QuestChainRuntimeInstaller.cs`, `QuestChainLogPanel.cs`, `UI/Objectives/Sources/QuestChainObjectiveSource.cs`: context-aware dispatch and current-step display. Stage full snapshots separately; root applies only intended regions to preserve concurrent work.
- Shared `Inventory.TryExchange(...)` API is owned by adult worker and must be confirmed before production staging; root applies inventory edits. No separate inventory implementation in this worker.

Exact new log APIs: `bool AcceptWithContext(ScriptableObject chain, QuestRuntimeContext context)`; `void RecordEventWithContext(QuestEvent event, QuestRuntimeContext context, RewardRuntimeContext rewards)`; `bool EvaluateStepProgress(ScriptableObject chain, QuestRuntimeContext context, RewardRuntimeContext rewards)`; `QuestStepDefinition GetCurrentStep(ScriptableObject chain)`.

Rules: a step with no authored transitions advances to the next array step, ending only at last; a step with authored transitions waits if none matches; first matching transition chooses target or explicit terminal; invalid/unregistered/already-completed target cannot advance or grant. Block and completion conditions are re-evaluated with context. Objective credit remains when a reward is temporarily unavailable; no completed-step/reward flags or target change occurs until all rewards can commit. Duplicate event key cannot advance the next step after reload. Chain completion rewards remain explicit claims. Empty-objective nodes do not automatically recurse; evaluation processes at most one node to prevent unbounded loops.

## Task 1 — Stage and observe RED

- [x] Stage `QuestChainBranchingTests.cs` covering sequential two-step progress; old single-step behavior; duplicate event after transition/reload; ordered conditional branch; unmet completion/start/block conditions; invalid/cyclic destination; cumulative item grants exceeding one free slot; retry after capacity frees; step and chain reward idempotency; save/load current node/history; current-step public accessor.
- [x] Root imports only the test via Unity script-update-or-create and runs `Rootborn.Tests.EditMode.StudentLife.QuestChainBranchingTests` using Unity test runner. Expected failures: first step incorrectly completes whole chain and new context/transition API missing. Compile errors are not accepted RED.
- [x] Worker receives exact failure summary before writing production code.

## Task 2 — Minimal engine and transaction integration

- [ ] Stage engine/strategy/reward files after RED. Preserve existing API names and signatures. Implement pending-completion retry and state commit once.
- [ ] Root imports staged production files through Unity MCP plus coordinated inventory method. Run new suite and `QuestChainStateTests`, `QuestRewardTests`, `QuestChainRegistryAssetTests`, `CareerInterestQuestLinkPersistenceTests`, `QuestChainDailyPlanRouteRewardAssetWiringTests`.
- [ ] Fix failures without weakening assertions; record results in this directory.

## Task 3 — Runtime and visible current-step integration

- [ ] Stage UI tests before UI patches, asserting Journal/panel reports current step after first objective rather than first serialized step.
- [ ] Root runs UI RED, then imports three UI changes, then runs relevant Journal/quest panel tests and `CareerInterestQuestChainStateE2EScenarioTests`.
- [ ] Root verifies a temporary test-data two-step branch through normal Town interaction and Journal UI, screenshots current-step/blocked/reward state, reloads and repeats event to prove no duplicate reward. No production story-canon data is added by this worker.

## Validation boundaries

Do not claim content-complete main story from an engine fix. Existing tests that force state or call internal methods cannot substitute for actual player flow under AGENTS.md. Performance: bounded one-node evaluation; cached chain lookup; no new Update polling; aggregate inventory preflight bounded by 32 slots and reward count. No commits or Assets disk writes from this worker.

RED evidence reported by root: chain-red.json 1 passed / 14 failed; expected sequential completion, aggregate reward and missing-API failures. chain-ui-red.json: 2 failed, first.objective rendered instead of second.objective. Production staging begins only after this report.
