using System.Collections.Generic;

namespace Rootborn.Game.Common
{
    public static class ModernUiCommonPanel48Sprites
    {
        public const string BasePath = "Assets/modernuserinterface-win/48x48/Sliced_UI_Result/Panel/CommonPanel/";
        public const string AddressPrefix = "sprites/ui/modern/48/common-panel/";

        public const string TopLeftAddress = AddressPrefix + "tl";
        public const string TopAddress = AddressPrefix + "t";
        public const string TopRightAddress = AddressPrefix + "tr";
        public const string LeftAddress = AddressPrefix + "l";
        public const string FillAddress = AddressPrefix + "fill";
        public const string RightAddress = AddressPrefix + "r";
        public const string BottomLeftAddress = AddressPrefix + "bl";
        public const string BottomAddress = AddressPrefix + "b";
        public const string BottomRightAddress = AddressPrefix + "br";

        public const string TopLeftPath = BasePath + "ModernUI_48_Style2_r2_c0_common_panel_tl.png";
        public const string TopPath = BasePath + "ModernUI_48_Style2_r2_c1_common_panel_t.png";
        public const string TopRightPath = BasePath + "ModernUI_48_Style2_r2_c2_common_panel_tr.png";
        public const string LeftPath = BasePath + "ModernUI_48_Style2_r3_c0_common_panel_l.png";
        public const string FillPath = BasePath + "ModernUI_48_Style2_r3_c1_common_panel_fill.png";
        public const string RightPath = BasePath + "ModernUI_48_Style2_r3_c2_common_panel_r.png";
        public const string BottomLeftPath = BasePath + "ModernUI_48_Style2_r4_c0_common_panel_bl.png";
        public const string BottomPath = BasePath + "ModernUI_48_Style2_r4_c1_common_panel_b.png";
        public const string BottomRightPath = BasePath + "ModernUI_48_Style2_r4_c2_common_panel_br.png";

        public static readonly ModernUiSpriteKey TopLeft = Tile(TopLeftAddress, 2, 0, "corner-tl");
        public static readonly ModernUiSpriteKey Top = Tile(TopAddress, 2, 1, "edge-t");
        public static readonly ModernUiSpriteKey TopRight = Tile(TopRightAddress, 2, 2, "corner-tr");
        public static readonly ModernUiSpriteKey Left = Tile(LeftAddress, 3, 0, "edge-l");
        public static readonly ModernUiSpriteKey Fill = Tile(FillAddress, 3, 1, "fill");
        public static readonly ModernUiSpriteKey Right = Tile(RightAddress, 3, 2, "edge-r");
        public static readonly ModernUiSpriteKey BottomLeft = Tile(BottomLeftAddress, 4, 0, "corner-bl");
        public static readonly ModernUiSpriteKey Bottom = Tile(BottomAddress, 4, 1, "edge-b");
        public static readonly ModernUiSpriteKey BottomRight = Tile(BottomRightAddress, 4, 2, "corner-br");

        public static readonly IReadOnlyList<ModernUiSpriteKey> Tiles = new[]
        {
            TopLeft, Top, TopRight,
            Left, Fill, Right,
            BottomLeft, Bottom, BottomRight,
        };

        public static readonly (string assetPath, string address)[] AddressableEntries = new[]
        {
            (TopLeftPath, TopLeftAddress),
            (TopPath, TopAddress),
            (TopRightPath, TopRightAddress),
            (LeftPath, LeftAddress),
            (FillPath, FillAddress),
            (RightPath, RightAddress),
            (BottomLeftPath, BottomLeftAddress),
            (BottomPath, BottomAddress),
            (BottomRightPath, BottomRightAddress),
        };

        private static ModernUiSpriteKey Tile(string address, int row, int column, string role)
        {
            return new ModernUiSpriteKey(address, string.Empty, row, column, role);
        }
    }
}
