# Goal Prompt Sequence: Recommended Current ROOTBORN Feature Order

Date: 2026-05-20

이 문서는 현재 브랜치의 진행 상태를 기준으로 다음 goal 세션들을 추천 순서대로 실행하기 위한 프롬프트 모음이다.

현재 기준:

- `Career practice Daily Plan route`는 이미 구현/검증/커밋 완료 상태다.
- 다음 시작점은 `Quest Chain Rewards A Daily Plan Route`다.
- 각 goal은 이전 goal이 구현, PlayMode 검증, 스크린샷 확인, 정적 검사, 커밋까지 끝난 뒤에만 다음으로 넘어간다.
- 구현 중 `Assets/AddressableAssetsData/link.xml` 또는 `.meta`가 삭제되면 즉시 복구하고 원인을 기록한다.

추천 순서:

1. Quest Chain Rewards A Daily Plan Route
2. Host-Mode Route And Relationship Save Smoke
3. Quest Chain Rewards An Interior Unlock
4. Second NPC Relationship Route
5. Daily Plan Route Completion And Day Result Feedback

---

## Prompt 1: Quest Chain Rewards A Daily Plan Route

Paste this prompt into a goal session and execute it as written.

### Objective

Add one data-driven quest-chain completion reward that unlocks a Daily Plan route and is visible through the Daily Plan board, Objective Journal, and tracked objective flow.

This is first because career-practice route wiring is already complete, and quest rewards are the next highest-value route source that needs transaction and persistence proof.

### Non-Negotiable Constraints

- All quest-chain, reward, world-state, route, activity, and Journal behavior must be ScriptableObject-driven.
- Do not branch production C# on quest IDs, chain IDs, reward IDs, world-state IDs, route IDs, activity IDs, location IDs, NPC IDs, career IDs, crop IDs, or tool IDs.
- Reward application must be atomic and idempotent.
- Failed reward validation must not mutate inventory, unlock state, route state, world-state progress, or quest reward state.
- `ObjectiveJournalPanel` owns quest and route guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for Unity C# files.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Save/load persistence must be verified after player-facing reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create one quest-chain reward route slice:

- one quest-chain completion reward strategy or existing reward wiring that activates a world-state flag
- one Daily Plan route asset gated by the same world-state flag
- one route requirement asset that evaluates false before reward claim and true after reward claim
- registry/default route-set wiring for the new data
- quest-chain runtime reward context wiring so UI reward claim can apply and persist world-state progress
- Objective Journal hint/goal path through existing route and quest sources
- save/load handling that keeps quest completion, reward claim, world-state flag, and route availability idempotent

### Required TDD

Add failing tests first:

- quest-chain runtime reward context provides `WorldStateProgress`
- reward context saves world-state progress after reward claim
- registered quest-chain completion reward references the same world-state flag used by the Daily Plan route
- route is unavailable before reward claim
- route becomes available after reward claim
- Objective Journal exposes the unlocked unselected route as a hint, then selected route as a goal
- repeated claim or reload does not duplicate reward, world-state, route, Journal, or HUD state

Expected RED:

- tests fail because quest reward-to-route data or world-state reward context wiring is missing.

Expected GREEN:

- completing and claiming the quest-chain reward unlocks one Daily Plan route exactly once without production entity-ID branching.

### Required PlayMode Scenario

Create or extend:

