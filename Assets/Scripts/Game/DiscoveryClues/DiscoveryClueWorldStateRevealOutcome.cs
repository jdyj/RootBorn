using System;
using System.Collections.Generic;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueOutcome_WorldStateReveal", menuName = "Rootborn/Discovery Clues/Outcomes/World State Reveal")]
    public sealed class DiscoveryClueWorldStateRevealOutcome : DiscoveryClueOutcomeBase
    {
        [SerializeField] private WorldStateFlagDefinition[] _flags = Array.Empty<WorldStateFlagDefinition>();
        public override string OutcomeId => DiscoveryClueOutcomeIds.WorldState;

        public override bool Apply(in DiscoveryClueContext context, DiscoveryClueDefinition clue, List<string> discoveredEntries, List<string> careerHints, List<string> followUpQuests, List<string> worldStateFlags)
        {
            bool applied = false;
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (flag == null || string.IsNullOrEmpty(flag.Id)) continue;
                if (context.WorldStateProgress == null || context.WorldStateProgress.TryActivate(flag, new WorldStateActivationSource(string.Empty, string.Empty, clue != null ? clue.Id : string.Empty, context.CurrentDay))) applied = true;
                DiscoveryClueOutcomeIds.AddUnique(worldStateFlags, flag.Id);
            }

            return applied;
        }

        public void ConfigureForTests(WorldStateFlagDefinition[] flags)
        {
            _flags = flags ?? Array.Empty<WorldStateFlagDefinition>();
        }
    }
}
