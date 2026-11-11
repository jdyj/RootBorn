using System;

namespace Rootborn.Core.Quests
{
    /// <summary>
    /// A yes/no rule about the player's state. When it blocks, <see cref="BlockedReasonId"/> and <see cref="BlockedSummary"/>
    /// say why; by default the type name and the definition's name (the game used the asset name).
    /// </summary>
    public abstract class QuestCondition
    {
        protected QuestCondition(string name = null)
        {
            Name = name ?? string.Empty;
        }

        public string Name { get; }
        public virtual string BlockedReasonId => GetType().Name;
        public virtual string BlockedSummary => Name;

        public abstract bool IsSatisfied(in QuestRuntimeContext context);
    }

    public sealed class DayAtLeastCondition : QuestCondition
    {
        private readonly string _blockedSummaryKey;

        public DayAtLeastCondition(int minimumDay, string blockedSummaryKey = null, string name = null) : base(name)
        {
            MinimumDay = minimumDay;
            _blockedSummaryKey = blockedSummaryKey ?? string.Empty;
        }

        public int MinimumDay { get; }
        public override string BlockedReasonId => "wait.day." + MinimumDay;
        public override string BlockedSummary => string.IsNullOrEmpty(_blockedSummaryKey) ? base.BlockedSummary : _blockedSummaryKey;

        public override bool IsSatisfied(in QuestRuntimeContext context) => context.StudentLifeProgress != null && context.StudentLifeProgress.CurrentDay >= MinimumDay;
    }

    public sealed class KnowledgeUnlockedCondition : QuestCondition
    {
        public KnowledgeUnlockedCondition(string knowledgeId, string name = null) : base(name) { KnowledgeId = knowledgeId; }
        public string KnowledgeId { get; }

        public override bool IsSatisfied(in QuestRuntimeContext context)
            => context.KnowledgeProgress != null && !string.IsNullOrEmpty(KnowledgeId) && context.KnowledgeProgress.IsUnlocked(KnowledgeId);
    }

    /// <summary>The activity is in the student's activity log or today's activities.</summary>
    public sealed class LocationActivityCompletedCondition : QuestCondition
    {
        private readonly string _blockedReasonId;
        private readonly string _blockedSummary;

        public LocationActivityCompletedCondition(string activityId, string blockedReasonId = "missing.location.activity",
            string blockedSummary = "Complete the required location activity to continue.", string name = null) : base(name)
        {
            ActivityId = activityId;
            _blockedReasonId = blockedReasonId ?? string.Empty;
            _blockedSummary = blockedSummary ?? string.Empty;
        }

        public string ActivityId { get; }
        public override string BlockedReasonId => string.IsNullOrEmpty(_blockedReasonId) ? base.BlockedReasonId : _blockedReasonId;
        public override string BlockedSummary => string.IsNullOrEmpty(_blockedSummary) ? base.BlockedSummary : _blockedSummary;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            if (ActivityId == null || context.StudentLifeProgress == null) return false;
            return Array.IndexOf(context.StudentLifeProgress.GetActivityLogIds(), ActivityId) >= 0
                || Array.IndexOf(context.StudentLifeProgress.GetTodayActivityIds(), ActivityId) >= 0;
        }
    }

    public sealed class QuestStateCondition : QuestCondition
    {
        public QuestStateCondition(string questId, QuestStage requiredState = QuestStage.RewardClaimed, string name = null) : base(name)
        {
            QuestId = questId;
            RequiredState = requiredState;
        }

        public string QuestId { get; }
        public QuestStage RequiredState { get; }

        public override bool IsSatisfied(in QuestRuntimeContext context)
            => context.QuestLog != null && !string.IsNullOrEmpty(QuestId) && context.QuestLog.GetState(QuestId) == RequiredState;
    }

    public sealed class StoryFlagCondition : QuestCondition
    {
        public StoryFlagCondition(string flagId, string name = null) : base(name) { FlagId = flagId; }
        public string FlagId { get; }

        public override bool IsSatisfied(in QuestRuntimeContext context)
            => context.StoryFlags != null && !string.IsNullOrEmpty(FlagId) && context.StoryFlags.IsSet(FlagId);
    }
}
