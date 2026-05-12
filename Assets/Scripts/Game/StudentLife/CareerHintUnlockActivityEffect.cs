using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ActivityFx_CareerHint", menuName = "Rootborn/Student Life/Activity Effects/Career Hint Unlock")]
    public sealed class CareerHintUnlockActivityEffect : LifeActivityEffectBase
    {
        [SerializeField] private CareerDefinition _career;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.UnlockCareerHint(_career);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
