using Rootborn.Core.Common;

namespace Rootborn.Core.Time
{
    /// <summary>시각(0~24)이 하루의 어느 때인지. 아침 5~11, 낮 11~17, 저녁 17~20, 밤 20~5.</summary>
    public static class DayParts
    {
        public static float WrapHour(float hour) => CoreMath.Repeat(hour, 24f);

        public static bool IsMorningHour(float hour)
        {
            hour = WrapHour(hour);
            return hour >= 5f && hour < 11f;
        }

        public static bool IsAfternoonHour(float hour)
        {
            hour = WrapHour(hour);
            return hour >= 11f && hour < 17f;
        }

        public static bool IsEveningHour(float hour)
        {
            hour = WrapHour(hour);
            return hour >= 17f && hour < 20f;
        }

        public static bool IsNightHour(float hour)
        {
            hour = WrapHour(hour);
            return hour >= 20f || hour < 5f;
        }
    }
}
