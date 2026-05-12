# Quest System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a data-driven NPC dialogue quest system with playable Farm-scene UI, safe reward claiming, persistence, registry wiring, and QUEST-001 through QUEST-015 coverage.

**Architecture:** Implement `Rootborn.Game.Quests` as the runtime authority for quest state, objective counting, reward preflight, reward application, completion effects, and save data. Dialogue, NPC providers, scene interaction, and UI are thin adapters over `QuestLog`, so state transitions remain deterministic and easy to test. Rewards must preflight inventory capacity and duplicate state before mutation, then apply all-or-nothing and remain idempotent after save/load.

**Tech Stack:** Unity 6000.3.13f1, C#, ScriptableObject strategy assets, NUnit EditMode tests, Unity PlayMode tests, UGUI, Unity MCP `script-update-or-create` for every `Assets/**/*.cs` create/modify.

---

## Constraints For Implementers

- Do not edit `Assets/**/*.cs` with shell writes, `apply_patch`, `cat`, redirection, or external editors. Use Unity MCP `script-update-or-create` for C#.
- `.md`, `.asmdef`, and non-C# assets can be edited directly, but prefer Unity MCP asset tools for `.asset` files.
- Do not revert existing user changes. Check `git status --short` before each task and stage only the task files.
- Keep runtime entity matching by ScriptableObject reference. Stable `_id` fields are allowed for saves, validation, display diagnostics, and tests, not for special-case branching.
- Every task with runtime behavior follows RED -> GREEN -> regression command -> commit.

## File Structure

### Runtime Game

- Create `Assets/Scripts/Game/Quests/QuestState.cs`  
  Defines `QuestState`.

- Create `Assets/Scripts/Game/Quests/QuestEventKind.cs`  
  Defines broad event kinds: `Gather`, `Defeat`, `Collect`, `Harvest`, `Talk`.

- Create `Assets/Scripts/Game/Quests/QuestEvent.cs`  
  Value object with SO references and deterministic event key.

- Create `Assets/Scripts/Game/Quests/QuestDefinition.cs`  
  Data root for quest display keys, conditions, objectives, rewards, effects.

- Create `Assets/Scripts/Game/Quests/QuestObjectiveBase.cs`  
  Base SO strategy for matching and counting quest events.

- Create `Assets/Scripts/Game/Quests/Objectives/GatherQuestObjective.cs`  
  Counts resource gather events by `ResourceNodeDefinition` reference.

- Create `Assets/Scripts/Game/Quests/Objectives/DefeatQuestObjective.cs`  
  Counts defeat events by a target SO reference.

- Create `Assets/Scripts/Game/Quests/Objectives/CollectQuestObjective.cs`  
  Counts item acquisition events by `ItemDefinition` reference.

- Create `Assets/Scripts/Game/Quests/Objectives/HarvestQuestObjective.cs`  
  Counts crop harvest events by `CropDefinition` or `ItemDefinition` reference.

- Create `Assets/Scripts/Game/Quests/Objectives/TalkQuestObjective.cs`  
  Counts talk events by `NpcDefinition` reference.

- Create `Assets/Scripts/Game/Quests/QuestProgress.cs`  
  Stores state, objective counters, and processed event keys.

- Create `Assets/Scripts/Game/Quests/QuestLog.cs`  
  Accepts quests, records events, checks offers, claims rewards, saves/loads.

- Create `Assets/Scripts/Game/Quests/QuestSaveData.cs`  
  JSON-serializable quest progress and story flag DTOs.

- Create `Assets/Scripts/Game/Quests/QuestConditionBase.cs`  
  Base SO strategy for prerequisite/provider condition checks.

- Create `Assets/Scripts/Game/Quests/Conditions/QuestStateCondition.cs`  
  Requires another quest state.

- Create `Assets/Scripts/Game/Quests/Conditions/StoryFlagCondition.cs`  
  Requires a story flag state.

- Create `Assets/Scripts/Game/Quests/Conditions/KnowledgeUnlockedCondition.cs`  
  Requires a `KnowledgeNode` to be unlocked.

- Create `Assets/Scripts/Game/Quests/QuestRewardBase.cs`  
  Base SO strategy for preflight and apply.

- Create `Assets/Scripts/Game/Quests/Rewards/ItemQuestReward.cs`  
  Grants `ItemDefinition` after inventory capacity preflight.

- Create `Assets/Scripts/Game/Quests/Rewards/KnowledgeQuestReward.cs`  
  Unlocks `KnowledgeNode`.

- Create `Assets/Scripts/Game/Quests/Rewards/StoryFlagQuestReward.cs`  
  Sets `StoryFlagDefinition`.

- Create `Assets/Scripts/Game/Quests/Rewards/ToolUnlockQuestReward.cs`  
  Grants a tool item through `ItemQuestReward`-equivalent preflight.

- Create `Assets/Scripts/Game/Quests/QuestCompletionEffectBase.cs`  
  Base SO strategy for post-completion effects.

- Create `Assets/Scripts/Game/Quests/Effects/SetStoryFlagCompletionEffect.cs`  
  Sets a story flag.

- Create `Assets/Scripts/Game/Quests/Effects/UnlockKnowledgeCompletionEffect.cs`  
  Unlocks knowledge.

- Create `Assets/Scripts/Game/Story/StoryFlagDefinition.cs`  
  SO identity for story progression flags.

- Create `Assets/Scripts/Game/Story/StoryFlagSet.cs`  
  Runtime set with save/load IDs.

- Create `Assets/Scripts/Game/Dialogue/NpcDefinition.cs`  
  NPC SO with display key, dialogue, quests.

- Create `Assets/Scripts/Game/Dialogue/DialogueDefinition.cs`  
  Dialogue line keys and choices.

- Create `Assets/Scripts/Game/Dialogue/DialogueChoiceDefinition.cs`  
  Dialogue choice data with quest action and conditions.

- Create `Assets/Scripts/Game/Dialogue/DialogueSession.cs`  
  Pure runtime model for opening, choosing, closing dialogue.

- Create `Assets/Scripts/Game/Dialogue/QuestProvider.cs`  
  MonoBehaviour provider usable by NPCs and non-NPC interactables.

- Create `Assets/Scripts/Game/Dialogue/NpcInteractor.cs`  
  MonoBehaviour that opens dialogue and emits talk events.

- Modify `Assets/Scripts/Game/Common/GameDataRegistry.cs`  
  Add arrays for quests, quest strategies, NPCs, dialogue, story flags.

- Modify `Assets/Scripts/Game/Common/GameDataRegistry.cs`  
  Add inventory capacity helpers: `CanAdd(ItemDefinition item, int count)` and `CanAddAll(IReadOnlyList<InventoryGrant> grants)`. Keep existing `Add` behavior.

- Modify `Assets/Scripts/Game/Player/GatherInteractor.cs`  
  Emit quest gather events from the actual resource hit/break path.

- Modify `Assets/Scripts/Game/Tools/Effects/HarvestCropEffect.cs`  
  Emit harvest and collect quest events only after successful harvest and inventory grant.

- Modify `Assets/Scripts/Game/Save/SaveService.cs`  
  Add typed helpers for quest save JSON, or use existing `ReadJson`/`WriteJson` through `QuestLogSaveAdapter`.

### Runtime UI

- Create `Assets/Scripts/UI/Quests/QuestLogPanel.cs`  
  Displays active/completed quest names, objective progress, completion status, reward claimed state.

- Create `Assets/Scripts/UI/Quests/DialoguePanel.cs`  
  Displays NPC name, dialogue lines, and choices.

- Create `Assets/Scripts/UI/Quests/QuestRewardButton.cs`  
  Enables only when `QuestLog.CanClaimReward` succeeds and shows full-inventory failure state.

- Modify `Assets/Scripts/UI/HUD/StatusHud.cs` or Farm UI setup path only to attach/open the new quest UI. Avoid adding quest logic to this file.

### Editor And Data

- Modify `Assets/Scripts/Editor/DataValidators/GameDataRegistryValidator.cs`  
  Validate quest, reward, objective, NPC, dialogue, story flag arrays and IDs.

- Modify `Assets/Scripts/Editor/Tools/GenerateDefaultData.cs`  
  Create starter NPC, dialogue, quest, objective, reward, story flag, and register them.

- Create data folders:
  - `Assets/Data/Quests/`
  - `Assets/Data/Quests/Objectives/`
  - `Assets/Data/Quests/Rewards/`
  - `Assets/Data/Quests/Conditions/`
  - `Assets/Data/Quests/Effects/`
  - `Assets/Data/NPCs/`
  - `Assets/Data/Dialogue/`
  - `Assets/Data/Story/`

### Tests

- Modify `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs`  
  Add `QUEST_001` through `QUEST_015`.

- Create `Assets/Tests/EditMode/Quests/QuestProgressTests.cs`.
- Create `Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs`.
- Create `Assets/Tests/EditMode/Quests/QuestRewardTests.cs`.
- Create `Assets/Tests/EditMode/Quests/QuestConditionTests.cs`.
- Create `Assets/Tests/EditMode/Quests/QuestSaveTests.cs`.
- Create `Assets/Tests/EditMode/Quests/QuestRegistryTests.cs`.
- Create `Assets/Tests/EditMode/Dialogue/DialogueQuestTests.cs`.
- Create `Assets/Tests/PlayMode/Quests/QuestDialogueScenarioTests.cs`.
- Create `Assets/Tests/PlayMode/Quests/QuestProviderScenarioTests.cs`.

---

### Task 1: Scenario IDs And First Quest State RED

**Files:**
- Modify: `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestProgressTests.cs`
- Create: `Assets/Scripts/Game/Quests/QuestState.cs`

- [ ] **Step 1: Write the failing scenario ID/test file**

Use Unity MCP `script-update-or-create` for both `.cs` files.

`Assets/Tests/PlayMode/Scenarios/ScenarioId.cs` content should preserve existing constants and add:

