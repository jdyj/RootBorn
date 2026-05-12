using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "OutsideSchoolRequirement_TraitThreshold", menuName = "Rootborn/Student Life/Outside School/Requirements/Trait Threshold")]
    public sealed class OutsideSchoolTraitThresholdRequirement : OutsideSchoolRequirementBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimumValue;

        public TraitDefinition Trait => _trait;
        public int MinimumValue => Mathf.Max(0, _minimumValue);

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _trait != null && progress.GetTraitValue(_trait) >= MinimumValue;
        }

        public void ConfigureForTests(TraitDefinition trait, int minimumValue)
        {
            _trait = trait;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "OutsideSchoolRequirement_SkillThreshold", menuName = "Rootborn/Student Life/Outside School/Requirements/Skill Threshold")]
    public sealed class OutsideSchoolSkillThresholdRequirement : OutsideSchoolRequirementBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _minimumValue;

        public SkillDefinition Skill => _skill;
        public int MinimumValue => Mathf.Max(0, _minimumValue);

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _skill != null && progress.GetSkillValue(_skill) >= MinimumValue;
        }

        public void ConfigureForTests(SkillDefinition skill, int minimumValue)
        {
            _skill = skill;
            _minimumValue = minimumValue;
        }
    }
}
