# Goal Prompt Sequence: Recommended Next Feature Work After Relationship Milestone

Date: 2026-05-20

Use these prompts in order. This sequence assumes the current branch already includes:

- Addressables `link.xml` preservation guard
- Town Help relationship outcome wiring
- First Guide relationship milestone slice
- completed milestone route recommendation filtering
- post-relationship feature inventory refresh

Recommended order:

1. Career Practice Unlocks A Daily Plan Route
2. Quest Chain Rewards A Daily Plan Route
3. Host-Mode Route And Relationship Save Smoke
4. Quest Chain Rewards An Interior Unlock
5. Second NPC Relationship Route

Do not start the next prompt until the previous prompt is implemented, verified, and committed. If any PlayMode player-facing verification is blocked, stop and report the blocker before continuing.

---

## Prompt 1: Career Practice Unlocks A Daily Plan Route

Paste this prompt into a goal session and execute it as written.

### Objective

Add one small data-driven career practice slice where performing a visible career practice unlocks or recommends a Daily Plan route and surfaces the next action through the existing Objective Journal contract.

This is first because Discovery has already proven route unlocks twice, relationship has one milestone slice, and career data is mature enough to become the next non-discovery Daily Plan source.

### Non-Negotiable Constraints

- All career, practice, route, activity, Journal, and unlock behavior must be ScriptableObject-driven.
- Do not branch production C# on career IDs, practice IDs, route IDs, activity IDs, location IDs, quest IDs, NPC IDs, crop IDs, or tool IDs.
- Do not create a career-specific HUD, route hint HUD, permanent Canvas, or goal-like UI outside `ObjectiveJournalPanel`.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for Unity C# files.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Save/load persistence must be verified after reload.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create the smallest vertical slice that connects existing career practice to Daily Plan routing:

- one career-hint or career-practice route requirement expressed as a reusable ScriptableObject requirement
- one Daily Plan route asset unlocked or recommended by that requirement
- one existing or new route activity connection, only if the route model already supports it cleanly
- one Objective Journal hint or goal path through existing source contracts
- registry/default route-set wiring for the new data
- save/load behavior proving the career hint/practice state and route availability restore without duplication

If route completion through an activity is broader than one safe cycle, scope this prompt to route availability, route selection, Journal visibility, and reload persistence. Defer route completion to a later goal.

### Required TDD

Add failing tests first:

- career-hint route requirement evaluates false before the career hint/practice state exists
- the same requirement evaluates true after the career hint/practice state is unlocked
- the new route asset is registered and participates through the default route set
- Objective Journal source emits the route as a hint or goal according to the existing route source contract
- save/load restores the unlock state and does not duplicate route recommendations

Expected RED:

- tests fail because the career-based Daily Plan route requirement or route data does not exist yet.

Expected GREEN:

- the route becomes available from career practice state without any entity-ID production branching.

### Required PlayMode Scenario

Create or extend a strict player-facing PlayMode test:

```text
CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual keyboard movement or visible UI flow to select or expose the relevant career interest/practice state.
4. Use actual interact key at the Daily Plan board and verify the career route is locked, absent, or not selectable before the career condition is met.
5. Perform the visible career practice or mentor/work action through player input.
6. Verify runtime state on career hint/practice progress and Daily Plan route availability.
7. Open Objective Journal with Tab and verify the career route next action appears through Hints or Goals.
8. Track it if applicable and verify exactly one `TrackedObjectiveHud`.
9. Capture screenshot:

```text
Builds/Logs/career-daily-plan-route-e2e.png
```

10. Reload the same save slot through the player-facing SaveSlot UI.
11. Verify career state, route availability, selected route if applicable, Journal state, and tracked HUD state restore without duplicates.

Forbidden shortcuts:

- direct route availability mutation as the only proof
- direct scene loading as the only proof
- direct Objective Journal source calls as the only PlayMode proof
- JSON-only save verification without player-facing reload

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerDailyPlanRouteE2EScenarioTests.CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload
Get-ChildItem Builds/Logs/career-daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One career practice or career hint unlocks one Daily Plan route through data.
- Objective Journal owns the next-action guidance.
- The player can observe the route unlock through actual Town input/UI flow.
- Save/load restores career state and route state without duplication.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][ASSET][TEST] Career practice Daily Plan route 연결
```

---

## Prompt 2: Quest Chain Rewards A Daily Plan Route

Paste this prompt into a goal session only after Prompt 1 is committed.

### Objective

Add one data-driven quest-chain completion reward that unlocks a Daily Plan route and is visible through Objective Journal and the Daily Plan board.

