using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Building
{
    /// <summary>
    /// 잠긴 꾸밈을 가게에서 산다: 값을 내고 그 해금 토큰을 얻는다(가구 <see cref="FurniturePurchase"/>와 같은 뜻, 같은 결과 값).
    /// 해금은 가구와 같은 <see cref="FurnitureUnlockProgress"/>에 든다 — 세이브 모양이 늘지 않는다. 둘은 함께 바뀌고, 실패하면 아무것도 바뀌지 않는다.
    /// </summary>
    public static class RoomFinishPurchase
    {
        /// <param name="priceItem">정의의 <see cref="RoomFinishDefinition.PriceItemId"/>에 해당하는 아이템(부르는 쪽이 정의 표에서 찾는다). 없거나 다른 아이템이면 <see cref="FurniturePurchaseResult.Unavailable"/>.</param>
        public static FurniturePurchaseResult Try(RoomFinishDefinition finish, ItemDefinition priceItem, ItemInventory inventory, FurnitureUnlockProgress unlocks)
        {
            if (finish == null || !finish.IsForSale) return FurniturePurchaseResult.NotForSale;
            if (priceItem == null || !string.Equals(priceItem.Id, finish.PriceItemId, StringComparison.Ordinal)
                || inventory == null || unlocks == null) return FurniturePurchaseResult.Unavailable;
            if (unlocks.IsUnlocked(finish.UnlockToken)) return FurniturePurchaseResult.AlreadyOwned;
            var cost = new[] { new InventoryGrant(priceItem, finish.Price) };
            return inventory.TryExchange(cost, Array.Empty<InventoryGrant>(), () => unlocks.TryUnlock(finish.UnlockToken))
                ? FurniturePurchaseResult.Purchased
                : FurniturePurchaseResult.NotEnoughMoney;
        }

        /// <summary>그 장소에서 파는 꾸밈(가게 화면의 살 목록). 목록 순서 그대로다.</summary>
        public static List<RoomFinishDefinition> ForSaleAt(IEnumerable<RoomFinishDefinition> catalog, string locationId)
        {
            var result = new List<RoomFinishDefinition>();
            if (catalog == null || string.IsNullOrEmpty(locationId)) return result;
            foreach (var finish in catalog)
                if (finish != null && finish.IsForSale && string.Equals(finish.PurchaseLocationId, locationId, StringComparison.Ordinal)) result.Add(finish);
            return result;
        }
    }
}
