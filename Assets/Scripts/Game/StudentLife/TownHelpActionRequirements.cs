using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "HelpActionRequirement_TraitThreshold", menuName = "Rootborn/Student Life/Town Help/Requirements/Trait Threshold")]
    public sealed class HelpActionTraitThresholdRequirement : HelpActionRequirementBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _trait != null && progress.GetTraitValue(_trait) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(TraitDefinition trait, int minimumValue)
        {
            _trait = trait;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "HelpActionRequirement_SkillThreshold", menuName = "Rootborn/Student Life/Town Help/Requirements/Skill Threshold")]
    public sealed class HelpActionSkillThresholdRequirement : HelpActionRequirementBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _skill != null && progress.GetSkillValue(_skill) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(SkillDefinition skill, int minimumValue)
        {
            _skill = skill;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "HelpActionRequirement_RelationshipThreshold", menuName = "Rootborn/Student Life/Town Help/Requirements/Relationship Threshold")]
    public sealed class HelpActionRelationshipThresholdRequirement : HelpActionRequirementBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _relationship != null && progress.GetRelationshipValue(_relationship) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int minimumValue)
        {
            _relationship = relationship;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "HelpActionRequirement_StatusThreshold", menuName = "Rootborn/Student Life/Town Help/Requirements/Status Threshold")]
    public sealed class HelpActionStatusThresholdRequirement : HelpActionRequirementBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _status != null && progress.GetStatusValue(_status) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(StatusDefinition status, int minimumValue)
        {
            _status = status;
            _minimumValue = minimumValue;
        }
    }
}
