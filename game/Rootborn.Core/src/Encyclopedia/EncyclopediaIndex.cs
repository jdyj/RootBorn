using System;
using System.Collections.Generic;

namespace Rootborn.Core.Encyclopedia
{
    public readonly struct EncyclopediaEntryRow
    {
        public readonly EncyclopediaEntry Entry;
        public readonly bool IsUnlocked;
        public readonly bool IsNew;
        public readonly string Title;
        public readonly string Summary;

        public EncyclopediaEntryRow(EncyclopediaEntry entry, bool isUnlocked, bool isNew)
        {
            Entry = entry;
            IsUnlocked = isUnlocked;
            IsNew = isNew;
            Title = entry == null ? string.Empty : isUnlocked ? entry.DisplayName : entry.LockedDisplayName;
            Summary = entry == null ? string.Empty : isUnlocked ? entry.Description : entry.LockedHint;
        }
    }

    /// <summary>분류별 항목 목록을 한 번 만들어 두고 쪽 단위로 꺼낸다. 분류는 정렬 순서, 항목은 id 순.</summary>
    public sealed class EncyclopediaIndex
    {
        private readonly Dictionary<EncyclopediaCategory, List<EncyclopediaEntry>> _byCategory = new Dictionary<EncyclopediaCategory, List<EncyclopediaEntry>>();
        private readonly Dictionary<string, EncyclopediaEntry> _byId = new Dictionary<string, EncyclopediaEntry>(StringComparer.Ordinal);
        private readonly List<EncyclopediaCategory> _categories = new List<EncyclopediaCategory>();

        public EncyclopediaIndex(IReadOnlyList<EncyclopediaCategory> categories, IReadOnlyList<EncyclopediaEntry> entries)
        {
            BuildVersion = 1;
            AddCategories(categories);
            AddEntries(entries);
            _categories.Sort((a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : string.CompareOrdinal(a.Id, b.Id));
            foreach (var pair in _byCategory) pair.Value.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        public int BuildVersion { get; }
        public IReadOnlyList<EncyclopediaCategory> Categories => _categories;
        public bool TryGetEntry(string entryId, out EncyclopediaEntry entry) => _byId.TryGetValue(string.IsNullOrEmpty(entryId) ? string.Empty : entryId, out entry);

        public IReadOnlyList<EncyclopediaEntry> GetEntries(EncyclopediaCategory category)
            => category != null && _byCategory.TryGetValue(category, out var entries) ? entries : (IReadOnlyList<EncyclopediaEntry>)Array.Empty<EncyclopediaEntry>();

        public IReadOnlyList<EncyclopediaEntryRow> GetVisibleRows(EncyclopediaCategory category, EncyclopediaProgress progress, int pageIndex, int pageSize)
        {
            var entries = GetEntries(category);
            int size = Math.Max(1, pageSize);
            int start = Math.Max(0, pageIndex) * size;
            var rows = new List<EncyclopediaEntryRow>(Math.Min(size, Math.Max(0, entries.Count - start)));
            for (int i = start; i < entries.Count && rows.Count < size; i++)
            {
                var entry = entries[i];
                bool unlocked = progress != null && progress.IsUnlocked(entry.Id);
                rows.Add(new EncyclopediaEntryRow(entry, unlocked, progress != null && progress.IsNew(entry.Id)));
            }
            return rows;
        }

        private void AddCategories(IReadOnlyList<EncyclopediaCategory> categories)
        {
            if (categories == null) return;
            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                if (category == null || _byCategory.ContainsKey(category)) continue;
                _categories.Add(category);
                _byCategory.Add(category, new List<EncyclopediaEntry>());
            }
        }

        private void AddEntries(IReadOnlyList<EncyclopediaEntry> entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Category == null) continue;
                _byId[entry.Id] = entry;
                if (!_byCategory.TryGetValue(entry.Category, out var list))
                {
                    list = new List<EncyclopediaEntry>();
                    _byCategory.Add(entry.Category, list);
                    _categories.Add(entry.Category);
                }
                list.Add(entry);
            }
        }
    }

    /// <summary>조건을 만족한 항목을 해금한다(원본 EncyclopediaProgressComponent.EvaluateUnlocks). 저장은 부르는 쪽이 한다.</summary>
    public static class EncyclopediaUnlocker
    {
        public static int EvaluateUnlocks(IReadOnlyList<EncyclopediaEntry> entries, in EncyclopediaUnlockContext context, EncyclopediaProgress progress, long nowUtcTicks, Action<EncyclopediaEntry> onUnlocked)
        {
            if (progress == null || entries == null) return 0;
            int unlocked = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.IsUnlocked(context)) continue;
                if (progress.TryUnlock(entry.Id, entry.RevealStage, nowUtcTicks))
                {
                    unlocked++;
                    onUnlocked?.Invoke(entry);
                }
            }
            return unlocked;
        }
    }
}
