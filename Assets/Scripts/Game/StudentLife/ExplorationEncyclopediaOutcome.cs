using System;
using Rootborn.Game.Encyclopedia;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_Encyclopedia", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Encyclopedia")]
    public sealed class ExplorationEncyclopediaOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private EncyclopediaEntryDefinition _entry;

        public override string OutcomeId => _entry != null ? "encyclopedia:" + _entry.Id : string.Empty;

        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice)
        {
            return _entry != null;
        }

        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (_entry == null || string.IsNullOrEmpty(_entry.Id)) return false;
            bool applied = context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(_entry.Id, _entry.RevealStage, DateTime.UtcNow.Ticks);
            collector?.AddEncyclopediaEntry(_entry.Id);
            return applied;
        }

        public void ConfigureForTests(EncyclopediaEntryDefinition entry)
        {
            _entry = entry;
        }
    }
}
