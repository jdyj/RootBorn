using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ActivityReq_SkillThreshold", menuName = "Rootborn/Student Life/Activity Requirements/Skill Threshold")]
    public sealed class SkillThresholdActivityRequirement : LifeActivityRequirementBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && progress.GetSkillValue(_skill) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(SkillDefinition skill, int minimumValue)
        {
            _skill = skill;
            _minimumValue = minimumValue;
        }
    }
}
