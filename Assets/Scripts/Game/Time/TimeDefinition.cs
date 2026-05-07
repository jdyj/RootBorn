using UnityEngine;

namespace Rootborn.Game.Time
{
    [CreateAssetMenu(fileName = "Time_Default", menuName = "Rootborn/Time/Time Definition")]
    public sealed class TimeDefinition : ScriptableObject
    {
        [SerializeField] private float _realSecondsPerGameDay = 600f;
        [SerializeField] private int _startDay = 1;
        [SerializeField] private float _initialTimeScale = 1f;

        public float RealSecondsPerGameDay => Mathf.Max(1f, _realSecondsPerGameDay);
        public int StartDay => Mathf.Max(1, _startDay);
        public float InitialTimeScale => Mathf.Max(0f, _initialTimeScale);
    }
}
