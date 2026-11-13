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
        public void EnterHouse(IPlacementGrid grid, string surfaceId) => EnterPlacementSurface(grid, surfaceId);

        // 집으로 연다(EnterPlacementSurface가 그 면의 일터가 없을 때 부른다).
        private void EnterHouseLayout(IPlacementGrid grid, string surfaceId)
        {
            LoadHouse(grid, surfaceId);
            _houseEntered = true;
            // 열려 있던 일터의 점수는 놓인 가구로 냈다. 닫힌 뒤에는 파일 기록(되살리지 못한 기록 포함)으로 다시 낸다.
            _workspaceScores.Clear();
            if (ReplayPlacedFurniture()) SaveAndNotify(AnnounceTopics, GameChangeTopics.Furniture);
            // 옛 가구를 옮기거나 보관했으면 알림이 한 번 열리도록 가구 알림을 낸다(저장하지 않는다 — 읽기만으로 파일을 쓰지 않는다).
            else if (!_relocationNoticeTaken && _houseRelocation != null && (_houseRelocation.Moved > 0 || _houseRelocation.Stored > 0)) Notify(null, GameChangeTopics.Furniture);
        }

        /// <summary>
        /// 집 밖에서 집 배치를 읽어 둔다(아직 <see cref="House"/>가 없을 때만): 집 맵의 걷는 격자에 가구가 막는 칸을 넣으려고(NPC 자리는 플레이어가
        /// 어느 맵에 있든 같아야 한다). <see cref="EnterHouse"/>와 같은 배치를 읽지만 체인 단계에 놓인 가구를 다시 알리지 않는다 — 그것은 집에 들어설 때 한다.
        /// 같이 쓰는 집이면 방이 알린 배치이고, 그 뒤 방의 집이 바뀌면 다른 맵에 있어도 따라간다. 알림: 읽었으면 <see cref="GameChangeTopics.Furniture"/>.
        /// </summary>
        /// <remarks>일터 면(data/workspaces)이면 아무것도 하지 않는다: 집 기록을 일터 방에 집 배치로 열면 집 가구가 일터 격자에 들고 House가 일터 면의 집이 된다.</remarks>
        public void PrepareHouse(IPlacementGrid grid, string surfaceId)
        {
            if (House != null || (surfaceId != null && WorkspaceBySurface.ContainsKey(surfaceId))) return;
            LoadHouse(grid, surfaceId);
            Notify(null, GameChangeTopics.Furniture);
        }

        // 이 판(메모리)에서 집에 들어선 적이 있는가. 저장하지 않는다. 다음 날 시작의 다시 알리기는 이때만 한다(PrepareHouse가 읽어 둔 배치로는 달라지지 않는다).
        private bool _houseEntered;

        private void LoadHouse(IPlacementGrid grid, string surfaceId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            _houseGrid = grid;
            HouseFurnitureLayout layout;
            if (_roomHouse != null) layout = SharedHouseLayout(grid, surfaceId); // 같이 쓰는 집: 방이 알린 배치(GameSession.SharedHouse)
            else
            {
                // 집 맵이 바뀌어 못 놓는 옛 기록은 가까운 자리로 옮기고 나머지는 보관한다(설계 7.3절). 읽기만으로 파일을 쓰지 않는다.
                layout = new HouseFurnitureLayout(new HousePlacementModel(grid), surfaceId, CommitHouseLayout);
                _houseRelocation = layout.RestoreRelocating(FurnitureSaveFiles.LoadHouseLayout(_store, SaveSlot, HouseStage), _definitions.All<FurnitureDefinition>());
            }

            UseHouseLayout(layout);
        }

        // 마지막으로 집을 열 때 옮긴 수·보관한 수(메모리만). 알림은 이 판에서 한 번만(_relocationNoticeTaken).
        private HouseFurnitureRelocation _houseRelocation;
        private bool _relocationNoticeTaken;

        /// <summary>
        /// 집 배치를 읽어 둔 뒤(<see cref="EnterHouse"/>나 <see cref="PrepareHouse"/>, 집을 나온 뒤에도)에 놓지 못해 보관한 가구(기록 순서, <see cref="HouseFurnitureLayout.Stored"/>).
        /// 집 배치를 아직 읽지 않았거나 일터가 열려 있으면 빈 목록. 집 밖(마을)에서 불러도 된다.
        /// 같이 쓰는 집이면 방의 기록 중 보관한 것이다(꺼내기는 아직 안 된다 — <see cref="PlaceStoredFurniture"/>).
        /// </summary>
        public IReadOnlyList<StoredFurniture> StoredFurniture()
            => House != null && ActiveWorkspace == null ? House.Stored : (IReadOnlyList<StoredFurniture>)Array.Empty<StoredFurniture>();

        /// <summary>보관한 가구(<see cref="Rootborn.Core.Placement.StoredFurniture.Index"/>)를 그 자리·방향에 놓을 수 있는가. 혼자 하는 집에서만.</summary>
        public bool CanPlaceStoredFurniture(int index, GridPoint anchor, FurniturePlacementDirection direction)
            => MayPlaceStored() && House.CanPlaceStored(index, anchor, direction);

        /// <summary>
        /// 보관한 가구를 그 자리·방향에 다시 놓는다(상자는 내용물째). 배치 파일에 한 번 저장되고 Furniture를 알린다. 맞지 않거나 저장이 실패하면 아무것도 바뀌지 않는다.
        /// 같은 index를 다시 부르면 false다(이미 놓았다). 같이 쓰는 집·일터에서는 false(같이 쓰는 집은 다음 계획에서 방 요청으로 연다).
        /// 가구 배치 퀘스트 이벤트는 내지 않는다(새로 놓은 가구가 아니다).
        /// </summary>
        public bool PlaceStoredFurniture(int index, GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (!MayPlaceStored() || !House.TryPlaceStored(index, anchor, direction)) return false;
            Notify(null, GameChangeTopics.Furniture, null);
            return true;
        }

        private bool MayPlaceStored() => House != null && ActiveWorkspace == null && _roomHouse == null;

        /// <summary>
        /// 집에 들어선 뒤 이 판에서 처음 한 번: 집을 열며 옮긴 가구 수와 보관한 가구 수. 둘 다 0이거나, 이미 받았거나, 집이 열려 있지 않으면 false.
        /// 메모리만이고 저장하지 않는다 — 옮긴 자리는 플레이어가 무엇을 바꿀 때 파일에 쓰이므로, 그 전에 끝낸 판은 다음 실행에서 다시 알린다.
        /// </summary>
        public bool TryTakeHouseRelocationNotice(out int moved, out int stored)
        {
            moved = 0;
            stored = 0;
            if (_relocationNoticeTaken || !_houseEntered || House == null || ActiveWorkspace != null || _houseRelocation == null) return false;
            if (_houseRelocation.Moved == 0 && _houseRelocation.Stored == 0) return false;
            moved = _houseRelocation.Moved;
            stored = _houseRelocation.Stored;
            _relocationNoticeTaken = true;
            return true;
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
            if (ActiveWorkspace != null) return PlaceInWorkspace(furniture, anchor, direction); // 일터: 각자 것, 한도(GameSession.Workspace)
            if (_roomHouse != null) return RequestSharedPlace(furniture, anchor, direction);
            bool placed = House.TryPlace(furniture, anchor, direction);
            bool grown = placed && GrowByPlacing(House.SurfaceId);
            return Saved(placed, placed, AnnounceTopics, GameChangeTopics.Furniture, grown ? GameChangeTopics.Workspace : null);
        }

        /// <summary>놓인 가구를 옮긴다(instanceId는 House.Placed의 InstanceId). 다시 알리지 않는다.</summary>
        /// <remarks>
        /// 같이 쓰는 집이면 놓기·옮기기·치우기는 방에 요청만 하고 false다(허락이 오면 바뀐다 — GameSession.SharedHouse).
        /// 일터는 자기 시설일 때만 된다(한도와 무관).
        /// </remarks>
        public bool MoveFurniture(string instanceId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (!MayChangeOpenLayout()) return false;
            if (House != null && SharedHouseOpen) return RequestSharedMove(instanceId, anchor, direction);
            return FurnitureChanged(House != null && House.TryMove(instanceId, anchor, direction));
        }

        public bool RemoveFurniture(string instanceId)
        {
            if (!MayChangeOpenLayout()) return false;
            if (House != null && SharedHouseOpen) return RequestSharedRemove(instanceId);
            return FurnitureChanged(House != null && House.Delete(instanceId));
        }

        /// <summary>놓인 가구를 모두 치운다. 놓지 못했던 기록은 남긴다.</summary>
        public bool ClearFurniture()
        {
            if (!MayChangeOpenLayout()) return false;
            if (House != null && SharedHouseOpen) return RequestSharedClear();
            return FurnitureChanged(House != null && House.ClearUserFurniture());
        }

        // 같이 쓰는 집이 열려 있다(일터는 방에 있어도 각자 것이라 공유 길을 타지 않는다).
        private bool SharedHouseOpen => _roomHouse != null && ActiveWorkspace == null;

        // 남의 일터는 바꾸지 못한다.
        private bool MayChangeOpenLayout() => ActiveWorkspace == null || OwnsActiveWorkspace;

        // 옮기기·치우기는 배치 기록만 바꾼다. 집은 그 기록을 House가 바로 쓰므로(CommitHouseLayout) 알리기만 한다.
        // 일터는 commit이 표시만 세우므로(CommitWorkspaceLayout) 저장 묶음으로 쓰고 알린다.
        private bool FurnitureChanged(bool changed)
        {
            if (!changed) return false;
            if (ActiveWorkspace != null) SaveAndNotify(null, GameChangeTopics.Furniture, GameChangeTopics.Workspace);
            else Notify(null, GameChangeTopics.Furniture, null);
            return true;
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

        // Unity AnnouncePlacedFurniture: 일찍 놓은 가구도 그 단계가 시작된 뒤 방에 들어오거나 (이 판에서 집에 들어간 적이 있으면) 새 날이 시작되면 센다.
        private bool ReplayPlacedFurniture()
        {
            // 가구 퀘스트 이벤트는 집에서만 낸다(일터는 내지 않는다).
            if (House == null || ActiveWorkspace != null || !FurnitureQuestEvents.AnyCurrentStepWantsFurniture(QuestChains, _chains)) return false;
            var replays = FurnitureQuestEvents.Replays(House, _newEventKey);
            foreach (var replay in replays) Announce(replay, true);
            return replays.Count > 0;
        }
    }
}
