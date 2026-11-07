using System;
using System.Collections.Generic;

namespace Rootborn.Core.Inventory
{
    /// <summary>One inventory slot. An empty slot has no item or a count of zero.</summary>
    public readonly struct ItemStack<TItem> where TItem : class
    {
        public ItemStack(TItem item, int count)
        {
            Item = item;
            Count = count;
        }

        public TItem Item { get; }
        public int Count { get; }
    }

    /// <summary>An amount of one item to take or give.</summary>
    public readonly struct ItemAmount<TItem> where TItem : class
    {
        public ItemAmount(TItem item, int count)
        {
            Item = item;
            Count = count;
        }

        public TItem Item { get; }
        public int Count { get; }
    }

    /// <summary>
    /// Plans an all-or-nothing exchange on a copy of the slots, so a caller can commit the result together with quest or
    /// reward state, or change nothing. Items are compared with the item type's default equality.
    /// </summary>
    public static class InventoryPlanner
    {
        /// <summary>
        /// Takes <paramref name="costs"/> (summed per item, from the last stacks first), then gives <paramref name="rewards"/>
        /// (topping up stacks, then reusing empty slots, then appending up to <paramref name="maxSlots"/>).
        /// Returns false if any amount is invalid, a cost is not available, or a reward does not fit. The input is never changed.
        /// </summary>
        public static bool TryPlanExchange<TItem>(IReadOnlyList<ItemStack<TItem>> slots, int maxSlots,
            IReadOnlyList<ItemAmount<TItem>> costs, IReadOnlyList<ItemAmount<TItem>> rewards, Func<TItem, int> maxStack,
            out List<ItemStack<TItem>> next) where TItem : class
        {
            if (maxStack == null) throw new ArgumentNullException(nameof(maxStack));
            var comparer = EqualityComparer<TItem>.Default;
            int count = slots != null ? slots.Count : 0;
            var items = new List<TItem>(count);
            var counts = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add(slots[i].Item);
                counts.Add(slots[i].Count);
            }

            next = null;
            var totals = new Dictionary<TItem, long>(comparer);
            var order = new List<TItem>();
            if (costs != null)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    var cost = costs[i];
                    if (IsNone(cost.Item, comparer) || cost.Count <= 0) return false;
                    if (!totals.TryGetValue(cost.Item, out long sum)) order.Add(cost.Item);
                    totals[cost.Item] = sum + cost.Count;
                }
            }

            foreach (var item in order)
            {
                long wanted = totals[item];
                long available = 0;
                for (int i = 0; i < items.Count; i++)
                    if (Same(items[i], item, comparer)) available += Math.Max(0, counts[i]);
                if (available < wanted) return false;
                for (int i = items.Count - 1; i >= 0 && wanted > 0; i--)
                {
                    if (!Same(items[i], item, comparer) || counts[i] <= 0) continue;
                    int take = (int)Math.Min(wanted, counts[i]);
                    counts[i] -= take;
                    wanted -= take;
                    if (counts[i] == 0) items[i] = null;
                }
            }

            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    var reward = rewards[i];
                    if (IsNone(reward.Item, comparer) || reward.Count <= 0) return false;
                    int stack = maxStack(reward.Item);
                    int remaining = reward.Count;
                    for (int j = 0; j < items.Count && remaining > 0; j++)
                    {
                        if (!Same(items[j], reward.Item, comparer) || counts[j] <= 0) continue;
                        int put = Math.Min(Math.Max(0, stack - counts[j]), remaining);
                        counts[j] += put;
                        remaining -= put;
                    }

                    for (int j = 0; j < items.Count && remaining > 0; j++)
                    {
                        if (!IsNone(items[j], comparer) && counts[j] > 0) continue;
                        int put = Math.Min(stack, remaining);
                        items[j] = reward.Item;
                        counts[j] = put;
                        remaining -= put;
                    }

                    while (remaining > 0 && stack > 0 && items.Count < maxSlots)
                    {
                        int put = Math.Min(stack, remaining);
                        items.Add(reward.Item);
                        counts.Add(put);
                        remaining -= put;
                    }

                    if (remaining > 0) return false;
                }
            }

            next = new List<ItemStack<TItem>>(items.Count);
            for (int i = 0; i < items.Count; i++) next.Add(new ItemStack<TItem>(items[i], counts[i]));
            return true;
        }

        // Default equality lets engine objects that report themselves as gone compare equal to null.
        private static bool IsNone<TItem>(TItem item, EqualityComparer<TItem> comparer) where TItem : class
            => item == null || comparer.Equals(item, null);

        private static bool Same<TItem>(TItem slotItem, TItem item, EqualityComparer<TItem> comparer) where TItem : class
            => !IsNone(slotItem, comparer) && comparer.Equals(slotItem, item);
    }
}
