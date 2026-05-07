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
        public static readonly ModernUiTileRecipe CommonPanel = new ModernUiTileRecipe(new[]
        {
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r0_c0", 0, 0, "corner-tl"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r0_c1", 0, 1, "edge-t"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r0_c2", 0, 2, "corner-tr"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r1_c0", 1, 0, "edge-l"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r1_c1", 1, 1, "fill"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r1_c2", 1, 2, "edge-r"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r2_c0", 2, 0, "corner-bl"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r2_c1", 2, 1, "edge-b"),
            new ModernUiSpriteKey(ModernUISpriteAddresses.Style16, "ModernUI_16_Style1_r2_c2", 2, 2, "corner-br"),
        });

        public static readonly ModernUiWindowRecipe SettingsWindow = new ModernUiWindowRecipe(
            "settings", new[] { "commonPanel", "title-tabs", "close-button", "toggle-row", "slider-row" });

        public static readonly ModernUiWindowRecipe InventoryWindow = new ModernUiWindowRecipe(
            "inventory", new[] { "commonPanel", "title-tabs", "slot-grid", "scrollbar", "selection-cursor" });

        public static readonly ModernUiWindowRecipe StatusWindow = new ModernUiWindowRecipe(
            "status", new[] { "commonPanel", "title-tabs", "portrait-frame", "gauge-row", "bottom-buttons" });
    }
}
