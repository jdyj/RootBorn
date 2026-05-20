# Recommended Goal Prompts: Sequential Feature Order

Date: 2026-05-20

이 문서는 현재 브랜치 진행 상태를 기준으로 다음 기능 개발 goal 프롬프트를 추천 순서대로 정리한다.

이미 완료된 전제:

- Career practice Daily Plan route 연결
- Quest chain Daily Plan route reward 연결
- Host route + relationship save smoke

현재 권장 순서:

1. Quest Chain Rewards An Interior Unlock
2. Second NPC Relationship Route
3. Daily Plan Route Completion And Day Result Feedback
4. Dedicated Server Route/Relationship Persistence Smoke
5. Objective Journal Route Source Regression Hardening

각 goal은 이전 goal이 구현, 테스트, PlayMode 시각 검증, 저장/로드 검증, 커밋까지 끝난 뒤 순차적으로 실행한다.

공통 절대 조건:

- `Assets/**/*.cs`는 shell 직접 쓰기 금지. Unity MCP `script-update-or-create`를 사용한다.
- 신규/변경 동작은 실패 테스트를 먼저 작성한다.
- visible/UI/interaction/scene/placement 변경은 실제 PlayMode 플레이어 흐름으로 검증한다.
- Game View 또는 Camera screenshot을 남긴다.
- 저장/로드가 관련되면 player-facing reload 후 상태를 재검증한다.
- `ObjectiveJournalPanel`이 목표성 UI를 소유한다.
- 상시 HUD는 `TrackedObjectiveHud` 하나만 허용하고 추적 목표 1개만 표시한다.
- production C#에서 quest, route, NPC, furniture, activity, location, reward, career, crop, tool ID별 분기 금지.
- `Assets/AddressableAssetsData/link.xml` 또는 `.meta`가 Unity tooling 중 삭제되면 즉시 복구한다.
- 관련 없는 dirty/untracked 파일은 건드리거나 stage하지 않는다.

---

## Prompt 1: Quest Chain Rewards An Interior Unlock

Paste this prompt into a new goal session and execute it as written.

### Objective

Add one data-driven quest-chain reward that unlocks one furniture palette option, then verify the unlocked furniture can be selected, placed visibly in the House scene, saved, and restored without duplicate reward or placement state.

### Why First

Quest route reward와 host save smoke가 끝났으므로, 다음으로 가장 가치가 큰 연결은 Town quest reward가 House/interior의 눈에 보이는 보상으로 이어지는 흐름이다.

### Required Work

- Add furniture unlock progress and persistence.
- Add one quest reward strategy that grants a furniture unlock token.
- Gate one furniture catalog entry by that unlock token.
- Wire the reward asset into an existing test quest chain.
- Filter locked furniture from the placement palette before reward claim.
- Show/select/place the unlocked furniture after reward claim.
- Save and reload quest state, reward claim state, furniture unlock state, and placed furniture.
- Keep reward claim and unlock application idempotent.

### Required RED Tests

- `FurnitureUnlockQuestReward` cannot apply without unlock progress.
- The reward unlocks its token exactly once.
- The registered quest-chain completion rewards include the furniture unlock reward.
- The selected furniture is hidden from `InteriorFurnitureCatalog` before unlock.
- The selected furniture appears after unlock.
- Unlock progress persists per save slot/player.

### Required PlayMode Scenario

Create or extend:

