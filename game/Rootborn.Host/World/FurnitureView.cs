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
        private readonly Dictionary<string, Rootborn.Core.Building.HouseBuildFrame> _frames = new Dictionary<string, Rootborn.Core.Building.HouseBuildFrame>(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>
        /// 그 면 id(placementSurface)의 맵이 짓는 집(맵 성질 build)이면 그 틀, 아니면 null. 내 PC에서 연 방이 같이 쓰는 집의 꾸밈을 판정할 때 쓴다
        /// (Core RoomHouseRules). 맵마다 한 번 만들어 기억한다 — 같은 맵이면 같은 인스턴스.
        /// </summary>
        public Rootborn.Core.Building.HouseBuildFrame FrameForSurface(string surfaceId)
        {
            if (string.IsNullOrEmpty(surfaceId)) return null;
            foreach (string mapId in _interactions.MapIds)
            {
                if (_interactions.Map(mapId).PlacementSurface != surfaceId) continue;
                LoadedMap loaded = _maps.LoadMap(mapId);
                if (!_frames.TryGetValue(loaded.Entry.Id, out var frame))
                    _frames[loaded.Entry.Id] = frame = Rootborn.World.HouseBuildFrames.From(loaded);
                return frame;
            }

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

        /// <summary>걷는 캐릭터가 서 있는 칸에서 바라보는 방향으로 한 칸(키보드로 가구 놓기). 방 밖이면 false.</summary>
        public bool TryFacingCell(out GridPoint cell)
        {
            cell = default;
            FurnitureRoom room = Room;
            Walker walker = _world.Session.Walker;
            if (room == null || walker == null) return false;
            CellPos here = CellPos.Containing(walker.X, walker.Y, room.Map.Grid.CellSize);
            int dx = walker.Facing == Facing.Left ? -1 : walker.Facing == Facing.Right ? 1 : 0;
            int dy = walker.Facing == Facing.Up ? -1 : walker.Facing == Facing.Down ? 1 : 0;
            var front = new CellPos(here.X + dx, here.Y + dy);
            if (!room.Map.Grid.Contains(front.X, front.Y)) return false;
            cell = room.ToPlacement(front);
            return true;
        }

        /// <summary>꾸밈 미리 보기를 월드에(WorldScreen.ShowFinishPreview — 같은 것이면 아무것도 하지 않는다).</summary>
        public void ShowFinishPreview(Rootborn.Core.Building.HouseFinishPreview preview) => _world.ShowFinishPreview(preview);

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
