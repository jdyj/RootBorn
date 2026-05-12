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
        public static readonly ModernHudSpriteKey HudPanel = ForPurpose("HudPanel", ModernUiStyle2Sprites.Panel.Base);
        public static readonly ModernHudSpriteKey HintPanel = ForPurpose("HintPanel", ModernUiStyle2Sprites.Panel.Hint);
        public static readonly ModernHudSpriteKey SmallButton = ForPurpose("SmallButton", ModernUiStyle2Sprites.Button.Small);
        public static readonly ModernHudSpriteKey ItemSlot = ForPurpose("ItemSlot", ModernUiStyle2Sprites.Slot.Item);
        public static readonly ModernHudSpriteKey EquipmentSlot = ForPurpose("EquipmentSlot", ModernUiStyle2Sprites.Slot.Equipment);
        public static readonly ModernHudSpriteKey ItemsRibbon = ForPurpose("ItemsRibbon", ModernUiStyle2Sprites.Ribbon.Items);
        public static readonly ModernHudSpriteKey DescriptionRibbon = ForPurpose("DescriptionRibbon", ModernUiStyle2Sprites.Ribbon.Description);
        public static readonly ModernHudSpriteKey EquipmentRibbon = ForPurpose("EquipmentRibbon", ModernUiStyle2Sprites.Ribbon.Equipment);
        public static readonly ModernHudSpriteKey CutterShort = ForPurpose("CutterShort", ModernUiStyle2Sprites.Divider.Short);
        public static readonly ModernHudSpriteKey CutterLong = ForPurpose("CutterLong", ModernUiStyle2Sprites.Divider.Long);
        public static readonly ModernHudSpriteKey InscriptionPlus = ForPurpose("InscriptionPlus", ModernUiStyle2Sprites.Button.Action);
        public static readonly ModernHudSpriteKey BookmarkAll = ForPurpose("BookmarkAll", ModernUiStyle2Sprites.Tab.All);
        public static readonly ModernHudSpriteKey BookmarkResource = ForPurpose("BookmarkResource", ModernUiStyle2Sprites.Tab.Resource);
        public static readonly ModernHudSpriteKey BookmarkTool = ForPurpose("BookmarkTool", ModernUiStyle2Sprites.Tab.Tool);
        public static readonly ModernHudSpriteKey BookmarkEquipment = ForPurpose("BookmarkEquipment", ModernUiStyle2Sprites.Tab.Equipment);
        public static readonly ModernHudSpriteKey BookmarkMisc = ForPurpose("BookmarkMisc", ModernUiStyle2Sprites.Tab.Misc);
        public static readonly ModernHudSpriteKey Character = ForPurpose("Character", ModernUiStyle2Sprites.Portrait.Character);

        public static readonly IReadOnlyList<ModernHudSpriteKey> All = new[]
        {
            HudPanel, HintPanel, SmallButton, ItemSlot, EquipmentSlot,
            ItemsRibbon, DescriptionRibbon, EquipmentRibbon, CutterShort, CutterLong,
            InscriptionPlus, BookmarkAll, BookmarkResource, BookmarkTool, BookmarkEquipment,
            BookmarkMisc, Character,
        };

        private static ModernHudSpriteKey ForPurpose(string purpose, ModernHudSpriteKey style2Sprite)
        {
            return new ModernHudSpriteKey(purpose, style2Sprite.SheetAddress, style2Sprite.SubSpriteName);
        }
    }
}
