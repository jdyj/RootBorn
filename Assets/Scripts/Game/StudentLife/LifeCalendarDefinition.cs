using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LifeCalendar", menuName = "Rootborn/Student Life/Calendar")]
    public sealed class LifeCalendarDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] private int _weekdayBudget = 2;
        [SerializeField, Min(0)] private int _weekendBudget = 3;
        [SerializeField, Range(0, 127)] private int _weekendDaysMask = 96;

        public int GetWeekdayIndex(int day) => WeekdayIndexForDay(day);
        internal static int WeekdayIndexForDay(int day) => (Mathf.Max(1, day) - 1) % 7;
        public bool IsWeekend(int day) => (_weekendDaysMask & (1 << GetWeekdayIndex(day))) != 0;
        public int GetBudget(int day) => Mathf.Max(0, IsWeekend(day) ? _weekendBudget : _weekdayBudget);
        public void ConfigureForTests(int weekdayBudget, int weekendBudget)
        {
            _weekdayBudget = Mathf.Max(0, weekdayBudget);
            _weekendBudget = Mathf.Max(0, weekendBudget);
        }
    }
}
