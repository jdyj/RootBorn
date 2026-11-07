using Rootborn.Core.Quests;
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

        // No target accepts any location activity.
        protected override QuestObjectiveSpec BuildSpec() => _targetActivity == null
            ? QuestObjectiveSpec.AnyEvent((int)QuestEventKind.LocationActivity)
            : QuestObjectiveSpec.Listed((int)QuestEventKind.LocationActivity, QuestSubjectIds.Of(_targetActivity));
    }
}
