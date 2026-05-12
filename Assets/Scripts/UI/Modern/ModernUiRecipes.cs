using System.Collections.Generic;
using Rootborn.Game.Common;

namespace Rootborn.UI.Modern
{
    public sealed class ModernUiTileRecipe
    {
        public ModernUiTileRecipe(IReadOnlyList<ModernUiSpriteKey> tiles)
        {
            Tiles = tiles;
        }

        public IReadOnlyList<ModernUiSpriteKey> Tiles { get; }

        public ModernUiSpriteKey FindByRole(string role)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                if (Tiles[i].Role == role)
                {
                    return Tiles[i];
                }
            }

            return default;
        }
    }

    public sealed class ModernUiWindowRecipe
    {
        public ModernUiWindowRecipe(string name, IReadOnlyList<string> requiredParts)
        {
            Name = name;
            RequiredParts = requiredParts;
        }

        public string Name { get; }
        public IReadOnlyList<string> RequiredParts { get; }
    }

    public static class ModernUiRecipes
    {
        public static readonly ModernUiTileRecipe CommonPanel = new ModernUiTileRecipe(ModernUiStyle2Sprites.CommonPanel.Tiles);

        public static readonly ModernUiWindowRecipe SettingsWindow = new ModernUiWindowRecipe(
            "settings", new[] { "commonPanel", "title-tabs", "close-button", "toggle-row", "slider-row" });

        public static readonly ModernUiWindowRecipe InventoryWindow = new ModernUiWindowRecipe(
            "inventory", new[] { "commonPanel", "title-tabs", "slot-grid", "scrollbar", "selection-cursor", "popup" });

        public static readonly ModernUiWindowRecipe StatusWindow = new ModernUiWindowRecipe(
            "status", new[] { "commonPanel", "title-tabs", "portrait-frame", "gauge-row", "icon-frame", "bottom-buttons" });

        public static readonly ModernUiWindowRecipe QuestWindow = new ModernUiWindowRecipe(
            "quest", new[] { "commonPanel", "quest-list", "quest-detail", "objective-progress", "reward-row", "claim-button", "scrollbar" });

        public static readonly ModernUiWindowRecipe PopupWindow = new ModernUiWindowRecipe(
            "popup", new[] { "commonPanel", "item-icon", "item-name", "item-description", "item-count", "action-buttons" });
    }
}
