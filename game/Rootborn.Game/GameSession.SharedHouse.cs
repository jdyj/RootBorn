using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;

namespace Rootborn.Game
{
    /// <summary>
    /// 같이 쓰는 집(집과 가구 공유를 켠 방, 멀티플레이 설계 4.1절). 켜지면 집의 배치는 자기 파일이 아니라 방의 것이다:
    /// - 집에 들어오면(<see cref="EnterHouse"/>) 방이 알린 그 면의 가구로 <see cref="House"/>를 채운다. 인스턴스 id는 방이 준 것이다.
    /// - 놓기·옮기기·치우기는 지갑처럼 "요청 → 방이 허락한 뒤 적용"이다: 여기서는 요청만 적고 저장한다(<see cref="WaitingForRoom"/>).
    ///   방이 허락하면 바뀐 상태가 오고 그것으로 집을 다시 채운다(되돌리는 흐름 없음). 거절되면 아무것도 바뀌지 않는다(<see cref="LastRoomCommandApplied"/> false).
    /// - 남이 놓거나 옮기거나 치워도 상태가 오고 집을 다시 채운 뒤 Furniture를 알린다(Host가 가구 그림·충돌을 다시 맞춘다).
    /// - 놓을 수 있는 가구는 내가 해금한 것뿐이다(해금·구매는 각자). 해금은 놓아도 줄지 않으므로 누가 치워도 놓은 사람은 그 가구를 계속 가진다.
    /// - 자기 배치 파일은 쓰지도 지우지도 않는다(공유를 끈 방·혼자 하기의 집은 그대로 남는다).
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] SharedHouseTopics = { GameChangeTopics.Furniture, GameChangeTopics.Room };
        private RoomHouseClient _roomHouse;
        private IPlacementGrid _houseGrid;
        private string _houseWaiting;
        private readonly List<FurniturePlacementSaveData> _houseRecords = new List<FurniturePlacementSaveData>(16);
        private readonly List<string> _houseIds = new List<string>(16);

        /// <summary>집과 가구를 같이 쓰는 방이다(집의 배치가 방의 것).</summary>
        public bool SharesHouse => _roomHouse != null;

        private void UseSharedHouse(RoomShares shares) => _roomHouse = shares?.Find<RoomHouseClient>();

        private void ListenToSharedHouse()
        {
            if (_roomHouse != null) _roomHouse.Answered += OnHouseAnswered;
        }

        // 방이 알린 그 면의 가구로 집을 채운 판. 이 판은 스스로 저장하지 않는다(바꾸기는 방에 요청한다).
        private HouseFurnitureLayout SharedHouseLayout(IPlacementGrid grid, string surfaceId)
        {
            var layout = new HouseFurnitureLayout(new HousePlacementModel(grid), surfaceId, _ => false);
            _roomHouse.CopySurface(surfaceId, _houseRecords, _houseIds);
            // 방(RoomHouseRules.Layout)과 같은 Core 코드로 옮겨 본다 — 같은 기록이면 같은 자리다.
            _houseRelocation = layout.RestoreRelocating(_houseRecords, _definitions.All<FurnitureDefinition>(), _houseIds);
            return layout;
        }

        private void UseHouseLayout(HouseFurnitureLayout layout)
        {
            if (House != null) House.PlacementCommitted -= AnnouncePlacement;
            layout.PlacementCommitted += AnnouncePlacement;
            House = layout;
        }

        // 방의 집 상태가 바뀌었다(남이 바꿨거나 내 요청이 허락됐다): 집에 들어와 있으면 다시 채우고 알린다.
        private void FollowRoomHouse()
        {
            // 일터가 열려 있으면 따르지 않는다(일터는 각자 것이다). 집에 다시 들어오면 방의 배치로 채운다.
            if (_roomHouse == null || House == null || _houseGrid == null || ActiveWorkspace != null) return;
            UseHouseLayout(SharedHouseLayout(_houseGrid, House.SurfaceId));
            Notify(null, GameChangeTopics.Furniture);
        }

        private bool RequestSharedPlace(FurnitureDefinition furniture, GridPoint anchor, FurniturePlacementDirection direction)
            => RequestHouse(House.CanPlace(furniture, anchor, direction), () => _roomHouse.RequestPlace(House.SurfaceId, furniture.StableId, anchor, direction));

        private bool RequestSharedMove(string instanceId, GridPoint anchor, FurniturePlacementDirection direction)
            => RequestHouse(CanMoveShared(instanceId, anchor, direction), () => _roomHouse.RequestMove(instanceId, anchor, direction));

        private bool RequestSharedRemove(string instanceId) => RequestHouse(Find(instanceId) != null, () => _roomHouse.RequestRemove(instanceId));

        private bool RequestSharedClear() => RequestHouse(House.Placed.Count > 0, () => _roomHouse.RequestClear(House.SurfaceId));

        // 요청은 한 번에 하나만 기다린다(화면은 기다리는 동안 버튼을 다시 누를 수 없다). 요청은 저장과 함께(또는 저장 뒤 방 접속이) 방으로 간다.
        // 돌려주는 값은 "지금 바뀌었다"이므로 늘 false다 — 화면은 WaitingForRoom과 LastRoomCommandApplied로 결과를 고른다.
        private bool RequestHouse(bool allowed, Func<string> request)
        {
            if (!allowed || _houseWaiting != null) return false;
            LastRoomRefusal = false;
            LastRoomCommandApplied = null;
            _houseWaiting = request();
            Save();
            Notify(SharedHouseTopics);
            return false;
        }

        private void OnHouseAnswered(RoomRequestSaveData request, bool applied)
        {
            if (request.RequestId == _houseWaiting) _houseWaiting = null;
            LastRoomCommandApplied = applied;
            // 내가 새로 놓은 가구는 퀘스트·체인에 알린다(혼자 하기의 PlacementCommitted와 같다). 남이 놓은 것은 알리지 않는다.
            bool announced = false;
            if (applied && request.Kind == RoomHouse.Place && request.Ids.Length > 1
                && _definitions.TryGet<FurnitureDefinition>(request.Ids[1], out var furniture))
            {
                Announce(FurnitureQuestEvents.Placed(furniture, request.Ids[0], _newEventKey(FurnitureQuestEvents.Action)), true);
                announced = true;
            }

            SaveAndNotify(announced ? AnnounceTopics : null, SharedHouseTopics);
        }

        // 옮기기: 그 가구를 뺀 나머지 위에 놓을 수 있는지(방의 판정과 같다).
        private bool CanMoveShared(string instanceId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            var moving = Find(instanceId);
            if (moving == null || _houseGrid == null) return false;
            var records = new List<FurniturePlacementSaveData>(House.Placed.Count);
            var ids = new List<string>(House.Placed.Count);
            _roomHouse.CopySurface(House.SurfaceId, records, ids);
            int index = ids.IndexOf(instanceId);
            if (index < 0) return false;
            records.RemoveAt(index);
            ids.RemoveAt(index);
            var others = new HouseFurnitureLayout(new HousePlacementModel(_houseGrid), House.SurfaceId, _ => false);
            others.RestoreRelocating(records, _definitions.All<FurnitureDefinition>(), ids); // 방의 판정과 같게 옮겨 본다
            return others.CanPlace(moving.Definition, anchor, direction);
        }

        private PlacedFurniture Find(string instanceId)
        {
            if (House == null || string.IsNullOrEmpty(instanceId)) return null;
            foreach (var p in House.Placed) if (p.InstanceId == instanceId) return p;
            return null;
        }
    }
}
