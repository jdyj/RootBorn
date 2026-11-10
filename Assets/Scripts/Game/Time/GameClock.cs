using System;
using UnityEngine;

namespace Rootborn.Game.Time
{
    public sealed class GameClock : MonoBehaviour
    {
        [SerializeField] private TimeDefinition _timeDefinition;
        [SerializeField] private float _realSecondsPerGameDay = 600f;
        [SerializeField] private int _startDay = 1;
        [SerializeField] private float _timeScale = 1f;

        public float ElapsedRealSeconds { get; private set; }
        public int Day { get; private set; } = 1;
        public float DayProgress01 { get; private set; }
        public float RealSecondsPerGameDay => Mathf.Max(1f, _realSecondsPerGameDay);
        public float ElapsedGameDays => ElapsedRealSeconds / RealSecondsPerGameDay;
        public float CurrentHour => DayProgress01 * 24f;
        public int Hour => Mathf.FloorToInt(CurrentHour);
        public int Minute => Mathf.FloorToInt((CurrentHour - Hour) * 60f);
        public bool IsMorning => IsMorningHour(CurrentHour);
        public bool IsAfternoon => IsAfternoonHour(CurrentHour);
        public bool IsEvening => IsEveningHour(CurrentHour);
        public bool IsNight => IsNightHour(CurrentHour);

        // Fires when the integer day changes. FarmGrid subscribes to reset daily water/fertilizer state.
        public event Action<int> OnDayRolled;
        public event Action<float> OnTimeChanged;

        public static GameClock Instance { get; private set; }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

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

        public static float WrapHour(float hour)
        {
            return Mathf.Repeat(hour, 24f);
        }

        public void Configure(TimeDefinition definition)
        {
            _timeDefinition = definition;
            ApplyDefinition();
            RecalculateDay();
            OnTimeChanged?.Invoke(CurrentHour);
        }

        public void SetCurrentHour(float hour)
        {
            float wrapped = WrapHour(hour);
            float dayStartSeconds = Mathf.Floor(ElapsedRealSeconds / RealSecondsPerGameDay) * RealSecondsPerGameDay;
            ElapsedRealSeconds = dayStartSeconds + wrapped / 24f * RealSecondsPerGameDay;
            RecalculateDay();
            OnTimeChanged?.Invoke(CurrentHour);
        }

        private void Awake()
        {
            Instance = this;
            ApplyDefinition();
            RecalculateDay();
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Tick(UnityEngine.Time.deltaTime * _timeScale);
        }

        public void Tick(float deltaSeconds)
        {
            int prevDay = Day;
            ElapsedRealSeconds += Mathf.Max(0f, deltaSeconds);
            RecalculateDay();

            for (int rolledDay = prevDay + 1; rolledDay <= Day; rolledDay++)
            {
                OnDayRolled?.Invoke(rolledDay);
            }

            OnTimeChanged?.Invoke(CurrentHour);
        }

        private void ApplyDefinition()
        {
            if (_timeDefinition == null)
            {
                _realSecondsPerGameDay = Mathf.Max(1f, _realSecondsPerGameDay);
                _startDay = Mathf.Max(1, _startDay);
                _timeScale = Mathf.Max(0f, _timeScale);
                return;
            }

            _realSecondsPerGameDay = _timeDefinition.RealSecondsPerGameDay;
            _startDay = _timeDefinition.StartDay;
            _timeScale = _timeDefinition.InitialTimeScale;
        }

        private void RecalculateDay()
        {
            float dayLen = RealSecondsPerGameDay;
            DayProgress01 = (ElapsedRealSeconds % dayLen) / dayLen;
            Day = _startDay + Mathf.FloorToInt(ElapsedRealSeconds / dayLen);
        }
    }
}
