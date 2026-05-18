using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateUsageOutcome_ActivityUnlock", menuName = "Rootborn/World State/Usage/Outcomes/Activity Unlock")]
    public sealed class WorldStateUsageActivityUnlockOutcome : WorldStateUsageOutcomeBase
    {
        [SerializeField] private LocationActivityDefinition[] _activities = Array.Empty<LocationActivityDefinition>();
        public override string OutcomeId => WorldStateUsageOutcomeIds.Activity;

        public override bool Apply(in WorldStateUsageContext context, WorldStateUsageDefinition usage, List<string> unlockedActivities, List<string> unlockedQuests, List<string> discoveredEntries, List<string> careerHints)
        {
            bool applied = false;
            for (int i = 0; i < _activities.Length; i++)
            {
                var activity = _activities[i];
                if (activity == null || string.IsNullOrEmpty(activity.Id)) continue;
                WorldStateUsageOutcomeIds.AddUnique(unlockedActivities, activity.Id);
                applied = true;
            }
            return applied;
        }

        public void ConfigureForTests(LocationActivityDefinition[] activities)
        {
            _activities = activities ?? Array.Empty<LocationActivityDefinition>();
        }
    }
}
