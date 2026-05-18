using System;

namespace Rootborn.Game.VerticalSlice
{
    public readonly struct VerticalSliceSummary
    {
        public readonly string StableKey;
        public readonly string[] ChangedDomainIds;
        public readonly string NextObjectiveText;
        public readonly string NextActionText;
        public readonly string FollowUpMotivationText;
        public readonly string DayResultGuideText;

        public VerticalSliceSummary(string stableKey, string[] changedDomainIds, string nextObjectiveText, string nextActionText, string followUpMotivationText, string dayResultGuideText)
        {
            StableKey = string.IsNullOrEmpty(stableKey) ? "vertical.slice" : stableKey;
            ChangedDomainIds = changedDomainIds ?? Array.Empty<string>();
            NextObjectiveText = nextObjectiveText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            FollowUpMotivationText = followUpMotivationText ?? string.Empty;
            DayResultGuideText = dayResultGuideText ?? string.Empty;
        }
    }
}