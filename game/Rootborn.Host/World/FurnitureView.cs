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
