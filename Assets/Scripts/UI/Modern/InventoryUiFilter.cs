using System;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;

namespace Rootborn.UI.Modern
{
    /// <summary>Presentation-only grouping for the inventory browse tabs.</summary>
    public static class InventoryUiFilter
    {
        public static bool Matches(ItemDefinition item, int tab, string query)
        {
            if (item == null) return false;

            bool categoryMatches;
            switch (tab)
            {
                case 1:
                    categoryMatches = item.Category == ItemCategory.Tool;
                    break;
                case 2:
                    categoryMatches = item.Category == ItemCategory.Resource || item.Category == ItemCategory.Seed;
                    break;
                case 3:
                    categoryMatches = item.Category == ItemCategory.Misc;
                    break;
                default:
                    categoryMatches = true;
                    break;
            }

            if (!categoryMatches) return false;
            string trimmedQuery = query?.Trim();
            return string.IsNullOrEmpty(trimmedQuery)
                || GameText.Get(item.DisplayKey).IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string CategoryKey(int tab)
        {
            switch (tab)
            {
                case 1: return "ui.inventory.category.tools";
                case 2: return "ui.inventory.category.resources";
                case 3: return "ui.inventory.category.misc";
                default: return "ui.inventory.category.all";
            }
        }
    }
}