This is second because quest reward semantics need stricter transaction and idempotency handling than the career practice route slice.

### Non-Negotiable Constraints

- All quest-chain, reward, route, activity, and Journal behavior must be ScriptableObject-driven.
- Do not branch production C# on quest IDs, chain IDs, reward IDs, route IDs, activity IDs, location IDs, NPC IDs, career IDs, crop IDs, or tool IDs.
- Reward application must be atomic and idempotent.
- Failed reward validation must not mutate inventory, unlock state, route state, or quest reward state.
- `ObjectiveJournalPanel` owns quest and route guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools.
- Every behavior change follows TDD.
- Visible and interactive changes require direct PlayMode player-flow verification and Game View or Camera screenshot evidence.
- Keep unrelated dirty or untracked files untouched and unstaged.

### Required Work

Create one quest-chain reward route slice:

- one quest-chain completion effect or reward strategy that unlocks route availability
- one Daily Plan route asset gated by the quest reward state
- one Objective Journal hint/goal path through existing quest and route sources
- save/load handling that keeps quest completion, reward claim, and route unlock idempotent

### Required TDD

Add failing tests first:

- route reward is unavailable before quest-chain completion
- quest-chain completion validates and applies the route reward exactly once
- repeated claim or reload does not duplicate route unlock or reward state
- Objective Journal reflects the quest and route state through stable keys/categories
- save/load restores quest-chain completion and route availability once

Expected RED:

- tests fail because quest-chain reward-to-route data or behavior is missing.

Expected GREEN:

- completing the quest-chain unlocks one Daily Plan route exactly once.

### Required PlayMode Scenario

Create or extend:

```text
QUEST_ROUTE_REWARD_E2E_001_CompleteChainUnlockRouteReload
```

The scenario must:

1. Start from MainMenu SaveSlot flow.
2. Enter Town.
3. Use actual keyboard movement to reach the named NPC, quest marker, or interaction source.
4. Use actual interact key and visible UI buttons to accept and complete the quest-chain step.
5. Verify visible quest or Objective Journal state in Game View.
6. Verify runtime quest-chain completion, reward claim, and route availability.
7. Open the Daily Plan board and verify the rewarded route is visible/selectable.
8. Capture screenshot:

```text
Builds/Logs/quest-route-reward-e2e.png
```

9. Reload the same save slot through SaveSlot UI.
10. Verify quest state, reward claim, route availability, Journal state, and tracked HUD state restore without duplicates.

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
- Reward application is atomic and idempotent.
- The player sees the unlocked route through actual UI flow.
- Objective Journal owns the guidance.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

### Commit

Use:

```text
[FEATURE][TEST] Quest chain Daily Plan route 보상 연결
```

---

## Prompt 3: Host-Mode Route And Relationship Save Smoke

Paste this prompt into a goal session only after Prompts 1 and 2 are committed.

### Objective

Add one host-mode PlayMode smoke scenario that verifies recent route and relationship progress survives save/load without duplication under the host flow.

This is third because it protects recent feature slices before moving into higher-risk visible housing/interior work.

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

Add failing PlayMode coverage or a failing host-mode fixture test first:

- host flow can enter Town with a valid player identity
- route state and relationship/milestone state can be observed under host mode
- reload does not duplicate route, relationship, milestone, or tracked HUD state

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
- Save/load restores without duplicate route, relationship, milestone, or HUD state.
- Screenshot evidence exists and has non-zero length.
- Scope remains host-mode smoke, not a full dedicated-server campaign.

### Commit

Use:

```text
[NETWORK][TEST] Host route relationship save smoke 추가
```

---

## Prompt 4: Quest Chain Rewards An Interior Unlock

Paste this prompt into a goal session only after Prompt 3 is committed.

### Objective

Add one quest-chain reward that unlocks a tangible interior or furniture placement option, then verify the unlock and placement through actual player-facing House or Interior flow.

This is fourth because it has high player value but also higher visual, persistence, catalog, and placement verification cost.

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

## Prompt 5: Second NPC Relationship Route

Paste this prompt into a goal session only after Prompt 4 is committed, or earlier only if the user explicitly prioritizes relationship content over housing/interior rewards.

### Objective

Add a second data-driven NPC relationship route so relationship progress starts to feel like a repeatable Town system instead of a single First Guide sample.

This is fifth because it is valuable, but the project first benefits from proving another Daily Plan source and one reward-to-home integration.

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
[FEATURE][ASSET][TEST] 두 번째 NPC 관계 route 추가
```

