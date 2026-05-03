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

        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Mathf.Max(0f, value);
        }

        private void Update()
        {
            Tick(UnityEngine.Time.deltaTime * _timeScale);
        }

        public void Tick(float deltaSeconds)
        {
            ElapsedRealSeconds += deltaSeconds;
            float dayLen = Mathf.Max(1f, _realSecondsPerGameDay);
            DayProgress01 = (ElapsedRealSeconds % dayLen) / dayLen;
            Day = 1 + Mathf.FloorToInt(ElapsedRealSeconds / dayLen);
        }
    }
}
