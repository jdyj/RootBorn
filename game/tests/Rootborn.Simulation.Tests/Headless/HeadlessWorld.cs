using System;
using System.Collections.Generic;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Simulation.Headless
{
    /// <summary>
    /// 창 없는 월드: Host WorldScreen의 그리지 않는 부분과 같다(걷기·문은 Core WorldSession, NPC 자리는 일정과 학생 시각, 상호작용 후보,
    /// 가구 방이면 가구가 막는 칸을 더한 걷는 맵). 그림·카메라·낮밤 색은 없다.
    /// Host WorldScreen과 RoomAwareMaps를 옮겨 적은 것이라 둘이 바뀌면 같이 고친다(필요한 변경: 이 부분을 Rootborn.World로 빼면 중복이 없어진다).
    /// </summary>
    public sealed class HeadlessWorld
    {
        public readonly WorldSession Session;
        public readonly InteractionTargets Interactions;
        public readonly HeadlessRooms Rooms;
        private readonly PlayAtlas _atlas;
        private readonly NpcScheduleLookupCache _schedules;
        private readonly NpcScheduleAccess _access;
        private readonly NpcArt _npcArt;
        private readonly Func<int> _day;
        private readonly Func<float> _hour;
        private string _placedMap, _placedSlot;
        private int _placedDay;

        public HeadlessWorld(PlayAtlas atlas, IWorldArrivals arrivals, string startMap, string spawn, Func<int> day, Func<float> hour)
        {
            _atlas = atlas;
            _day = day;
            _hour = hour;
            Rooms = new HeadlessRooms(atlas.Maps, atlas.Interactions);
            Session = new WorldSession(Rooms, atlas.Maps.Catalog.Walker) { Arrivals = arrivals };
            if (!Session.Enter(startMap, spawn)) throw new InvalidOperationException(Session.LastProblem);
            Interactions = new InteractionTargets(atlas.Interactions.RadiusCells);
            _schedules = new NpcScheduleLookupCache(atlas.Definitions.All<NpcSchedule>());
            _access = new NpcScheduleAccess(_schedules);
            _npcArt = NpcArt.Load(atlas.RepoRoot);
            Refresh();
        }

        public FurnitureRoom Room => Rooms.RoomOf(Session.Map.Id);
        public MapInteractions HereData => _atlas.Interactions.Map(Session.Map.Id);

        public void Update(int dx, int dy, float dt)
        {
            Session.Update(dx, dy, dt);
            Refresh();
        }

        private void Refresh()
        {
            string map = Session.Map.Id;
            int day = _day();
            string slot = LocationStateTimeSlots.FromMinutes((int)(_hour() * 60f));
            if (map == _placedMap && slot == _placedSlot && day == _placedDay) { Interactions.Update(Session.Walker.X, Session.Walker.Y); return; }
            Place(map, day, slot);
        }

        /// <summary>
        /// 맵·날·시간대가 바뀌었을 때만. 따로 둔 까닭: 아래 지역 함수가 day·slot을 잡으면 그 메서드에 들어올 때마다 잡는 객체가 생긴다
        /// (Host WorldScreen.RefreshInteractions가 같은 모양으로 프레임마다 약 40바이트를 잡는다 — 보고함).
        /// </summary>
        private void Place(string map, int day, string slot)
        {
            _placedMap = map; _placedSlot = slot; _placedDay = day;
            MapInteractions here = HereData;
            string LocationOf(string npcId) => _schedules.TryGetByNpcId(npcId, out NpcSchedule s) ? _access.ResolveLocationId(s.Id, day, slot, null) : string.Empty;
            List<PlacedNpc> npcs = NpcPlacement.Place(_npcArt.Order, LocationOf, here);
            Interactions.MapId = map;
            Interactions.RadiusPixels = _atlas.Interactions.RadiusCells * Session.Map.Grid.CellSize;
            Interactions.SetCandidates(NpcPlacement.Candidates(_atlas.Interactions, here, npcs, Session.Map.Doors));
            Interactions.Update(Session.Walker.X, Session.Walker.Y);
        }
    }

    /// <summary>걷는 맵: 가구 방이면 가구가 막는 칸을 더한 맵(Host RoomAwareMaps와 같다).</summary>
    public sealed class HeadlessRooms : IWorldMapSource
    {
        private readonly WorldMapLoader _maps;
        private readonly InteractionData _interactions;
        private readonly Dictionary<string, FurnitureRoom> _rooms = new Dictionary<string, FurnitureRoom>(StringComparer.OrdinalIgnoreCase);

        public HeadlessRooms(WorldMapLoader maps, InteractionData interactions) { _maps = maps; _interactions = interactions; }

        public FurnitureRoom RoomOf(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return null;
            if (_rooms.TryGetValue(mapId, out FurnitureRoom room)) return room;
            LoadedMap loaded = _maps.LoadMap(mapId);
            room = string.IsNullOrEmpty(_interactions.Map(loaded.Entry.Id).PlacementSurface) ? null : new FurnitureRoom(loaded.World);
            _rooms[mapId] = room;
            _rooms[loaded.Entry.Id] = room;
            return room;
        }

        public WorldMap Load(string mapId) => RoomOf(mapId)?.WalkMap ?? ((IWorldMapSource)_maps).Load(mapId);
    }

    /// <summary>창 없는 배치 카메라: 방 전체를 1920x1080 가운데에 맞춘다(Host 배치 카메라의 "전체 보기"와 같은 모양).</summary>
    public sealed class HeadlessPlacementView : IPlacementView
    {
        private readonly Func<FurnitureRoom> _room;
        private readonly float _width, _height;

        public HeadlessPlacementView(Func<FurnitureRoom> room, float width, float height) { _room = room; _width = width; _height = height; }

        public FurnitureRoom Room => _room();

        private (float Scale, float X, float Y) Fit(FurnitureRoom room)
        {
            var g = room.Map.Grid;
            float scale = Math.Min(_width / g.PixelWidth, _height / g.PixelHeight) * 0.9f;
            return (scale, (_width - g.PixelWidth * scale) / 2f, (_height - g.PixelHeight * scale) / 2f);
        }

        public bool TryCellAt(float x, float y, out GridPoint cell)
        {
            cell = default;
            FurnitureRoom room = Room;
            if (room == null) return false;
            var (scale, ox, oy) = Fit(room);
            CellPos c = CellPos.Containing((x - ox) / scale, (y - oy) / scale, room.Map.Grid.CellSize);
            if (!room.Map.Grid.Contains(c.X, c.Y)) return false;
            cell = room.ToPlacement(c);
            return true;
        }

        public UiRect CellRect(GridPoint cell)
        {
            FurnitureRoom room = Room;
            if (room == null) return default;
            var (scale, ox, oy) = Fit(room);
            AreaRect a = room.CellArea(cell);
            return new UiRect(ox + a.X * scale, oy + a.Y * scale, a.W * scale, a.H * scale);
        }
    }
}
