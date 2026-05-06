using Rootborn.Game.Common;

namespace Rootborn.Game.Quests
{
    public readonly struct InventoryGrant
    {
        public readonly ItemDefinition Item;
        public readonly int Count;

        public InventoryGrant(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
