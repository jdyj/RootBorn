using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateRelationshipCondition : LocationStateConditionBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _minimumValue;
        public override bool IsSatisfied(in LocationStateContext context) => context.StudentProgress != null && context.StudentProgress.GetRelationshipValue(_relationship) >= Mathf.Max(0, _minimumValue);
        public void ConfigureForTests(RelationshipDefinition relationship, int minimumValue) { _relationship = relationship; _minimumValue = minimumValue; }
    }
}
