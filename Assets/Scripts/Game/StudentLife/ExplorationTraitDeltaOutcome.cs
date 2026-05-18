using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_TraitDelta", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Trait Delta")]
    public sealed class ExplorationTraitDeltaOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;
        public override string OutcomeId => _trait != null ? "trait:" + _trait.Id : string.Empty;
        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice) => true;
        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || _trait == null || _delta == 0) return false;
            context.StudentProgress.AddTrait(_trait, _delta);
            return true;
        }
        public void ConfigureForTests(TraitDefinition trait, int delta) { _trait = trait; _delta = delta; }
    }
}