```csharp
public const string QUEST_001 = "QUEST-001";
public const string QUEST_002 = "QUEST-002";
public const string QUEST_003 = "QUEST-003";
public const string QUEST_004 = "QUEST-004";
public const string QUEST_005 = "QUEST-005";
public const string QUEST_006 = "QUEST-006";
public const string QUEST_007 = "QUEST-007";
public const string QUEST_008 = "QUEST-008";
public const string QUEST_009 = "QUEST-009";
public const string QUEST_010 = "QUEST-010";
public const string QUEST_011 = "QUEST-011";
public const string QUEST_012 = "QUEST-012";
public const string QUEST_013 = "QUEST-013";
public const string QUEST_014 = "QUEST-014";
public const string QUEST_015 = "QUEST-015";
```

`Assets/Tests/EditMode/Quests/QuestProgressTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.Quests;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestProgressTests
    {
        [Test]
        public void QUEST_002_AcceptedQuest_BecomesActive()
        {
            var progress = new QuestProgress(objectiveCount: 1);

            Assert.AreEqual(QuestState.NotStarted, progress.State);
            Assert.IsTrue(progress.TryAccept());

            Assert.AreEqual(QuestState.Active, progress.State);
        }
    }
}
```

- [ ] **Step 2: Run test to verify RED**

Run EditMode tests through Unity MCP `tests-run` filtered to `QuestProgressTests`.

Expected: FAIL to compile because `Rootborn.Game.Quests.QuestProgress` and `QuestState` do not exist.

- [ ] **Step 3: Implement minimal QuestState and QuestProgress**

Use Unity MCP `script-update-or-create`.

`Assets/Scripts/Game/Quests/QuestState.cs`:

```csharp
namespace Rootborn.Game.Quests
{
    public enum QuestState
    {
        NotStarted,
        Active,
        Completed,
        RewardClaimed
    }
}
```

`Assets/Scripts/Game/Quests/QuestProgress.cs`:

