using System;
using Rootborn.Core.Building;

namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 집 짓기(data/building): 꾸밈(벽지·바닥 종류). 등록부(registry.json)에는 넣지 않고 파일 순서대로 읽는다(일터와 같다).
    /// 올바르지 않은 정의는 던져서 읽기 오류(constructor)로 모은다.
    /// </summary>
    internal static class BuildingBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // 판매 필드(priceItem, price, purchaseLocationId)는 나중에 더해져 잠긴 꾸밈에만 있다(가구와 같은 꼴). 없으면 팔지 않는다.
            b.Add("RoomFinishDefinition", BuilderHelpers.AddedLater(new[] { "priceItem", "price", "purchaseLocationId" }, (n, c) =>
            {
                var finish = new RoomFinishDefinition(n.Id, n.Str("slot"), n.Str("nameKey"), n.Str("sheet"), n.Str("art"),
                    BuilderHelpers.StrictItemAmounts(n, "costItems", "costCounts"), n.Str("unlockToken"),
                    n.RefId("priceItem"), n.Int("price"), n.Str("purchaseLocationId"));
                if (!finish.IsValid)
                    throw new InvalidOperationException("꾸밈이 올바르지 않다(id·slot·nameKey·sheet·art가 있고, 비용은 아이템과 1 이상의 개수)");
                return finish;
            }));
        }
    }
}
