using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Save;
using Rootborn.Core.World;

namespace Rootborn.Game
{
    /// <summary>
    /// 짓는 집의 꾸밈(방마다 벽지·바닥). 규칙(대상 칸, 바뀌는 칸, 비용, 거절 이유)은 Core <see cref="HouseFinishes"/>가 정한다. 여기서는 잇고 저장한다.
    /// 기록은 슬롯 공용 파일 house-build.json(<see cref="SaveFileNames.HouseBuild"/>)이고, 시작할 때 읽고 꾸밈이 바뀐 저장 묶음에서만 쓴다
    /// (읽기·붙이기만으로 쓰지 않는다). 프레임(출처표)은 Host가 짓는 집에 들어설 때 <see cref="EnterHouseBuild"/>로 넘긴다.
    /// 같이 쓰는 집은 아직 꾸밀 수 없다(이유 <see cref="HouseFinishReasons.SharedHouse"/>).
    /// </summary>
    public sealed partial class GameSession
    {
        private readonly HouseFinishes _houseFinishes = new HouseFinishes();
        private bool _houseBuildDirty;
        private Dictionary<string, IReadOnlyList<RoomFinishDefinition>> _finishOptions;

        private void LoadHouseBuild(ISaveStore store)
        {
            store.TryRead<HouseBuildSaveData>(SaveSlot, SaveFileNames.HouseBuild, out var data);
            _houseFinishes.Load(data);
        }

        // 저장 묶음 안에서: 꾸밈이 바뀌었으면(붙었든 아니든) 기록 전체를 같은 묶음으로 쓴다. 읽은 기록(모르는 id, 지금 바닥이 아닌 칸)도 그대로 든다.
        private void SaveHouseBuildIfDirty(ISaveStore batch)
        {
            if (!_houseBuildDirty) return;
            batch.Write(SaveSlot, SaveFileNames.HouseBuild, _houseFinishes.ToSaveData());
        }

        /// <summary>짓는 집에 들어선다(Host가 부른다). 같은 프레임이면 다시 붙이지 않는다. 저장하지 않는다. 알림: <see cref="GameChangeTopics.HouseBuild"/>.</summary>
        public void EnterHouseBuild(HouseBuildFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (ReferenceEquals(_houseFinishes.Frame, frame)) return;
            _houseFinishes.Attach(frame, FindFinish);
            Notify(null, GameChangeTopics.HouseBuild);
        }

        /// <summary>꾸밀 수 있는 집이 열려 있다: 프레임이 붙었고, 집 배치가 있고, 일터가 아니다.</summary>
        public bool HouseBuildOpen => _houseFinishes.Attached && House != null && ActiveWorkspace == null;

        /// <summary>그 바닥 칸의 보이는 벽지 id(기록이 없으면 프레임 기본, 붙지 않았으면 빈 글).</summary>
        public string HouseWallAt(CellPos floorCell) => _houseFinishes.WallAt(floorCell);

        /// <summary>그 바닥 칸의 보이는 바닥 id.</summary>
        public string HouseFloorAt(CellPos floorCell) => _houseFinishes.FloorAt(floorCell);

        /// <summary>띠 색 줄 이름(기록이 비면 프레임 기본).</summary>
        public string HouseBandColors() => _houseFinishes.Band;

        /// <summary>꾸밈 상태가 바뀔 때마다 오른다(화면이 미리 보기를 다시 낼지 본다).</summary>
        public int HouseBuildVersion => _houseFinishes.Version;

        /// <summary>그 자리(<see cref="RoomFinishSlots"/>)의 꾸밈, 데이터 순서, 잠긴 것 포함.</summary>
        public IReadOnlyList<RoomFinishDefinition> HouseFinishOptions(string slot)
        {
            _finishOptions ??= new Dictionary<string, IReadOnlyList<RoomFinishDefinition>>(StringComparer.Ordinal);
            string key = slot ?? string.Empty;
            if (_finishOptions.TryGetValue(key, out var cached)) return cached;
            var list = new List<RoomFinishDefinition>();
            foreach (var finish in _definitions.All<RoomFinishDefinition>())
                if (string.Equals(finish.Slot, key, StringComparison.Ordinal)) list.Add(finish);
            _finishOptions[key] = list;
            return list;
        }

