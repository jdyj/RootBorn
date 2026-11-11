namespace Rootborn.Core.Network
{
    /// <summary>
    /// The clock hour that stands for each shared part of day (morning, afternoon, evening, night), so local systems that
    /// read hours, such as day-night lighting, show the same part of day as the shared world time.
    /// </summary>
    public static class WorldTimeHours
    {
        private static readonly float[] Hours = { 9f, 14f, 18.5f, 22f };

        public static float Representative(int partOfDay)
        {
            int index = partOfDay < 0 ? 0 : partOfDay >= Hours.Length ? Hours.Length - 1 : partOfDay;
            return Hours[index];
        }
    }
}
