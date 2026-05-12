using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "StatusDeltaEffect_New", menuName = "Rootborn/Student Life/Effects/Condition Status Delta")]
    public sealed class StatusDeltaEffect : LifeActivityEffectBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public StatusDefinition Status => _status;
        public int Delta => _delta;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.AddStatus(_status, _delta, string.Empty);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }
}
