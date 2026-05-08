using System.Collections.Generic;

namespace Rootborn.Game.Common
{
    public readonly struct ModernHudSpriteKey
    {
        public ModernHudSpriteKey(string purpose, string sheetAddress, string subSpriteName)
        {
            Purpose = purpose;
            SheetAddress = sheetAddress;
            SubSpriteName = subSpriteName;
        }

        public string Purpose { get; }
        public string SheetAddress { get; }
        public string SubSpriteName { get; }
    }

    public static class ModernHudSpriteKeys
    {
        public static readonly ModernHudSpriteKey HudPanel = new ModernHudSpriteKey(
            "HudPanel", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c0");
        public static readonly ModernHudSpriteKey HintPanel = new ModernHudSpriteKey(
            "HintPanel", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c0");
        public static readonly ModernHudSpriteKey SmallButton = new ModernHudSpriteKey(
            "SmallButton", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c8");
        public static readonly ModernHudSpriteKey ItemSlot = new ModernHudSpriteKey(
            "ItemSlot", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c4");
        public static readonly ModernHudSpriteKey EquipmentSlot = new ModernHudSpriteKey(
            "EquipmentSlot", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c5");
        public static readonly ModernHudSpriteKey ItemsRibbon = new ModernHudSpriteKey(
            "ItemsRibbon", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c1");
        public static readonly ModernHudSpriteKey DescriptionRibbon = new ModernHudSpriteKey(
            "DescriptionRibbon", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c2");
        public static readonly ModernHudSpriteKey EquipmentRibbon = new ModernHudSpriteKey(
            "EquipmentRibbon", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r0_c3");
        public static readonly ModernHudSpriteKey CutterShort = new ModernHudSpriteKey(
            "CutterShort", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r2_c3");
        public static readonly ModernHudSpriteKey CutterLong = new ModernHudSpriteKey(
            "CutterLong", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r3_c3");
        public static readonly ModernHudSpriteKey InscriptionPlus = new ModernHudSpriteKey(
            "InscriptionPlus", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r1_c7");
        public static readonly ModernHudSpriteKey BookmarkAll = new ModernHudSpriteKey(
            "BookmarkAll", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r1_c8");
        public static readonly ModernHudSpriteKey BookmarkResource = new ModernHudSpriteKey(
            "BookmarkResource", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r1_c9");
        public static readonly ModernHudSpriteKey BookmarkTool = new ModernHudSpriteKey(
            "BookmarkTool", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r2_c8");
        public static readonly ModernHudSpriteKey BookmarkEquipment = new ModernHudSpriteKey(
            "BookmarkEquipment", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r2_c9");
        public static readonly ModernHudSpriteKey BookmarkMisc = new ModernHudSpriteKey(
            "BookmarkMisc", ModernUISpriteAddresses.Style16Alt, "ModernUI_16_Style2_r3_c8");
        public static readonly ModernHudSpriteKey Character = new ModernHudSpriteKey(
            "Character", ModernUISpriteAddresses.Style32Alt, "ModernUI_32_Style2_r1_c0");

        public static readonly IReadOnlyList<ModernHudSpriteKey> All = new[]
        {
            HudPanel, HintPanel, SmallButton, ItemSlot, EquipmentSlot,
            ItemsRibbon, DescriptionRibbon, EquipmentRibbon, CutterShort, CutterLong,
            InscriptionPlus, BookmarkAll, BookmarkResource, BookmarkTool, BookmarkEquipment,
            BookmarkMisc, Character,
        };
    }
}