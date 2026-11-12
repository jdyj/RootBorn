using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 걷는 맵을 내주는 곳: 가구 방(interactions.json의 placementSurface)이면 가구가 막는 칸을 더한 맵(FurnitureRoom.WalkMap),
    /// 아니면 맵 그대로. 방은 맵마다 한 번 만든다(맵 크기는 맵에서 온다).
    /// </summary>
    public sealed class RoomAwareMaps : IWorldMapSource
    {
        private readonly WorldMapLoader _maps;
        private readonly InteractionData _interactions;
        private readonly Dictionary<string, FurnitureRoom> _rooms = new Dictionary<string, FurnitureRoom>(StringComparer.OrdinalIgnoreCase);

        public RoomAwareMaps(WorldMapLoader maps, InteractionData interactions)
        {
            _maps = maps ?? throw new ArgumentNullException(nameof(maps));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        /// <summary>그 맵이 가구 방이면 방, 아니면 null.</summary>
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

        /// <summary>방마다 마지막으로 맞춘 막힌 칸(가구가 바뀌었는지 보려고).</summary>
        private readonly Dictionary<FurnitureRoom, bool[]> _lastBlocked = new Dictionary<FurnitureRoom, bool[]>();

        /// <summary>
        /// 방의 걷는 맵에 가구가 막는 칸을 맞춘다(격자 배열을 제자리에서 고친다). 막힌 칸이 전과 달라졌으면 true.
        /// 창 없는 판의 HeadlessRooms.SetFurnitureBlocking과 같다 — 바뀌면 같이 고친다.
        /// </summary>
        public bool SetFurnitureBlocking(FurnitureRoom room, IEnumerable<GridPoint> cells)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            CollisionGrid walk = room.WalkMap.Collision;
            MapGrid grid = walk.Grid;
            if (!_lastBlocked.TryGetValue(room, out bool[] last))
            {
                // 처음에는 걷는 맵이 가구 없는 맵과 같다.
                _lastBlocked[room] = last = new bool[grid.Count];
                for (int y = 0; y < grid.Rows; y++)
                    for (int x = 0; x < grid.Columns; x++) last[grid.Index(x, y)] = room.Map.Collision.IsBlocked(x, y);
            }
            room.SetFurnitureBlocking(cells);
            bool changed = false;
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                {
                    int i = grid.Index(x, y);
                    bool now = walk.IsBlocked(x, y);
                    if (last[i] != now) changed = true;
                    last[i] = now;
                }
            return changed;
        }

        /// <summary>
        /// 그 면 id(placementSurface)의 가구 방 칸 — 이 PC의 화면이 GameSession.EnterHouse에 넘기는 것과 같은 칸. 내 PC에서 연 방이 같이 쓰는 집을
        /// 판정할 때 쓴다(Core RoomHouseRules). 그런 맵이 없으면 null.
        /// </summary>
        public Rootborn.Core.Placement.IPlacementGrid GridForSurface(string surfaceId)
        {
            if (string.IsNullOrEmpty(surfaceId)) return null;
            foreach (string mapId in _interactions.MapIds)
                if (_interactions.Map(mapId).PlacementSurface == surfaceId) return RoomOf(mapId)?.Grid;
            return null;
        }
    }

    /// <summary>배치 화면이 묻는 화면 좌표 ↔ 가구 칸(지난 프레임에 그린 카메라 기준).</summary>
    public sealed class HostPlacementView : IPlacementView
    {
        private readonly WorldScreen _world;
        private readonly Func<float> _uiScale;

        /// <param name="uiScale">화면 픽셀 / 논리 좌표(Host ScreenFit.Scale).</param>
        public HostPlacementView(WorldScreen world, Func<float> uiScale)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _uiScale = uiScale ?? throw new ArgumentNullException(nameof(uiScale));
        }

        public FurnitureRoom Room => _world.Room;

        /// <summary>논리 좌표 1이 맵 몇 픽셀인가의 역수: 맵 1픽셀이 논리 좌표 몇인가.</summary>
        private float MapToUi => _world.Scale / _uiScale();

        public bool TryCellAt(float x, float y, out GridPoint cell)
        {
            cell = default;
            FurnitureRoom room = Room;
            if (room == null) return false;
            float mapX = _world.CameraOffset.X + x / MapToUi, mapY = _world.CameraOffset.Y + y / MapToUi;
            int size = room.Map.Grid.CellSize;
            CellPos c = CellPos.Containing(mapX, mapY, size);
            if (!room.Map.Grid.Contains(c.X, c.Y)) return false;
            cell = room.ToPlacement(c);
            return true;
        }

        public UiRect CellRect(GridPoint cell)
        {
            FurnitureRoom room = Room;
            if (room == null) return default;
            AreaRect area = room.CellArea(cell);
            float k = MapToUi;
            return new UiRect((area.X - _world.CameraOffset.X) * k, (area.Y - _world.CameraOffset.Y) * k, area.W * k, area.H * k);
        }
    }
}
