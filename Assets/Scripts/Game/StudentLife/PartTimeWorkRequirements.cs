using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "WorkRequirement_TraitThreshold", menuName = "Rootborn/Student Life/Part-Time Work/Requirements/Trait Threshold")]
    public sealed class WorkTraitThresholdRequirement : WorkRequirementBase
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

    [CreateAssetMenu(fileName = "WorkRequirement_SkillThreshold", menuName = "Rootborn/Student Life/Part-Time Work/Requirements/Skill Threshold")]
    public sealed class WorkSkillThresholdRequirement : WorkRequirementBase
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

    [CreateAssetMenu(fileName = "WorkRequirement_RelationshipThreshold", menuName = "Rootborn/Student Life/Part-Time Work/Requirements/Relationship Threshold")]
    public sealed class WorkRelationshipThresholdRequirement : WorkRequirementBase
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

    [CreateAssetMenu(fileName = "WorkRequirement_StatusThreshold", menuName = "Rootborn/Student Life/Part-Time Work/Requirements/Status Threshold")]
    public sealed class WorkStatusThresholdRequirement : WorkRequirementBase
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
