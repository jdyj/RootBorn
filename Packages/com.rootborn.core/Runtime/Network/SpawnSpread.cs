namespace Rootborn.Core.Network
{
    public readonly struct SpawnOffset
    {
        public SpawnOffset(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// Where the n-th joining player stands around the shared spawn point, so players do not appear on top of each other.
    /// Two rows of four, one unit apart (a body is one unit wide); after eight the pattern repeats.
    /// </summary>
    public static class SpawnSpread
    {
        private static readonly float[] Columns = { 0f, 1f, -1f, 2f };

        public static SpawnOffset Offset(int joinIndex)
        {
            int index = joinIndex < 0 ? 0 : joinIndex % 8;
            return new SpawnOffset(Columns[index % 4], index < 4 ? 0f : -1f);
        }
    }
}
