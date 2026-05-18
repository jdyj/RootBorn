using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_LocationActivity", menuName = "Rootborn/Quests/Objectives/Location Activity")]
    public sealed class LocationActivityQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private LocationActivityDefinition _targetActivity;

        public LocationActivityDefinition TargetActivity => _targetActivity;

        public void ConfigureForRuntime(string displayKey, int requiredCount, LocationActivityDefinition targetActivity)
        {
            base.ConfigureForRuntime(displayKey, requiredCount);
            _targetActivity = targetActivity;
        }

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.LocationActivity
                && (_targetActivity == null || questEvent.Activity == _targetActivity);
        }
    }
}
