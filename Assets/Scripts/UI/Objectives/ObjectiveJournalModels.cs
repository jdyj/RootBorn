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
        /// <summary>Optional detail action (e.g. claiming a completed quest's reward); null when the item has none.</summary>
        public readonly ObjectiveJournalAction Action;

        public ObjectiveJournalItem(string stableKey, string categoryId, string title, string subtitle, string state, string progressText, string nextHintText, bool isTracked, int sortPriority, ObjectiveJournalAction action = null)
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
            Action = action;
        }
    }

    /// <summary>What a detail action reports back: whether it changed anything and the message to show the player.</summary>
    public readonly struct ObjectiveJournalActionResult
    {
        public readonly bool Succeeded;
        public readonly string Message;

        public ObjectiveJournalActionResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = string.IsNullOrEmpty(message) ? string.Empty : message;
        }
    }

    /// <summary>
    /// A button the journal detail shows for one item. Sources supply it from data and state (no entity ID checks);
    /// the panel only shows the label and runs it.
    /// </summary>
    public sealed class ObjectiveJournalAction
    {
        private readonly Func<ObjectiveJournalActionResult> _run;

        public ObjectiveJournalAction(string label, Func<ObjectiveJournalActionResult> run)
        {
            Label = string.IsNullOrEmpty(label) ? string.Empty : label;
            _run = run;
        }

        public string Label { get; }

        public ObjectiveJournalActionResult Run()
        {
            return _run != null ? _run() : new ObjectiveJournalActionResult(false, string.Empty);
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
