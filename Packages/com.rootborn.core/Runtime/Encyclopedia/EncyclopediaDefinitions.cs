using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Encyclopedia
{
    /// <summary>지식(레거시 영역)이 해금됐는지 묻는다. 지식 영역을 옮기기 전까지 이 인터페이스로 받는다.</summary>
    public interface IKnowledgeUnlockLookup
    {
        bool IsUnlocked(string knowledgeId);
    }

    public sealed class EncyclopediaCategory
    {
        public EncyclopediaCategory(string id, string displayName, int sortOrder)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            SortOrder = sortOrder;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int SortOrder { get; }
    }

    public readonly struct EncyclopediaUnlockContext
    {
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly IKnowledgeUnlockLookup Knowledge;
        public readonly ItemInventory Inventory;

        public EncyclopediaUnlockContext(StudentLifeProgress studentLifeProgress, IKnowledgeUnlockLookup knowledge, ItemInventory inventory)
        {
            StudentLifeProgress = studentLifeProgress;
            Knowledge = knowledge;
            Inventory = inventory;
        }
    }

    public interface IEncyclopediaUnlockCondition
    {
        bool IsSatisfied(in EncyclopediaUnlockContext context);
    }

    /// <summary>장소 방문이 활동 기록에 남아 있으면 참.</summary>
    public sealed class EncyclopediaLocationUnlockedCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaLocationUnlockedCondition(string locationId) { LocationId = locationId ?? string.Empty; }
        public string LocationId { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress, LocationId);
    }

    /// <summary>NPC를 만난 기록이 활동 기록에 남아 있으면 참.</summary>
    public sealed class EncyclopediaNpcMetCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaNpcMetCondition(string npcId) { NpcId = npcId ?? string.Empty; }
        public string NpcId { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress, NpcId);
    }

    public sealed class EncyclopediaActivityCompletedCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaActivityCompletedCondition(string activityId) { ActivityId = activityId ?? string.Empty; }
        public string ActivityId { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress, ActivityId);
    }

    public sealed class EncyclopediaDiscoveryCompletedCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaDiscoveryCompletedCondition(string discoveryId) { DiscoveryId = discoveryId ?? string.Empty; }
        public string DiscoveryId { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress, DiscoveryId);
    }

    public sealed class EncyclopediaCareerHintUnlockedCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaCareerHintUnlockedCondition(string careerId) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => context.StudentLifeProgress != null && context.StudentLifeProgress.IsCareerHintUnlocked(CareerId);
    }

    public sealed class EncyclopediaKnowledgeUnlockedCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaKnowledgeUnlockedCondition(string knowledgeId) { KnowledgeId = knowledgeId ?? string.Empty; }
        public string KnowledgeId { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => !string.IsNullOrEmpty(KnowledgeId) && context.Knowledge != null && context.Knowledge.IsUnlocked(KnowledgeId);
    }

    public sealed class EncyclopediaItemPossessedCondition : IEncyclopediaUnlockCondition
    {
        public EncyclopediaItemPossessedCondition(ItemDefinition item) { Item = item; }
        public ItemDefinition Item { get; }
        public bool IsSatisfied(in EncyclopediaUnlockContext context) => Item != null && context.Inventory != null && context.Inventory.CountOf(Item) > 0;
    }

    internal static class EncyclopediaConditionHelpers
    {
        public static bool HasRecorded(StudentLifeProgress progress, string id)
        {
            if (progress == null || string.IsNullOrEmpty(id)) return false;
            string[] ids = progress.GetActivityLogIds();
            for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return true;
            return false;
        }
    }

    /// <summary>도감 항목 하나. 해금 조건이 하나도 없으면 해금되지 않는다(원본과 같다).</summary>
    public sealed class EncyclopediaEntry
    {
        private readonly string[] _detailLines;
        private readonly IEncyclopediaUnlockCondition[] _unlockConditions;

        public EncyclopediaEntry(string id, EncyclopediaCategory category, string displayName, string lockedDisplayName, string description, string lockedHint,
            string[] detailLines, IEncyclopediaUnlockCondition[] unlockConditions, int revealStage = 1)
        {
            Id = id ?? string.Empty;
            Category = category;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            LockedDisplayName = string.IsNullOrEmpty(lockedDisplayName) ? "???" : lockedDisplayName;
            Description = string.IsNullOrEmpty(description) ? DisplayName : description;
            LockedHint = string.IsNullOrEmpty(lockedHint) ? "???" : lockedHint;
            _detailLines = detailLines ?? Array.Empty<string>();
            _unlockConditions = unlockConditions ?? Array.Empty<IEncyclopediaUnlockCondition>();
            RevealStage = Math.Max(1, revealStage);
        }

        public string Id { get; }
        public EncyclopediaCategory Category { get; }
        public string DisplayName { get; }
        public string LockedDisplayName { get; }
        public string Description { get; }
        public string LockedHint { get; }
        public IReadOnlyList<string> DetailLines => _detailLines;
        public IReadOnlyList<IEncyclopediaUnlockCondition> UnlockConditions => _unlockConditions;
        public int RevealStage { get; }

        public bool IsUnlocked(in EncyclopediaUnlockContext context)
        {
            if (_unlockConditions.Length == 0) return false;
            for (int i = 0; i < _unlockConditions.Length; i++)
            {
                var condition = _unlockConditions[i];
                if (condition == null || !condition.IsSatisfied(context)) return false;
            }
            return true;
        }
    }
}
