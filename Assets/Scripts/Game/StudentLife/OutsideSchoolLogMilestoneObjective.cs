using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "MilestoneObjective_OutsideSchoolLog", menuName = "Rootborn/Student Life/Milestones/Objectives/Outside School Log")]
    public sealed class OutsideSchoolLogMilestoneObjective : MilestoneObjectiveBase
    {
        [SerializeField] private string _targetId;

        public string TargetId => string.IsNullOrEmpty(_targetId) ? string.Empty : _targetId;

        public void ConfigureForTests(string id, string targetId, int requiredDelta)
        {
            ConfigureObjectiveForTests(id, requiredDelta);
            _targetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
        }

        protected override int EvaluateCurrent(in MilestoneEvaluationContext context)
        {
            int total = 0;
            AddLogs(context.StudentLifeProgress != null ? context.StudentLifeProgress.GetTodayResultLogIds() : null, ref total);
            AddLogs(context.StudentLifeProgress != null ? context.StudentLifeProgress.GetPreviousDayResultLogIds() : null, ref total);
            AddLogs(context.AdditionalResultLogIds, ref total);
            return total;
        }

        private void AddLogs(string[] logs, ref int total)
        {
            if (logs == null) return;
            for (int i = 0; i < logs.Length; i++)
            {
                if (!OutsideSchoolLogCodec.TryParseDelta(logs[i], out var entry)) continue;
                if (!string.IsNullOrEmpty(TargetId) && !string.Equals(entry.TargetId, TargetId, StringComparison.Ordinal)) continue;
                total += Mathf.Max(0, entry.Delta);
            }
        }
    }
}
