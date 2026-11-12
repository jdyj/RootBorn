using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;
using Rootborn.Data;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 일터 테스트의 방 칸. 맵이 아직 없으므로 그 일터의 템플릿 가구가 차지하는 칸을 모두 덮고 둘레에 여유 3칸을 둔 빈 바닥 사각형이다.
    /// 출발 칸(문 앞)은 왼쪽 위 여유 칸 한 곳이다. 템플릿 좌표가 바뀌어도 칸이 따라간다.
    /// </summary>
    internal static class WorkspaceRoom
    {
        public const int Margin = 3;

        public static PlacementGrid GridFor(GameDefinitions definitions, string workspaceId)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var template in definitions.All<WorkspaceTemplate>())
            {
                if (template.WorkspaceId != workspaceId) continue;
                foreach (var placement in template.Placements)
                {
                    var furniture = definitions.Get<FurnitureDefinition>(placement.FurnitureId);
                    foreach (var local in furniture.GetFootprintCells(placement.Direction)) Grow(placement.Anchor.X + local.X, placement.Anchor.Y + local.Y);
                    foreach (var part in furniture.GetTileParts(placement.Direction)) Grow(placement.Anchor.X + part.LocalCell.X, placement.Anchor.Y + part.LocalCell.Y);
                }
            }

            if (minX == int.MaxValue) throw new InvalidOperationException(workspaceId + "의 템플릿 가구가 없다");
            var floor = new List<GridPoint>();
            for (int y = minY - Margin; y <= maxY + Margin; y++)
                for (int x = minX - Margin; x <= maxX + Margin; x++)
                    floor.Add(new GridPoint(x, y));
            var spawn = new GridPoint(minX - Margin, minY - Margin);
            return new PlacementGrid(floor, Array.Empty<GridPoint>(), spawn, new[] { spawn });

            void Grow(int x, int y)
            {
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        /// <summary>지금 열린 배치(game.House)에서 그 가구를 놓을 수 있는 첫 칸. 없으면 false.</summary>
        public static bool TryFreeAnchor(GameSession game, string furnitureId, out GridPoint anchor, int skip = 0)
        {
            var furniture = game.Definitions.Get<FurnitureDefinition>(furnitureId);
            for (int y = -20; y <= 40; y++)
                for (int x = -20; x <= 40; x++)
                {
                    anchor = new GridPoint(x, y);
                    if (game.House.CanPlace(furniture, anchor, FurniturePlacementDirection.North) && skip-- <= 0) return true;
                }

            anchor = default;
            return false;
        }

        public static GridPoint FreeAnchor(GameSession game, string furnitureId, int skip = 0)
            => TryFreeAnchor(game, furnitureId, out var anchor, skip) ? anchor : throw new InvalidOperationException(furnitureId + "를 놓을 칸이 없다");
    }
}
