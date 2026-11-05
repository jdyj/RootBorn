using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_DayAtLeast", menuName = "Rootborn/Quests/Conditions/Day At Least")]
    public sealed class DayAtLeastCondition : QuestConditionBase
    {
        [SerializeField] private int _minimumDay = 1;
        [SerializeField] private string _blockedSummaryKey;

        public int MinimumDay => _minimumDay;
        public override string BlockedReasonId => "wait.day." + _minimumDay;
        public override string BlockedSummary => string.IsNullOrEmpty(_blockedSummaryKey) ? base.BlockedSummary : _blockedSummaryKey;

        public override bool IsSatisfied(in QuestRuntimeContext context) => context.StudentLifeProgress != null && context.StudentLifeProgress.CurrentDay >= _minimumDay;

        public void ConfigureForTests(int minimumDay, string blockedSummaryKey)
        {
            _minimumDay = minimumDay;
            _blockedSummaryKey = blockedSummaryKey ?? string.Empty;
        }
    }
}
