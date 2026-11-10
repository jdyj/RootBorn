using System;
using System.Collections.Generic;
using Rootborn.Core.Rewards;

namespace Rootborn.Core.Inventory
{
    /// <summary>
    /// The player's slot inventory: 32 slots, stacking by each item's max stack, all-or-nothing exchanges.
    /// Same rules and API as the game's Inventory; the slot logic is <see cref="ItemSlots{TItem,TSlot}"/>.
    /// </summary>
    public sealed class ItemInventory : IRewardInventory<ItemDefinition>
    {
        public const int MaxSlots = 32;

        public sealed class Slot : ItemSlot<ItemDefinition>
        {
        }

        private readonly ItemSlots<ItemDefinition, Slot> _slots = new ItemSlots<ItemDefinition, Slot>(MaxSlots, item => item.MaxStack);

        public IReadOnlyList<Slot> Slots => _slots.Slots;

        public event Action OnChanged
        {
            add => _slots.Changed += value;
            remove => _slots.Changed -= value;
        }

        public bool CanExchange(IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards)
            => _slots.CanExchange(ToAmounts(costs), ToAmounts(rewards));

        public bool CanReceive(IReadOnlyList<ItemAmount<ItemDefinition>> grants) => _slots.CanExchange(null, grants);

        public bool TryReceive(IReadOnlyList<ItemAmount<ItemDefinition>> grants, Action commit) => _slots.TryExchange(null, grants, commit);

        public bool TryAddAll(IReadOnlyList<InventoryGrant> rewards, Action commitState = null) => TryExchange(null, rewards, commitState);

        // commitState must contain only prevalidated, nonthrowing progress changes and no I/O.
        // Observers run after both sides commit; the reentry guard stays held through notification.
        public bool TryExchange(IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards, Action commitState = null)
            => _slots.TryExchange(ToAmounts(costs), ToAmounts(rewards), commitState);

        public void Add(ItemDefinition item, int count = 1) => _slots.Add(item, count);

        public bool CanAdd(ItemDefinition item, int count = 1) => _slots.CanAdd(item, count);

        public bool CanAddAll(IReadOnlyList<InventoryGrant> grants) => _slots.CanAddAll(ToAmounts(grants));

        public bool TryMoveSlot(int sourceIndex, int targetIndex) => _slots.TryMoveSlot(sourceIndex, targetIndex);

        public bool Remove(ItemDefinition item, int count = 1) => _slots.Remove(item, count);

        public int CountOf(ItemDefinition item) => _slots.CountOf(item);

        public void Clear() => _slots.Clear();

        /// <summary>Saves filled slots by item id, in slot order. Empty slots and items without an id are left out.</summary>
        public PlayerInventorySaveData ToSaveData(string equippedToolItemId)
        {
            var slots = _slots.Slots;
            var items = new List<PlayerInventoryItemSaveData>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Item == null || slot.Count <= 0 || string.IsNullOrEmpty(slot.Item.Id)) continue;
                items.Add(new PlayerInventoryItemSaveData { ItemId = slot.Item.Id, Count = slot.Count });
            }

            return new PlayerInventorySaveData { Items = items.ToArray(), EquippedToolItemId = equippedToolItemId ?? string.Empty };
        }

        /// <summary>
        /// Replaces the contents with the saved items. Ids <paramref name="findById"/> cannot resolve are skipped.
        /// A null save changes nothing. The equipped tool id is left to the caller.
        /// </summary>
        public void LoadFromSaveData(PlayerInventorySaveData saveData, Func<string, ItemDefinition> findById)
        {
            if (saveData == null) return;
            Clear();
            if (saveData.Items == null || findById == null) return;
            for (int i = 0; i < saveData.Items.Length; i++)
            {
                var saved = saveData.Items[i];
                if (string.IsNullOrEmpty(saved.ItemId)) continue;
                var item = findById(saved.ItemId);
                if (item != null) Add(item, saved.Count);
            }
        }

        private static List<ItemAmount<ItemDefinition>> ToAmounts(IReadOnlyList<InventoryGrant> grants)
        {
            if (grants == null) return null;
            var amounts = new List<ItemAmount<ItemDefinition>>(grants.Count);
            for (int i = 0; i < grants.Count; i++) amounts.Add(new ItemAmount<ItemDefinition>(grants[i].Item, grants[i].Count));
            return amounts;
        }
    }

    /// <summary>Same field names as the game's save file (written by JsonUtility).</summary>
    [Serializable]
    public sealed class PlayerInventorySaveData
    {
        public PlayerInventoryItemSaveData[] Items = Array.Empty<PlayerInventoryItemSaveData>();
        public string EquippedToolItemId;
    }

    [Serializable]
    public struct PlayerInventoryItemSaveData
    {
        public string ItemId;
        public int Count;
    }
}
