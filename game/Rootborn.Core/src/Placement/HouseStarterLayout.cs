using System;
using System.Collections.Generic;
using Rootborn.Core.Workspaces;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 새 게임의 집에 처음 놓이는 가구 묶음(data/building/HouseStarterLayout.json). 그 면(<see cref="SurfaceId"/>)의 배치 기록으로 바뀐다.
    /// 놓을 수 있는지와 막혔을 때 옮기기는 집 배치(<see cref="HouseFurnitureLayout.RestoreRelocating"/>)가 옛 기록과 같은 길로 정한다.
    /// 언제 주는지(새 게임에만, 한 번만)는 이 클래스가 아니라 게임 한 판이 정한다. 잘못된 값은 던지지 않고 <see cref="IsValid"/>로 알린다.
    /// </summary>
    public sealed class HouseStarterLayout
    {
        private readonly TemplatePlacement[] _placements;

        public HouseStarterLayout(string id, string surfaceId, TemplatePlacement[] placements)
        {
            Id = id ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            _placements = placements != null ? (TemplatePlacement[])placements.Clone() : Array.Empty<TemplatePlacement>();
        }

        public string Id { get; }
        public string SurfaceId { get; }
        public IReadOnlyList<TemplatePlacement> Placements => _placements;

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || SurfaceId.Length == 0) return false;
                for (int i = 0; i < _placements.Length; i++)
                    if (!_placements[i].IsValid) return false;
                return true;
            }
        }

        /// <summary>
        /// 배치 기록(<see cref="HouseFurnitureLayout.BuildSaveRecords"/>와 같은 꼴: 가구 id, 기준 칸, 방향, 면). 부를 때마다 새 기록이다.
        /// 인스턴스 id는 기록에 없다 — 집 배치가 읽을 때 매긴다.
        /// </summary>
        public FurniturePlacementSaveData[] Records()
        {
            var records = new FurniturePlacementSaveData[_placements.Length];
            for (int i = 0; i < records.Length; i++)
            {
                var p = _placements[i];
                records[i] = new FurniturePlacementSaveData
                {
                    FurnitureId = p.FurnitureId,
                    SurfaceId = SurfaceId,
                    AnchorX = p.Anchor.X,
                    AnchorY = p.Anchor.Y,
                    Direction = p.Direction,
                };
            }

            return records;
        }
    }
}
