using System;
using UnityEngine;

namespace Rootborn.Game.Time
{
    public sealed class GameClock : MonoBehaviour
    {
        [SerializeField] private float _realSecondsPerGameDay = 360f;
        [SerializeField] private float _timeScale = 1f;

        public float ElapsedRealSeconds { get; private set; }
        public int Day { get; private set; } = 1;
        public float DayProgress01 { get; private set; }

        // 새 day 정수가 시작될 때 발화. FarmGrid 가 구독하여 일일 물/비료 리셋.
        public event Action<int> OnDayRolled;

        public static GameClock Instance { get; private set; }

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        private void Awake()
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
            ElapsedRealSeconds += deltaSeconds;
            float dayLen = Mathf.Max(1f, _realSecondsPerGameDay);
            DayProgress01 = (ElapsedRealSeconds % dayLen) / dayLen;
            Day = 1 + Mathf.FloorToInt(ElapsedRealSeconds / dayLen);
            if (Day != prevDay)
            {
                OnDayRolled?.Invoke(Day);
            }
        }
    }
}
