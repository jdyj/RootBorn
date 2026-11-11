using System;
using System.Collections.Generic;

namespace Rootborn.Core.Inventory
{
    /// <summary>One inventory slot. Only <see cref="ItemSlots{TItem,TSlot}"/> changes it; an empty slot has no item or a count of zero.</summary>
    public class ItemSlot<TItem> where TItem : class
    {
        public TItem Item { get; internal set; }
        public int Count { get; internal set; }
    }

    /// <summary>
    /// A slot inventory: stacks up to each item's max stack, at most <see cref="MaxSlots"/> slots. Exchanges are
    /// all-or-nothing and run a state commit inside, rolled back if it throws. Items compare with the item type's
    /// default equality, so engine objects that report themselves as gone count as no item.
    /// </summary>
    public sealed class ItemSlots<TItem, TSlot> where TItem : class where TSlot : ItemSlot<TItem>, new()
    {
        private readonly List<TSlot> _slots;
        private readonly Func<TItem, int> _maxStack;
        private readonly EqualityComparer<TItem> _comparer = EqualityComparer<TItem>.Default;
        private bool _exchangeInProgress;

        public ItemSlots(int maxSlots, Func<TItem, int> maxStack)
        {
            if (maxSlots < 0) throw new ArgumentOutOfRangeException(nameof(maxSlots));
            MaxSlots = maxSlots;
            _maxStack = maxStack ?? throw new ArgumentNullException(nameof(maxStack));
            _slots = new List<TSlot>(maxSlots);
        }

        public int MaxSlots { get; }
        public IReadOnlyList<TSlot> Slots => _slots;
        public event Action Changed;

        public bool CanExchange(IReadOnlyList<ItemAmount<TItem>> costs, IReadOnlyList<ItemAmount<TItem>> rewards)
            => !_exchangeInProgress && InventoryPlanner.TryPlanExchange(Stacks(), MaxSlots, costs, rewards, _maxStack, out _);

        // commitState must contain only prevalidated, nonthrowing state changes and no I/O.
        // Observers run after both sides commit; the reentry guard stays held through notification.
        public bool TryExchange(IReadOnlyList<ItemAmount<TItem>> costs, IReadOnlyList<ItemAmount<TItem>> rewards, Action commitState = null)
        {
            if (_exchangeInProgress || !InventoryPlanner.TryPlanExchange(Stacks(), MaxSlots, costs, rewards, _maxStack, out var planned)) return false;
            _exchangeInProgress = true;
            var previous = Stacks();
            try
            {
                Load(planned);
                try { commitState?.Invoke(); }
                catch
                {
                    Load(previous);
                    throw;
                }

                Changed?.Invoke();
                return true;
            }
            finally { _exchangeInProgress = false; }
        }

        /// <summary>Tops up stacks, then fills emptied slots, then appends. What does not fit is dropped; check with CanAdd first.</summary>
        public void Add(TItem item, int count = 1)
        {
            if (IsNone(item) || count <= 0) return;
            int remaining = count;
            int max = _maxStack(item);
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (!Same(slot.Item, item)) continue;
                int room = max - slot.Count;
                if (room <= 0) continue;
                int put = Math.Min(room, remaining);
                slot.Count += put;
                remaining -= put;
            }

            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (!IsNone(slot.Item) && slot.Count > 0) continue;
                int put = Math.Min(max, remaining);
                slot.Item = item;
                slot.Count = put;
                remaining -= put;
            }

            while (remaining > 0 && max > 0 && _slots.Count < MaxSlots)
            {
                int put = Math.Min(max, remaining);
                _slots.Add(new TSlot { Item = item, Count = put });
                remaining -= put;
            }

            Changed?.Invoke();
        }

        public bool CanAdd(TItem item, int count = 1)
        {
            if (IsNone(item) || count <= 0) return false;
            int remaining = count;
            int max = _maxStack(item);
            int occupied = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!IsNone(slot.Item) && slot.Count > 0) occupied++;
                if (!Same(slot.Item, item)) continue;
                int room = max - slot.Count;
                if (room <= 0) continue;
                remaining -= Math.Min(room, remaining);
                if (remaining <= 0) return true;
            }

            int free = MaxSlots - occupied;
            while (remaining > 0 && free > 0 && max > 0)
            {
                remaining -= Math.Min(max, remaining);
                free--;
            }

            return remaining <= 0;
        }

        /// <summary>Whether every grant fits together (each fitting alone does not mean they all fit).</summary>
        public bool CanAddAll(IReadOnlyList<ItemAmount<TItem>> grants)
        {
            if (grants == null) return true;
            return InventoryPlanner.TryPlanExchange(Stacks(), MaxSlots, null, grants, _maxStack, out _);
        }

        public bool TryMoveSlot(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || targetIndex < 0) return false;
            EnsureSlotCount(Math.Max(sourceIndex, targetIndex) + 1);
            if (sourceIndex >= _slots.Count || targetIndex >= _slots.Count) return false;
            var source = _slots[sourceIndex];
            if (IsNone(source.Item) || source.Count <= 0) return false;
            if (sourceIndex == targetIndex) return true;
            var target = _slots[targetIndex];
            if (!IsNone(target.Item) && target.Count > 0)
            {
                if (!Same(target.Item, source.Item)) return false;
                int room = _maxStack(source.Item) - target.Count;
                if (room <= 0) return false;
                int moved = Math.Min(room, source.Count);
                target.Count += moved;
                source.Count -= moved;
                if (source.Count <= 0)
                {
                    source.Item = null;
                    source.Count = 0;
                }

                Changed?.Invoke();
                return true;
            }

            target.Item = source.Item;
            target.Count = source.Count;
            source.Item = null;
            source.Count = 0;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes from the last stacks first; emptied slots are removed from the list.</summary>
        public bool Remove(TItem item, int count = 1)
        {
            if (IsNone(item) || count <= 0) return false;
            if (CountOf(item) < count) return false;
            int remaining = count;
            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = _slots[i];
                if (!Same(slot.Item, item)) continue;
                int take = Math.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;
                if (slot.Count == 0) _slots.RemoveAt(i);
            }

            Changed?.Invoke();
            return true;
        }

        public int CountOf(TItem item)
        {
            if (IsNone(item)) return 0;
            int sum = 0;
            for (int i = 0; i < _slots.Count; i++) if (Same(_slots[i].Item, item)) sum += _slots[i].Count;
            return sum;
        }

        public void Clear()
        {
            if (_slots.Count == 0) return;
            _slots.Clear();
            Changed?.Invoke();
        }

        private List<ItemStack<TItem>> Stacks()
        {
            var stacks = new List<ItemStack<TItem>>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++) stacks.Add(new ItemStack<TItem>(_slots[i].Item, _slots[i].Count));
            return stacks;
        }

        private void Load(List<ItemStack<TItem>> stacks)
        {
            _slots.Clear();
            for (int i = 0; i < stacks.Count; i++) _slots.Add(new TSlot { Item = stacks[i].Item, Count = stacks[i].Count });
        }

        private void EnsureSlotCount(int count)
        {
            while (_slots.Count < count && _slots.Count < MaxSlots) _slots.Add(new TSlot());
        }

        private bool IsNone(TItem item) => item == null || _comparer.Equals(item, null);
        private bool Same(TItem slotItem, TItem item) => !IsNone(slotItem) && _comparer.Equals(slotItem, item);
    }
}
