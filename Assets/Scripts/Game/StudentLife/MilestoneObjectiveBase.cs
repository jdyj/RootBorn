using System;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public abstract class MilestoneObjectiveBase : StudentLifeDefinitionBase
    {
        [SerializeField] private int _requiredValue = 1;

        public int RequiredValue => Mathf.Max(1, _requiredValue);

        public MilestoneObjectiveEvaluation Evaluate(in MilestoneEvaluationContext context)
        {
            return new MilestoneObjectiveEvaluation(Id, EvaluateCurrent(in context), RequiredValue);
        }

        protected void ConfigureObjectiveForTests(string id, int requiredValue)
        {
            ConfigureForTests(id, id);
            _requiredValue = Mathf.Max(1, requiredValue);
        }

        protected abstract int EvaluateCurrent(in MilestoneEvaluationContext context);
    }

    [CreateAssetMenu(fileName = "MilestoneObjective_Trait", menuName = "Rootborn/Student Life/Milestones/Objectives/Trait")]
    public sealed class TraitMilestoneObjective : MilestoneObjectiveBase
    {
        [SerializeField] private TraitDefinition _trait;

        public TraitDefinition Trait => _trait;

        public void ConfigureForTests(string id, TraitDefinition trait, int requiredValue)
        {
            ConfigureObjectiveForTests(id, requiredValue);
            _trait = trait;
        }

        protected override int EvaluateCurrent(in MilestoneEvaluationContext context)
        {
            return context.StudentLifeProgress == null || _trait == null ? 0 : context.StudentLifeProgress.GetTraitValue(_trait);
        }
    }

    [CreateAssetMenu(fileName = "MilestoneObjective_Skill", menuName = "Rootborn/Student Life/Milestones/Objectives/Skill")]
    public sealed class SkillMilestoneObjective : MilestoneObjectiveBase
    {
        [SerializeField] private SkillDefinition _skill;

        public SkillDefinition Skill => _skill;

        public void ConfigureForTests(string id, SkillDefinition skill, int requiredValue)
        {
            ConfigureObjectiveForTests(id, requiredValue);
            _skill = skill;
        }

        protected override int EvaluateCurrent(in MilestoneEvaluationContext context)
        {
            return context.StudentLifeProgress == null || _skill == null ? 0 : context.StudentLifeProgress.GetSkillValue(_skill);
        }
    }

    [CreateAssetMenu(fileName = "MilestoneObjective_Relationship", menuName = "Rootborn/Student Life/Milestones/Objectives/Relationship")]
    public sealed class RelationshipMilestoneObjective : MilestoneObjectiveBase
    {
        [SerializeField] private RelationshipDefinition _relationship;

        public RelationshipDefinition Relationship => _relationship;

        public void ConfigureForTests(string id, RelationshipDefinition relationship, int requiredValue)
        {
            ConfigureObjectiveForTests(id, requiredValue);
            _relationship = relationship;
        }

        protected override int EvaluateCurrent(in MilestoneEvaluationContext context)
        {
            return context.StudentLifeProgress == null || _relationship == null ? 0 : context.StudentLifeProgress.GetRelationshipValue(_relationship);
        }
    }

    [CreateAssetMenu(fileName = "MilestoneObjective_Status", menuName = "Rootborn/Student Life/Milestones/Objectives/Status")]
    public sealed class StatusMilestoneObjective : MilestoneObjectiveBase
    {
        [SerializeField] private StatusDefinition _status;

        public StatusDefinition Status => _status;

        public void ConfigureForTests(string id, StatusDefinition status, int requiredValue)
        {
            ConfigureObjectiveForTests(id, requiredValue);
            _status = status;
        }

        protected override int EvaluateCurrent(in MilestoneEvaluationContext context)
        {
            return context.StudentLifeProgress == null || _status == null ? 0 : context.StudentLifeProgress.GetStatusValue(_status);
        }
    }
}