```csharp
using System;

namespace Rootborn.Game.Quests
{
    public sealed class QuestProgress
    {
        private readonly int[] _objectiveCounts;

        public QuestProgress(int objectiveCount)
        {
            if (objectiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(objectiveCount));
            }

            _objectiveCounts = new int[objectiveCount];
            State = QuestState.NotStarted;
        }

        public QuestState State { get; private set; }
        public int ObjectiveCount => _objectiveCounts.Length;

        public bool TryAccept()
        {
            if (State != QuestState.NotStarted)
            {
                return false;
            }

            State = QuestState.Active;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run test to verify GREEN**

Run Unity MCP `tests-run` filtered to `QuestProgressTests`.

Expected: PASS for `QUEST_002_AcceptedQuest_BecomesActive`.

- [ ] **Step 5: Commit**

Stage only:

```powershell
git add -- Assets/Tests/PlayMode/Scenarios/ScenarioId.cs Assets/Tests/EditMode/Quests/QuestProgressTests.cs Assets/Scripts/Game/Quests/QuestState.cs Assets/Scripts/Game/Quests/QuestProgress.cs
git commit -m "[TEST][FEATURE] 퀘스트 상태 전이 기반 추가"
```

### Task 2: Objective Counting And Duplicate Event Guard

**Files:**
- Create: `Assets/Scripts/Game/Quests/QuestEventKind.cs`
- Create: `Assets/Scripts/Game/Quests/QuestEvent.cs`
- Create: `Assets/Scripts/Game/Quests/QuestObjectiveBase.cs`
- Modify: `Assets/Scripts/Game/Quests/QuestProgress.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs`

- [ ] **Step 1: Write failing tests for QUEST-004 and duplicate guard**

Use Unity MCP `script-update-or-create`.

`Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.Quests;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestObjectiveTests
    {
        [Test]
        public void QUEST_004_AllObjectivesRequiredBeforeCompletion()
        {
            var progress = new QuestProgress(objectiveCount: 2);
            progress.TryAccept();

            Assert.IsTrue(progress.TryAddObjectiveCount(0, 1, requiredCount: 1, eventKey: "event-a"));
            Assert.AreEqual(QuestState.Active, progress.State);

            Assert.IsTrue(progress.TryAddObjectiveCount(1, 1, requiredCount: 1, eventKey: "event-b"));
            Assert.AreEqual(QuestState.Completed, progress.State);
        }

        [Test]
        public void QUEST_007_DuplicateEventKey_DoesNotCountTwice()
        {
            var progress = new QuestProgress(objectiveCount: 1);
            progress.TryAccept();

            Assert.IsTrue(progress.TryAddObjectiveCount(0, 1, requiredCount: 2, eventKey: "gather-1"));
            Assert.IsFalse(progress.TryAddObjectiveCount(0, 1, requiredCount: 2, eventKey: "gather-1"));

            Assert.AreEqual(1, progress.GetObjectiveCount(0));
            Assert.AreEqual(QuestState.Active, progress.State);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Run Unity MCP `tests-run` filtered to `QuestObjectiveTests`.

Expected: compile failure because `TryAddObjectiveCount` and `GetObjectiveCount` do not exist.

- [ ] **Step 3: Implement counting and event guard**

Use Unity MCP `script-update-or-create` to replace `QuestProgress.cs` with:

```csharp
using System;
using System.Collections.Generic;

namespace Rootborn.Game.Quests
{
    public sealed class QuestProgress
    {
        private readonly int[] _objectiveCounts;
        private readonly bool[] _objectiveComplete;
        private readonly HashSet<string> _processedEventKeys = new HashSet<string>();

        public QuestProgress(int objectiveCount)
        {
            if (objectiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(objectiveCount));
            }

            _objectiveCounts = new int[objectiveCount];
            _objectiveComplete = new bool[objectiveCount];
            State = QuestState.NotStarted;
        }

        public QuestState State { get; private set; }
        public int ObjectiveCount => _objectiveCounts.Length;

        public bool TryAccept()
        {
            if (State != QuestState.NotStarted)
            {
                return false;
            }

            State = QuestState.Active;
            return true;
        }

        public int GetObjectiveCount(int objectiveIndex)
        {
            ValidateObjectiveIndex(objectiveIndex);
            return _objectiveCounts[objectiveIndex];
        }

        public bool TryAddObjectiveCount(int objectiveIndex, int delta, int requiredCount, string eventKey)
        {
            ValidateObjectiveIndex(objectiveIndex);
            if (State != QuestState.Active || delta <= 0 || requiredCount <= 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(eventKey) && !_processedEventKeys.Add(eventKey))
            {
                return false;
            }

            if (_objectiveComplete[objectiveIndex])
            {
                return false;
            }

            int next = _objectiveCounts[objectiveIndex] + delta;
            _objectiveCounts[objectiveIndex] = next > requiredCount ? requiredCount : next;
            _objectiveComplete[objectiveIndex] = _objectiveCounts[objectiveIndex] >= requiredCount;

            if (AllObjectivesComplete())
            {
                State = QuestState.Completed;
            }

            return true;
        }

        private bool AllObjectivesComplete()
        {
            if (_objectiveComplete.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _objectiveComplete.Length; i++)
            {
                if (!_objectiveComplete[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ValidateObjectiveIndex(int objectiveIndex)
        {
            if (objectiveIndex < 0 || objectiveIndex >= _objectiveCounts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(objectiveIndex));
            }
        }
    }
}
```

- [ ] **Step 4: Add event/objective base files**

Use Unity MCP `script-update-or-create`.

`QuestEventKind.cs`:

```csharp
namespace Rootborn.Game.Quests
{
    public enum QuestEventKind
    {
        Gather,
        Defeat,
        Collect,
        Harvest,
        Talk
    }
}
```

`QuestEvent.cs`:

```csharp
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    public readonly struct QuestEvent
    {
        public readonly QuestEventKind Kind;
        public readonly string EventKey;
        public readonly ResourceNodeDefinition Resource;
        public readonly ItemDefinition Item;
        public readonly CropDefinition Crop;
        public readonly ToolDefinition Tool;
        public readonly NpcDefinition Npc;
        public readonly ScriptableObject DefeatTarget;
        public readonly int Count;

        public QuestEvent(
            QuestEventKind kind,
            string eventKey,
            int count = 1,
            ResourceNodeDefinition resource = null,
            ItemDefinition item = null,
            CropDefinition crop = null,
            ToolDefinition tool = null,
            NpcDefinition npc = null,
            ScriptableObject defeatTarget = null)
        {
            Kind = kind;
            EventKey = eventKey;
            Count = count;
            Resource = resource;
            Item = item;
            Crop = crop;
            Tool = tool;
            Npc = npc;
            DefeatTarget = defeatTarget;
        }
    }
}
```

`QuestObjectiveBase.cs`:

```csharp
using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestObjectiveBase : ScriptableObject
    {
        [SerializeField] private string _displayKey;
        [SerializeField] private int _requiredCount = 1;

        public string DisplayKey => _displayKey;
        public int RequiredCount => Mathf.Max(1, _requiredCount);

        public abstract bool Matches(in QuestEvent questEvent);

        public virtual int GetDelta(in QuestEvent questEvent)
        {
            return Matches(in questEvent) ? Mathf.Max(1, questEvent.Count) : 0;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify GREEN**

Run Unity MCP `tests-run` filtered to `QuestObjectiveTests` and `QuestProgressTests`.

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Quests Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs
git commit -m "[FEATURE][TEST] 퀘스트 목표 카운팅과 중복 이벤트 방지 추가"
```

### Task 3: Data-Driven Objective Strategy SOs

**Files:**
- Create: `Assets/Scripts/Game/Quests/Objectives/GatherQuestObjective.cs`
- Create: `Assets/Scripts/Game/Quests/Objectives/DefeatQuestObjective.cs`
- Create: `Assets/Scripts/Game/Quests/Objectives/CollectQuestObjective.cs`
- Create: `Assets/Scripts/Game/Quests/Objectives/HarvestQuestObjective.cs`
- Create: `Assets/Scripts/Game/Quests/Objectives/TalkQuestObjective.cs`
- Modify: `Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs`
- Create: `Assets/Scripts/Game/Dialogue/NpcDefinition.cs`

- [ ] **Step 1: Write failing objective strategy tests**

Append to `QuestObjectiveTests.cs`:

```csharp
[Test]
public void QUEST_007_GatherObjective_MatchesResourceReference()
{
    var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
    var other = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
    var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
    SetField(objective, "_targetResource", resource);

    Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g1", resource: resource)));
    Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g2", resource: other)));
}

[Test]
public void QUEST_008_DefeatObjective_MatchesTargetReference()
{
    var target = ScriptableObject.CreateInstance<ScriptableObject>();
    var other = ScriptableObject.CreateInstance<ScriptableObject>();
    var objective = ScriptableObject.CreateInstance<DefeatQuestObjective>();
    SetField(objective, "_target", target);

    Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Defeat, "d1", defeatTarget: target)));
    Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Defeat, "d2", defeatTarget: other)));
}

[Test]
public void QUEST_009_CollectObjective_MatchesItemReference()
{
    var item = ScriptableObject.CreateInstance<ItemDefinition>();
    var other = ScriptableObject.CreateInstance<ItemDefinition>();
    var objective = ScriptableObject.CreateInstance<CollectQuestObjective>();
    SetField(objective, "_targetItem", item);

    Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c1", item: item)));
    Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c2", item: other)));
}
```

Add helper imports and helper method:

```csharp
using System.Reflection;
using Rootborn.Game.Common;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Resources;
using UnityEngine;

private static void SetField(object target, string fieldName, object value)
{
    var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.IsNotNull(field, fieldName);
    field.SetValue(target, value);
}
```

- [ ] **Step 2: Run tests to verify RED**

Run Unity MCP `tests-run` filtered to `QuestObjectiveTests`.

Expected: compile failure because objective strategy classes do not exist.

- [ ] **Step 3: Create NpcDefinition**

Use Unity MCP `script-update-or-create`.

```csharp
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "Npc_New", menuName = "Rootborn/Dialogue/NPC Definition")]
    public sealed class NpcDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private DialogueDefinition _defaultDialogue;
        [SerializeField] private QuestDefinition[] _availableQuests = System.Array.Empty<QuestDefinition>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public DialogueDefinition DefaultDialogue => _defaultDialogue;
        public QuestDefinition[] AvailableQuests => _availableQuests;
    }
}
```

- [ ] **Step 4: Create objective strategy implementations**

Use Unity MCP `script-update-or-create`.

`GatherQuestObjective.cs`:

```csharp
using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Gather", menuName = "Rootborn/Quests/Objectives/Gather")]
    public sealed class GatherQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ResourceNodeDefinition _targetResource;

        public ResourceNodeDefinition TargetResource => _targetResource;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Gather
                && _targetResource != null
                && questEvent.Resource == _targetResource;
        }
    }
}
```

`DefeatQuestObjective.cs`:

```csharp
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Defeat", menuName = "Rootborn/Quests/Objectives/Defeat")]
    public sealed class DefeatQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ScriptableObject _target;

        public ScriptableObject Target => _target;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Defeat
                && _target != null
                && questEvent.DefeatTarget == _target;
        }
    }
}
```

`CollectQuestObjective.cs`:

```csharp
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Collect", menuName = "Rootborn/Quests/Objectives/Collect")]
    public sealed class CollectQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ItemDefinition _targetItem;

        public ItemDefinition TargetItem => _targetItem;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Collect
                && _targetItem != null
                && questEvent.Item == _targetItem;
        }
    }
}
```

`HarvestQuestObjective.cs`:

```csharp
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Harvest", menuName = "Rootborn/Quests/Objectives/Harvest")]
    public sealed class HarvestQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private CropDefinition _targetCrop;
        [SerializeField] private ItemDefinition _targetHarvestItem;

        public override bool Matches(in QuestEvent questEvent)
        {
            if (questEvent.Kind != QuestEventKind.Harvest)
            {
                return false;
            }

            bool cropMatches = _targetCrop != null && questEvent.Crop == _targetCrop;
            bool itemMatches = _targetHarvestItem != null && questEvent.Item == _targetHarvestItem;
            return cropMatches || itemMatches;
        }
    }
}
```

`TalkQuestObjective.cs`:

```csharp
using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Talk", menuName = "Rootborn/Quests/Objectives/Talk")]
    public sealed class TalkQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private NpcDefinition _targetNpc;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Talk
                && _targetNpc != null
                && questEvent.Npc == _targetNpc;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify GREEN**

Run Unity MCP `tests-run` filtered to `QuestObjectiveTests`.

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Quests/Objectives Assets/Scripts/Game/Dialogue/NpcDefinition.cs Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs
git commit -m "[FEATURE][TEST] 데이터 기반 퀘스트 목표 전략 추가"
```

### Task 4: QuestDefinition And QuestLog Accept/Record Flow

**Files:**
- Create: `Assets/Scripts/Game/Quests/QuestDefinition.cs`
- Create: `Assets/Scripts/Game/Quests/QuestLog.cs`
- Modify: `Assets/Tests/EditMode/Quests/QuestProgressTests.cs`

- [ ] **Step 1: Write failing QuestLog tests**

Append:

```csharp
[Test]
public void QUEST_002_QuestLogAccept_ActivatesDefinition()
{
    var quest = ScriptableObject.CreateInstance<QuestDefinition>();
    SetField(quest, "_objectives", new QuestObjectiveBase[] { ScriptableObject.CreateInstance<AlwaysMatchObjective>() });
    var log = new QuestLog(new[] { quest });

    Assert.IsTrue(log.Accept(quest));

    Assert.AreEqual(QuestState.Active, log.GetState(quest));
}

[Test]
public void QUEST_004_QuestLogRecordEvent_CompletesWhenObjectiveSatisfied()
{
    var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
    SetField(objective, "_requiredCount", 1);
    var quest = ScriptableObject.CreateInstance<QuestDefinition>();
    SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
    var log = new QuestLog(new[] { quest });
    log.Accept(quest);

    log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "event-1"));

    Assert.AreEqual(QuestState.Completed, log.GetState(quest));
}

private sealed class AlwaysMatchObjective : QuestObjectiveBase
{
    public override bool Matches(in QuestEvent questEvent) => true;
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failure for `QuestDefinition` and `QuestLog`.

- [ ] **Step 3: Implement QuestDefinition**

Use Unity MCP.

```csharp
using UnityEngine;

namespace Rootborn.Game.Quests
{
    [CreateAssetMenu(fileName = "Quest_New", menuName = "Rootborn/Quests/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private QuestConditionBase[] _providerConditions = System.Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _prerequisites = System.Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestObjectiveBase[] _objectives = System.Array.Empty<QuestObjectiveBase>();
        [SerializeField] private QuestRewardBase[] _rewards = System.Array.Empty<QuestRewardBase>();
        [SerializeField] private QuestCompletionEffectBase[] _completionEffects = System.Array.Empty<QuestCompletionEffectBase>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DescriptionKey => _descriptionKey;
        public QuestConditionBase[] ProviderConditions => _providerConditions;
        public QuestConditionBase[] Prerequisites => _prerequisites;
        public QuestObjectiveBase[] Objectives => _objectives;
        public QuestRewardBase[] Rewards => _rewards;
        public QuestCompletionEffectBase[] CompletionEffects => _completionEffects;
    }
}
```

- [ ] **Step 4: Implement QuestLog**

Use Unity MCP.

```csharp
using System.Collections.Generic;

namespace Rootborn.Game.Quests
{
    public sealed class QuestLog
    {
        private readonly Dictionary<QuestDefinition, QuestProgress> _progressByQuest;

        public QuestLog(IEnumerable<QuestDefinition> quests)
        {
            _progressByQuest = new Dictionary<QuestDefinition, QuestProgress>();
            if (quests == null)
            {
                return;
            }

            foreach (var quest in quests)
            {
                if (quest == null || _progressByQuest.ContainsKey(quest))
                {
                    continue;
                }

                int objectiveCount = quest.Objectives == null ? 0 : quest.Objectives.Length;
                _progressByQuest.Add(quest, new QuestProgress(objectiveCount));
            }
        }

        public QuestState GetState(QuestDefinition quest)
        {
            return TryGetProgress(quest, out var progress) ? progress.State : QuestState.NotStarted;
        }

        public bool Accept(QuestDefinition quest)
        {
            return TryGetProgress(quest, out var progress) && progress.TryAccept();
        }

        public void RecordEvent(in QuestEvent questEvent)
        {
            foreach (var pair in _progressByQuest)
            {
                var quest = pair.Key;
                var progress = pair.Value;
                if (quest.Objectives == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Length; i++)
                {
                    var objective = quest.Objectives[i];
                    if (objective == null || !objective.Matches(in questEvent))
                    {
                        continue;
                    }

                    int delta = objective.GetDelta(in questEvent);
                    progress.TryAddObjectiveCount(i, delta, objective.RequiredCount, questEvent.EventKey);
                }
            }
        }

        private bool TryGetProgress(QuestDefinition quest, out QuestProgress progress)
        {
            if (quest != null)
            {
                return _progressByQuest.TryGetValue(quest, out progress);
            }

            progress = null;
            return false;
        }
    }
}
```

- [ ] **Step 5: Add temporary base abstractions to compile**

Use Unity MCP to create minimal base files:

```csharp
namespace Rootborn.Game.Quests
{
    public abstract class QuestConditionBase : UnityEngine.ScriptableObject
    {
        public abstract bool IsSatisfied(in QuestRuntimeContext context);
    }
}
```

```csharp
namespace Rootborn.Game.Quests
{
    public abstract class QuestRewardBase : UnityEngine.ScriptableObject
    {
        public abstract bool CanApply(in RewardRuntimeContext context);
        public abstract void Apply(in RewardRuntimeContext context);
    }
}
```

```csharp
namespace Rootborn.Game.Quests
{
    public abstract class QuestCompletionEffectBase : UnityEngine.ScriptableObject
    {
        public abstract bool CanApply(in RewardRuntimeContext context);
        public abstract void Apply(in RewardRuntimeContext context);
    }
}
```

```csharp
namespace Rootborn.Game.Quests
{
    public readonly struct QuestRuntimeContext
    {
        public readonly QuestLog QuestLog;

        public QuestRuntimeContext(QuestLog questLog)
        {
            QuestLog = questLog;
        }
    }
}
```

```csharp
namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;

        public RewardRuntimeContext(QuestLog questLog)
        {
            QuestLog = questLog;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify GREEN**

Run Unity MCP `tests-run` filtered to quest EditMode tests.

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/Quests Assets/Tests/EditMode/Quests/QuestProgressTests.cs
git commit -m "[FEATURE][TEST] 퀘스트 로그 수락과 이벤트 기록 추가"
```

### Task 5: Inventory Capacity And Atomic Item Rewards

**Files:**
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Create: `Assets/Scripts/Game/Quests/InventoryGrant.cs`
- Modify: `Assets/Scripts/Game/Quests/RewardRuntimeContext.cs`
- Create: `Assets/Scripts/Game/Quests/Rewards/ItemQuestReward.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestRewardTests.cs`

- [ ] **Step 1: Write failing reward safety tests**

Use Unity MCP.

```csharp
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestRewardTests
    {
        [Test]
        public void QUEST_005_ItemReward_CanApplyThenAddsExactlyOnce()
        {
            var item = MakeItem(maxStack: 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 3);
            var inventory = new Inventory();
            var context = new RewardRuntimeContext(null, inventory, null, null);

            Assert.IsTrue(reward.CanApply(in context));
            reward.Apply(in context);

            Assert.AreEqual(3, inventory.CountOf(item));
        }

        [Test]
        public void QUEST_006_FullInventory_PreflightFailsAndMutatesNothing()
        {
            var fullItem = MakeItem(maxStack: 1);
            var rewardItem = MakeItem(maxStack: 1);
            var inventory = new Inventory();
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                inventory.Add(fullItem, 1);
            }

            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", rewardItem);
            SetField(reward, "_count", 1);
            var context = new RewardRuntimeContext(null, inventory, null, null);

            Assert.IsFalse(reward.CanApply(in context));
            Assert.AreEqual(0, inventory.CountOf(rewardItem));
        }

        private static ItemDefinition MakeItem(int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", maxStack);
            return item;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failure for `Inventory.MaxSlots`, `RewardRuntimeContext` constructor, and `ItemQuestReward`.

- [ ] **Step 3: Add inventory preflight helpers**

Use Unity MCP to modify `GameDataRegistry.cs` carefully. Add inside `Inventory`:

```csharp
public const int MaxSlots = 32;

public bool CanAdd(ItemDefinition item, int count = 1)
{
    if (item == null || count <= 0)
    {
        return false;
    }

    int remaining = count;
    int max = item.MaxStack;
    int occupiedSlots = 0;

    for (int i = 0; i < _slots.Count; i++)
    {
        var slot = _slots[i];
        if (slot.Item != null && slot.Count > 0)
        {
            occupiedSlots++;
        }

        if (slot.Item != item)
        {
            continue;
        }

        int room = max - slot.Count;
        if (room <= 0)
        {
            continue;
        }

        int put = Mathf.Min(room, remaining);
        remaining -= put;
        if (remaining <= 0)
        {
            return true;
        }
    }

    int freeSlots = MaxSlots - occupiedSlots;
    while (remaining > 0 && freeSlots > 0)
    {
        remaining -= Mathf.Min(max, remaining);
        freeSlots--;
    }

    return remaining <= 0;
}

public bool CanAddAll(IReadOnlyList<InventoryGrant> grants)
{
    if (grants == null)
    {
        return true;
    }

    var clone = new Inventory();
    for (int i = 0; i < _slots.Count; i++)
    {
        var slot = _slots[i];
        if (slot.Item != null && slot.Count > 0)
        {
            clone.Add(slot.Item, slot.Count);
        }
    }

    for (int i = 0; i < grants.Count; i++)
    {
        var grant = grants[i];
        if (!clone.CanAdd(grant.Item, grant.Count))
        {
            return false;
        }

        clone.Add(grant.Item, grant.Count);
    }

    return true;
}
```

If `Add` currently permits more than 32 slots, modify its new-slot loop to stop at `MaxSlots`. Existing tests that depend on unlimited slots must be updated only if they contradict the new constitution.

- [ ] **Step 4: Create InventoryGrant and reward context**

Use Unity MCP.

```csharp
using Rootborn.Game.Common;

namespace Rootborn.Game.Quests
{
    public readonly struct InventoryGrant
    {
        public readonly ItemDefinition Item;
        public readonly int Count;

        public InventoryGrant(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
```

Replace `RewardRuntimeContext.cs`:

```csharp
using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;

namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly Inventory Inventory;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
        {
            QuestLog = questLog;
            Inventory = inventory;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
        }
    }
}
```

- [ ] **Step 5: Implement ItemQuestReward**

Use Unity MCP.

```csharp
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Quests.Rewards
{
    [CreateAssetMenu(fileName = "Reward_Item", menuName = "Rootborn/Quests/Rewards/Item")]
    public sealed class ItemQuestReward : QuestRewardBase
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;

        public ItemDefinition Item => _item;
        public int Count => Mathf.Max(1, _count);

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.Inventory != null
                && _item != null
                && context.Inventory.CanAdd(_item, Count);
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context))
            {
                return;
            }

            context.Inventory.Add(_item, Count);
        }
    }
}
```

- [ ] **Step 6: Run reward tests**

Run Unity MCP `tests-run` filtered to `QuestRewardTests`.

Expected: PASS.

- [ ] **Step 7: Run existing inventory regression tests**

Run Unity MCP `tests-run` for `InventoryUiTests`, `InventoryEquipmentScenarioTests`, and `GatherInteractorTests`.

Expected: PASS or only expected failures from the newly enforced 32-slot capacity. If a failure reveals prior tests expected unlimited inventory, adjust tests to the constitutional capacity rule and document the change in commit body.

- [ ] **Step 8: Commit**

```powershell
git add -- Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Scripts/Game/Quests Assets/Tests/EditMode/Quests/QuestRewardTests.cs
git commit -m "[FEATURE][TEST] 보상 인벤토리 사전 검증 추가"
```

### Task 6: Atomic Quest Reward Claim And Idempotency

**Files:**
- Modify: `Assets/Scripts/Game/Quests/QuestLog.cs`
- Modify: `Assets/Scripts/Game/Quests/QuestProgress.cs`
- Modify: `Assets/Tests/EditMode/Quests/QuestRewardTests.cs`

- [ ] **Step 1: Write failing claim idempotency tests**

Append:

```csharp
[Test]
public void QUEST_006_QuestLogClaimReward_RepeatedCallDoesNotDuplicate()
{
    var item = MakeItem(maxStack: 99);
    var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
    SetField(reward, "_item", item);
    SetField(reward, "_count", 2);
    var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
    SetField(objective, "_requiredCount", 1);
    var quest = ScriptableObject.CreateInstance<QuestDefinition>();
    SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
    SetField(quest, "_rewards", new QuestRewardBase[] { reward });
    var log = new QuestLog(new[] { quest });
    var inventory = new Inventory();
    var context = new RewardRuntimeContext(log, inventory, null, null);

    log.Accept(quest);
    log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

    Assert.IsTrue(log.ClaimReward(quest, in context));
    Assert.IsFalse(log.ClaimReward(quest, in context));

    Assert.AreEqual(2, inventory.CountOf(item));
    Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
}

[Test]
public void QUEST_005_QuestLogClaimReward_FullInventoryMutatesNothing()
{
    var filler = MakeItem(maxStack: 1);
    var rewardItem = MakeItem(maxStack: 1);
    var inventory = new Inventory();
    for (int i = 0; i < Inventory.MaxSlots; i++)
    {
        inventory.Add(filler, 1);
    }

    var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
    SetField(reward, "_item", rewardItem);
    SetField(reward, "_count", 1);
    var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
    SetField(objective, "_requiredCount", 1);
    var quest = ScriptableObject.CreateInstance<QuestDefinition>();
    SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
    SetField(quest, "_rewards", new QuestRewardBase[] { reward });
    var log = new QuestLog(new[] { quest });
    var context = new RewardRuntimeContext(log, inventory, null, null);

    log.Accept(quest);
    log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

    Assert.IsFalse(log.ClaimReward(quest, in context));

    Assert.AreEqual(0, inventory.CountOf(rewardItem));
    Assert.AreEqual(QuestState.Completed, log.GetState(quest));
}

private sealed class AlwaysMatchObjective : QuestObjectiveBase
{
    public override bool Matches(in QuestEvent questEvent) => true;
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failure for `QuestLog.ClaimReward`.

- [ ] **Step 3: Implement MarkRewardClaimed and ClaimReward**

Use Unity MCP.

Add to `QuestProgress`:

```csharp
public bool TryMarkRewardClaimed()
{
    if (State != QuestState.Completed)
    {
        return false;
    }

    State = QuestState.RewardClaimed;
    return true;
}
```

Add to `QuestLog`:

```csharp
public bool CanClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
{
    if (!TryGetProgress(quest, out var progress) || progress.State != QuestState.Completed)
    {
        return false;
    }

    if (quest.Rewards != null)
    {
        for (int i = 0; i < quest.Rewards.Length; i++)
        {
            var reward = quest.Rewards[i];
            if (reward != null && !reward.CanApply(in context))
            {
                return false;
            }
        }
    }

    if (quest.CompletionEffects != null)
    {
        for (int i = 0; i < quest.CompletionEffects.Length; i++)
        {
            var effect = quest.CompletionEffects[i];
            if (effect != null && !effect.CanApply(in context))
            {
                return false;
            }
        }
    }

    return true;
}

public bool ClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
{
    if (!CanClaimReward(quest, in context))
    {
        return false;
    }

    if (quest.Rewards != null)
    {
        for (int i = 0; i < quest.Rewards.Length; i++)
        {
            quest.Rewards[i]?.Apply(in context);
        }
    }

    if (quest.CompletionEffects != null)
    {
        for (int i = 0; i < quest.CompletionEffects.Length; i++)
        {
            quest.CompletionEffects[i]?.Apply(in context);
        }
    }

    return TryGetProgress(quest, out var progress) && progress.TryMarkRewardClaimed();
}
```

- [ ] **Step 4: Run tests**

Run Unity MCP `tests-run` filtered to `QuestRewardTests`.

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Quests Assets/Tests/EditMode/Quests/QuestRewardTests.cs
git commit -m "[FEATURE][TEST] 퀘스트 보상 원자적 수령 처리 추가"
```

### Task 7: Story Flags, Conditions, And Completion Effects

**Files:**
- Create: `Assets/Scripts/Game/Story/StoryFlagDefinition.cs`
- Create: `Assets/Scripts/Game/Story/StoryFlagSet.cs`
- Create: `Assets/Scripts/Game/Quests/Conditions/StoryFlagCondition.cs`
- Create: `Assets/Scripts/Game/Quests/Conditions/QuestStateCondition.cs`
- Create: `Assets/Scripts/Game/Quests/Conditions/KnowledgeUnlockedCondition.cs`
- Create: `Assets/Scripts/Game/Quests/Effects/SetStoryFlagCompletionEffect.cs`
- Create: `Assets/Scripts/Game/Quests/Effects/UnlockKnowledgeCompletionEffect.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestConditionTests.cs`

- [ ] **Step 1: Write failing tests for QUEST-010 and QUEST-011**

Use Unity MCP.

```csharp
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Conditions;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestConditionTests
    {
        [Test]
        public void QUEST_010_CompletionEffect_SetsStoryFlag()
        {
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var flags = new StoryFlagSet();
            var effect = ScriptableObject.CreateInstance<SetStoryFlagCompletionEffect>();
            SetField(effect, "_flag", flag);
            var context = new RewardRuntimeContext(null, null, null, flags);

            Assert.IsTrue(effect.CanApply(in context));
            effect.Apply(in context);

            Assert.IsTrue(flags.IsSet(flag));
        }

        [Test]
        public void QUEST_011_StoryFlagCondition_BlocksWhenMissing()
        {
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var flags = new StoryFlagSet();
            var condition = ScriptableObject.CreateInstance<StoryFlagCondition>();
            SetField(condition, "_requiredFlag", flag);

            Assert.IsFalse(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, flags)));
            flags.Set(flag);
            Assert.IsTrue(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, flags)));
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failures for Story and condition/effect classes.

- [ ] **Step 3: Implement StoryFlagDefinition and StoryFlagSet**

Use Unity MCP.

```csharp
using UnityEngine;

namespace Rootborn.Game.Story
{
    [CreateAssetMenu(fileName = "StoryFlag_New", menuName = "Rootborn/Story/Story Flag")]
    public sealed class StoryFlagDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;

        public string Id => _id;
        public string DisplayKey => _displayKey;
    }
}
```

```csharp
using System.Collections.Generic;

namespace Rootborn.Game.Story
{
    public sealed class StoryFlagSet
    {
        private readonly HashSet<StoryFlagDefinition> _flags = new HashSet<StoryFlagDefinition>();

        public bool IsSet(StoryFlagDefinition flag)
        {
            return flag != null && _flags.Contains(flag);
        }

        public bool Set(StoryFlagDefinition flag)
        {
            return flag != null && _flags.Add(flag);
        }
    }
}
```

- [ ] **Step 4: Expand QuestRuntimeContext**

Use Unity MCP.

```csharp
using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;

namespace Rootborn.Game.Quests
{
    public readonly struct QuestRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly QuestDefinition Quest;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;

        public QuestRuntimeContext(
            QuestLog questLog,
            QuestDefinition quest,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
        {
            QuestLog = questLog;
            Quest = quest;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
        }
    }
}
```

- [ ] **Step 5: Implement conditions and effects**

Use Unity MCP.

`StoryFlagCondition.cs`:

```csharp
using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_StoryFlag", menuName = "Rootborn/Quests/Conditions/Story Flag")]
    public sealed class StoryFlagCondition : QuestConditionBase
    {
        [SerializeField] private StoryFlagDefinition _requiredFlag;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            return context.StoryFlags != null && context.StoryFlags.IsSet(_requiredFlag);
        }
    }
}
```

`QuestStateCondition.cs`:

```csharp
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_QuestState", menuName = "Rootborn/Quests/Conditions/Quest State")]
    public sealed class QuestStateCondition : QuestConditionBase
    {
        [SerializeField] private QuestDefinition _quest;
        [SerializeField] private QuestState _requiredState = QuestState.RewardClaimed;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            return context.QuestLog != null
                && _quest != null
                && context.QuestLog.GetState(_quest) == _requiredState;
        }
    }
}
```

`KnowledgeUnlockedCondition.cs`:

```csharp
using Rootborn.Game.Knowledge;
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_KnowledgeUnlocked", menuName = "Rootborn/Quests/Conditions/Knowledge Unlocked")]
    public sealed class KnowledgeUnlockedCondition : QuestConditionBase
    {
        [SerializeField] private KnowledgeNode _knowledge;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            return context.KnowledgeProgress != null && context.KnowledgeProgress.IsUnlocked(_knowledge);
        }
    }
}
```

`SetStoryFlagCompletionEffect.cs`:

```csharp
using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Game.Quests.Effects
{
    [CreateAssetMenu(fileName = "Effect_SetStoryFlag", menuName = "Rootborn/Quests/Effects/Set Story Flag")]
    public sealed class SetStoryFlagCompletionEffect : QuestCompletionEffectBase
    {
        [SerializeField] private StoryFlagDefinition _flag;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.StoryFlags != null && _flag != null;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (CanApply(in context))
            {
                context.StoryFlags.Set(_flag);
            }
        }
    }
}
```

`UnlockKnowledgeCompletionEffect.cs`:

```csharp
using Rootborn.Game.Knowledge;
using UnityEngine;