```text
QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Complete and claim the quest-chain reward through actual movement, interact key, and visible UI buttons.
4. Verify visible quest or Objective Journal state in Game View.
5. Verify furniture unlock token runtime/save state.
6. Transition to House through the project-supported player-facing path.
7. Open placement UI through actual input/UI flow.
8. Verify the furniture is unavailable before reward and available after reward.
9. Select the unlocked furniture.
10. Place it visibly.
11. Inspect runtime selected furniture asset name and placement position.
12. Capture screenshot:

```text
Builds/Logs/quest-interior-reward-e2e.png
```

13. Save furniture layout.
14. Reload the same save slot.
15. Verify duplicate reward claim is blocked and placed furniture persists.
16. Inspect serialized unlock and furniture layout save files.

### Required Verification

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

- One quest-chain reward unlocks one furniture option.
- Locked furniture is absent before reward and visible after reward.
- The player can place the unlocked item through actual UI flow.
- Reward, unlock, and placement persistence are idempotent.
- Screenshot evidence exists and has non-zero length.

### Commit

```text
[FEATURE][ASSET][TEST] Quest interior unlock 보상 연결
```

---

## Prompt 2: Second NPC Relationship Route

Paste this prompt only after Prompt 1 is committed.

### Objective

Add a second data-driven NPC relationship route so Town social progression becomes a repeatable system instead of a single First Guide sample.

### Why Second

Interior reward가 Town-to-House reward loop를 증명한 뒤, 관계 시스템은 두 번째 NPC를 통해 재사용 가능한 데이터 구조인지 검증해야 한다.

### Required Work

- Add or reuse one registered NPC definition.
- Add schedule/routine data that makes the NPC reachable in Town.
- Add one relationship progress or milestone definition.
- Add one data-driven dialogue/help/action outcome that advances the milestone.
- Surface the next action through Objective Journal Hints or Goals.
- Save/load relationship milestone state without duplicate relationship deltas.

### Required RED Tests

- The NPC routine makes the NPC reachable in the expected Town context.
- The relationship action advances the milestone exactly once.
- Repeated action or reload does not double-apply relationship progress.
- Objective Journal emits the relationship next action from data.
- The hint disappears or changes after milestone resolution.
- Save/load restores milestone and Journal state without duplication.

### Required PlayMode Scenario

Create or extend:

```text
NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use keyboard movement to reach the second NPC at the scheduled location.
4. Press interact.
5. Click the visible dialogue/help/action button.
6. Verify visible dialogue/result text in Game View.
7. Verify relationship milestone runtime state.
8. Open Objective Journal with Tab.
9. Verify the relationship next action appears in Hints or Goals.
10. Track it if applicable and verify exactly one `TrackedObjectiveHud`.
11. Capture screenshot:

```text
Builds/Logs/npc-relationship-second-slice-e2e.png
```

12. Reload the same save slot through SaveSlot UI.
13. Verify milestone, Journal state, and tracked HUD state restore without duplicates.

### Required Verification

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
- The player can complete it through actual Town input/UI flow.
- Objective Journal owns relationship next-action guidance.
- Save/load restores milestone and Journal state without duplicate relationship progress.

### Commit

```text
[FEATURE][ASSET][TEST] 두 번째 NPC 관계 route 추가
```

---

## Prompt 3: Daily Plan Route Completion And Day Result Feedback

Paste this prompt only after Prompt 2 is committed.

### Objective

Connect one selected Daily Plan route to a completion/result feedback loop so route selection becomes part of the daily gameplay loop.

### Why Third

Route sources가 career, quest reward, relationship으로 넓어진 뒤에는 route selection 이후 완료와 결과 피드백을 안정화해야 한다.

### Required Work

- Select one available Daily Plan route through the existing board.
- Complete the route through an existing player-facing activity or interaction.
- Persist route completion/result state.
- Move the route out of active Journal Goals or update it to the next state.
- Clear or update tracked HUD without duplicates.
- Show completion/result feedback through an existing result surface when available.
- Reload and verify completion does not re-award or re-complete.

### Required RED Tests

- Selected route is active before completion.
- Completing the configured activity records completion once.
- Completed route is filtered from active recommendations/goals.
- Repeated completion and reload do not duplicate rewards, outcomes, Journal entries, or HUD entries.
- Result summary reflects the completed route through existing source contracts.

### Required PlayMode Scenario

Create or extend:

