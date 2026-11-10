using System;

namespace Rootborn.Core.Inventory
{
    public enum ItemCategory { Resource, Tool, Misc, Seed }

    /// <summary>
    /// An item any inventory can hold. Inventories compare items by reference, like the game's item assets did,
    /// so one definition object per item id should be shared.
    /// </summary>
    public sealed class ItemDefinition
    {
        private readonly int _maxStack;

        public ItemDefinition(string id, int maxStack = 99, ItemCategory category = ItemCategory.Resource,
            string displayKey = null, string description = null, string toolSpritePrefix = null)
        {
            Id = id ?? string.Empty;
            _maxStack = maxStack;
            Category = category;
            DisplayKey = displayKey ?? string.Empty;
            Description = description ?? string.Empty;
            ToolSpritePrefix = toolSpritePrefix ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayKey { get; }
        public int MaxStack => Math.Max(1, _maxStack);
        public ItemCategory Category { get; }
        public string Description { get; }
        public string ToolSpritePrefix { get; }
    }

    /// <summary>An amount of one item to give or take.</summary>
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