```text
QUEST_ROUTE_REWARD_E2E_001_CompleteChainUnlockRouteReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual keyboard movement to reach the quest-chain interaction source.
4. Use actual interact key and visible UI buttons to accept, complete, and claim the quest-chain reward.
5. Verify visible quest or Objective Journal state in Game View.
6. Verify runtime quest-chain completion, reward claim, world-state flag, and route availability.
7. Open the Daily Plan board and verify the rewarded route is visible/selectable.
8. Open Objective Journal with Tab and verify the route appears in the correct Hints/Goals category.
9. Track it if applicable and verify exactly one `TrackedObjectiveHud`.
10. Capture screenshot:

```text
Builds/Logs/quest-route-reward-e2e.png
```

11. Reload the same save slot through SaveSlot UI.
12. Verify quest state, reward claim, world-state flag, route availability, Journal state, and tracked HUD state restore without duplicates.

Forbidden shortcuts:

- direct reward or route mutation as the only proof
- direct scene loading as the only proof
- direct Objective Journal source calls as the only PlayMode proof
- JSON-only save verification without player-facing reload

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestRouteRewardE2EScenarioTests.QUEST_ROUTE_REWARD_E2E_001_CompleteChainUnlockRouteReload
Get-ChildItem Builds/Logs/quest-route-reward-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One quest-chain reward unlocks one Daily Plan route.
- Reward application and world-state persistence are atomic and idempotent.
- The player sees and selects the unlocked route through actual UI flow.
- Objective Journal owns all goal-like guidance.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][TEST] Quest chain Daily Plan route 보상 연결
```

---

## Prompt 2: Host-Mode Route And Relationship Save Smoke

Paste this prompt into a goal session only after Prompt 1 is committed.

### Objective

Add one host-mode PlayMode smoke scenario that verifies recent route and relationship progress survives save/load without duplication under the host flow.

This is second because it protects the recent Daily Plan route and relationship milestone slices before moving into higher-risk interior reward work.

### Non-Negotiable Constraints

- This is a network/host-mode validation slice, not a broad dedicated-server campaign.
- Do not introduce new gameplay data unless a missing test fixture is unavoidable.
- Do not replace real player-facing movement, interaction, UI selection, or reload with internal method calls as the only proof.
- `ObjectiveJournalPanel` owns goal-like guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create a small host-mode smoke test using existing route and relationship data:

- host session starts through the player-facing flow available in the project
- one existing route unlock or route selection is performed
- one existing First Guide relationship milestone state is advanced or verified
- Objective Journal and tracked HUD state are inspected
- save/load or host reload path restores state without duplication

Do not expand this prompt into dedicated server process orchestration unless current test infrastructure already makes that trivial.

### Required TDD

Add a failing PlayMode host smoke test first:

- host flow can enter Town with a valid player identity
- route state and relationship/milestone state can be observed under host mode
- reload does not duplicate route, relationship, milestone, Journal, or tracked HUD state

Expected RED:

- the host smoke test is missing or exposes a host save/load gap.

Expected GREEN:

- recent route and relationship slices survive host-mode save/reload.

### Required PlayMode Scenario

Create or extend:

```text
NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload
```

The scenario must:

1. Start from the host-capable player-facing flow.
2. Enter Town.
3. Use actual keyboard movement to reach the route or relationship interaction source.
4. Use actual interact key or visible UI buttons to advance state.
5. Verify visible UI text/state in Game View.
6. Verify runtime network player identity, route state, relationship progress, milestone state, Journal state, and tracked HUD state.
7. Capture screenshot:

```text
Builds/Logs/network-route-relationship-host-e2e.png
```

8. Reload through the project-supported host/save slot path.
9. Verify state restores without duplication.

### Required Verification

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.NetworkRouteRelationshipHostE2EScenarioTests.NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload
Get-ChildItem Builds/Logs/network-route-relationship-host-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Host-mode route and relationship progress are covered by one strict smoke flow.
- Runtime state and visible Game View state are both verified.
- Save/load restores without duplicate route, relationship, milestone, Journal, or HUD state.
- Screenshot evidence exists and has non-zero length.
- Scope remains host-mode smoke, not a full dedicated-server campaign.

### Commit

Use:

```text
[NETWORK][TEST] Host route relationship save smoke 추가
```

---

## Prompt 3: Quest Chain Rewards An Interior Unlock

Paste this prompt into a goal session only after Prompt 2 is committed.

### Objective

Add one quest-chain reward that unlocks a tangible interior or furniture placement option, then verify the unlock and placement through actual player-facing House or Interior flow.

This is third because it has high player value but also higher visual, persistence, catalog, and placement verification cost.

### Non-Negotiable Constraints

