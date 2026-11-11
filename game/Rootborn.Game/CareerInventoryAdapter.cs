using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Data;

namespace Rootborn.Game
{
    /// <summary>
    /// 플레이어 인벤토리(ItemInventory, 아이템 = 정의)를 성인 직업 인벤토리(아이템 = id)로 쓴다. id는 정의 표로 바꾼다.
    /// 정의가 없는 id가 섞이면 교환하지 않는다. 판정은 ItemInventory가 한다.
    /// </summary>
    internal sealed class CareerInventoryAdapter : ICareerInventory
    {
        private readonly ItemInventory _inventory;
        private readonly GameDefinitions _definitions;

        public CareerInventoryAdapter(ItemInventory inventory, GameDefinitions definitions)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public bool CanExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards)
            => TryConvert(costs, out var c) && TryConvert(rewards, out var r) && _inventory.CanExchange(c, r);

        public bool TryExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards, Action commit)
            => TryConvert(costs, out var c) && TryConvert(rewards, out var r) && _inventory.TryExchange(c, r, commit);

        public int CountOf(string itemId) => _definitions.TryGet<ItemDefinition>(itemId, out var item) ? _inventory.CountOf(item) : 0;

        private bool TryConvert(IReadOnlyList<ItemAmount<string>> amounts, out List<InventoryGrant> grants)
        {
            grants = null;
            if (amounts == null) return true;
            grants = new List<InventoryGrant>(amounts.Count);
            for (int i = 0; i < amounts.Count; i++)
            {
                if (!_definitions.TryGet<ItemDefinition>(amounts[i].Item, out var item)) return false;
                grants.Add(new InventoryGrant(item, amounts[i].Count));
            }

            return true;
        }
    }
}
