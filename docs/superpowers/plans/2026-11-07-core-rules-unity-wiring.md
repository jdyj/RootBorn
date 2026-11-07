# Rootborn.Core 규칙을 게임에 연결하기 (Unity 필요)

2026-11-07. 순수 규칙(`QuestTracker`, `InventoryPlanner`, `WearableSave`)은 dotnet test로 검증해 커밋했다. 아래는 Unity 컴파일과 테스트가 필요해 아직 적용하지 않은 게임 쪽 연결이다.

1. 아래 QuestProgress 코드로 `Assets/Scripts/Game/Quests/QuestProgress.cs` 교체 (기존 파일 수정이라 직접 쓰기 허용).
2. `Assets/Scripts/Game/Rootborn.Game.asmdef` references에 `"Rootborn.Core"` 추가.
3. QuestState에 QuestStage와 같은 순서·이름임을 지키는 EditMode 테스트 1개 (값 4개 이름/순서 비교).
4. EditMode `Quests` 필터 + 퀘스트 PlayMode 실제 입력 테스트로 확인 후 커밋.
5. 새 core 파일 `.meta` (Characters/WearableSave.cs, Quests/QuestTracker.cs, Inventory/InventoryPlanner.cs, Quests·Inventory 폴더) 생성분 커밋.
6. 아래 TryBuildExchange 코드 적용 (Inventory 교환 계획을 Rootborn.Core.InventoryPlanner로). 보상·인벤토리 EditMode/PlayMode 테스트로 확인.

주의: Unity asmdef는 autoReferenced만으로 다른 asmdef를 참조하지 않는다. Rootborn.Game 등 쓰는 어셈블리마다 `Rootborn.Core`를 references에 넣는다.

## QuestProgress.cs

```csharp
using Rootborn.Core.Quests;

namespace Rootborn.Game.Quests
{
    /// <summary>Game-facing wrapper; the progress rules live in Rootborn.Core (QuestTracker) and are tested with dotnet test.</summary>
    public sealed class QuestProgress
    {
        private readonly QuestTracker _tracker;

        public QuestProgress(int objectiveCount)
        {
            _tracker = new QuestTracker(objectiveCount);
        }

        public QuestState State => (QuestState)_tracker.Stage;
        public int ObjectiveCount => _tracker.ObjectiveCount;

        public bool TryAccept() => _tracker.TryAccept();
        public int GetObjectiveCount(int objectiveIndex) => _tracker.GetObjectiveCount(objectiveIndex);

        public bool TryAddObjectiveCount(int objectiveIndex, int delta, int requiredCount, string eventKey)
            => _tracker.TryAddObjectiveCount(objectiveIndex, delta, requiredCount, eventKey);

        public bool TryMarkRewardClaimed() => _tracker.TryMarkRewardClaimed();

        public QuestProgressSaveData ToSaveData(string questId)
        {
            var snapshot = _tracker.ToSnapshot();
            var state = (QuestState)snapshot.Stage;
            return new QuestProgressSaveData
            {
                QuestId = questId,
                State = state,
                StateName = state.ToString(),
                ObjectiveCounts = snapshot.ObjectiveCounts,
                ProcessedEventKeys = snapshot.ProcessedEventKeys,
            };
        }

        public void LoadFromSaveData(QuestProgressSaveData data)
        {
            if (data == null) return;
            _tracker.Restore((QuestStage)data.State, data.StateName, data.ObjectiveCounts, data.ProcessedEventKeys);
        }
    }
}
```

## Inventory.TryBuildExchange

```csharp
// Replace Inventory.TryBuildExchange in Assets/Scripts/Game/Common/GameDataRegistry.cs (add `using Rootborn.Core.Inventory;`):
        private bool TryBuildExchange(IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards, out List<Slot> next)
        {
            var stacks = new List<ItemStack<ItemDefinition>>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++) stacks.Add(new ItemStack<ItemDefinition>(_slots[i].Item, _slots[i].Count));
            next = null;
            if (!InventoryPlanner.TryPlanExchange(stacks, MaxSlots, ToAmounts(costs), ToAmounts(rewards), item => item.MaxStack, out var planned))
                return false;
            next = new List<Slot>(planned.Count);
            for (int i = 0; i < planned.Count; i++) next.Add(new Slot { Item = planned[i].Item, Count = planned[i].Count });
            return true;
        }

        private static List<ItemAmount<ItemDefinition>> ToAmounts(IReadOnlyList<InventoryGrant> grants)
        {
            if (grants == null) return null;
            var amounts = new List<ItemAmount<ItemDefinition>>(grants.Count);
            for (int i = 0; i < grants.Count; i++) amounts.Add(new ItemAmount<ItemDefinition>(grants[i].Item, grants[i].Count));
            return amounts;
        }
```
