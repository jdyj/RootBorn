using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CampaignObjective_ActivityLog", menuName = "Rootborn/Student Life/Campaigns/Objectives/Activity Log")]
    public sealed class CampaignActivityLogObjective : CampaignObjectiveBase
    {
        [SerializeField] private string _requiredLogPrefix;
        [SerializeField] private int _requiredCount = 1;

        public void ConfigureForTests(string id, string requiredLogPrefix, int requiredCount, bool requiresSchoolClass)
        {
            ConfigureObjectiveForTests(id, requiresSchoolClass);
            _requiredLogPrefix = string.IsNullOrEmpty(requiredLogPrefix) ? string.Empty : requiredLogPrefix;
            _requiredCount = Mathf.Max(1, requiredCount);
        }

        public override CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context)
        {
            int count = 0;
            var progress = context.StudentProgress;
            if (progress != null)
            {
                CountMatches(progress.GetActivityLogIds(), ref count);
                CountMatches(progress.GetTodayResultLogIds(), ref count);
                CountMatches(progress.GetPreviousDayResultLogIds(), ref count);
            }
            return new CampaignObjectiveEvaluation(count, _requiredCount);
        }

        private void CountMatches(string[] values, ref int count)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i]) && values[i].StartsWith(_requiredLogPrefix, StringComparison.Ordinal)) count++;
        }
    }
}
