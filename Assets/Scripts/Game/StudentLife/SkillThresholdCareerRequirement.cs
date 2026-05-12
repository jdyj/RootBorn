using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CareerReq_SkillThreshold", menuName = "Rootborn/Student Life/Career Requirements/Skill Threshold")]
    public sealed class SkillThresholdCareerRequirement : CareerUnlockRequirementBase
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
