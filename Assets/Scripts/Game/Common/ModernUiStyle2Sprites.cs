using System.Collections.Generic;

namespace Rootborn.Game.Common
{
    public static class ModernUiStyle2Sprites
    {
        private const string Prefix = "ModernUI_16_Style2_";

        public const string SheetAddress = ModernUISpriteAddresses.Style16Alt;
        public const string First = Prefix + "r0_c0";
        public const string Last = Prefix + "r33_c48";

        public static class CommonPanel
        {
            public const string TopLeftName = Prefix + "r2_c0";
            public const string TopName = Prefix + "r2_c1";
            public const string TopRightName = Prefix + "r2_c2";
            public const string LeftName = Prefix + "r3_c0";
            public const string FillName = Prefix + "r3_c1";
            public const string RightName = Prefix + "r3_c2";
            public const string BottomLeftName = Prefix + "r4_c0";
            public const string BottomName = Prefix + "r4_c1";
            public const string BottomRightName = Prefix + "r4_c2";

            public static readonly ModernUiSpriteKey TopLeft = Tile(TopLeftName, 2, 0, "corner-tl");
            public static readonly ModernUiSpriteKey Top = Tile(TopName, 2, 1, "edge-t");
            public static readonly ModernUiSpriteKey TopRight = Tile(TopRightName, 2, 2, "corner-tr");
            public static readonly ModernUiSpriteKey Left = Tile(LeftName, 3, 0, "edge-l");
            public static readonly ModernUiSpriteKey Fill = Tile(FillName, 3, 1, "fill");
            public static readonly ModernUiSpriteKey Right = Tile(RightName, 3, 2, "edge-r");
            public static readonly ModernUiSpriteKey BottomLeft = Tile(BottomLeftName, 4, 0, "corner-bl");
            public static readonly ModernUiSpriteKey Bottom = Tile(BottomName, 4, 1, "edge-b");
            public static readonly ModernUiSpriteKey BottomRight = Tile(BottomRightName, 4, 2, "corner-br");

            public static readonly IReadOnlyList<ModernUiSpriteKey> Tiles = new[]
            {
                TopLeft, Top, TopRight,
                Left, Fill, Right,
                BottomLeft, Bottom, BottomRight,
            };
        }

        public static class Panel
        {
            public const string BaseName = First;
            public const string FillName = Prefix + "r1_c1";
            public const string InsetFillName = Prefix + "r1_c2";

            public static readonly ModernHudSpriteKey Base = Key("PanelBase", BaseName);
            public static readonly ModernHudSpriteKey Hint = Key("HintPanel", BaseName);
            public static readonly ModernHudSpriteKey Fill = Key("PanelFill", FillName);
            public static readonly ModernHudSpriteKey InsetFill = Key("PanelInsetFill", InsetFillName);
        }

        public static class Ribbon
        {
            public const string ItemsName = Prefix + "r0_c1";
            public const string DescriptionName = Prefix + "r0_c2";
            public const string EquipmentName = Prefix + "r0_c3";

            public static readonly ModernHudSpriteKey Items = Key("ItemsRibbon", ItemsName);
            public static readonly ModernHudSpriteKey Description = Key("DescriptionRibbon", DescriptionName);
            public static readonly ModernHudSpriteKey Equipment = Key("EquipmentRibbon", EquipmentName);
        }

        public static class Button
        {
            public const string SmallName = Prefix + "r0_c8";
            public const string ActionName = Prefix + "r1_c8";
            public const string CloseName = Prefix + "r1_c9";
            public const string SelectedName = Prefix + "r2_c9";

            public static readonly ModernHudSpriteKey Small = Key("SmallButton", SmallName);
            public static readonly ModernHudSpriteKey Action = Key("ActionButton", ActionName);
            public static readonly ModernHudSpriteKey Close = Key("CloseButton", CloseName);
            public static readonly ModernHudSpriteKey Selected = Key("SelectedButton", SelectedName);
        }

        public static class Slot
        {
            public const string ItemName = Prefix + "r0_c4";
            public const string EquipmentName = Prefix + "r0_c5";
            public const string IconFrameName = Prefix + "r3_c0";

            public static readonly ModernHudSpriteKey Item = Key("ItemSlot", ItemName);
            public static readonly ModernHudSpriteKey Equipment = Key("EquipmentSlot", EquipmentName);
            public static readonly ModernHudSpriteKey IconFrame = Key("IconFrame", IconFrameName);
        }

        public static class Tab
        {
            public const string AllName = Prefix + "r1_c8";
            public const string ResourceName = Prefix + "r1_c9";
            public const string TitleName = Prefix + "r2_c8";
            public const string SelectedName = Prefix + "r2_c9";
            public const string MiscName = Prefix + "r3_c9";

            public static readonly ModernHudSpriteKey All = Key("BookmarkAll", AllName);
            public static readonly ModernHudSpriteKey Resource = Key("BookmarkResource", ResourceName);
            public static readonly ModernHudSpriteKey Title = Key("TitleTab", TitleName);
            public static readonly ModernHudSpriteKey Tool = Key("BookmarkTool", TitleName);
            public static readonly ModernHudSpriteKey Selected = Key("SelectedTab", SelectedName);
            public static readonly ModernHudSpriteKey Equipment = Key("BookmarkEquipment", SelectedName);
            public static readonly ModernHudSpriteKey Misc = Key("BookmarkMisc", MiscName);
        }

