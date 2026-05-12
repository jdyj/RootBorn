namespace Rootborn.Game.Common
{
    public static class ModernUISpriteAddresses
    {
        public const string Style16 = "sprites/ui/modern/16/style-1";
        public const string Style16Alt = "sprites/ui/modern/16/style-2";
        public const string Gamepad16 = "sprites/ui/modern/16/gamepad";

        public const string Style16First = "ModernUI_16_Style1_r0_c0";
        public const string Style16Last = "ModernUI_16_Style1_r41_c38";
        public const string Gamepad16First = "ModernUI_16_Gamepad_r0_c0";
        public const string Gamepad16Last = "ModernUI_16_Gamepad_r50_c50";

        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[]
        {
            (Style16, new[]
            {
                Style16First, Style16Last,
                "ModernUI_16_Style1_r0_c1", "ModernUI_16_Style1_r0_c2",
                "ModernUI_16_Style1_r1_c0", "ModernUI_16_Style1_r1_c1", "ModernUI_16_Style1_r1_c2",
                "ModernUI_16_Style1_r1_c8", "ModernUI_16_Style1_r1_c9",
                "ModernUI_16_Style1_r2_c0", "ModernUI_16_Style1_r2_c1", "ModernUI_16_Style1_r2_c2",
                "ModernUI_16_Style1_r2_c3", "ModernUI_16_Style1_r2_c8", "ModernUI_16_Style1_r2_c9",
                "ModernUI_16_Style1_r3_c0", "ModernUI_16_Style1_r3_c3", "ModernUI_16_Style1_r3_c9",
                "ModernUI_16_Style1_r5_c3",
            }),
            (Style16Alt, ModernUiStyle2Sprites.PreloadSubSprites),
        };
    }
}
