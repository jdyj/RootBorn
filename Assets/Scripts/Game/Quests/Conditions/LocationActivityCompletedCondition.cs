using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_LocationActivityCompleted", menuName = "Rootborn/Quests/Conditions/Location Activity Completed")]
    public sealed class LocationActivityCompletedCondition : QuestConditionBase
    {
        [SerializeField] private LocationActivityDefinition _activity;
        [SerializeField] private string _blockedReasonId = "missing.location.activity";
        [SerializeField] private string _blockedSummary = "Complete the required location activity to continue.";

        public override string BlockedReasonId => string.IsNullOrEmpty(_blockedReasonId) ? base.BlockedReasonId : _blockedReasonId;
        public override string BlockedSummary => string.IsNullOrEmpty(_blockedSummary) ? base.BlockedSummary : _blockedSummary;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            if (_activity == null || context.StudentLifeProgress == null) return false;
            var ids = context.StudentLifeProgress.GetActivityLogIds();
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == _activity.Id) return true;
            }

            var todayIds = context.StudentLifeProgress.GetTodayActivityIds();
            for (int i = 0; i < todayIds.Length; i++)
            {
                if (todayIds[i] == _activity.Id) return true;
            }

            return false;
        }

        public void ConfigureForTests(LocationActivityDefinition activity, string blockedReasonId, string blockedSummary)
        {
            _activity = activity;
            _blockedReasonId = blockedReasonId ?? string.Empty;
            _blockedSummary = blockedSummary ?? string.Empty;
        }
    }
}
