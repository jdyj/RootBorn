using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ActivityFx_TraitDelta", menuName = "Rootborn/Student Life/Activity Effects/Trait Delta")]
    public sealed class TraitDeltaActivityEffect : LifeActivityEffectBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.AddTrait(_trait, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }
}
