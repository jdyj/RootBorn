using System;
using System.Collections.Generic;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationOutcome_WorldState", menuName = "Rootborn/Discovery Clues/Interpretations/Outcomes/World State")]
    public sealed class ClueInterpretationWorldStateOutcome : ClueInterpretationOutcomeBase
    {
        [SerializeField] private WorldStateFlagDefinition[] _flags = Array.Empty<WorldStateFlagDefinition>();
        public override string OutcomeId => ClueInterpretationOutcomeIds.WorldState;
        public override bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds)
        {
            bool applied = false;
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (flag == null || string.IsNullOrEmpty(flag.Id)) continue;
                if (context.WorldStateProgress == null || context.WorldStateProgress.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, interpretation != null ? interpretation.Id : string.Empty, context.CurrentDay))) applied = true;
                ClueInterpretationOutcomeIds.AddUnique(worldStateIds, flag.Id);
            }
            return applied;
        }
        public void ConfigureForTests(WorldStateFlagDefinition[] flags) => _flags = flags ?? Array.Empty<WorldStateFlagDefinition>();
    }
}
