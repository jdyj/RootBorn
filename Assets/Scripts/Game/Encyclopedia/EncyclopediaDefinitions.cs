using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Knowledge;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Encyclopedia
{
    [CreateAssetMenu(fileName = "EncyclopediaCategory_New", menuName = "Rootborn/Encyclopedia/Category")]
    public sealed class EncyclopediaCategoryDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private int _sortOrder;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? Id : _displayName;
        public int SortOrder => _sortOrder;

        public void ConfigureForTests(string id, string displayName, int sortOrder)
        {
            _id = id;
            _displayName = displayName;
            _sortOrder = sortOrder;
        }
    }

    public readonly struct EncyclopediaUnlockContext
    {
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly Inventory Inventory;

        public EncyclopediaUnlockContext(StudentLifeProgress studentLifeProgress, KnowledgeProgress knowledgeProgress, Inventory inventory)
        {
            StudentLifeProgress = studentLifeProgress;
            KnowledgeProgress = knowledgeProgress;
            Inventory = inventory;
        }
    }

    public abstract class EncyclopediaUnlockConditionBase : ScriptableObject
    {
        public abstract bool IsSatisfied(in EncyclopediaUnlockContext context);
    }

    [CreateAssetMenu(fileName = "EncyclopediaCondition_LocationUnlocked", menuName = "Rootborn/Encyclopedia/Conditions/Location Unlocked")]
    public sealed class EncyclopediaLocationUnlockedCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private LocationDefinition _location;
        public override bool IsSatisfied(in EncyclopediaUnlockContext context) => _location != null && context.StudentLifeProgress != null && EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress.GetActivityLogIds(), _location.Id);
    }

    [CreateAssetMenu(fileName = "EncyclopediaCondition_NpcMet", menuName = "Rootborn/Encyclopedia/Conditions/NPC Met")]
    public sealed class EncyclopediaNpcMetCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private NpcDefinition _npc;
        public override bool IsSatisfied(in EncyclopediaUnlockContext context) => _npc != null && context.StudentLifeProgress != null && EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress.GetActivityLogIds(), _npc.Id);
    }

    [CreateAssetMenu(fileName = "EncyclopediaCondition_ActivityCompleted", menuName = "Rootborn/Encyclopedia/Conditions/Activity Completed")]
    public sealed class EncyclopediaActivityCompletedCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private LifeActivityDefinition _activity;
        public override bool IsSatisfied(in EncyclopediaUnlockContext context) => _activity != null && context.StudentLifeProgress != null && EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress.GetActivityLogIds(), _activity.Id);
    }

    [CreateAssetMenu(fileName = "EncyclopediaCondition_CareerHintUnlocked", menuName = "Rootborn/Encyclopedia/Conditions/Career Hint Unlocked")]
    public sealed class EncyclopediaCareerHintUnlockedCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private CareerDefinition _career;
        public override bool IsSatisfied(in EncyclopediaUnlockContext context) => _career != null && context.StudentLifeProgress != null && context.StudentLifeProgress.IsCareerHintUnlocked(_career);
    }

    [CreateAssetMenu(fileName = "EncyclopediaCondition_KnowledgeUnlocked", menuName = "Rootborn/Encyclopedia/Conditions/Knowledge Unlocked")]
    public sealed class EncyclopediaKnowledgeUnlockedCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private KnowledgeNode _knowledge;
        public override bool IsSatisfied(in EncyclopediaUnlockContext context) => _knowledge != null && context.KnowledgeProgress != null && context.KnowledgeProgress.IsUnlocked(_knowledge);
    }

    [CreateAssetMenu(fileName = "EncyclopediaCondition_ItemPossessed", menuName = "Rootborn/Encyclopedia/Conditions/Item Possessed")]
    public sealed class EncyclopediaItemPossessedCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private ItemDefinition _item;
        public override bool IsSatisfied(in EncyclopediaUnlockContext context) => _item != null && context.Inventory != null && context.Inventory.CountOf(_item) > 0;
    }

    internal static class EncyclopediaConditionHelpers
    {
        public static bool HasRecorded(string[] ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return true;
            return false;
        }
    }

    public readonly struct EncyclopediaEntryRow
    {
        public readonly EncyclopediaEntryDefinition Entry;
        public readonly bool IsUnlocked;
        public readonly bool IsNew;
        public readonly string Title;
        public readonly string Summary;

        public EncyclopediaEntryRow(EncyclopediaEntryDefinition entry, bool isUnlocked, bool isNew)
        {
            Entry = entry;
            IsUnlocked = isUnlocked;
            IsNew = isNew;
            Title = entry == null ? string.Empty : isUnlocked ? entry.DisplayName : entry.LockedDisplayName;
            Summary = entry == null ? string.Empty : isUnlocked ? entry.Description : entry.LockedHint;
        }
    }

    public sealed class EncyclopediaIndex
    {
        private readonly Dictionary<EncyclopediaCategoryDefinition, List<EncyclopediaEntryDefinition>> _byCategory = new Dictionary<EncyclopediaCategoryDefinition, List<EncyclopediaEntryDefinition>>();
        private readonly Dictionary<string, EncyclopediaEntryDefinition> _byId = new Dictionary<string, EncyclopediaEntryDefinition>(StringComparer.Ordinal);
        private readonly List<EncyclopediaCategoryDefinition> _categories = new List<EncyclopediaCategoryDefinition>();

        public EncyclopediaIndex(IReadOnlyList<EncyclopediaCategoryDefinition> categories, IReadOnlyList<EncyclopediaEntryDefinition> entries)
        {
            BuildVersion = 1;
            AddCategories(categories);
            AddEntries(entries);
            _categories.Sort((a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : string.CompareOrdinal(a.Id, b.Id));
            foreach (var pair in _byCategory) pair.Value.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        public int BuildVersion { get; }
        public IReadOnlyList<EncyclopediaCategoryDefinition> Categories => _categories;
        public bool TryGetEntry(string entryId, out EncyclopediaEntryDefinition entry) => _byId.TryGetValue(string.IsNullOrEmpty(entryId) ? string.Empty : entryId, out entry);
        public IReadOnlyList<EncyclopediaEntryDefinition> GetEntries(EncyclopediaCategoryDefinition category) => category != null && _byCategory.TryGetValue(category, out var entries) ? entries : Array.Empty<EncyclopediaEntryDefinition>();

        public IReadOnlyList<EncyclopediaEntryRow> GetVisibleRows(EncyclopediaCategoryDefinition category, EncyclopediaProgress progress, int pageIndex, int pageSize)
        {
            var entries = GetEntries(category);
            int size = Mathf.Max(1, pageSize);
            int start = Mathf.Max(0, pageIndex) * size;
            var rows = new List<EncyclopediaEntryRow>(Mathf.Min(size, Mathf.Max(0, entries.Count - start)));
            for (int i = start; i < entries.Count && rows.Count < size; i++)
            {
                var entry = entries[i];
                bool unlocked = progress != null && progress.IsUnlocked(entry.Id);
                rows.Add(new EncyclopediaEntryRow(entry, unlocked, progress != null && progress.IsNew(entry.Id)));
            }
            return rows;
        }

        private void AddCategories(IReadOnlyList<EncyclopediaCategoryDefinition> categories)
        {
            if (categories == null) return;
            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                if (category == null || _byCategory.ContainsKey(category)) continue;
                _categories.Add(category);
                _byCategory.Add(category, new List<EncyclopediaEntryDefinition>());
            }
        }

        private void AddEntries(IReadOnlyList<EncyclopediaEntryDefinition> entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Category == null) continue;
                _byId[entry.Id] = entry;
                if (!_byCategory.TryGetValue(entry.Category, out var list))
                {
                    list = new List<EncyclopediaEntryDefinition>();
                    _byCategory.Add(entry.Category, list);
                    _categories.Add(entry.Category);
                }
                list.Add(entry);
            }
        }
    }
}
