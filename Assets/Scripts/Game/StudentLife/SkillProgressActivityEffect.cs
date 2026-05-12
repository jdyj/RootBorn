using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ActivityFx_SkillProgress", menuName = "Rootborn/Student Life/Activity Effects/Skill Progress")]
    public sealed class SkillProgressActivityEffect : LifeActivityEffectBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.AddSkill(_skill, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }
}
