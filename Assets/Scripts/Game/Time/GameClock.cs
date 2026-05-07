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

        // 새 day 정수가 시작될 때 발화. FarmGrid 가 구독하여 일일 물/비료 리셋.
        public event Action<int> OnDayRolled;

        public static GameClock Instance { get; private set; }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        public void Configure(TimeDefinition definition)
        {
            _timeDefinition = definition;
            ApplyDefinition();
            RecalculateDay();
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
