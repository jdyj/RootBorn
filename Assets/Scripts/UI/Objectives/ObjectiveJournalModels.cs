using System;

namespace Rootborn.UI.Objectives
{
    [Serializable]
    public readonly struct ObjectiveJournalCategory
    {
        public readonly string CategoryId;
        public readonly string DisplayName;

        public ObjectiveJournalCategory(string categoryId, string displayName)
        {
            CategoryId = string.IsNullOrEmpty(categoryId) ? string.Empty : categoryId;
            DisplayName = string.IsNullOrEmpty(displayName) ? CategoryId : displayName;
        }
    }

    [Serializable]
    public readonly struct ObjectiveJournalItem
    {
        public readonly string StableKey;
        public readonly string CategoryId;
        public readonly string Title;
        public readonly string Subtitle;
        public readonly string State;
        public readonly string ProgressText;
        public readonly string NextHintText;
        public readonly bool IsTracked;
        public readonly int SortPriority;

        public ObjectiveJournalItem(string stableKey, string categoryId, string title, string subtitle, string state, string progressText, string nextHintText, bool isTracked, int sortPriority)
        {
            StableKey = string.IsNullOrEmpty(stableKey) ? string.Empty : stableKey;
            CategoryId = string.IsNullOrEmpty(categoryId) ? string.Empty : categoryId;
            Title = string.IsNullOrEmpty(title) ? StableKey : title;
            Subtitle = string.IsNullOrEmpty(subtitle) ? string.Empty : subtitle;
            State = string.IsNullOrEmpty(state) ? string.Empty : state;
            ProgressText = string.IsNullOrEmpty(progressText) ? string.Empty : progressText;
            NextHintText = string.IsNullOrEmpty(nextHintText) ? string.Empty : nextHintText;
            IsTracked = isTracked;
            SortPriority = sortPriority;
        }
    }

    [Serializable]
    public readonly struct ObjectiveJournalDetail
    {
        public readonly string Title;
        public readonly string Body;
        public readonly string ProgressText;
        public readonly string RewardText;
        public readonly string ActionText;
        public readonly string EmptyStateText;

        public ObjectiveJournalDetail(string title, string body, string progressText, string rewardText, string actionText, string emptyStateText)
        {
            Title = string.IsNullOrEmpty(title) ? string.Empty : title;
            Body = string.IsNullOrEmpty(body) ? string.Empty : body;
            ProgressText = string.IsNullOrEmpty(progressText) ? string.Empty : progressText;
            RewardText = string.IsNullOrEmpty(rewardText) ? string.Empty : rewardText;
            ActionText = string.IsNullOrEmpty(actionText) ? string.Empty : actionText;
            EmptyStateText = string.IsNullOrEmpty(emptyStateText) ? string.Empty : emptyStateText;
        }
    }
}
