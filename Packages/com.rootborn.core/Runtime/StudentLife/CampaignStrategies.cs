using System;

namespace Rootborn.Core.StudentLife
{
    /// <summary>활동 기록·오늘 결과·어제 결과 중 접두어가 맞는 줄 수를 센다.</summary>
    public sealed class CampaignActivityLogObjective : CampaignObjective
    {
        public CampaignActivityLogObjective(string id, string requiredLogPrefix, int requiredCount, bool requiresSchoolClass) : base(id, requiresSchoolClass)
        {
            RequiredLogPrefix = string.IsNullOrEmpty(requiredLogPrefix) ? string.Empty : requiredLogPrefix;
            RequiredCount = Math.Max(1, requiredCount);
        }

        public string RequiredLogPrefix { get; }
        public int RequiredCount { get; }

        public override CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context)
        {
            int count = 0;
            StudentLifeProgress progress = context.StudentProgress;
            if (progress != null)
            {
                count += CountMatches(progress.GetActivityLogIds());
                count += CountMatches(progress.GetTodayResultLogIds());
                count += CountMatches(progress.GetPreviousDayResultLogIds());
            }
            return new CampaignObjectiveEvaluation(count, RequiredCount);
        }

        private int CountMatches(string[] values)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrEmpty(values[i]) && values[i].StartsWith(RequiredLogPrefix, StringComparison.Ordinal)) count++;
            return count;
        }
    }

    public sealed class CampaignCareerHintObjective : CampaignObjective
    {
        public CampaignCareerHintObjective(string id, string careerId) : base(id, false) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }

        public override CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context)
            => new CampaignObjectiveEvaluation(context.StudentProgress != null && context.StudentProgress.IsCareerHintUnlocked(CareerId) ? 1 : 0, 1);
    }

    /// <summary>방문한 장소 목록에서 정한 장소가 나온 횟수를 센다.</summary>
    public sealed class CampaignLocationVisitObjective : CampaignObjective
    {
        public CampaignLocationVisitObjective(string id, string locationId, int requiredCount) : base(id, false)
        {
            LocationId = locationId ?? string.Empty;
            RequiredCount = Math.Max(1, requiredCount);
        }

        public string LocationId { get; }
        public int RequiredCount { get; }

        public override CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context)
        {
            int count = 0;
            for (int i = 0; i < context.VisitedLocationIds.Count; i++)
                if (LocationId.Length > 0 && context.VisitedLocationIds[i] == LocationId) count++;
            return new CampaignObjectiveEvaluation(count, RequiredCount);
        }
    }

    public sealed class CampaignCareerHintReward : CampaignReward
    {
        public CampaignCareerHintReward(string id, string careerId) : base(id) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }
        public override void Apply(StudentLifeProgress progress) => progress?.UnlockCareerHint(CareerId);
    }
}
