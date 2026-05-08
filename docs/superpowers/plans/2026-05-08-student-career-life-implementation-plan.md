# Student Career Life Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first student-life playable core: data-driven activities can spend personal resources, apply trait/skill/career effects, save/load per player, and broadcast server-authoritative activity results without cross-player contamination.

**Architecture:** Add a focused `Rootborn.Game.StudentLife` runtime domain for activity definitions, requirements, effects, player progress, and save data. Add a pure `Rootborn.Network.StudentLife` broadcaster modeled after the existing quest broadcaster so EditMode tests can verify server authority, duplicate suppression, and linear delivery counts before any scene work.

**Tech Stack:** Unity 6000.3 C#, ScriptableObject definitions, NUnit EditMode tests, Unity MCP `script-update-or-create`, existing `GameDataRegistry`, existing shell CI gate `Scripts/ci/check-no-entity-id-branching.sh`.

---

## File Structure

- Create `Assets/Scripts/Game/StudentLife/LifeActivityCategory.cs`: UI/content category enum, not a gameplay branch key.
- Create `Assets/Scripts/Game/StudentLife/LifeActivityResultKind.cs`: result states for activity execution.
- Create `Assets/Scripts/Game/StudentLife/StudentLifeProgress.cs`: player-specific energy/focus/stress/time, trait values, skill values, unlocked career hints, applied request ids, save/load.
- Create `Assets/Scripts/Game/StudentLife/StudentLifeProgressSaveData.cs`: serializable save DTO.
- Create `Assets/Scripts/Game/StudentLife/TraitDefinition.cs`: SO trait identity.
- Create `Assets/Scripts/Game/StudentLife/SkillDefinition.cs`: SO skill identity.
- Create `Assets/Scripts/Game/StudentLife/CareerDefinition.cs`: SO career hint identity and unlock requirements.
- Create `Assets/Scripts/Game/StudentLife/CareerUnlockRequirementBase.cs`: SO strategy base for career conditions.
- Create `Assets/Scripts/Game/StudentLife/TraitThresholdCareerRequirement.cs`: data-driven career requirement.
- Create `Assets/Scripts/Game/StudentLife/SkillThresholdCareerRequirement.cs`: data-driven career requirement.
- Create `Assets/Scripts/Game/StudentLife/LifeActivityRequirementBase.cs`: SO strategy base for activity preconditions.
- Create `Assets/Scripts/Game/StudentLife/TraitThresholdActivityRequirement.cs`: data-driven activity requirement.
- Create `Assets/Scripts/Game/StudentLife/SkillThresholdActivityRequirement.cs`: data-driven activity requirement.
- Create `Assets/Scripts/Game/StudentLife/LifeActivityEffectBase.cs`: SO strategy base for activity effects.
- Create `Assets/Scripts/Game/StudentLife/TraitDeltaActivityEffect.cs`: data-driven trait delta.
- Create `Assets/Scripts/Game/StudentLife/SkillProgressActivityEffect.cs`: data-driven skill delta.
- Create `Assets/Scripts/Game/StudentLife/CareerHintUnlockActivityEffect.cs`: data-driven career hint unlock.
- Create `Assets/Scripts/Game/StudentLife/LifeActivityDefinition.cs`: SO activity definition with base costs, requirements, and effects.
- Create `Assets/Scripts/Game/StudentLife/LifeActivityRunner.cs`: request-id based idempotent activity execution.
- Create `Assets/Scripts/Game/StudentLife/LifeActivityResult.cs`: immutable result DTO.
- Create `Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs`: pure broadcaster with server authority and duplicate suppression counters.
- Modify `Assets/Scripts/Game/Common/GameDataRegistry.cs`: expose arrays for life activities, traits, skills, and careers.
- Create `Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs`: LIFE-STUDENT-002 through LIFE-STUDENT-008 core tests.
- Create `Assets/Tests/EditMode/StudentLife/StudentLifeRegistryTests.cs`: LIFE-STUDENT-001 registry surface test.
- Create `Assets/Tests/EditMode/StudentLife/StudentLifeNetworkTests.cs`: LIFE-STUDENT-NET-001 through NET-011 pure network tests.

