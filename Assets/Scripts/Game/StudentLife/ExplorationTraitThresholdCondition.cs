using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationCondition_TraitThreshold", menuName = "Rootborn/Student Life/Exploration Choices/Conditions/Trait Threshold")]
    public sealed class ExplorationTraitThresholdCondition : ExplorationConditionBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimum;

        public override bool Evaluate(in ExplorationInteractionContext context, ExplorationChoiceDefinition choice)
        {
            return context.StudentProgress != null && _trait != null && context.StudentProgress.GetTraitValue(_trait) >= _minimum;
        }

        public void ConfigureForTests(TraitDefinition trait, int minimum, string lockedReasonKey)
        {
            _trait = trait;
            _minimum = minimum;
            SetLockedReasonForTests(lockedReasonKey);
        }
    }
}
