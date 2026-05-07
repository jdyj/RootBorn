namespace Rootborn.Game.Common
{
    public static class ModernUISpriteAddresses
    {
        public const string Style16 = "sprites/ui/modern/16/style-1";
        public const string Style16Alt = "sprites/ui/modern/16/style-2";
        public const string Gamepad16 = "sprites/ui/modern/16/gamepad";
        public const string Style32 = "sprites/ui/modern/32/style-1";
        public const string Style32Alt = "sprites/ui/modern/32/style-2";
        public const string Gamepad32 = "sprites/ui/modern/32/gamepad";
        public const string Style48 = "sprites/ui/modern/48/style-1";
        public const string Style48Alt = "sprites/ui/modern/48/style-2";
        public const string Gamepad48 = "sprites/ui/modern/48/gamepad";

        public const string Style16First = "ModernUI_16_Style1_r0_c0";
        public const string Style16Last = "ModernUI_16_Style1_r42_c60";
        public const string Style16AltFirst = "ModernUI_16_Style2_r0_c0";
        public const string Style16AltLast = "ModernUI_16_Style2_r33_c48";
        public const string Gamepad16First = "ModernUI_16_Gamepad_r0_c0";
        public const string Gamepad16Last = "ModernUI_16_Gamepad_r50_c50";

        public const string Style32First = "ModernUI_32_Style1_r0_c0";
        public const string Style32Last = "ModernUI_32_Style1_r42_c60";
        public const string Style32AltFirst = "ModernUI_32_Style2_r0_c0";
        public const string Style32AltLast = "ModernUI_32_Style2_r33_c48";
        public const string Gamepad32First = "ModernUI_32_Gamepad_r0_c0";
        public const string Gamepad32Last = "ModernUI_32_Gamepad_r50_c50";

        public const string Style48First = "ModernUI_48_Style1_r0_c0";
        public const string Style48Last = "ModernUI_48_Style1_r42_c60";
        public const string Style48AltFirst = "ModernUI_48_Style2_r0_c0";
        public const string Style48AltLast = "ModernUI_48_Style2_r33_c48";
        public const string Gamepad48First = "ModernUI_48_Gamepad_r0_c0";
        public const string Gamepad48Last = "ModernUI_48_Gamepad_r50_c50";

        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[]
        {
            (Style16, new[]
            {
                Style16First, Style16Last,
                "ModernUI_16_Style1_r0_c1", "ModernUI_16_Style1_r0_c2",
                "ModernUI_16_Style1_r1_c0", "ModernUI_16_Style1_r1_c1", "ModernUI_16_Style1_r1_c2",
                "ModernUI_16_Style1_r1_c7", "ModernUI_16_Style1_r1_c8", "ModernUI_16_Style1_r1_c9",
                "ModernUI_16_Style1_r2_c0", "ModernUI_16_Style1_r2_c1", "ModernUI_16_Style1_r2_c2",
                "ModernUI_16_Style1_r2_c3", "ModernUI_16_Style1_r2_c8", "ModernUI_16_Style1_r2_c9",
                "ModernUI_16_Style1_r3_c0", "ModernUI_16_Style1_r3_c3", "ModernUI_16_Style1_r3_c8",
                "ModernUI_16_Style1_r4_c3",
            }),
            (Style16Alt, new[] { Style16AltFirst, Style16AltLast }),
            (Gamepad16, new[] { Gamepad16First, Gamepad16Last }),
            (Style32, new[]
            {
                Style32First, Style32Last,
                "ModernUI_32_Style1_r0_c1", "ModernUI_32_Style1_r0_c2", "ModernUI_32_Style1_r0_c3",
                "ModernUI_32_Style1_r0_c4", "ModernUI_32_Style1_r0_c5", "ModernUI_32_Style1_r0_c8",
            }),
            (Style32Alt, new[] { Style32AltFirst, Style32AltLast, "ModernUI_32_Style2_r1_c0" }),
            (Gamepad32, new[] { Gamepad32First, Gamepad32Last }),
            (Style48, new[] { Style48First, Style48Last }),
            (Style48Alt, new[] { Style48AltFirst, Style48AltLast }),
            (Gamepad48, new[] { Gamepad48First, Gamepad48Last }),
        };
    }
}