```text
DAILY_PLAN_ROUTE_E2E_002_SelectCompleteRouteResultReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Open Daily Plan board and select one available route.
4. Open Objective Journal with Tab and verify the route is in Goals.
5. Track it and verify exactly one `TrackedObjectiveHud`.
6. Use movement, interact key, and visible UI to complete the configured route activity.
7. Verify visible completion/result feedback in Game View.
8. Verify runtime route completion, Journal state, and HUD state.
9. Capture screenshot:

```text
Builds/Logs/daily-plan-route-completion-e2e.png
```

10. Reload the same save slot through SaveSlot UI.
11. Verify route completion, result state, Journal filtering, and HUD state restore without duplicates.

### Required Verification

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

### Commit

```text
[FEATURE][TEST] Daily Plan route 완료 피드백 연결
```

---

## Prompt 4: Dedicated Server Route/Relationship Persistence Smoke

Paste this prompt only after Prompt 3 is committed.

### Objective

Add one dedicated-server smoke scenario that verifies route, relationship, and save slot state survive server/client flow without host-only assumptions.

### Why Fourth

Host smoke는 끝났지만 dedicated server는 실행 인자, player identity, save slot, persistence path가 다르다. 코어 loop가 더 커지기 전에 최소 회귀망을 추가해야 한다.

### Required Work

- Start or simulate the dedicated server path through existing network test infrastructure.
- Connect a client with explicit `saveSlot`, `port`, and player identity.
- Advance one already-existing route or relationship state through player-facing flow where possible.
- Persist and reload the state.
- Verify no host-only fallback identity or local-only save path is used.

### Required RED Tests

- Dedicated server flow has a valid player identity distinct from local single-player fallback.
- Route/relationship progress saves under the expected slot/player path.
- Reload restores state once.
- Duplicate network objects, stale HUD entries, or duplicate Journal entries are not produced.

### Required PlayMode Scenario

Create or extend:

```text
NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload
```

The scenario must:

1. Start server/client or the closest current automated dedicated-server harness.
2. Enter Town as a client.
3. Advance one route or relationship state through player-facing input/UI if the harness supports it.
4. Verify visible Game View state on the client.
5. Verify server/client player identity and save slot state.
6. Capture screenshot:

```text
Builds/Logs/network-dedicated-route-relationship-e2e.png
```

7. Restart/reload through the supported server/client path.
8. Verify state restores without duplication.

If true dedicated-server process orchestration is blocked, stop and report the blocker. Do not claim host-mode coverage as dedicated-server coverage.

### Required Verification

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.NetworkDedicatedPersistenceE2EScenarioTests.NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated server path has a persistence smoke test distinct from host mode.
- Save slot and player identity are verified.
- Runtime state, visible state, and reload state agree.

### Commit

```text
[NETWORK][TEST] Dedicated server route relationship 저장 smoke 추가
```

---

## Prompt 5: Objective Journal Route Source Regression Hardening

Paste this prompt only after Prompt 4 is committed.

### Objective

Harden Objective Journal route/source regression coverage so career, quest reward, relationship, completion, and network save slices cannot regress silently.

### Why Fifth

여러 source가 Journal로 모인 뒤에는 새 기능보다 회귀망 강화가 우선이다. 이 goal은 기능 확장이 아니라 기반 안정화다.

### Required Work

- Add a compact regression matrix for Journal source ownership.
- Verify route hints/goals from career, quest reward, relationship, and completed route states.
- Verify tracked HUD uniqueness across source changes.
- Verify reload does not duplicate Journal rows.
- Ensure legacy HUD or sample/debug objective panels do not reappear.

### Required RED Tests

- Multiple source types can be loaded into Journal without duplicate keys.
- Completed/invalidated source rows disappear or change category correctly.
- `TrackedObjectiveHud` count remains exactly one.
- Legacy HUD classes/prefabs are absent or disabled according to the migration rule.
- Reload preserves selected/tracked state without duplicate rows.

### Required PlayMode Scenario

Create or extend:

```text
OBJECTIVE_JOURNAL_REGRESSION_PM_001_MultiSourceReloadNoDuplicateHud
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Produce at least two different Journal source states through player-facing flow or existing validated fixtures.
4. Open Objective Journal with Tab.
5. Verify row categories, stable keys, and absence of legacy goal HUDs.
6. Track one goal and verify exactly one `TrackedObjectiveHud`.
7. Capture screenshot:

```text
Builds/Logs/objective-journal-regression-e2e.png
```

8. Reload the same save slot.
9. Verify no duplicate Journal rows or HUDs appear.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalRegressionE2EScenarioTests.OBJECTIVE_JOURNAL_REGRESSION_PM_001_MultiSourceReloadNoDuplicateHud
Get-ChildItem Builds/Logs/objective-journal-regression-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Objective Journal handles multiple source types without duplicate rows.
- Legacy goal HUD ownership does not regress.
- Tracked HUD uniqueness is enforced.
- Reload preserves Journal state without duplicate UI.

### Commit

```text
[TEST][REFACTOR] Objective Journal source 회귀망 강화
```
