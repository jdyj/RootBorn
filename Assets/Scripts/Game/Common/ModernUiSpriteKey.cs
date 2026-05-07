namespace Rootborn.Game.Common
{
    public readonly struct ModernUiSpriteKey
    {
        public ModernUiSpriteKey(string sheetAddress, string subSpriteName, int row, int column, string role)
        {
            SheetAddress = sheetAddress;
            SubSpriteName = subSpriteName;
            Row = row;
            Column = column;
            Role = role;
        }

        public string SheetAddress { get; }
        public string SubSpriteName { get; }
        public int Row { get; }
        public int Column { get; }
        public string Role { get; }
    }
}
