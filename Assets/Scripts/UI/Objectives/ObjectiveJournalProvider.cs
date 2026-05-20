using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    public readonly struct ObjectiveJournalContext
    {
        public ObjectiveJournalContext(GameDataRegistry registry, GameObject player, SaveSlotMetadata activeSave)
        {
            Registry = registry;
            Player = player;
            ActiveSave = activeSave;
        }

        public GameDataRegistry Registry { get; }
        public GameObject Player { get; }
        public SaveSlotMetadata ActiveSave { get; }
    }

    public interface IObjectiveJournalSource
    {
        string SourceId { get; }
        ObjectiveJournalCategory Category { get; }
        ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context);
    }

    public sealed class ObjectiveJournalAggregator
    {
        private readonly List<IObjectiveJournalSource> _sources = new List<IObjectiveJournalSource>();

        public ObjectiveJournalAggregator(IEnumerable<IObjectiveJournalSource> sources)
        {
            if (sources == null)
            {
                return;
            }

            foreach (var source in sources)
            {
                if (source != null)
                {
                    _sources.Add(source);
                }
            }
        }

        public Dictionary<string, ObjectiveJournalItem[]> Build(ObjectiveJournalContext context)
        {
            var byCategory = new Dictionary<string, List<ObjectiveJournalItem>>(StringComparer.Ordinal);
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                var categoryId = source.Category.CategoryId;
                if (string.IsNullOrEmpty(categoryId))
                {
                    continue;
                }

                if (!byCategory.TryGetValue(categoryId, out var items))
                {
                    items = new List<ObjectiveJournalItem>();
                    byCategory.Add(categoryId, items);
                }

                var sourceItems = source.BuildItems(context) ?? Array.Empty<ObjectiveJournalItem>();
                for (int j = 0; j < sourceItems.Length; j++)
                {
                    if (!string.IsNullOrEmpty(sourceItems[j].StableKey))
                    {
                        items.Add(sourceItems[j]);
                    }
                }
            }

            var result = new Dictionary<string, ObjectiveJournalItem[]>(StringComparer.Ordinal);
            foreach (var pair in byCategory)
            {
                pair.Value.Sort(CompareItems);
                result[pair.Key] = pair.Value.ToArray();
            }

            return result;
        }

        public void Refresh(ObjectiveJournalPanel panel, ObjectiveJournalContext context, string trackedStableKey)
        {
            if (panel == null)
            {
                return;
            }

            var itemsByCategory = Build(context);
            foreach (var pair in itemsByCategory)
            {
                panel.SetItems(pair.Key, pair.Value);
            }

            if (!string.IsNullOrEmpty(trackedStableKey) && !TrackStableKey(panel, itemsByCategory, trackedStableKey))
            {
                panel.HideTrackedObjective();
            }
        }

        private static bool TrackStableKey(ObjectiveJournalPanel panel, Dictionary<string, ObjectiveJournalItem[]> itemsByCategory, string stableKey)
        {
            foreach (var pair in itemsByCategory)
            {
                var items = pair.Value ?? Array.Empty<ObjectiveJournalItem>();
                for (int i = 0; i < items.Length; i++)
                {
                    if (!string.Equals(items[i].StableKey, stableKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    panel.SelectCategory(pair.Key);
                    panel.SelectItem(i);
                    panel.TrackSelectedItem();
                    return true;
                }
            }

            return false;
        }

        private static int CompareItems(ObjectiveJournalItem left, ObjectiveJournalItem right)
        {
            int priority = left.SortPriority.CompareTo(right.SortPriority);
            if (priority != 0)
            {
                return priority;
            }

            int title = string.Compare(left.Title, right.Title, StringComparison.Ordinal);
            return title != 0 ? title : string.Compare(left.StableKey, right.StableKey, StringComparison.Ordinal);
        }
    }
}
