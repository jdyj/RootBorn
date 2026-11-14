using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.World;

namespace Rootborn.Game
{
    /// <summary>
    /// 짓는 집의 꾸밈(방마다 벽지·바닥). 규칙(대상 칸, 바뀌는 칸, 비용, 거절 이유)은 Core <see cref="HouseFinishes"/>가 정한다. 여기서는 잇고 저장한다.
    /// 기록은 슬롯 공용 파일 house-build.json(<see cref="SaveFileNames.HouseBuild"/>)이고, 시작할 때 읽고 꾸밈이 바뀐 저장 묶음에서만 쓴다
    /// (읽기·붙이기만으로 쓰지 않는다). 프레임(출처표)은 Host가 짓는 집에 들어설 때 <see cref="EnterHouseBuild"/>로 넘긴다.
    /// 같이 쓰는 집은 아직 꾸밀 수 없고(이유 <see cref="HouseFinishReasons.SharedHouse"/>) 보이는 꾸밈도 프레임 기본이다(꾸밈 공유는 다음 계획).
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

        /// <summary>
        /// 짓는 집에 들어선다(Host가 부른다). 같은 프레임이면 다시 붙이지 않는다(떠나 있었으면 다시 연다). 저장하지 않는다.
        /// 알림: <see cref="GameChangeTopics.HouseBuild"/>(붙였거나 다시 열었을 때만).
        /// </summary>
        public void EnterHouseBuild(HouseBuildFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (ReferenceEquals(_houseFinishes.Frame, frame))
            {
                if (_inHouseBuild) return;
                _inHouseBuild = true;
                Notify(null, GameChangeTopics.HouseBuild);
                return;
            }
            _houseFinishes.Attach(frame, FindFinish);
            _inHouseBuild = true;
            Notify(null, GameChangeTopics.HouseBuild);
        }

        /// <summary>
        /// 짓는 집이 아닌 맵에 들어섰다(Host가 부른다): 꾸미기를 닫는다. 프레임과 기록은 그대로 들고 있다(버리지도 쓰지도 않는다) —
        /// 다시 들어오면 꾸밈이 그대로다. 이미 떠났으면 아무것도 하지 않는다. 알림: <see cref="GameChangeTopics.HouseBuild"/>.
        /// </summary>
        public void LeaveHouseBuild()
        {
            if (!_inHouseBuild) return;
            _inHouseBuild = false;
            Notify(null, GameChangeTopics.HouseBuild);
        }

        // 짓는 집 맵에 서 있다(EnterHouseBuild ~ LeaveHouseBuild). 집 배치(House)는 가구 방을 떠나도 남으므로 따로 든다.
        private bool _inHouseBuild;

        /// <summary>꾸밀 수 있는 집이 열려 있다: 짓는 집 맵에 있고(프레임이 붙었고), 집 배치가 있고, 일터가 아니다.</summary>
        public bool HouseBuildOpen => _inHouseBuild && _houseFinishes.Attached && House != null && ActiveWorkspace == null;

        // 같이 쓰는 집(방의 집)은 손님의 자기 꾸밈으로 그리지 않는다 — 꾸밈 공유가 생길 때까지 프레임 기본이다.
        // 자기 기록은 그대로 들고 있다(버리지도 쓰지도 않는다). 혼자 하는 집(공유 없는 판)에서는 자기 꾸밈이 그대로 보인다.
        private bool ShowsOwnFinishes => _roomHouse == null;

        /// <summary>그 바닥 칸의 보이는 벽지 id(기록이 없으면 프레임 기본, 붙지 않았으면 빈 글). 같이 쓰는 집은 프레임 기본.</summary>
        public string HouseWallAt(CellPos floorCell)
            => ShowsOwnFinishes ? _houseFinishes.WallAt(floorCell) : _houseFinishes.Frame?.DefaultWall ?? string.Empty;

        /// <summary>그 바닥 칸의 보이는 바닥 id. 같이 쓰는 집은 프레임 기본.</summary>
        public string HouseFloorAt(CellPos floorCell)
            => ShowsOwnFinishes ? _houseFinishes.FloorAt(floorCell) : _houseFinishes.Frame?.DefaultFloor ?? string.Empty;

