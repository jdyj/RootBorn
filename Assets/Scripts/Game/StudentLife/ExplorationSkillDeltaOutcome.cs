using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_SkillDelta", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Skill Delta")]
    public sealed class ExplorationSkillDeltaOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;
        public override string OutcomeId => _skill != null ? "skill:" + _skill.Id : string.Empty;
        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice) => true;
        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || _skill == null || _delta == 0) return false;
            context.StudentProgress.AddSkill(_skill, _delta);
            return true;
        }
        public void ConfigureForTests(SkillDefinition skill, int delta) { _skill = skill; _delta = delta; }
    }
}
