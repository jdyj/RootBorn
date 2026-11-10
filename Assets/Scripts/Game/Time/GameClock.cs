using System;
using Rootborn.Core.Time;
using UnityEngine;

namespace Rootborn.Game.Time
{
    /// <summary>씬에 놓이는 게임 시계. 계산은 Rootborn.Core의 DayClock이 하고, 여기서는 프레임 시간을 넘기고 이벤트를 알린다.</summary>
    public sealed class GameClock : MonoBehaviour
    {
        [SerializeField] private TimeDefinition _timeDefinition;
        [SerializeField] private float _realSecondsPerGameDay = 600f;
        [SerializeField] private int _startDay = 1;
        [SerializeField] private float _timeScale = 1f;

        private readonly DayClock _clock = new DayClock();

        public float ElapsedRealSeconds => _clock.ElapsedRealSeconds;
        public int Day => _clock.Day;
        public float DayProgress01 => _clock.DayProgress01;
        public float RealSecondsPerGameDay => Mathf.Max(1f, _realSecondsPerGameDay);
        public float ElapsedGameDays => ElapsedRealSeconds / RealSecondsPerGameDay;
        public float CurrentHour => _clock.CurrentHour;
        public int Hour => _clock.Hour;
        public int Minute => _clock.Minute;
        public bool IsMorning => DayParts.IsMorningHour(CurrentHour);
        public bool IsAfternoon => DayParts.IsAfternoonHour(CurrentHour);
        public bool IsEvening => DayParts.IsEveningHour(CurrentHour);
        public bool IsNight => DayParts.IsNightHour(CurrentHour);

        // Fires when the integer day changes.
        public event Action<int> OnDayRolled;
        public event Action<float> OnTimeChanged;

        public static GameClock Instance { get; private set; }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        public static bool IsMorningHour(float hour) => DayParts.IsMorningHour(hour);
        public static bool IsAfternoonHour(float hour) => DayParts.IsAfternoonHour(hour);
        public static bool IsEveningHour(float hour) => DayParts.IsEveningHour(hour);
        public static bool IsNightHour(float hour) => DayParts.IsNightHour(hour);
        public static float WrapHour(float hour) => DayParts.WrapHour(hour);

        public void Configure(TimeDefinition definition)
        {
            _timeDefinition = definition;
            ApplyDefinition();
            OnTimeChanged?.Invoke(CurrentHour);
        }

        public void SetCurrentHour(float hour)
        {
            SyncClockSettings();
            _clock.SetCurrentHour(hour);
            OnTimeChanged?.Invoke(CurrentHour);
        }

        private void Awake()
        {
            Instance = this;
            ApplyDefinition();
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
            SyncClockSettings();
            int previousDay = _clock.Day;
            int rolled = _clock.Tick(deltaSeconds);

            for (int i = 1; i <= rolled; i++)
            {
                OnDayRolled?.Invoke(previousDay + i);
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
            }
            else
            {
                _realSecondsPerGameDay = _timeDefinition.RealSecondsPerGameDay;
                _startDay = _timeDefinition.StartDay;
                _timeScale = _timeDefinition.InitialTimeScale;
            }

            SyncClockSettings();
        }

        // 하루 길이와 시작 일차는 직렬화 필드가 원본이다. 계산 직전에 Core 시계에 넘긴다.
        private void SyncClockSettings()
        {
            _clock.Configure(_realSecondsPerGameDay, _startDay);
        }
    }
}
