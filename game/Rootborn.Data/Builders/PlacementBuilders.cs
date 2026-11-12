using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;

namespace Rootborn.Data.Builders
{
    internal static class PlacementBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // 판매 필드(displayNameKey, priceItem, price, purchaseLocationId)는 나중에 더해져 대부분의 에셋에 없다.
            b.Add("FurnitureDefinition", BuilderHelpers.AddedLater(new[] { "displayNameKey", "priceItem", "price", "purchaseLocationId" }, (n, c) => new FurnitureDefinition(
                n.Str("stableId"),
                n.Str("displayName"),
                n.Str("displayNameKey"),
                n.Str("category"),
                n.Strings("allowedSurfaceIds"),
                TileParts(n),
                BuilderHelpers.Grids(n, "footprintCells"),
                n.Bool("blocksMovement"),
                n.Enum<FurniturePlacementPreference>("placementPreference"),
                n.Str("unlockToken"),
                c.One<ItemDefinition>(n, "priceItem"),
                n.Int("price"),
                n.Str("purchaseLocationId"))));

            // 상자: 어느 가구가 몇 칸을 담는가(MonoGame 판이 더한 종류). 어느 그림을 상자로 쓸지는 이 파일만 고친다.
            b.Add("FurnitureStorageDefinition", (n, c) => new FurnitureStorageDefinition(n.Id, c.One<FurnitureDefinition>(n, "furniture"), n.Int("slots")));
        }

        /// <summary>
        /// 그림 칸. Unity는 그림(tile)이 null인 칸을 버렸다. 변환기가 풀지 못한 참조({ "unresolved": guid })도
        /// Unity에서 타일로 읽히지 않던 것(스프라이트를 가리킴)이라 null과 같이 버린다.
        /// </summary>
        private static FurnitureTilePart[] TileParts(NodeReader n)
        {
            var parts = new List<FurnitureTilePart>();
            foreach (var part in n.Items("tileParts"))
            {
                if (!part.TryChild("tile", out var tile) || tile.Has("unresolved")) continue;
                parts.Add(new FurnitureTilePart(BuilderHelpers.Grid(part, "localCell"), tile.Str("sprite"), tile.Str("name")));
            }

            return parts.ToArray();
        }
    }
}