        /// <summary>보이는 띠 색 줄 이름(고른 띠 꾸밈의 그림, 기록이 비거나 데이터에 없으면 프레임 기본). 같이 쓰는 집은 프레임 기본.</summary>
        public string HouseBandColors()
            => ShowsOwnFinishes ? _houseFinishes.BandColors : _houseFinishes.Frame?.DefaultBand ?? string.Empty;

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
            var readOnly = list.AsReadOnly(); // 부르는 쪽이 형 변환으로 기억한 목록을 고치지 못하게
            _finishOptions[key] = readOnly;
            return readOnly;
        }

        /// <summary>그 꾸밈을 쓸 수 있다(해금 토큰이 없거나 얻었다).</summary>
        public bool IsFinishUnlocked(RoomFinishDefinition finish)
            => HouseFinishes.IsUnlocked(finish, FurnitureUnlocks);

        /// <summary>그 장소의 가게에서 파는 꾸밈(정의 순서). 잠긴 꾸밈을 사는 길이다(가구 <see cref="FurnitureForSaleAt"/>와 같은 꼴).</summary>
        public IReadOnlyList<RoomFinishDefinition> FinishesForSaleAt(string locationId)
            => RoomFinishPurchase.ForSaleAt(_definitions.All<RoomFinishDefinition>(), locationId);

        /// <summary>
        /// 꾸밈을 산다: 값을 내고 해금 토큰을 얻는다(가구 해금과 같은 기록). 둘은 함께 바뀌고, 실패하면 아무것도 바뀌지 않는다.
        /// 이미 가졌으면 값을 받지 않는다. 알림: <see cref="GameChangeTopics.HouseBuild"/>, <see cref="GameChangeTopics.Furniture"/>, <see cref="GameChangeTopics.Inventory"/>.
        /// </summary>
        public FurniturePurchaseResult PurchaseFinish(string finishId)
        {
            var finish = FindFinish(finishId);
            if (finish == null) return FurniturePurchaseResult.NotForSale;
            var priceItem = FindItem(finish.PriceItemId);
            return Shared(() =>
            {
                var result = RoomFinishPurchase.Try(finish, priceItem, Inventory, FurnitureUnlocks);
                return Saved(result, result == FurniturePurchaseResult.Purchased, null,
                    GameChangeTopics.HouseBuild, GameChangeTopics.Furniture, GameChangeTopics.Inventory);
            }, r => r == FurniturePurchaseResult.Purchased);
        }

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
            => Shared(() => CommitFinish(PreviewHouseFinish(finishId, tool, pointer)), done => done);

        /// <summary>띠 색 한 번의 미리 보기(바꾸지 않는다). 거절 이유는 <see cref="PreviewHouseFinish"/>와 같다(집 밖, 같이 쓰는 집).</summary>
        public HouseFinishPreview PreviewHouseBand(string finishId)
        {
            if (!HouseBuildOpen) return HouseFinishPreview.Refused(finishId, SlotOf(finishId), string.Empty, HouseFinishReasons.NotInHouse);
            if (_roomHouse != null) return HouseFinishPreview.Refused(finishId, SlotOf(finishId), string.Empty, HouseFinishReasons.SharedHouse);
            return _houseFinishes.PreviewBand(finishId, FurnitureUnlocks, CanPayFinish);
        }

        /// <summary>
        /// 띠 색을 바꾼다: <see cref="ApplyHouseFinish"/>와 같은 길(미리 보기 → 한 바꾸기 → 한 저장 묶음). 지금 데이터는 무료라 빈 비용의 바꾸기다.
        /// 안 되면 false·무변경. 알림: <see cref="GameChangeTopics.HouseBuild"/>, <see cref="GameChangeTopics.Inventory"/>.
        /// </summary>
        public bool ApplyHouseBand(string finishId)
            => Shared(() => CommitFinish(PreviewHouseBand(finishId)), done => done);

        // 미리 보기 하나를 돈 빼기와 함께 한 바꾸기로 적용하고 한 저장 묶음으로 쓴다(칸 꾸밈·띠 색이 같이 쓴다).
        private bool CommitFinish(HouseFinishPreview preview)
        {
            if (!preview.CanApply || !TryGrants(preview.Cost, out var costs)) return false;
            bool done;
            try
            {
                // 적용이 거절되면 커밋 안에서 던져 바꾸기가 돈을 되돌리게 한다(ItemInventory의 WalletRefused와 같은 꼴). 돈만 빠지는 길이 없다.
                done = Inventory.TryExchange(costs, null, () =>
                {
                    HouseFinishCommitting?.Invoke();
                    if (!_houseFinishes.Apply(preview)) throw new FinishRefused();
                    _houseBuildDirty = true;
                });
            }
            catch (FinishRefused)
            {
                done = false;
            }
            return Saved(done, done, null, GameChangeTopics.HouseBuild, GameChangeTopics.Inventory);
        }

        /// <summary>시험용 이음매: 꾸밈 바꾸기 커밋 안, 적용 바로 앞에서 부른다(Game 테스트가 적용 거절을 일부러 만든다). 게임은 쓰지 않는다.</summary>
        internal Action HouseFinishCommitting;

        // 바꾸기 커밋 안에서 꾸밈 적용이 거절됐다(바꾸기가 되돌리고 ApplyHouseFinish가 받아 false로 바꾼다. 밖으로 나가지 않는다).
        private sealed class FinishRefused : Exception { }

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
