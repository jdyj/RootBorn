using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationCondition_RelationshipThreshold", menuName = "Rootborn/Discovery Clues/Interpretations/Conditions/Relationship Threshold")]
    public sealed class ClueInterpretationRelationshipThresholdCondition : ClueInterpretationConditionBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _minimumValue;

        public override bool Evaluate(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation)
        {
            return context.StudentLifeProgress != null && _relationship != null && context.StudentLifeProgress.GetRelationshipValue(_relationship) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int minimumValue)
        {
            _relationship = relationship;
            _minimumValue = Mathf.Max(0, minimumValue);
        }
    }
}