namespace Rootborn.Game.Quests.Effects
{
    [CreateAssetMenu(fileName = "Effect_UnlockKnowledge", menuName = "Rootborn/Quests/Effects/Unlock Knowledge")]
    public sealed class UnlockKnowledgeCompletionEffect : QuestCompletionEffectBase
    {
        [SerializeField] private KnowledgeNode _knowledge;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.KnowledgeProgress != null && _knowledge != null;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (CanApply(in context))
            {
                context.KnowledgeProgress.Seed(_knowledge);
            }
        }
    }
}
```

- [ ] **Step 6: Run tests**

Run Unity MCP `tests-run` filtered to `QuestConditionTests`.

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/Story Assets/Scripts/Game/Quests/Conditions Assets/Scripts/Game/Quests/Effects Assets/Scripts/Game/Quests/QuestRuntimeContext.cs Assets/Tests/EditMode/Quests/QuestConditionTests.cs
git commit -m "[FEATURE][TEST] 스토리 플래그 조건과 완료 효과 추가"
```

### Task 8: Quest Save And Load

**Files:**
- Create: `Assets/Scripts/Game/Quests/QuestSaveData.cs`
- Modify: `Assets/Scripts/Game/Quests/QuestProgress.cs`
- Modify: `Assets/Scripts/Game/Quests/QuestLog.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestSaveTests.cs`

