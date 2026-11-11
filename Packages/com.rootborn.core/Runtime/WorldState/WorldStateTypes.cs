using System;

namespace Rootborn.Core.WorldState
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

    /// <summary>월드 상태를 켠 원인. 퀘스트 체인·단계·이벤트 id와 날짜.</summary>
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

        public WorldStateSummaryModel(string flagId, string displayName, string description, string locationName, string npcName, string nextAction,
            WorldStateChangeKind changeKind, WorldStateScopeKind scope, WorldStateBadgeKind[] badges, int sortPriority, bool seenNotification)
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

    /// <summary>
    /// 요약에 쓰는 장소·NPC 표시 이름 키를 id로 찾는다. 원본은 정의 객체의 DisplayNameKey를 직접 읽었다.
    /// 장소·NPC 정의가 Core에 모이면 그 조회로 바꾼다.
    /// </summary>
    public interface IWorldStateDisplayNames
    {
        string LocationName(string locationId);
        string NpcName(string npcId);
    }
}