        public static class Scrollbar
        {
            public const string ThumbName = Prefix + "r2_c3";
            public const string TrackName = Prefix + "r3_c3";

            public static readonly ModernHudSpriteKey Thumb = Key("ScrollbarThumb", ThumbName);
            public static readonly ModernHudSpriteKey Track = Key("ScrollbarTrack", TrackName);
        }

        public static class Divider
        {
            public const string ShortName = Scrollbar.ThumbName;
            public const string LongName = Scrollbar.TrackName;

            public static readonly ModernHudSpriteKey Short = Key("DividerShort", ShortName);
            public static readonly ModernHudSpriteKey Long = Key("DividerLong", LongName);
        }

        public static class Gauge
        {
            public const string TrackName = Scrollbar.TrackName;
            public const string FillName = Prefix + "r5_c3";

            public static readonly ModernHudSpriteKey Track = Key("GaugeTrack", TrackName);
            public static readonly ModernHudSpriteKey Fill = Key("GaugeFill", FillName);
        }

        public static class Portrait
        {
            public const string CharacterName = Prefix + "r1_c0";
            public const string FrameName = Prefix + "r3_c9";

            public static readonly ModernHudSpriteKey Character = Key("Character", CharacterName);
            public static readonly ModernHudSpriteKey Frame = Key("PortraitFrame", FrameName);
        }

        public static class Hud
        {
            public const string PanelBaseName = Panel.BaseName;
            public const string SmallButtonName = Button.SmallName;
            public const string ItemSlotName = Slot.ItemName;
            public const string EquipmentSlotName = Slot.EquipmentName;
            public const string ItemsRibbonName = Ribbon.ItemsName;
            public const string DescriptionRibbonName = Ribbon.DescriptionName;
            public const string EquipmentRibbonName = Ribbon.EquipmentName;
            public const string CutterShortName = Divider.ShortName;
            public const string CutterLongName = Divider.LongName;
            public const string InscriptionPlusName = Button.ActionName;
            public const string BookmarkResourceName = Tab.ResourceName;
            public const string BookmarkToolName = Tab.TitleName;
            public const string BookmarkEquipmentName = Tab.SelectedName;
            public const string BookmarkMiscName = Tab.MiscName;
            public const string CharacterName = Portrait.CharacterName;

            public static readonly ModernHudSpriteKey PanelBase = Panel.Base;
            public static readonly ModernHudSpriteKey HintPanel = Panel.Hint;
            public static readonly ModernHudSpriteKey SmallButton = Button.Small;
            public static readonly ModernHudSpriteKey ItemSlot = Slot.Item;
            public static readonly ModernHudSpriteKey EquipmentSlot = Slot.Equipment;
            public static readonly ModernHudSpriteKey ItemsRibbon = Ribbon.Items;
            public static readonly ModernHudSpriteKey DescriptionRibbon = Ribbon.Description;
            public static readonly ModernHudSpriteKey EquipmentRibbon = Ribbon.Equipment;
            public static readonly ModernHudSpriteKey CutterShort = Divider.Short;
            public static readonly ModernHudSpriteKey CutterLong = Divider.Long;
            public static readonly ModernHudSpriteKey InscriptionPlus = Button.Action;
            public static readonly ModernHudSpriteKey BookmarkAll = Tab.All;
            public static readonly ModernHudSpriteKey BookmarkResource = Tab.Resource;
            public static readonly ModernHudSpriteKey BookmarkTool = Tab.Tool;
            public static readonly ModernHudSpriteKey BookmarkEquipment = Tab.Equipment;
            public static readonly ModernHudSpriteKey BookmarkMisc = Tab.Misc;
            public static readonly ModernHudSpriteKey Character = Portrait.Character;
        }

        public static readonly string[] PreloadSubSprites = new[]
        {
            First,
            Ribbon.ItemsName, Ribbon.DescriptionName, Ribbon.EquipmentName,
            Slot.ItemName, Slot.EquipmentName, Button.SmallName,
            Portrait.CharacterName, Panel.FillName, Panel.InsetFillName,
            Button.ActionName, Button.CloseName,
            CommonPanel.TopLeftName, CommonPanel.TopName, CommonPanel.TopRightName,
            Scrollbar.ThumbName, Tab.TitleName, Tab.SelectedName,
            CommonPanel.LeftName, CommonPanel.FillName, CommonPanel.RightName,
            Scrollbar.TrackName, Tab.MiscName,
            CommonPanel.BottomLeftName, CommonPanel.BottomName, CommonPanel.BottomRightName,
            Gauge.FillName,
        };

        private static ModernHudSpriteKey Key(string purpose, string subSpriteName)
        {
            return new ModernHudSpriteKey(purpose, SheetAddress, subSpriteName);
        }

        private static ModernUiSpriteKey Tile(string subSpriteName, int row, int column, string role)
        {
            return new ModernUiSpriteKey(SheetAddress, subSpriteName, row, column, role);
        }
    }
}
