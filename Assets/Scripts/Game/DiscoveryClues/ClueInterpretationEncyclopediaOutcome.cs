using System;
using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationOutcome_Encyclopedia", menuName = "Rootborn/Discovery Clues/Interpretations/Outcomes/Encyclopedia")]
    public sealed class ClueInterpretationEncyclopediaOutcome : ClueInterpretationOutcomeBase
    {
        [SerializeField] private EncyclopediaEntryDefinition[] _entries = Array.Empty<EncyclopediaEntryDefinition>();
        public override string OutcomeId => ClueInterpretationOutcomeIds.Encyclopedia;
        public override bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds)
        {
            bool applied = false;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                if (context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(entry.Id, entry.RevealStage, DateTime.UtcNow.Ticks)) applied = true;
                ClueInterpretationOutcomeIds.AddUnique(encyclopediaEntryIds, entry.Id);
            }
            return applied;
        }
        public void ConfigureForTests(EncyclopediaEntryDefinition[] entries) => _entries = entries ?? Array.Empty<EncyclopediaEntryDefinition>();
    }
}
