using System;
using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueOutcome_Encyclopedia", menuName = "Rootborn/Discovery Clues/Outcomes/Encyclopedia")]
    public sealed class DiscoveryClueEncyclopediaOutcome : DiscoveryClueOutcomeBase
    {
        [SerializeField] private EncyclopediaEntryDefinition[] _entries = Array.Empty<EncyclopediaEntryDefinition>();
        public override string OutcomeId => DiscoveryClueOutcomeIds.Encyclopedia;

        public override bool Apply(in DiscoveryClueContext context, DiscoveryClueDefinition clue, List<string> discoveredEntries, List<string> careerHints, List<string> followUpQuests, List<string> worldStateFlags)
        {
            bool applied = false;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                if (context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(entry.Id, entry.RevealStage, DateTime.UtcNow.Ticks)) applied = true;
                DiscoveryClueOutcomeIds.AddUnique(discoveredEntries, entry.Id);
            }

            return applied;
        }

        public void ConfigureForTests(EncyclopediaEntryDefinition[] entries)
        {
            _entries = entries ?? Array.Empty<EncyclopediaEntryDefinition>();
        }
    }
}