- [ ] **Step 1: Write failing QUEST-012 and QUEST-013 tests**

Use Unity MCP.

```csharp
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestSaveTests
    {
        [Test]
        public void QUEST_012_ActiveProgress_RoundTripsThroughSaveData()
        {
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 3);
            var quest = MakeQuest("quest.active", objective, null);
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

            var save = log.ToSaveData();
            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(save);

            Assert.AreEqual(QuestState.Active, loaded.GetState(quest));
            Assert.AreEqual(1, loaded.GetObjectiveCount(quest, 0));
        }

        [Test]
        public void QUEST_013_RewardClaimed_AfterLoadCannotPayAgain()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 1);
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = MakeQuest("quest.claimed", objective, reward);
            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            var context = new RewardRuntimeContext(log, inventory, null, null);
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            Assert.IsTrue(log.ClaimReward(quest, in context));

            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(log.ToSaveData());
            var loadedContext = new RewardRuntimeContext(loaded, inventory, null, null);

            Assert.IsFalse(loaded.ClaimReward(quest, in loadedContext));
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        private static QuestDefinition MakeQuest(string id, QuestObjectiveBase objective, QuestRewardBase reward)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_id", id);
            SetField(quest, "_objectives", new[] { objective });
            if (reward != null)
            {
                SetField(quest, "_rewards", new[] { reward });
            }
            return quest;
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failure for save APIs.

- [ ] **Step 3: Implement save DTOs**

Use Unity MCP.

```csharp
using System;

namespace Rootborn.Game.Quests
{
    [Serializable]
    public sealed class QuestLogSaveData
    {
        public QuestProgressSaveData[] Quests = Array.Empty<QuestProgressSaveData>();
    }

    [Serializable]
    public sealed class QuestProgressSaveData
    {
        public string QuestId;
        public QuestState State;
        public int[] ObjectiveCounts = Array.Empty<int>();
        public string[] ProcessedEventKeys = Array.Empty<string>();
    }
}
```

- [ ] **Step 4: Add progress export/import**

Use Unity MCP to add to `QuestProgress`:

```csharp
public QuestProgressSaveData ToSaveData(string questId)
{
    return new QuestProgressSaveData
    {
        QuestId = questId,
        State = State,
        ObjectiveCounts = (int[])_objectiveCounts.Clone(),
        ProcessedEventKeys = new List<string>(_processedEventKeys).ToArray(),
    };
}

public void LoadFromSaveData(QuestProgressSaveData data)
{
    if (data == null)
    {
        return;
    }

    State = data.State;
    for (int i = 0; i < _objectiveCounts.Length && data.ObjectiveCounts != null && i < data.ObjectiveCounts.Length; i++)
    {
        _objectiveCounts[i] = data.ObjectiveCounts[i];
        _objectiveComplete[i] = _objectiveCounts[i] > 0;
    }

    _processedEventKeys.Clear();
    if (data.ProcessedEventKeys != null)
    {
        for (int i = 0; i < data.ProcessedEventKeys.Length; i++)
        {
            var key = data.ProcessedEventKeys[i];
            if (!string.IsNullOrEmpty(key))
            {
                _processedEventKeys.Add(key);
            }
        }
    }
}
```

- [ ] **Step 5: Add QuestLog save APIs**

Use Unity MCP to add:

```csharp
public int GetObjectiveCount(QuestDefinition quest, int objectiveIndex)
{
    return TryGetProgress(quest, out var progress) ? progress.GetObjectiveCount(objectiveIndex) : 0;
}

