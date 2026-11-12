using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Game
{
    /// <summary>
    /// 가구: 해금(퀘스트 보상·가게 구매), 집 방의 배치, 배치가 퀘스트에 알리는 이벤트.
    /// 해금은 성인 직업 스냅샷의 FurnitureUnlocks에 저장한다(스냅샷이 없던 슬롯은 독립 해금 파일에서 읽는다, Unity 판과 같음).
    /// 배치는 집 단계마다 파일 하나(house-furniture-layout-stage-N.json)이고, 단계 번호는 house-state.json에서 읽는다.
    /// 방 칸은 Host가 집 맵에서 만들어 <see cref="EnterHouse"/>로 넘긴다. 넘기기 전에는 배치 명령을 받지 않는다.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>얻은 가구 해금(읽기용).</summary>
        public FurnitureUnlockProgress FurnitureUnlocks { get; private set; }

        /// <summary>집 방의 가구 배치(읽기용: Placed, BlockingCells, CanPlace). 집에 들어오기 전에는 null이다.</summary>
        public HouseFurnitureLayout House { get; private set; }

        /// <summary>지금 집 단계 번호(house-state.json). 배치 파일이 단계마다 따로다.</summary>
        public int HouseStage { get; private set; }

        private void LoadFurniture(ISaveStore store, AdultCareerSnapshotSaveData snapshot)
        {
            if (snapshot != null)
            {
                // 스냅샷이 해금의 원본이다. 슬롯·플레이어는 지금 것으로 읽는다(복사한 슬롯).
                FurnitureUnlocks = new FurnitureUnlockProgress(SaveSlot, PlayerId);
                var tokens = snapshot.FurnitureUnlocks?.UnlockTokens;
                if (tokens != null) foreach (var token in tokens) FurnitureUnlocks.TryUnlock(token);
            }
            else
            {
                FurnitureUnlocks = Rebind(FurnitureSaveFiles.LoadUnlocksOrCreate(store, SaveSlot, PlayerId));
            }

            HouseStage = FurnitureSaveFiles.LoadHouseStage(store, SaveSlot);
        }

        /// <summary>
        /// 집 방에 들어온다(Unity AuthoredHousePlacementBinding.Bind): 지금 단계의 배치 기록을 방에 다시 놓고,
        /// 가구를 기다리는 체인 단계가 있으면 놓인 가구를 다시 알린다. surfaceId는 방의 면 id다(집은 데이터의 "house").
        /// 들어올 때마다 기록을 새로 읽는다. 놓지 못한 기록은 버리지 않고 함께 저장한다.
        /// </summary>
        public void EnterHouse(IPlacementGrid grid, string surfaceId)
        {
            LoadHouse(grid, surfaceId);
            if (ReplayPlacedFurniture()) SaveAndNotify(AnnounceTopics, GameChangeTopics.Furniture);
        }

        /// <summary>
        /// 집 밖에서 집 배치를 읽어 둔다(아직 <see cref="House"/>가 없을 때만): 집 맵의 걷는 격자에 가구가 막는 칸을 넣으려고(NPC 자리는 플레이어가
        /// 어느 맵에 있든 같아야 한다). <see cref="EnterHouse"/>와 같은 배치를 읽지만 체인 단계에 놓인 가구를 다시 알리지 않는다 — 그것은 집에 들어설 때 한다.
        /// 같이 쓰는 집이면 방이 알린 배치이고, 그 뒤 방의 집이 바뀌면 다른 맵에 있어도 따라간다. 알림: 읽었으면 <see cref="GameChangeTopics.Furniture"/>.
        /// </summary>
        public void PrepareHouse(IPlacementGrid grid, string surfaceId)
        {
            if (House != null) return;
            LoadHouse(grid, surfaceId);
            Notify(null, GameChangeTopics.Furniture);
        }

        private void LoadHouse(IPlacementGrid grid, string surfaceId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            _houseGrid = grid;
            HouseFurnitureLayout layout;
            if (_roomHouse != null) layout = SharedHouseLayout(grid, surfaceId); // 같이 쓰는 집: 방이 알린 배치(GameSession.SharedHouse)
            else
            {
                layout = new HouseFurnitureLayout(new HousePlacementModel(grid), surfaceId, CommitHouseLayout);
                layout.Restore(FurnitureSaveFiles.LoadHouseLayout(_store, SaveSlot, HouseStage), _definitions.All<FurnitureDefinition>());
            }

            UseHouseLayout(layout);
        }

        /// <summary>배치 목록에 보이는 가구(해금이 필요 없거나 해금한 것, 얻은 가구가 먼저).</summary>
        public IReadOnlyList<FurnitureDefinition> AvailableFurniture() => FurnitureCatalog.Available(_definitions.All<FurnitureDefinition>(), FurnitureUnlocks);

        /// <summary>그 장소의 가게에서 파는 가구(정의 순서).</summary>
        public IReadOnlyList<FurnitureDefinition> FurnitureForSaleAt(string locationId) => FurnitureCatalog.ForSaleAt(_definitions.All<FurnitureDefinition>(), locationId);

        /// <summary>가구를 산다: 값을 내고 해금을 얻는다. 둘은 함께 바뀌고, 실패하면 아무것도 바뀌지 않는다.</summary>
        public FurniturePurchaseResult PurchaseFurniture(string furnitureId)
        {
            if (!_definitions.TryGet<FurnitureDefinition>(furnitureId, out var furniture)) return FurniturePurchaseResult.NotForSale;
            return Shared(() =>
            {
                var result = FurniturePurchase.Try(furniture, Inventory, FurnitureUnlocks);
                return Saved(result, result == FurniturePurchaseResult.Purchased, null, GameChangeTopics.Furniture, GameChangeTopics.Inventory);
            }, r => r == FurniturePurchaseResult.Purchased);
        }

        /// <summary>목록에 보이는 가구를 방에 놓는다. 놓으면 가구 배치 이벤트를 퀘스트·체인에 알린다.</summary>
        public bool PlaceFurniture(string furnitureId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (House == null || !_definitions.TryGet<FurnitureDefinition>(furnitureId, out var furniture)
                || !FurnitureCatalog.IsAvailable(furniture, FurnitureUnlocks)) return false;
            if (_roomHouse != null) return RequestSharedPlace(furniture, anchor, direction);
            bool placed = House.TryPlace(furniture, anchor, direction);
            return Saved(placed, placed, AnnounceTopics, GameChangeTopics.Furniture);
        }

        /// <summary>놓인 가구를 옮긴다(instanceId는 House.Placed의 InstanceId). 다시 알리지 않는다.</summary>
        /// <remarks>같이 쓰는 집이면 놓기·옮기기·치우기는 방에 요청만 하고 false다(허락이 오면 바뀐다 — GameSession.SharedHouse).</remarks>
        public bool MoveFurniture(string instanceId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (House != null && _roomHouse != null) return RequestSharedMove(instanceId, anchor, direction);
            return FurnitureChanged(House != null && House.TryMove(instanceId, anchor, direction));
        }

        public bool RemoveFurniture(string instanceId)
        {
            if (House != null && _roomHouse != null) return RequestSharedRemove(instanceId);
            return FurnitureChanged(House != null && House.Delete(instanceId));
        }

        /// <summary>놓인 가구를 모두 치운다. 놓지 못했던 기록은 남긴다.</summary>
        public bool ClearFurniture()
        {
            if (House != null && _roomHouse != null) return RequestSharedClear();
            return FurnitureChanged(House != null && House.ClearUserFurniture());
        }

        // 옮기기·치우기는 배치 기록만 바꾸고 그 기록은 House가 바로 쓴다(CommitHouseLayout). 여기서는 알리기만 한다.
        private bool FurnitureChanged(bool changed)
        {
            if (changed) Notify(null, GameChangeTopics.Furniture);
            return changed;
        }

        private bool CommitHouseLayout(FurniturePlacementSaveData[] records)
        {
            try
            {
                FurnitureSaveFiles.SaveHouseLayout(_store, SaveSlot, HouseStage, records);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private void AnnouncePlacement(FurnitureDefinition furniture)
            => Announce(FurnitureQuestEvents.Placed(furniture, House.SurfaceId, _newEventKey(FurnitureQuestEvents.Action)), true);

        // Unity AnnouncePlacedFurniture: 일찍 놓은 가구도 그 단계가 시작된 뒤 방에 들어오거나 새 날이 시작되면 센다.
        private bool ReplayPlacedFurniture()
        {
            if (House == null || !FurnitureQuestEvents.AnyCurrentStepWantsFurniture(QuestChains, _chains)) return false;
            var replays = FurnitureQuestEvents.Replays(House, _newEventKey);
            foreach (var replay in replays) Announce(replay, true);
            return replays.Count > 0;
        }
    }
}
