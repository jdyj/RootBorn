using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.Placement
{
    /// <summary>가구가 보는 방향. 숫자는 Unity 판 FurniturePlacementDirection과 같다(세이브에 숫자로 들어 있다).</summary>
    public enum FurniturePlacementDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>놓을 자리 추천에 쓰는 선호. 숫자는 Unity 판과 같다. 배치 판정은 이 값을 보지 않는다.</summary>
    public enum FurniturePlacementPreference
    {
        Any = 0,
        AvoidBlockedCells = 1,
        NearWall = 2,
        NearWindow = 3,
    }

    /// <summary>가구 그림 한 칸. 판정은 칸만 보고, 그림 주소와 이름은 화면이 쓴다.</summary>
    public readonly struct FurnitureTilePart
    {
        public FurnitureTilePart(GridPoint localCell, string sprite, string name)
        {
            LocalCell = localCell;
            Sprite = sprite ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public GridPoint LocalCell { get; }
        public string Sprite { get; }
        public string Name { get; }
    }

    /// <summary>
    /// 가구 하나의 규칙 값(원본 Assets/Scripts/Game/Placement/FurnitureDefinition.cs).
    /// 차지 칸이 비면 그림 칸을 차지 칸으로 쓴다. 허용 면이 비면 모든 면에 놓을 수 있다.
    /// </summary>
    public sealed class FurnitureDefinition
    {
        private readonly FurnitureTilePart[] _tileParts;
        private readonly GridPoint[] _footprint;
        private readonly string[] _allowedSurfaceIds;

        public FurnitureDefinition(
            string stableId,
            string displayName,
            string displayNameKey,
            string category,
            IReadOnlyList<string> allowedSurfaceIds,
            IReadOnlyList<FurnitureTilePart> tileParts,
            IReadOnlyList<GridPoint> footprintCells,
            bool blocksMovement,
            FurniturePlacementPreference placementPreference,
            string unlockToken = null,
            ItemDefinition priceItem = null,
            int price = 0,
            string purchaseLocationId = null)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("가구 id가 비어 있다", nameof(stableId));
            StableId = stableId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? stableId : displayName;
            DisplayNameKey = displayNameKey ?? string.Empty;
            Category = category ?? string.Empty;
            _allowedSurfaceIds = DistinctNonEmpty(allowedSurfaceIds);
            _tileParts = tileParts == null ? Array.Empty<FurnitureTilePart>() : Copy(tileParts);
            _footprint = Distinct(footprintCells);
            if (_footprint.Length == 0)
            {
                var cells = new GridPoint[_tileParts.Length];
                for (int i = 0; i < cells.Length; i++) cells[i] = _tileParts[i].LocalCell;
                _footprint = Distinct(cells);
            }

            BlocksMovement = blocksMovement;
            PlacementPreference = placementPreference;
            UnlockToken = unlockToken ?? string.Empty;
            PriceItem = priceItem;
            Price = Math.Max(0, price);
            PurchaseLocationId = purchaseLocationId ?? string.Empty;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string DisplayNameKey { get; }
        public string Category { get; }
        public IReadOnlyList<string> AllowedSurfaceIds => _allowedSurfaceIds;
        public IReadOnlyList<FurnitureTilePart> TileParts => _tileParts;
        public IReadOnlyList<GridPoint> FootprintCells => _footprint;
        public bool BlocksMovement { get; }
        public FurniturePlacementPreference PlacementPreference { get; }

        /// <summary>비면 처음부터 쓸 수 있다. 있으면 그 해금이 있어야 목록에 보인다.</summary>
        public string UnlockToken { get; }

        public ItemDefinition PriceItem { get; }
        public int Price { get; }
        public string PurchaseLocationId { get; }

        /// <summary>값을 낼 물건, 1 이상의 값, 해금, 파는 장소가 모두 있어야 판다.</summary>
        public bool IsForSale => PriceItem != null && Price > 0 && UnlockToken.Length > 0 && PurchaseLocationId.Length > 0;

        public bool AllowsSurface(string surfaceId)
        {
            if (_allowedSurfaceIds.Length == 0) return true;
            for (int i = 0; i < _allowedSurfaceIds.Length; i++)
                if (string.Equals(_allowedSurfaceIds[i], surfaceId, StringComparison.Ordinal)) return true;
            return false;
        }

        public IReadOnlyList<GridPoint> GetFootprintCells(FurniturePlacementDirection direction)
        {
            if (direction == FurniturePlacementDirection.North) return _footprint;
            var rotated = new GridPoint[_footprint.Length];
            for (int i = 0; i < rotated.Length; i++) rotated[i] = Rotate(_footprint[i], direction);
            return Distinct(rotated);
        }

        public IReadOnlyList<FurnitureTilePart> GetTileParts(FurniturePlacementDirection direction)
        {
            if (direction == FurniturePlacementDirection.North) return _tileParts;
            var rotated = new FurnitureTilePart[_tileParts.Length];
            for (int i = 0; i < rotated.Length; i++)
                rotated[i] = new FurnitureTilePart(Rotate(_tileParts[i].LocalCell, direction), _tileParts[i].Sprite, _tileParts[i].Name);
            return rotated;
        }

        /// <summary>원본과 같은 회전: 동 (x,y)→(y,-x), 남 (-x,-y), 서 (-y,x).</summary>
        private static GridPoint Rotate(GridPoint cell, FurniturePlacementDirection direction)
        {
            switch (direction)
            {
                case FurniturePlacementDirection.East: return new GridPoint(cell.Y, -cell.X);
                case FurniturePlacementDirection.South: return new GridPoint(-cell.X, -cell.Y);
                case FurniturePlacementDirection.West: return new GridPoint(-cell.Y, cell.X);
                default: return cell;
            }
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return copy;
        }

        private static GridPoint[] Distinct(IReadOnlyList<GridPoint> cells)
        {
            if (cells == null || cells.Count == 0) return Array.Empty<GridPoint>();
            var seen = new HashSet<GridPoint>();
            var result = new List<GridPoint>(cells.Count);
            for (int i = 0; i < cells.Count; i++) if (seen.Add(cells[i])) result.Add(cells[i]);
            return result.ToArray();
        }

        private static string[] DistinctNonEmpty(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0) return Array.Empty<string>();
            var result = new List<string>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
                if (!string.IsNullOrWhiteSpace(ids[i]) && !result.Contains(ids[i])) result.Add(ids[i]);
            return result.ToArray();
        }
    }
}