- All quest, reward, unlock, furniture, placement, and Journal behavior must be data-driven.
- Do not branch production C# on quest IDs, reward IDs, furniture IDs, room IDs, route IDs, location IDs, NPC IDs, crop IDs, or tool IDs.
- Reward application must be atomic and idempotent.
- Visible placement work must verify both runtime Game View and saved state.
- If tilemap/sprite/prefab/scene state should persist, inspect saved EditMode asset or scene state separately.
- `ObjectiveJournalPanel` owns quest guidance; do not create new quest HUDs.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Every behavior change follows TDD.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create one quest-to-interior reward slice:

- one quest-chain completion or claim action grants one furniture/interior unlock
- one existing or new furniture catalog entry becomes available from that unlock
- placement UI can place the unlocked item through player-facing controls
- save/load restores quest state, reward claim, unlock state, and placed furniture
- duplicate claim does not duplicate inventory, unlocks, or placed items

### Required TDD

Add failing tests first:

- locked furniture is unavailable before reward claim
- quest-chain reward validates capacity/duplication before mutating state
- reward claim unlocks the furniture exactly once
- repeated claim and reload do not duplicate the unlock
- placed furniture persists after save/load

Expected RED:

- reward-to-interior unlock path is missing or non-idempotent.

Expected GREEN:

- one quest reward unlocks one interior option and the placement save path is stable.

### Required PlayMode Scenario

Create or extend:

```text
QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town and complete or claim the quest-chain reward through actual movement/interact/UI.
3. Verify visible quest or Objective Journal state in Game View.
4. Transition to House or Interior through the player-facing path.
5. Open placement UI through the actual input/UI flow.
6. Select the unlocked furniture/interior option.
7. Place it visibly.
8. Inspect runtime catalog/unlock/placement state, including selected asset name and placement position.
9. Capture screenshot:

```text
Builds/Logs/quest-interior-reward-e2e.png
```

10. Save and reload through SaveSlot UI.
11. Verify quest reward cannot be claimed twice and the placed item persists.
12. Inspect saved scene, prefab, or serialized save state as appropriate for the project storage path.

### Required Verification

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

### Acceptance Criteria

- One quest-chain reward unlocks one interior/furniture option.
- The unlock and placement are visible through actual player flow.
- Reward claim and placement persistence are idempotent.
- Runtime and saved-state evidence are both checked.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][ASSET][TEST] Quest interior unlock 보상 연결
```

---

## Prompt 4: Second NPC Relationship Route

Paste this prompt into a goal session only after Prompt 3 is committed, or earlier only if the user explicitly prioritizes relationship content over housing/interior rewards.

### Objective

Add a second data-driven NPC relationship route so relationship progress starts to feel like a repeatable Town system instead of a single First Guide sample.

This is fourth because it is valuable, but the project first benefits from proving another route source, host save smoke, and one reward-to-home integration.

### Non-Negotiable Constraints

- All NPC, relationship, schedule, dialogue/help, milestone, and hint behavior must be ScriptableObject-driven.
- Do not branch production C# on NPC IDs, relationship IDs, schedule IDs, dialogue IDs, route IDs, quest IDs, location IDs, crop IDs, or tool IDs.
- Do not create a relationship HUD, NPC hint HUD, floating objective panel, or permanent Canvas.
- `ObjectiveJournalPanel` owns relationship next-action guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Save/load persistence must be verified after reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create one second relationship route:

- one existing or new registered NPC definition suitable for Town
- one schedule/routine state that makes the NPC reachable through a real Town player flow
- one relationship progress or milestone definition
- one data-driven dialogue/help/action outcome that advances the relationship milestone
- one Objective Journal Hint or Goal item through existing milestone/source contracts
- save/load support using the existing save path, without duplicate relationship delta application

Prefer existing NPC, location, schedule, and dialogue infrastructure. Add new assets only when current data cannot express the slice.

### Required TDD

Add failing tests first:

- the NPC routine makes the selected NPC reachable in the expected Town context
- the relationship action advances the milestone exactly once
- repeated action or reload does not double-apply the relationship delta
- Objective Journal emits the relationship next action from data
- the relationship hint disappears or changes after the milestone is resolved
- save/load restores relationship milestone and hint state without duplication

Expected RED:

- tests fail because the second relationship route data path does not exist yet.