## Task 1: RED Tests

**Files:**
- Create: `Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs`
- Create: `Assets/Tests/EditMode/StudentLife/StudentLifeRegistryTests.cs`
- Create: `Assets/Tests/EditMode/StudentLife/StudentLifeNetworkTests.cs`

- [ ] **Step 1: Write failing tests**

Use Unity MCP `script-update-or-create` for all three test files. Tests should reference the desired API before production classes exist:

```csharp
var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
var runner = new LifeActivityRunner();
Assert.IsTrue(runner.TryPerform(activity, progress, "request-1", out var result));
```

- [ ] **Step 2: Verify RED**

Run: Unity MCP `tests-run` with `testMode=EditMode`, `testNamespace=Rootborn.Tests.EditMode.StudentLife`.

Expected: FAIL because `Rootborn.Game.StudentLife` and `Rootborn.Network.StudentLife` types do not exist yet.

## Task 2: Student Life Core

**Files:**
- Create runtime files under `Assets/Scripts/Game/StudentLife/`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`

- [ ] **Step 1: Implement minimal definitions and progress**

Use Unity MCP `script-update-or-create`. Define SO identities, progress dictionaries keyed by SO object/id, save DTO entries, and safe resource spending.

- [ ] **Step 2: Implement activity runner**

`LifeActivityRunner.TryPerform` must:

- reject null activity/progress/request id
- reject insufficient energy/focus
- reject failed `LifeActivityRequirementBase`
- not mutate on rejection
- suppress duplicate successful request ids
- spend costs, apply effects, record request id, and return success

- [ ] **Step 3: Run core EditMode tests**

Run: Unity MCP `tests-run` with `testMode=EditMode`, `testNamespace=Rootborn.Tests.EditMode.StudentLife`.

Expected: activity and registry tests pass; network tests may still fail until Task 3.

## Task 3: Student Life Network Broadcaster

**Files:**
- Create: `Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs`

- [ ] **Step 1: Implement pure broadcaster**

Model it after `QuestNetworkStateBroadcaster`:

- constructor accepts `bool isServerAuthority`, `string saveSlot`
- register/unregister clients
- `Publish(LifeActivityResult result)` ignores non-server, null/failed results
- duplicate key includes saveSlot, player id, request id, activity id, and result kind
- counters expose broadcast count, recipient delivery count, state change count, duplicate suppressed count

- [ ] **Step 2: Run network EditMode tests**

Run: Unity MCP `tests-run` with `testMode=EditMode`, `testNamespace=Rootborn.Tests.EditMode.StudentLife`.

Expected: all StudentLife EditMode tests pass.

## Task 4: Static Gate And Regression

**Files:**
- No new files unless tests reveal a narrow fix.

- [ ] **Step 1: Run entity branching CI**

Run: `Scripts/ci/check-no-entity-id-branching.sh`

Expected: exit 0.

- [ ] **Step 2: Run related existing tests**

Run existing EditMode tests for ModernSociety and quest network:

- `Rootborn.Tests.EditMode.ModernSociety`
- `Rootborn.Tests.EditMode.Quests`
- `Rootborn.Tests.EditMode.StudentLife`

Expected: pass.

## Task 5: Completion Audit And Commit

**Files:**
- Commit only the new/modified StudentLife implementation, tests, plan, and registry surface.

- [ ] **Step 1: Inspect diff**

Verify no unrelated dirty files are staged.

- [ ] **Step 2: Commit**

Commit message:

```bash
git commit -m "[FEATURE][TEST] 학생 진로 성장 활동 기반 추가"
```

- [ ] **Step 3: Report evidence**

Report changed files, test commands, pass/fail output, CI result, and any unverified PlayMode/scene work left for a later goal.
