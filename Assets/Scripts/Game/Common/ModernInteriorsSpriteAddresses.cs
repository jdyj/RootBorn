namespace Rootborn.Game.Common
{
    public static class ModernInteriorsSpriteAddresses
    {
        public const string Floors16 = "sprites/interiors/modern/16/floors";
        public const string Walls16 = "sprites/interiors/modern/16/walls";
        public const string Ui16 = "sprites/interiors/modern/16/ui";

        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[]
        {
            (Floors16, new[] { "ModernInterior_Floor_r0_c0", "ModernInterior_Floor_r39_c14" }),
            (Walls16, new[] { "ModernInterior_Wall_r0_c0", "ModernInterior_Wall_r39_c31" }),
            (Ui16, new[] { "ModernInterior_UI_r0_c0", "ModernInterior_UI_r15_c17" }),
        };
    }
}