Expected GREEN:

- NPC schedule, relationship, Objective Journal, and save/load tests pass for the second slice.

### Required PlayMode Scenario

Create or extend:

```text
NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual keyboard movement to reach the second NPC at the scheduled location.
4. Press the interact key.
5. Click the visible dialogue/help/action button.
6. Verify visible dialogue/result text in Game View.
7. Verify relationship milestone runtime state.
8. Open Objective Journal with Tab.
9. Verify the relationship next action appears in Hints or Goals according to the source contract.
10. Track it if applicable and verify exactly one `TrackedObjectiveHud`.
11. Capture screenshot:

```text
Builds/Logs/npc-relationship-second-slice-e2e.png
```

12. Reload the same save slot through the player-facing SaveSlot UI.
13. Verify NPC relationship milestone, Journal hint/goal state, and tracked HUD state restore without duplicates.

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
Get-ChildItem Builds/Logs/npc-relationship-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- A second data-driven NPC relationship route exists.
- The player can reach and complete it through actual Town input/UI flow.
- Objective Journal owns next-action guidance.
- Save/load restores milestone and Journal state without duplicate relationship progress.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][ASSET][TEST] 두번째 NPC 관계 route 추가
```

---

## Prompt 5: Daily Plan Route Completion And Day Result Feedback

Paste this prompt into a goal session only after Prompt 4 is committed, or earlier only if the user explicitly asks to prioritize Daily Plan feedback over new content.

### Objective

Connect one selected Daily Plan route to a clear completion/result feedback loop so route selection does not stop at recommendation and tracking.

This is fifth because the route sources should first be proven across career, quest, host save, interior reward, and relationship slices. After that, completion feedback becomes the next foundation for making routes feel like a real daily loop.

### Non-Negotiable Constraints

- All route, activity, outcome, reward, result, and Journal behavior must be data-driven.
- Do not branch production C# on route IDs, activity IDs, outcome IDs, quest IDs, NPC IDs, location IDs, career IDs, crop IDs, or tool IDs.
- Do not create a new permanent route result HUD.
- `ObjectiveJournalPanel` owns route guidance; day result UI may summarize completed outcomes only if it follows existing result UI ownership.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Save/load persistence must be verified after reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create the smallest completion loop:

- one selected Daily Plan route can be completed through an existing player-facing activity or interaction
- completion writes stable progress/result state
- Objective Journal moves the route out of active Goals or updates it to the next state
- tracked HUD clears or changes without duplication
- day result or summary UI reflects the completed route outcome if an existing result surface supports it
- save/load restores completed state without re-awarding or re-completing

### Required TDD

Add failing tests first:

- selected route is active before completion
- completing the route through its configured activity records completion once
- completed route is filtered from active recommendations/goals
- repeated completion and reload do not duplicate rewards, outcomes, or HUD entries
- result summary reflects the completed route using existing UI/source contracts

Expected RED:

- route completion/result state is missing or not connected to Journal/result UI.

Expected GREEN:

- one selected route completes through a data-driven activity and the result state persists once.

### Required PlayMode Scenario

Create or extend:

```text
DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Unlock or use an already-available Daily Plan route through player-facing flow.
4. Open the Daily Plan board and select the route.
5. Open Objective Journal with Tab and verify the route is in Goals.
6. Track it and verify exactly one `TrackedObjectiveHud`.
7. Use actual keyboard movement, interact key, and visible UI to complete the configured route activity.
8. Verify visible completion/result feedback in Game View.
9. Verify runtime route completion state, Journal state, and tracked HUD state.
10. Capture screenshot:

```text
Builds/Logs/daily-plan-route-completion-e2e.png
```

11. Reload the same save slot through SaveSlot UI.
12. Verify route completion, result state, Journal filtering, and tracked HUD state restore without duplicates.

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
Get-ChildItem Builds/Logs/daily-plan-route-completion-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One Daily Plan route can be selected and completed through actual player flow.
- Completion/result state is data-driven and persisted.
- Objective Journal and tracked HUD reflect completion without duplicates.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][TEST] Daily Plan route 완료 피드백 연결
```
