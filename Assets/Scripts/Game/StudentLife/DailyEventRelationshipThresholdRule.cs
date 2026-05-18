using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventRule_RelationshipThreshold", menuName = "Rootborn/Student Life/Daily Events/Rules/Relationship Threshold")]
    public sealed class DailyEventRelationshipThresholdRule : DailyEventAvailabilityRuleBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(DailyEventContext context, DailyEventDefinition dailyEvent)
        {
            return context.StudentLifeProgress != null && _relationship != null && context.StudentLifeProgress.GetRelationshipValue(_relationship) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int minimumValue)
        {
            _relationship = relationship;
            _minimumValue = minimumValue;
        }
    }
}
