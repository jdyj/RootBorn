using System;

namespace Rootborn.Game.WorldState
{
    public enum WorldStateScopeKind
    {
        Personal,
        Shared,
        SaveSlot
    }

    public enum WorldStateChangeKind
    {
        LocationUnlocked,
        ObjectRevealed,
        PortalEnabled,
        NpcDialogueChanged,
        ShopStockAdded,
        InteractionAdded,
        EventAvailable
    }

    public enum WorldStateSummarySurface
    {
        DayResult,
        LocationPanel,
        WorldLog
    }

    public enum WorldStateBadgeKind
    {
        Blocked,
        Locked,
        New,
        Seen,
        Shared,
        Personal
    }

    [Serializable]
    public readonly struct WorldStateActivationSource
    {
        public readonly string QuestChainId;
        public readonly string QuestStepId;
        public readonly string EventId;
        public readonly int Day;

        public WorldStateActivationSource(string questChainId, string questStepId, string eventId, int day)
        {
            QuestChainId = questChainId ?? string.Empty;
            QuestStepId = questStepId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Day = day;
        }
    }

    public readonly struct WorldStateSummaryModel
    {
        public readonly string FlagId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string LocationName;
        public readonly string NpcName;
        public readonly string NextAction;
        public readonly WorldStateChangeKind ChangeKind;
        public readonly WorldStateScopeKind Scope;
        public readonly WorldStateBadgeKind[] Badges;
        public readonly int SortPriority;
        public readonly bool SeenNotification;

        public WorldStateSummaryModel(
            string flagId,
            string displayName,
            string description,
            string locationName,
            string npcName,
            string nextAction,
            WorldStateChangeKind changeKind,
            WorldStateScopeKind scope,
            WorldStateBadgeKind[] badges,
            int sortPriority,
            bool seenNotification)
        {
            FlagId = flagId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            LocationName = locationName ?? string.Empty;
            NpcName = npcName ?? string.Empty;
            NextAction = nextAction ?? string.Empty;
            ChangeKind = changeKind;
            Scope = scope;
            Badges = badges ?? Array.Empty<WorldStateBadgeKind>();
            SortPriority = sortPriority;
            SeenNotification = seenNotification;
        }
    }
}
