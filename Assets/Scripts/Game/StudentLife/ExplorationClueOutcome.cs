using Rootborn.Game.DiscoveryClues;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_Clue", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Clue")]
    public sealed class ExplorationClueOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private DiscoveryClueDefinition _clue;
        [SerializeField] private DiscoveryClueSourceDefinition _source;

        public override string OutcomeId => _clue != null ? "clue:" + _clue.Id : string.Empty;

        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice)
        {
            return _clue != null;
        }

        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (_clue == null || string.IsNullOrEmpty(_clue.Id)) return false;
            bool alreadySeen = context.DiscoveryClueProgress != null && context.DiscoveryClueProgress.GetRecord(_clue.Id).Seen;
            if (context.DiscoveryClueProgress != null)
            {
                context.DiscoveryClueProgress.MarkSourceSeen(_clue, ResolveSource(), context.CurrentDay);
            }

            collector?.AddClue(_clue.Id);
            return !alreadySeen;
        }

        public void ConfigureForTests(DiscoveryClueDefinition clue, DiscoveryClueSourceDefinition source)
        {
            _clue = clue;
            _source = source;
        }

        private DiscoveryClueSourceDefinition ResolveSource()
        {
            if (_source != null) return _source;
            if (_clue == null || _clue.Sources == null || _clue.Sources.Count == 0) return null;
            return _clue.Sources[0];
        }
    }
}