public QuestLogSaveData ToSaveData()
{
    var list = new List<QuestProgressSaveData>(_progressByQuest.Count);
    foreach (var pair in _progressByQuest)
    {
        if (pair.Key == null || string.IsNullOrEmpty(pair.Key.Id))
        {
            continue;
        }

        list.Add(pair.Value.ToSaveData(pair.Key.Id));
    }

    return new QuestLogSaveData { Quests = list.ToArray() };
}

public void LoadFromSaveData(QuestLogSaveData saveData)
{
    if (saveData == null || saveData.Quests == null)
    {
        return;
    }

    for (int i = 0; i < saveData.Quests.Length; i++)
    {
        var saved = saveData.Quests[i];
        if (saved == null || string.IsNullOrEmpty(saved.QuestId))
        {
            continue;
        }

        foreach (var pair in _progressByQuest)
        {
            if (pair.Key != null && pair.Key.Id == saved.QuestId)
            {
                pair.Value.LoadFromSaveData(saved);
                break;
            }
        }
    }
}
```

This identity comparison is save-data resolution only. Do not add entity-specific branch logic.

- [ ] **Step 6: Run tests**

Run Unity MCP `tests-run` filtered to `QuestSaveTests`.

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/Quests Assets/Tests/EditMode/Quests/QuestSaveTests.cs
git commit -m "[FEATURE][TEST] 퀘스트 저장 로드 상태 보존 추가"
```

### Task 9: Dialogue Data And Quest Actions

**Files:**
- Create: `Assets/Scripts/Game/Dialogue/DialogueDefinition.cs`
- Create: `Assets/Scripts/Game/Dialogue/DialogueChoiceDefinition.cs`
- Create: `Assets/Scripts/Game/Dialogue/DialogueSession.cs`
- Create: `Assets/Tests/EditMode/Dialogue/DialogueQuestTests.cs`

- [ ] **Step 1: Write failing dialogue tests**

Use Unity MCP.

```csharp
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Dialogue
{
    public sealed class DialogueQuestTests
    {
        [Test]
        public void QUEST_001_DialogueSession_OpenAndClose()
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var session = new DialogueSession();

            session.Open(dialogue);
            Assert.IsTrue(session.IsOpen);

            session.Close();
            Assert.IsFalse(session.IsOpen);
        }

        [Test]
        public void QUEST_002_DialogueChoice_AcceptsQuest()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { ScriptableObject.CreateInstance<AlwaysMatchObjective>() });
            var choice = ScriptableObject.CreateInstance<DialogueChoiceDefinition>();
            SetField(choice, "_questAction", DialogueQuestAction.AcceptQuest);
            SetField(choice, "_quest", quest);
            var log = new QuestLog(new[] { quest });

            Assert.IsTrue(choice.TryExecute(new DialogueChoiceContext(log, default)));

            Assert.AreEqual(QuestState.Active, log.GetState(quest));
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failures for dialogue classes.

- [ ] **Step 3: Implement dialogue data/session**

Use Unity MCP.

`DialogueDefinition.cs`:

```csharp
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "Rootborn/Dialogue/Dialogue Definition")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [SerializeField] private string[] _lineKeys = System.Array.Empty<string>();
        [SerializeField] private DialogueChoiceDefinition[] _choices = System.Array.Empty<DialogueChoiceDefinition>();

        public string[] LineKeys => _lineKeys;
        public DialogueChoiceDefinition[] Choices => _choices;
    }
}
```

`DialogueChoiceDefinition.cs`:

```csharp
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    public enum DialogueQuestAction
    {
        None,
        AcceptQuest,
        ClaimReward,
        Close
    }

    public readonly struct DialogueChoiceContext
    {
        public readonly QuestLog QuestLog;
        public readonly RewardRuntimeContext RewardContext;

        public DialogueChoiceContext(QuestLog questLog, RewardRuntimeContext rewardContext)
        {
            QuestLog = questLog;
            RewardContext = rewardContext;
        }
    }

    [CreateAssetMenu(fileName = "DialogueChoice_New", menuName = "Rootborn/Dialogue/Dialogue Choice")]
    public sealed class DialogueChoiceDefinition : ScriptableObject
    {
        [SerializeField] private string _labelKey;
        [SerializeField] private DialogueQuestAction _questAction;
        [SerializeField] private QuestDefinition _quest;

        public string LabelKey => _labelKey;
        public DialogueQuestAction QuestAction => _questAction;
        public QuestDefinition Quest => _quest;

        public bool TryExecute(in DialogueChoiceContext context)
        {
            if (_questAction == DialogueQuestAction.AcceptQuest)
            {
                return context.QuestLog != null && context.QuestLog.Accept(_quest);
            }

            if (_questAction == DialogueQuestAction.ClaimReward)
            {
                return context.QuestLog != null && context.QuestLog.ClaimReward(_quest, context.RewardContext);
            }

            return _questAction == DialogueQuestAction.Close || _questAction == DialogueQuestAction.None;
        }
    }
}
```

`DialogueSession.cs`:

```csharp
namespace Rootborn.Game.Dialogue
{
    public sealed class DialogueSession
    {
        public DialogueDefinition Current { get; private set; }
        public bool IsOpen => Current != null;

        public void Open(DialogueDefinition dialogue)
        {
            Current = dialogue;
        }

