using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateUsageOutcome_CareerHint", menuName = "Rootborn/World State/Usage/Outcomes/Career Hint")]
    public sealed class WorldStateUsageCareerHintOutcome : WorldStateUsageOutcomeBase
    {
        [SerializeField] private CareerDefinition[] _careers = Array.Empty<CareerDefinition>();
        public override string OutcomeId => WorldStateUsageOutcomeIds.CareerHint;

        public override bool Apply(in WorldStateUsageContext context, WorldStateUsageDefinition usage, List<string> unlockedActivities, List<string> unlockedQuests, List<string> discoveredEntries, List<string> careerHints)
        {
            bool applied = false;
            for (int i = 0; i < _careers.Length; i++)
            {
                var career = _careers[i];
                if (career == null || string.IsNullOrEmpty(career.Id)) continue;
                if (context.StudentLifeProgress == null || !context.StudentLifeProgress.IsCareerHintUnlocked(career)) applied = true;
                context.StudentLifeProgress?.UnlockCareerHint(career);
                WorldStateUsageOutcomeIds.AddUnique(careerHints, career.Id);
            }
            return applied;
        }

        public void ConfigureForTests(CareerDefinition[] careers)
        {
            _careers = careers ?? Array.Empty<CareerDefinition>();
        }
    }
}
