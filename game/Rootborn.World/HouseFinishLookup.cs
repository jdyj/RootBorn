using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 짓는 집의 칸마다 보이는 꾸밈 id(엔진 없음). 조합기(<see cref="ComposedMapTiles"/>)가 주인 바닥 칸으로 묻는다.
    /// 빈 글·정의에 없는 id·다른 자리의 id는 조합기가 맵의 기본 꾸밈으로 그린다. 게임 한 판에서는 Host가 GameSession 조회를 넘긴다.
    /// </summary>
    public interface IHouseFinishLookup
    {
        /// <summary>그 바닥 칸의 벽지 id.</summary>
        string WallAt(CellPos floorCell);
        /// <summary>그 바닥 칸의 바닥 id.</summary>
        string FloorAt(CellPos floorCell);
        /// <summary>띠 색 줄 이름(비면 맵의 build.bandColors).</summary>
        string BandColors { get; }
    }

    /// <summary>
    /// 미리 보기를 얹은 조회: 미리 보기의 바뀌는 칸(<see cref="HouseFinishPreview.ChangedCells"/>)의 그 자리(<see cref="HouseFinishPreview.Slot"/>)만
    /// 미리 보기 꾸밈으로 덮고, 나머지는 안의 조회(없으면 빈 글 = 기본). 미리 보기가 바뀔 때만 만든다.
    /// </summary>
    public sealed class HouseFinishPreviewLookup : IHouseFinishLookup
    {
        private readonly IHouseFinishLookup _inner;
        private readonly HashSet<CellPos> _cells;
        private readonly string _finish;
        private readonly bool _wall, _floor;

        public HouseFinishPreviewLookup(IHouseFinishLookup inner, HouseFinishPreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            _inner = inner;
            _cells = new HashSet<CellPos>(preview.ChangedCells);
            _finish = preview.FinishId;
            _wall = string.Equals(preview.Slot, RoomFinishSlots.Wall, StringComparison.Ordinal);
            _floor = string.Equals(preview.Slot, RoomFinishSlots.Floor, StringComparison.Ordinal);
        }

        public string WallAt(CellPos floorCell) => _wall && _cells.Contains(floorCell) ? _finish : _inner?.WallAt(floorCell) ?? string.Empty;

        public string FloorAt(CellPos floorCell) => _floor && _cells.Contains(floorCell) ? _finish : _inner?.FloorAt(floorCell) ?? string.Empty;

        public string BandColors => _inner?.BandColors ?? string.Empty;
    }

    /// <summary>맵 → 짓는 집의 틀(Core <see cref="HouseBuildFrame"/>). 맵 이름이 아니라 맵 성질(build)과 출처표로 정한다.</summary>
    public static class HouseBuildFrames
    {
        /// <summary>짓는 집(build가 있고 출처표가 있다)이면 그 틀, 아니면 null. 부를 때마다 새 틀이다 — 같은 맵이면 부르는 쪽이 기억해 쓴다.</summary>
        public static HouseBuildFrame From(LoadedMap map)
        {
            MapBuildSettings build = map?.Entry?.Build;
            if (build == null || map.Tiles == null) return null;
            return new HouseBuildFrame(build.FrameId, map.Tiles, build.DefaultWall, build.DefaultFloor, build.BandColors);
        }
    }
}