        public void Close()
        {
            Current = null;
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run Unity MCP `tests-run` filtered to `DialogueQuestTests`.

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Dialogue Assets/Tests/EditMode/Dialogue/DialogueQuestTests.cs
git commit -m "[FEATURE][TEST] 대화 선택지 퀘스트 액션 추가"
```

### Task 10: Registry Fields And Validation

**Files:**
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Scripts/Editor/DataValidators/GameDataRegistryValidator.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestRegistryTests.cs`

- [ ] **Step 1: Write failing registry tests**

Use Unity MCP.

```csharp
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestRegistryTests
    {
        [Test]
        public void QUEST_014_GameDataRegistry_ExposesQuestDataArrays()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            Assert.IsNotNull(registry.Quests);
            Assert.IsNotNull(registry.QuestObjectives);
            Assert.IsNotNull(registry.QuestRewards);
            Assert.IsNotNull(registry.QuestCompletionEffects);
            Assert.IsNotNull(registry.QuestConditions);
            Assert.IsNotNull(registry.Npcs);
            Assert.IsNotNull(registry.Dialogues);
            Assert.IsNotNull(registry.StoryFlags);
        }
    }
}
```

- [ ] **Step 2: Run test to verify RED**

Expected: compile failure because registry properties do not exist.

- [ ] **Step 3: Add registry fields/properties**

Use Unity MCP to add fields and properties to `GameDataRegistry`.

```csharp
[SerializeField] private Rootborn.Game.Quests.QuestDefinition[] _quests = Array.Empty<Rootborn.Game.Quests.QuestDefinition>();
[SerializeField] private Rootborn.Game.Quests.QuestObjectiveBase[] _questObjectives = Array.Empty<Rootborn.Game.Quests.QuestObjectiveBase>();
[SerializeField] private Rootborn.Game.Quests.QuestRewardBase[] _questRewards = Array.Empty<Rootborn.Game.Quests.QuestRewardBase>();
[SerializeField] private Rootborn.Game.Quests.QuestCompletionEffectBase[] _questCompletionEffects = Array.Empty<Rootborn.Game.Quests.QuestCompletionEffectBase>();
[SerializeField] private Rootborn.Game.Quests.QuestConditionBase[] _questConditions = Array.Empty<Rootborn.Game.Quests.QuestConditionBase>();
[SerializeField] private Rootborn.Game.Dialogue.NpcDefinition[] _npcs = Array.Empty<Rootborn.Game.Dialogue.NpcDefinition>();
[SerializeField] private Rootborn.Game.Dialogue.DialogueDefinition[] _dialogues = Array.Empty<Rootborn.Game.Dialogue.DialogueDefinition>();
[SerializeField] private Rootborn.Game.Story.StoryFlagDefinition[] _storyFlags = Array.Empty<Rootborn.Game.Story.StoryFlagDefinition>();

public Rootborn.Game.Quests.QuestDefinition[] Quests => _quests;
public Rootborn.Game.Quests.QuestObjectiveBase[] QuestObjectives => _questObjectives;
public Rootborn.Game.Quests.QuestRewardBase[] QuestRewards => _questRewards;
public Rootborn.Game.Quests.QuestCompletionEffectBase[] QuestCompletionEffects => _questCompletionEffects;
public Rootborn.Game.Quests.QuestConditionBase[] QuestConditions => _questConditions;
public Rootborn.Game.Dialogue.NpcDefinition[] Npcs => _npcs;
public Rootborn.Game.Dialogue.DialogueDefinition[] Dialogues => _dialogues;
public Rootborn.Game.Story.StoryFlagDefinition[] StoryFlags => _storyFlags;
```

- [ ] **Step 4: Extend validator**

Use Unity MCP to modify `GameDataRegistryValidator.cs` and add checks:

```csharp
issues += CheckUnique(reg.Quests, "Quest", path);
issues += CheckUnique(reg.Npcs, "NPC", path);
issues += CheckUnique(reg.StoryFlags, "StoryFlag", path);
issues += CheckNonNull(reg.QuestObjectives, "QuestObjective", path);
issues += CheckNonNull(reg.QuestRewards, "QuestReward", path);
issues += CheckNonNull(reg.QuestCompletionEffects, "QuestCompletionEffect", path);
issues += CheckNonNull(reg.QuestConditions, "QuestCondition", path);
issues += CheckNonNull(reg.Dialogues, "Dialogue", path);
```

Add helper:

```csharp
private static int CheckNonNull<T>(T[] items, string kind, string registryPath) where T : UnityEngine.Object
{
    int issues = 0;
    if (items == null)
    {
        Debug.LogWarning($"[ROOTBORN] {kind} array is null in {registryPath}");
        return 1;
    }

    for (int i = 0; i < items.Length; i++)
    {
        if (items[i] == null)
        {
            Debug.LogWarning($"[ROOTBORN] {kind} index {i} is null in {registryPath}");
            issues++;
        }
    }

    return issues;
}
```

- [ ] **Step 5: Run tests**

Run Unity MCP `tests-run` filtered to `QuestRegistryTests`.

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Scripts/Editor/DataValidators/GameDataRegistryValidator.cs Assets/Tests/EditMode/Quests/QuestRegistryTests.cs
git commit -m "[FEATURE][TEST] 퀘스트 데이터 레지스트리 확장"
```

### Task 11: Real Event Adapters For Gather, Collect, Harvest

**Files:**
- Modify: `Assets/Scripts/Game/Player/GatherInteractor.cs`
- Modify: `Assets/Scripts/Game/Tools/Effects/HarvestCropEffect.cs`
- Create: `Assets/Scripts/Game/Quests/QuestEventSink.cs`
- Modify: `Assets/Tests/EditMode/GatherInteractorTests.cs`
- Modify: `Assets/Tests/EditMode/Farming/ToolEffectTests.cs`

- [ ] **Step 1: Create failing adapter tests**

Add tests that bind a fake sink and assert real success paths emit events:

```csharp
private sealed class RecordingQuestEventSink : IQuestEventSink
{
    public QuestEvent LastEvent;
    public int Count;

    public void Record(in QuestEvent questEvent)
    {
        LastEvent = questEvent;
        Count++;
    }
}
```

For `GatherInteractorTests`, invoke the existing interaction path against a breakable `ResourceNode`. Expected event:

```csharp
Assert.AreEqual(QuestEventKind.Gather, sink.LastEvent.Kind);
Assert.AreSame(resourceDefinition, sink.LastEvent.Resource);
```

For `ToolEffectTests`, harvest a crop through `HarvestCropEffect.Apply` and assert:

```csharp
Assert.AreEqual(QuestEventKind.Harvest, sink.LastEvent.Kind);
Assert.AreSame(crop, sink.LastEvent.Crop);
Assert.AreSame(harvestItem, sink.LastEvent.Item);
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failure for `IQuestEventSink` and missing bindings.

- [ ] **Step 3: Add quest event sink**

Use Unity MCP.

```csharp
namespace Rootborn.Game.Quests
{
    public interface IQuestEventSink
    {
        void Record(in QuestEvent questEvent);
    }
}
```

- [ ] **Step 4: Modify GatherInteractor**

Use Unity MCP. Add:

```csharp
private Rootborn.Game.Quests.IQuestEventSink _questEvents;

public void BindQuestEvents(Rootborn.Game.Quests.IQuestEventSink sink)
{
    _questEvents = sink;
}
```

After a real `node.Hit(_equippedTool)` succeeds and `node.Definition != null`, record:

```csharp
_questEvents?.Record(new Rootborn.Game.Quests.QuestEvent(
    Rootborn.Game.Quests.QuestEventKind.Gather,
    $"{node.GetInstanceID()}:{Time.frameCount}",
    resource: node.Definition,
    tool: _equippedTool));
```

This event key uses scene instance and frame for duplicate prevention. Unit tests can inject deterministic events directly into `QuestLog` for exact counting.

- [ ] **Step 5: Modify HarvestCropEffect**

Use Unity MCP. In `ToolUseContext`, add `IQuestEventSink QuestEvents` if that context is under project code. If changing `ToolUseContext` is too broad, resolve sink from `ctx.User.GetComponent<QuestEventRelay>()` with a cached component outside hot Update loops.

After successful harvest and inventory add:

```csharp
questEvents.Record(new QuestEvent(
    QuestEventKind.Harvest,
    $"{ctx.TargetCell}:{UnityEngine.Time.frameCount}",
    count: yield,
    crop: crop,
    item: crop.HarvestItem,
    tool: ctx.Tool));
```

- [ ] **Step 6: Run adapter tests and existing regressions**

Run Unity MCP `tests-run` for `GatherInteractorTests`, `ToolEffectTests`, and `FarmingScenarioTests`.

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/Quests/QuestEventSink.cs Assets/Scripts/Game/Player/GatherInteractor.cs Assets/Scripts/Game/Tools/Effects/HarvestCropEffect.cs Assets/Tests/EditMode/GatherInteractorTests.cs Assets/Tests/EditMode/Farming/ToolEffectTests.cs
git commit -m "[FEATURE][TEST] 실제 게임 이벤트 퀘스트 연동 추가"
```

### Task 12: Quest Providers, NPC Interactors, And PlayMode Dialogue

**Files:**
- Create: `Assets/Scripts/Game/Dialogue/QuestProvider.cs`
- Create: `Assets/Scripts/Game/Dialogue/NpcInteractor.cs`
- Create: `Assets/Tests/PlayMode/Quests/QuestDialogueScenarioTests.cs`
- Create: `Assets/Tests/PlayMode/Quests/QuestProviderScenarioTests.cs`

- [ ] **Step 1: Write failing PlayMode tests for QUEST-001 and QUEST-003**

Use Unity MCP.

`QuestDialogueScenarioTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestDialogueScenarioTests
    {
        [UnityTest]
        public IEnumerator QUEST_001_NpcInteraction_OpensAndClosesDialogue()
        {
            var npc = new GameObject("NPC");
            var interactor = npc.AddComponent<NpcInteractor>();
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var npcDef = ScriptableObject.CreateInstance<NpcDefinition>();
            SetField(npcDef, "_defaultDialogue", dialogue);
            interactor.Bind(npcDef);

            interactor.Interact();
            yield return null;

            Assert.IsTrue(interactor.Session.IsOpen);

            interactor.Close();
            yield return null;

            Assert.IsFalse(interactor.Session.IsOpen);
            Object.Destroy(npc);
        }
    }
}
```

`QuestProviderScenarioTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestProviderScenarioTests
    {
        [UnityTest]
        public IEnumerator QUEST_003_InteractableObject_ProvidesQuestDefinition()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            var providerGo = new GameObject("Quest Board");
            var provider = providerGo.AddComponent<QuestProvider>();
            provider.Bind(new[] { quest });

            yield return null;

            CollectionAssert.Contains(provider.Quests, quest);
            Object.Destroy(providerGo);
        }
    }
}
```

Include local `SetField` helper in each test.

- [ ] **Step 2: Run PlayMode tests to verify RED**

Run Unity MCP `tests-run` PlayMode filtered to quest scenario tests.

Expected: compile failure for provider/interactor.

- [ ] **Step 3: Implement QuestProvider**

Use Unity MCP.

```csharp
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class QuestProvider : MonoBehaviour
    {
        [SerializeField] private QuestDefinition[] _quests = System.Array.Empty<QuestDefinition>();

        public QuestDefinition[] Quests => _quests;

        public void Bind(QuestDefinition[] quests)
        {
            _quests = quests ?? System.Array.Empty<QuestDefinition>();
        }
    }
}
```

- [ ] **Step 4: Implement NpcInteractor**

Use Unity MCP.

```csharp
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class NpcInteractor : MonoBehaviour
    {
        [SerializeField] private NpcDefinition _npc;

        private readonly DialogueSession _session = new DialogueSession();
        private IQuestEventSink _questEvents;

        public DialogueSession Session => _session;
        public NpcDefinition Npc => _npc;

        public void Bind(NpcDefinition npc)
        {
            _npc = npc;
        }

        public void BindQuestEvents(IQuestEventSink questEvents)
        {
            _questEvents = questEvents;
        }

        public void Interact()
        {
            if (_npc == null)
            {
                return;
            }

            _session.Open(_npc.DefaultDialogue);
        }

        public void CompleteTalk(string eventKey)
        {
            if (_npc == null)
            {
                return;
            }

            _questEvents?.Record(new QuestEvent(QuestEventKind.Talk, eventKey, npc: _npc));
        }

        public void Close()
        {
            _session.Close();
        }
    }
}
```

- [ ] **Step 5: Run PlayMode tests**

Run Unity MCP `tests-run` PlayMode filtered to quest scenario tests.

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Dialogue/QuestProvider.cs Assets/Scripts/Game/Dialogue/NpcInteractor.cs Assets/Tests/PlayMode/Quests
git commit -m "[FEATURE][TEST] NPC와 오브젝트 퀘스트 제공자 추가"
```

### Task 13: Playable Quest UI

**Files:**
- Create: `Assets/Scripts/UI/Quests/DialoguePanel.cs`
- Create: `Assets/Scripts/UI/Quests/QuestLogPanel.cs`
- Create: `Assets/Scripts/UI/Quests/QuestRewardButton.cs`
- Create: `Assets/Tests/EditMode/Quests/QuestUiTests.cs`
- Modify: Farm UI setup path after reading current `StatusHud.cs` and scene bootstrap state.

- [ ] **Step 1: Write failing UI tests**

Use Unity MCP.

