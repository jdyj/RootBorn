using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ActivityTimePolicy", menuName = "Rootborn/Student Life/Activity Time Policy")]
    public sealed class ActivityTimePolicyDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField, Min(0)] private int _timePointCost = 1;
        [SerializeField, Range(0, 127)] private int _allowedDaysMask = 127;
        [SerializeField] private bool _oncePerDay;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public int TimePointCost => Mathf.Max(0, _timePointCost);
        public bool OncePerDay => _oncePerDay;
        public bool IsAllowedDay(int day) => (_allowedDaysMask & (1 << LifeCalendarDefinition.WeekdayIndexForDay(day))) != 0;
        public void ConfigureForTests(string id, int cost, int allowedDaysMask, bool oncePerDay)
        {
            _id = id;
            _timePointCost = Mathf.Max(0, cost);
            _allowedDaysMask = allowedDaysMask & 127;
            _oncePerDay = oncePerDay;
        }
    }
}
