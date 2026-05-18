using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueOutcome_CareerHint", menuName = "Rootborn/Discovery Clues/Outcomes/Career Hint")]
    public sealed class DiscoveryClueCareerHintOutcome : DiscoveryClueOutcomeBase
    {
        [SerializeField] private CareerDefinition[] _careers = Array.Empty<CareerDefinition>();
        public override string OutcomeId => DiscoveryClueOutcomeIds.CareerHint;

        public override bool Apply(in DiscoveryClueContext context, DiscoveryClueDefinition clue, List<string> discoveredEntries, List<string> careerHints, List<string> followUpQuests, List<string> worldStateFlags)
        {
            bool applied = false;
            for (int i = 0; i < _careers.Length; i++)
            {
                var career = _careers[i];
                if (career == null || string.IsNullOrEmpty(career.Id)) continue;
                if (context.StudentLifeProgress == null || !context.StudentLifeProgress.IsCareerHintUnlocked(career)) applied = true;
                context.StudentLifeProgress?.UnlockCareerHint(career);
                DiscoveryClueOutcomeIds.AddUnique(careerHints, career.Id);
            }

            return applied;
        }

        public void ConfigureForTests(CareerDefinition[] careers)
        {
            _careers = careers ?? Array.Empty<CareerDefinition>();
        }
    }
}