```csharp
using NUnit.Framework;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestUiTests
    {
        [Test]
        public void QuestLogPanel_BindWithoutQuestLog_DoesNotThrow()
        {
            var go = new GameObject("QuestLogPanel");
            var panel = go.AddComponent<QuestLogPanel>();

            Assert.DoesNotThrow(() => panel.Bind(null));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DialoguePanel_OpenClose_TracksVisibleState()
        {
            var go = new GameObject("DialoguePanel");
            var panel = go.AddComponent<DialoguePanel>();

            panel.Open(null, default);
            Assert.IsTrue(panel.IsOpen);
            panel.Close();
            Assert.IsFalse(panel.IsOpen);

            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Expected: compile failure for UI classes.

- [ ] **Step 3: Implement DialoguePanel**

Use Unity MCP.

```csharp
using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class DialoguePanel : MonoBehaviour
    {
        private DialogueDefinition _dialogue;
        private DialogueChoiceContext _context;

        public bool IsOpen { get; private set; }

        public void Open(DialogueDefinition dialogue, DialogueChoiceContext context)
        {
            _dialogue = dialogue;
            _context = context;
            IsOpen = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            IsOpen = false;
            _dialogue = null;
            gameObject.SetActive(false);
        }

        public bool Choose(int choiceIndex)
        {
            if (_dialogue == null || _dialogue.Choices == null)
            {
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= _dialogue.Choices.Length)
            {
                return false;
            }

            return _dialogue.Choices[choiceIndex] != null
                && _dialogue.Choices[choiceIndex].TryExecute(in _context);
        }
    }
}
```

- [ ] **Step 4: Implement QuestLogPanel**

Use Unity MCP.

```csharp
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestLogPanel : MonoBehaviour
    {
        private QuestLog _questLog;

        public QuestLog QuestLog => _questLog;

        public void Bind(QuestLog questLog)
        {
            _questLog = questLog;
        }
    }
}
```

- [ ] **Step 5: Implement QuestRewardButton**

Use Unity MCP.

```csharp
using Rootborn.Game.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestRewardButton : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private QuestLog _questLog;
        private QuestDefinition _quest;
        private RewardRuntimeContext _context;

        public void Bind(QuestLog questLog, QuestDefinition quest, RewardRuntimeContext context)
        {
            _questLog = questLog;
            _quest = quest;
            _context = context;
            Refresh();
        }

        public void Refresh()
        {
            if (_button != null)
            {
                _button.interactable = _questLog != null && _questLog.CanClaimReward(_quest, in _context);
            }
        }

        public bool Click()
        {
            bool claimed = _questLog != null && _questLog.ClaimReward(_quest, in _context);
            Refresh();
            return claimed;
        }
    }
}
```

- [ ] **Step 6: Run UI tests**

Run Unity MCP `tests-run` filtered to `QuestUiTests`.

Expected: PASS.

- [ ] **Step 7: Integrate into Farm UI setup**

Read the current Farm UI setup before editing. Add only a minimal button/panel host that can be opened from NPC interaction. Keep the quest UI in `Rootborn.UI.Quests`; do not add quest state logic to `StatusHud.cs`.

- [ ] **Step 8: Commit**

```powershell
git add -- Assets/Scripts/UI/Quests Assets/Tests/EditMode/Quests/QuestUiTests.cs
git commit -m "[UI][TEST] 플레이 가능한 퀘스트 UI 패널 추가"
```

### Task 14: Default SO Data And Registry Wiring

**Files:**
- Modify: `Assets/Scripts/Editor/Tools/GenerateDefaultData.cs`
- Create assets through Unity Editor/MCP:
  - `Assets/Data/Story/StoryFlag_FirstQuestComplete.asset`
  - `Assets/Data/Quests/Objectives/Objective_GatherWood.asset`
  - `Assets/Data/Quests/Rewards/Reward_WoodQuestStone.asset`
  - `Assets/Data/Quests/Effects/Effect_SetFirstQuestComplete.asset`
  - `Assets/Data/Quests/Quest_GatherWood.asset`
  - `Assets/Data/Dialogue/Dialogue_FirstNpc.asset`
  - `Assets/Data/Dialogue/Choice_AcceptGatherWood.asset`
  - `Assets/Data/Dialogue/Choice_ClaimGatherWood.asset`
  - `Assets/Data/NPCs/Npc_FirstGuide.asset`
- Modify: `Assets/Resources/GameDataRegistry.asset` or generated registry output.

- [ ] **Step 1: Write failing asset/registry validation test**

Add to `QuestRegistryTests.cs`:

```csharp
[Test]
public void QUEST_014_DefaultQuestData_IsRegistered()
{
    var registry = Resources.Load<GameDataRegistry>("GameDataRegistry");

    Assert.IsNotNull(registry);
    Assert.IsNotEmpty(registry.Quests);
    Assert.IsNotEmpty(registry.Npcs);
    Assert.IsNotEmpty(registry.Dialogues);
    Assert.IsNotEmpty(registry.StoryFlags);
}
```

- [ ] **Step 2: Run test to verify RED**

Expected: FAIL because registry has no quest data.

- [ ] **Step 3: Generate folders and SO assets through Unity MCP**

Use `assets_create_folder` for folders and `assets_modify`/Editor tooling for SO assets. Do not manually edit `.asset` YAML unless Unity MCP asset operations cannot express an array insertion and the file has been inspected first.

- [ ] **Step 4: Extend GenerateDefaultData**

Use Unity MCP. Add quest data creation after item/resource creation:

```csharp
EnsureFolder($"{DataRoot}/Quests");
EnsureFolder($"{DataRoot}/Quests/Objectives");
EnsureFolder($"{DataRoot}/Quests/Rewards");
EnsureFolder($"{DataRoot}/Quests/Effects");
EnsureFolder($"{DataRoot}/Dialogue");
EnsureFolder($"{DataRoot}/NPCs");
EnsureFolder($"{DataRoot}/Story");
```

Create/load each quest SO and wire fields with `SetField`, following existing `CreateOrLoad<T>` pattern.

- [ ] **Step 5: Register arrays in GameDataRegistry**

Update registry creation to include quest arrays and strategy arrays. Use the same SO instances created in the generator.

- [ ] **Step 6: Run registry test and validator**

Run Unity MCP `tests-run` filtered to `QuestRegistryTests`.

Run Unity MCP `reflection-method-call` or menu-equivalent validator if available for `GameDataRegistryValidator.Validate`.

Expected: PASS and validator logs `issues=0` for quest arrays.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Editor/Tools/GenerateDefaultData.cs Assets/Data/Quests Assets/Data/Dialogue Assets/Data/NPCs Assets/Data/Story Assets/Resources/GameDataRegistry.asset Assets/Tests/EditMode/Quests/QuestRegistryTests.cs
git commit -m "[ASSET][TEST] 기본 퀘스트 데이터와 레지스트리 등록"
```

### Task 15: Full PlayMode Scenario And Regressions

**Files:**
- Modify: `Assets/Tests/PlayMode/Quests/QuestDialogueScenarioTests.cs`
- Modify: `Assets/Tests/PlayMode/Quests/QuestProviderScenarioTests.cs`
- Modify scene/bootstrap files only if PlayMode test proves wiring is missing.

- [ ] **Step 1: Write full scenario test**

Add PlayMode scenario:

```csharp
[UnityTest]
public IEnumerator QUEST_015_NpcQuest_FullFlow_ClaimReward_SetsStoryFlag()
{
    var item = ScriptableObject.CreateInstance<ItemDefinition>();
    SetField(item, "_maxStack", 99);
    var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
    var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
    SetField(objective, "_targetResource", resource);
    SetField(objective, "_requiredCount", 1);
    var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
    SetField(reward, "_item", item);
    SetField(reward, "_count", 1);
    var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
    var effect = ScriptableObject.CreateInstance<SetStoryFlagCompletionEffect>();
    SetField(effect, "_flag", flag);
    var quest = ScriptableObject.CreateInstance<QuestDefinition>();
    SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
    SetField(quest, "_rewards", new QuestRewardBase[] { reward });
    SetField(quest, "_completionEffects", new QuestCompletionEffectBase[] { effect });

    var log = new QuestLog(new[] { quest });
    var inventory = new Inventory();
    var flags = new StoryFlagSet();
    var rewardContext = new RewardRuntimeContext(log, inventory, null, flags);

    Assert.IsTrue(log.Accept(quest));
    log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1", resource: resource));
    Assert.AreEqual(QuestState.Completed, log.GetState(quest));
    Assert.IsTrue(log.ClaimReward(quest, in rewardContext));

    yield return null;

    Assert.AreEqual(1, inventory.CountOf(item));
    Assert.IsTrue(flags.IsSet(flag));
    Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
}
```

- [ ] **Step 2: Run PlayMode quest tests**

Run Unity MCP `tests-run` PlayMode filtered to `Rootborn.Tests.PlayMode.Quests`.

Expected: PASS.

- [ ] **Step 3: Run required regression tests**

Run Unity MCP `tests-run` EditMode for:

```text
KnowledgeProgressTests
KnowledgeTriggerTests
GatherInteractorTests
InventoryUiTests
InventoryEquipmentScenarioTests
CropGrowthTests
ToolDefinitionStrategyTests
QuestProgressTests
QuestObjectiveTests
QuestRewardTests
QuestConditionTests
QuestSaveTests
QuestRegistryTests
DialogueQuestTests
```

Run Unity MCP `tests-run` PlayMode for:

```text
FarmingScenarioTests
QuestDialogueScenarioTests
QuestProviderScenarioTests
```

Expected: PASS.

- [ ] **Step 4: Run static entity branching gate**

Run the existing static usage gate:

```text
StaticRuntimeUsageGateTests
```

Expected: PASS. If quest/npc/resource/tool/crop ID branching appears in runtime code, remove the branch and replace it with SO strategy/reference matching.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Tests/PlayMode/Quests
git commit -m "[TEST] 퀘스트 전체 시나리오와 회귀 검증 추가"
```

### Task 16: Completion Audit

**Files:**
- No code changes unless audit finds a gap.

- [ ] **Step 1: Build prompt-to-artifact checklist**

Create an audit note in the final response mapping:

```text
QUEST-001 -> test file/method -> pass result
...
QUEST-015 -> test file/method -> pass result
Assets/**/*.cs safety -> MCP script-update-or-create usage
Data-driven SO rules -> files/assets/registry evidence
Reward inventory rule -> tests and implementation evidence
Existing regressions -> commands and pass/fail results
```

- [ ] **Step 2: Inspect current state**

Run:

```powershell
git status --short
git log -5 --oneline
```

Expected: only intentional changes remain. Do not revert user changes.

- [ ] **Step 3: Final test run**

Run the broadest feasible Unity MCP EditMode and PlayMode suites. If full PlayMode is too slow or blocked, run named quest and regression PlayMode tests and report the limitation.

- [ ] **Step 4: Final report**

Include:
- Modified files.
- Added SO/data.
- Added tests.
- Executed tests.
- Pass/fail result.
- Remaining risks.
- Any commands that could not be run and why.

---

## Self-Review Notes

- Spec coverage: Tasks 1-15 cover QUEST-001 through QUEST-015, data-driven SO strategy design, playable UI scope B, reward preflight/idempotency, save/load, registry registration, and regression tests.
- Completeness scan: The plan has concrete file paths, commands, expected outcomes, and target code shapes. Where implementation depends on current code shape, the step requires reading the current file before the Unity MCP edit and provides the target code shape.
- Type consistency: `QuestState`, `QuestEvent`, `QuestObjectiveBase`, `QuestProgress`, `QuestLog`, `RewardRuntimeContext`, `StoryFlagSet`, `DialogueChoiceContext`, and UI class names are consistent across tasks.
- Scope control: Combat defeat is implemented as deterministic `QuestEventKind.Defeat` with a defeat target SO reference. Real combat adapter wiring is not required until a combat domain exists, but QUEST-008 is covered by objective/event tests.
