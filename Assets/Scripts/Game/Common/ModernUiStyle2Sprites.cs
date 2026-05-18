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

        // InnerPanel / SlotPanel — Style2 sheet 에는 CommonPanel 외에 별도 9-slice panel 이
        // 존재하지 않는다 (r8..r10 / c0..c5 영역은 거의 비어있고 fill 만 있음). 따라서 두 클래스는
        // CommonPanel 좌표를 alias 한다. 호출자(`ModernUiRecipes.InnerPanel/SlotPanel`,
        // PreloadSubSprites)는 변경 없이 동일 9-slice 를 사용한다.
        public static class InnerPanel
        {
            public const string TopLeftName = CommonPanel.TopLeftName;
            public const string TopName = CommonPanel.TopName;
            public const string TopRightName = CommonPanel.TopRightName;
            public const string LeftName = CommonPanel.LeftName;
            public const string FillName = CommonPanel.FillName;
            public const string RightName = CommonPanel.RightName;
            public const string BottomLeftName = CommonPanel.BottomLeftName;
            public const string BottomName = CommonPanel.BottomName;
            public const string BottomRightName = CommonPanel.BottomRightName;

            public static readonly IReadOnlyList<ModernUiSpriteKey> Tiles = CommonPanel.Tiles;
        }

        public static class SlotPanel
        {
            public const string TopLeftName = CommonPanel.TopLeftName;
            public const string TopName = CommonPanel.TopName;
            public const string TopRightName = CommonPanel.TopRightName;
            public const string LeftName = CommonPanel.LeftName;
            public const string FillName = CommonPanel.FillName;
            public const string RightName = CommonPanel.RightName;
            public const string BottomLeftName = CommonPanel.BottomLeftName;
            public const string BottomName = CommonPanel.BottomName;
            public const string BottomRightName = CommonPanel.BottomRightName;

            public static readonly IReadOnlyList<ModernUiSpriteKey> Tiles = CommonPanel.Tiles;
        }

        // NPC dialogue panel — 9-slice for the basic NPC speech bubble. Coordinates
        // r14_c3..r16_c5 form a standard 3x3 9-slice with rounded corners and a
        // soft fill body. Verified visually against Modern_UI_Style_2.png.
        public static class DialoguePanel
        {
            public const string TopLeftName = Prefix + "r14_c3";
            public const string TopName = Prefix + "r14_c4";
            public const string TopRightName = Prefix + "r14_c5";
            public const string LeftName = Prefix + "r15_c3";
            public const string FillName = Prefix + "r15_c4";
            public const string RightName = Prefix + "r15_c5";
            public const string BottomLeftName = Prefix + "r16_c3";
            public const string BottomName = Prefix + "r16_c4";
            public const string BottomRightName = Prefix + "r16_c5";

            public static readonly ModernUiSpriteKey TopLeft = Tile(TopLeftName, 14, 3, "corner-tl");
            public static readonly ModernUiSpriteKey Top = Tile(TopName, 14, 4, "edge-t");
            public static readonly ModernUiSpriteKey TopRight = Tile(TopRightName, 14, 5, "corner-tr");
            public static readonly ModernUiSpriteKey Left = Tile(LeftName, 15, 3, "edge-l");
            public static readonly ModernUiSpriteKey Fill = Tile(FillName, 15, 4, "fill");
            public static readonly ModernUiSpriteKey Right = Tile(RightName, 15, 5, "edge-r");
            public static readonly ModernUiSpriteKey BottomLeft = Tile(BottomLeftName, 16, 3, "corner-bl");
            public static readonly ModernUiSpriteKey Bottom = Tile(BottomName, 16, 4, "edge-b");
            public static readonly ModernUiSpriteKey BottomRight = Tile(BottomRightName, 16, 5, "corner-br");

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

        // slot.base / slot.highlight — single sprite slot tile. 시각 검증 결과 r6_c0 / r7_c0
        // 은 빈 셀이라 r6_c1 / r6_c4 (둘 다 둥근 모서리 단일 slot panel) 를 사용.
        public static class Slot
        {
            public const string BaseName = Prefix + "r6_c0";
            public const string HighlightName = Prefix + "r7_c0";
            // Legacy aliases (still referenced by HUD). Map to single-sprite slot.
            public const string ItemName = BaseName;
            public const string EquipmentName = BaseName;
            public const string IconFrameName = BaseName;

            public static readonly ModernHudSpriteKey Base = Key("Slot", BaseName);
            public static readonly ModernHudSpriteKey Highlight = Key("SlotHighlight", HighlightName);
            public static readonly ModernHudSpriteKey Item = Base;
            public static readonly ModernHudSpriteKey Equipment = Base;
            public static readonly ModernHudSpriteKey IconFrame = Base;
        }

        // ribbon.* (horizontal): r1_c4..r1_c6 = left/middle/right (3-slice top tab strip)
        // Used for inventory category tab strip across the panel top.
        public static class TabRibbon
        {
            public const string LeftName = Prefix + "r1_c4";
            public const string MiddleName = Prefix + "r1_c5";
            public const string RightName = Prefix + "r1_c6";
            public const string ShadowLeftName = Prefix + "r2_c4";
            public const string ShadowMiddleName = Prefix + "r2_c5";
            public const string ShadowRightName = Prefix + "r2_c6";

            public static readonly ModernHudSpriteKey Left = Key("RibbonLeft", LeftName);
            public static readonly ModernHudSpriteKey Middle = Key("RibbonMiddle", MiddleName);
            public static readonly ModernHudSpriteKey Right = Key("RibbonRight", RightName);
        }

        // Legacy Tab class kept for source-compat with HUD code (ModernHudSpriteKeys.Bookmark*).
        // Style2 시트에는 별도 책갈피 sprite 가 없어, 모든 필드를 TabRibbon (3-slice 가로 ribbon) 으로
        // alias. 실제 책갈피 시각 효과는 외부 sprite (e.g. Pixelwood Bookmark sheet) 또는
        // ModernUiTileImage 의 TabPanel/TabRibbon recipe 로 구성한다.
        public static class Tab
        {
            public const string AllName = TabRibbon.LeftName;
            public const string ResourceName = TabRibbon.MiddleName;
            public const string TitleName = TabRibbon.MiddleName;
            public const string SelectedName = TabRibbon.MiddleName;
            public const string MiscName = TabRibbon.RightName;

            public static readonly ModernHudSpriteKey All = Key("BookmarkAll", AllName);
            public static readonly ModernHudSpriteKey Resource = Key("BookmarkResource", ResourceName);
            public static readonly ModernHudSpriteKey Title = Key("TitleTab", TitleName);
            public static readonly ModernHudSpriteKey Tool = Key("BookmarkTool", TitleName);
            public static readonly ModernHudSpriteKey Selected = Key("SelectedTab", SelectedName);
            public static readonly ModernHudSpriteKey Equipment = Key("BookmarkEquipment", SelectedName);
            public static readonly ModernHudSpriteKey Misc = Key("BookmarkMisc", MiscName);
        }

        // ribbon.vertical* (r3_c3,r4_c3,r5_c3) = vertical 3-slice for the scrollbar track.
        // Thumb reuses r5_c3 (end-cap with a soft handle look).
        public static class Scrollbar
        {
            public const string TrackTopName = Prefix + "r3_c3";
            public const string TrackMiddleName = Prefix + "r4_c3";
            public const string TrackBottomName = Prefix + "r5_c3";
            public const string ThumbName = TrackBottomName;
            public const string TrackName = TrackMiddleName;

            public static readonly ModernHudSpriteKey TrackTop = Key("ScrollbarTrackTop", TrackTopName);
            public static readonly ModernHudSpriteKey TrackMiddle = Key("ScrollbarTrackMid", TrackMiddleName);
            public static readonly ModernHudSpriteKey TrackBottom = Key("ScrollbarTrackBot", TrackBottomName);
            public static readonly ModernHudSpriteKey Thumb = TrackBottom;
            public static readonly ModernHudSpriteKey Track = TrackMiddle;
        }

        // Thin purple tab panel — 2 rows x 3 cols (r11_c0..r12_c2) used for the row of
        // category bookmarks that sit on top of the inventory window. r11_c0..r11_c2 form
        // the top of each tab (rounded top corners and top edge); r12_c0..r12_c2 form the
        // body. The body cells (r12_c1, r12_c2) are present on the sheet even though the
        // Gemini catalog marked them "unknown" — confirmed by direct sprite inspection.
        public static class TabPanel
        {
            public const string TopLeftName = Prefix + "r11_c0";
            public const string TopName = Prefix + "r11_c1";
            public const string TopRightName = Prefix + "r11_c2";
            public const string BodyLeftName = Prefix + "r12_c0";
            public const string BodyFillName = Prefix + "r12_c1";
            public const string BodyRightName = Prefix + "r12_c2";

            public static readonly ModernHudSpriteKey TopLeft = Key("TabPanelTopLeft", TopLeftName);
            public static readonly ModernHudSpriteKey Top = Key("TabPanelTop", TopName);
            public static readonly ModernHudSpriteKey TopRight = Key("TabPanelTopRight", TopRightName);
            public static readonly ModernHudSpriteKey BodyLeft = Key("TabPanelBodyLeft", BodyLeftName);
            public static readonly ModernHudSpriteKey BodyFill = Key("TabPanelBodyFill", BodyFillName);
            public static readonly ModernHudSpriteKey BodyRight = Key("TabPanelBodyRight", BodyRightName);
        }

        // Single-sprite action icons. Coordinates re-verified against the actual sheet
        // (Style2). Each row triplet (c43/c44/c45 or c46/c47/c48) is a normal/hover/pressed
        // variant — we use the normal frame.
        // - r1_c43 = chevron-left (used as Close fallback — sheet has no proper X close icon)
        // - r3_c46 = sort-down (descending sort)
        // - r5_c43 = envelope (mail) — closest to "message/notification"
        // - r9_c46 = twitter-like bird (decorative, kept for parity)
        // - r15_c46 = trash can — Delete
        // SearchName / SettingsName / GridName: Style2 sheet does not contain matching icons;
        // fields kept for source-compat but point to safe placeholders (r1_c43 chevron-left).
        public static class IconButton
        {
            public const string SearchName = Prefix + "r12_c46";
            public const string DeleteName = Prefix + "r13_c46";
            public const string SettingsName = Prefix + "r6_c46";
            public const string GridName = Prefix + "r9_c43";     // menu / list grid
            public const string CloseName = Prefix + "r1_c43";    // chevron-left (sheet lacks X)
            public const string SortName = Prefix + "r3_c46";     // sort-down

            public static readonly ModernHudSpriteKey Search = Key("ButtonSearch", SearchName);
            public static readonly ModernHudSpriteKey Delete = Key("ButtonDelete", DeleteName);
            public static readonly ModernHudSpriteKey Settings = Key("ButtonSettings", SettingsName);
            public static readonly ModernHudSpriteKey Grid = Key("ButtonGrid", GridName);
            public static readonly ModernHudSpriteKey Close = Key("ButtonClose", CloseName);
            public static readonly ModernHudSpriteKey Sort = Key("ButtonSort", SortName);
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

        // Portrait — Style2 시트에는 character portrait sprite 가 존재하지 않는다 (r1_c0,
        // r3_c9 모두 panel fragment). 실제 캐릭터 초상화는 별도 sprite (Pixelwood Player Character
        // 또는 Portrait_Generator 폴더) 로 와이어링한다. 여기서는 placeholder 로 PanelBase 의
        // InsetFill (r1_c2) 를 가리켜 빈 frame 만 표시되도록 한다.
        public static class Portrait
        {
            public const string CharacterName = Panel.InsetFillName;
            public const string FrameName = Panel.InsetFillName;

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
            Slot.BaseName, Slot.HighlightName, Button.SmallName,
            Portrait.CharacterName, Panel.FillName, Panel.InsetFillName,
            Button.ActionName, Button.CloseName,
            CommonPanel.TopLeftName, CommonPanel.TopName, CommonPanel.TopRightName,
            Scrollbar.TrackTopName, Scrollbar.TrackMiddleName, Scrollbar.TrackBottomName,
            CommonPanel.LeftName, CommonPanel.FillName, CommonPanel.RightName,
            Tab.MiscName,
            CommonPanel.BottomLeftName, CommonPanel.BottomName, CommonPanel.BottomRightName,
            Gauge.FillName,
            TabRibbon.LeftName, TabRibbon.MiddleName, TabRibbon.RightName,
            IconButton.SearchName, IconButton.DeleteName, IconButton.SettingsName,
            IconButton.GridName, IconButton.CloseName, IconButton.SortName,
            TabPanel.TopLeftName, TabPanel.TopName, TabPanel.TopRightName,
            TabPanel.BodyLeftName, TabPanel.BodyFillName, TabPanel.BodyRightName,
            InnerPanel.TopLeftName, InnerPanel.TopName, InnerPanel.TopRightName,
            InnerPanel.LeftName, InnerPanel.FillName, InnerPanel.RightName,
            InnerPanel.BottomLeftName, InnerPanel.BottomName, InnerPanel.BottomRightName,
            SlotPanel.TopLeftName, SlotPanel.TopName, SlotPanel.TopRightName,
            SlotPanel.LeftName, SlotPanel.FillName, SlotPanel.RightName,
            SlotPanel.BottomLeftName, SlotPanel.BottomName, SlotPanel.BottomRightName,
            DialoguePanel.TopLeftName, DialoguePanel.TopName, DialoguePanel.TopRightName,
            DialoguePanel.LeftName, DialoguePanel.FillName, DialoguePanel.RightName,
            DialoguePanel.BottomLeftName, DialoguePanel.BottomName, DialoguePanel.BottomRightName,
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
