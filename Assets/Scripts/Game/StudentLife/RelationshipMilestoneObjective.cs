using UnityEngine;

namespace Rootborn.Game.StudentLife
{
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
}