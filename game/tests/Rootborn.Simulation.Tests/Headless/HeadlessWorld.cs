using System;
using System.Collections.Generic;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Simulation.Headless
{
    /// <summary>
    /// 창 없는 월드: Host WorldScreen의 그리지 않는 부분과 같다(걷기·문은 Core WorldSession, NPC 자리는 Host와 같은 NpcStage가 일정과 그날의 분으로,
    /// 상호작용 후보, 가구 방이면 가구가 막는 칸을 더한 걷는 맵). 그림·카메라·낮밤 색은 없다.
    /// RoomAwareMaps를 옮겨 적은 HeadlessRooms가 있어 Host와 바뀌면 같이 고친다.
    /// </summary>
    public sealed class HeadlessWorld
    {
        public readonly WorldSession Session;
        public readonly InteractionTargets Interactions;
        public readonly HeadlessRooms Rooms;
        /// <summary>걷는 NPC(Host와 같은 공장으로 만든다). 테스트 드라이버는 TryPose로 NPC를 찾아간다.</summary>
        public readonly NpcStage Npcs;
        /// <summary>NPC 순서(data/registry.json npcs).</summary>
        public readonly IReadOnlyList<string> NpcIds;
        private readonly PlayAtlas _atlas;
        private readonly Func<int> _day;
        private readonly Func<double> _dayMinutes;
        private string _enteredMap;

        /// <param name="dayMinutes">그날의 분(하루 시작~하루 끝 축, GameSession.DayMinutesExact).</param>
        public HeadlessWorld(PlayAtlas atlas, IWorldArrivals arrivals, string startMap, string spawn, Func<int> day, Func<double> dayMinutes)
        {
            _atlas = atlas;
            _day = day ?? throw new ArgumentNullException(nameof(day));
            _dayMinutes = dayMinutes ?? throw new ArgumentNullException(nameof(dayMinutes));
            Rooms = new HeadlessRooms(atlas.Maps, atlas.Interactions);
            Session = new WorldSession(Rooms, atlas.Maps.Catalog.Walker) { Arrivals = arrivals };
            if (!Session.Enter(startMap, spawn)) throw new InvalidOperationException(Session.LastProblem);
            Interactions = new InteractionTargets(atlas.Interactions.RadiusCells);
            NpcIds = NpcArt.Load(atlas.RepoRoot).Order;
            Npcs = NpcStage.Create(new NpcScheduleLookupCache(atlas.Definitions.All<NpcSchedule>()), NpcIds, atlas.Interactions,
                atlas.Maps.Catalog, Rooms, atlas.Definitions.TimeRules);
            Refresh();
        }

        public FurnitureRoom Room => Rooms.RoomOf(Session.Map.Id);
        public MapInteractions HereData => _atlas.Interactions.Map(Session.Map.Id);

        public void Update(int dx, int dy, float dt)
        {
            Session.Update(dx, dy, dt);
            Refresh();
        }

        /// <summary>지금 가구 방의 막는 칸을 맞춘다. 막힌 칸이 바뀌었으면 NPC가 그 맵의 길과 머무름을 새 격자로 다시 만든다.</summary>
        public void SetFurnitureBlocking(IEnumerable<GridPoint> cells)
        {
            FurnitureRoom room = Room;
            if (room != null && room.SetFurnitureBlocking(cells)) Npcs.ForgetMap(room.Map.Id);
        }

        /// <summary>맵이 바뀌면 후보 목록을 한 번 만들고, 프레임마다 NPC 자리와 대상을 고친다(새 객체를 만들지 않는다).</summary>
        private void Refresh()
        {
            string map = Session.Map.Id;
            if (map != _enteredMap)
            {
                _enteredMap = map;
                Interactions.MapId = map;
                Interactions.RadiusPixels = _atlas.Interactions.RadiusCells * Session.Map.Grid.CellSize;
                Interactions.SetCandidates(Npcs.EnterMap(map, HereData, Session.Map.Doors));
            }
            Npcs.Tick(_day(), _dayMinutes());
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
