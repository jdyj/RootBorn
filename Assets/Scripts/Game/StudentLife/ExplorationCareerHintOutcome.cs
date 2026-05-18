using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_CareerHint", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Career Hint")]
    public sealed class ExplorationCareerHintOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private CareerDefinition _career;
        public override string OutcomeId => _career != null ? "career:" + _career.Id : string.Empty;
        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice) => true;
        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || _career == null) return false;
            bool already = context.StudentProgress.IsCareerHintUnlocked(_career);
            context.StudentProgress.UnlockCareerHint(_career);
            if (!already) collector?.AddCareerHint(_career.Id);
            return !already;
        }
        public void ConfigureForTests(CareerDefinition career) { _career = career; }
    }
}
