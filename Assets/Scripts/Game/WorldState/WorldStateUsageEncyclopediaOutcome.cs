using System;
using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateUsageOutcome_Encyclopedia", menuName = "Rootborn/World State/Usage/Outcomes/Encyclopedia")]
    public sealed class WorldStateUsageEncyclopediaOutcome : WorldStateUsageOutcomeBase
    {
        [SerializeField] private EncyclopediaEntryDefinition[] _entries = Array.Empty<EncyclopediaEntryDefinition>();
        public override string OutcomeId => WorldStateUsageOutcomeIds.Encyclopedia;

        public override bool Apply(in WorldStateUsageContext context, WorldStateUsageDefinition usage, List<string> unlockedActivities, List<string> unlockedQuests, List<string> discoveredEntries, List<string> careerHints)
        {
            bool applied = false;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                if (context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(entry.Id, entry.RevealStage, DateTime.UtcNow.Ticks)) applied = true;
                WorldStateUsageOutcomeIds.AddUnique(discoveredEntries, entry.Id);
            }
            return applied;
        }

        public void ConfigureForTests(EncyclopediaEntryDefinition[] entries)
        {
            _entries = entries ?? Array.Empty<EncyclopediaEntryDefinition>();
        }
    }
}