        /// <summary>그 꾸밈을 쓸 수 있다(해금 토큰이 없거나 얻었다).</summary>
        public bool IsFinishUnlocked(RoomFinishDefinition finish)
            => finish != null && (finish.UnlockToken.Length == 0 || (FurnitureUnlocks != null && FurnitureUnlocks.IsUnlocked(finish.UnlockToken)));

        /// <summary>꾸밈 한 번의 미리 보기(바꾸지 않는다). 집이 아니면 <see cref="HouseFinishReasons.NotInHouse"/>, 같이 쓰는 집이면 <see cref="HouseFinishReasons.SharedHouse"/>.</summary>
        public HouseFinishPreview PreviewHouseFinish(string finishId, string tool, CellPos pointer)
        {
            if (!HouseBuildOpen) return HouseFinishPreview.Refused(finishId, SlotOf(finishId), tool, HouseFinishReasons.NotInHouse);
            if (_roomHouse != null) return HouseFinishPreview.Refused(finishId, SlotOf(finishId), tool, HouseFinishReasons.SharedHouse);
            return _houseFinishes.Preview(finishId, tool, pointer, FurnitureUnlocks, CanPayFinish);
        }

        /// <summary>
        /// 꾸밈을 적용한다: 미리 보기를 새로 내고, 적용할 수 있으면 돈 빼기와 꾸밈 바꾸기를 한 바꾸기로 하고(메모리만) 한 저장 묶음으로 쓴다.
        /// 안 되면 false이고 아무것도 바뀌지 않는다. 같은 요청을 다시 하면 바뀌는 칸이 없어 false(돈이 들지 않는다). 비용이 빈 꾸밈도 같은 길이다.
        /// 알림: <see cref="GameChangeTopics.HouseBuild"/>, <see cref="GameChangeTopics.Inventory"/>.
        /// </summary>
        public bool ApplyHouseFinish(string finishId, string tool, CellPos pointer)
            => Shared(() =>
            {
                var preview = PreviewHouseFinish(finishId, tool, pointer);
                if (!preview.CanApply || !TryGrants(preview.Cost, out var costs)) return false;
                bool applied = false;
                bool exchanged = Inventory.TryExchange(costs, null, () =>
                {
                    applied = _houseFinishes.Apply(preview);
                    if (applied) _houseBuildDirty = true;
                });
                bool done = exchanged && applied;
                return Saved(done, done, null, GameChangeTopics.HouseBuild, GameChangeTopics.Inventory);
            }, done => done);

        private RoomFinishDefinition FindFinish(string id)
            => !string.IsNullOrEmpty(id) && _definitions.TryGet<RoomFinishDefinition>(id, out var finish) ? finish : null;

        private string SlotOf(string finishId) => FindFinish(finishId)?.Slot ?? string.Empty;

        private bool CanPayFinish(IReadOnlyList<ItemAmount<string>> cost)
            => TryGrants(cost, out var costs) && Inventory.CanExchange(costs, null);

        // 비용(아이템 id) → 가방 바꾸기 항목. 데이터에 없는 아이템이 있으면 낼 수 없다.
        private bool TryGrants(IReadOnlyList<ItemAmount<string>> cost, out InventoryGrant[] grants)
        {
            grants = new InventoryGrant[cost?.Count ?? 0];
            for (int i = 0; i < grants.Length; i++)
            {
                var item = FindItem(cost[i].Item);
                if (item == null) return false;
                grants[i] = new InventoryGrant(item, cost[i].Count);
            }
            return true;
        }
    }
}
