using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_StatusDelta", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Status Delta")]
    public sealed class ExplorationStatusDeltaOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;
        public override string OutcomeId => _status != null ? "status:" + _status.Id : string.Empty;
        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice) => true;
        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || _status == null || _delta == 0) return false;
            context.StudentProgress.AddStatus(_status, _delta, interaction != null ? interaction.Id : string.Empty);
            collector?.AddFatigueStatus(_status.Id);
            return true;
        }
        public void ConfigureForTests(StatusDefinition status, int delta) { _status = status; _delta = delta; }
    }
}
