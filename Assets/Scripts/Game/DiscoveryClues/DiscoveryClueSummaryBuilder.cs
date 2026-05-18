using System;
using System.Collections.Generic;

namespace Rootborn.Game.DiscoveryClues
{
    public readonly struct DiscoveryClueSummaryModel
    {
        public readonly string ClueId;
        public readonly string SourceId;
        public readonly DiscoveryClueSourceKind SourceKind;
        public readonly string DisplayName;
        public readonly string HintText;
        public readonly string PublicText;
        public readonly string HiddenText;
        public readonly int Strength;
        public readonly int SortPriority;
        public readonly bool Seen;
        public readonly bool Tracked;
        public readonly bool Completed;
        public readonly bool RewardClaimed;

        public DiscoveryClueSummaryModel(string clueId, string sourceId, DiscoveryClueSourceKind sourceKind, string displayName, string hintText, string publicText, string hiddenText, int strength, int sortPriority, bool seen, bool tracked, bool completed, bool rewardClaimed)
        {
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            SourceId = string.IsNullOrEmpty(sourceId) ? string.Empty : sourceId;
            SourceKind = sourceKind;
            DisplayName = string.IsNullOrEmpty(displayName) ? ClueId : displayName;
            HintText = string.IsNullOrEmpty(hintText) ? DisplayName : hintText;
            PublicText = string.IsNullOrEmpty(publicText) ? HintText : publicText;
            HiddenText = string.IsNullOrEmpty(hiddenText) ? "???" : hiddenText;
            Strength = Math.Max(0, strength);
            SortPriority = sortPriority;
            Seen = seen;
            Tracked = tracked;
            Completed = completed;
            RewardClaimed = rewardClaimed;
        }
    }

    public static class DiscoveryClueSummaryBuilder
    {
        public static DiscoveryClueSummaryModel[] BuildForSource(DiscoveryClueLookupCache cache, DiscoveryClueSourceKind kind, string sourceId, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface)
        {
            if (cache == null) return Array.Empty<DiscoveryClueSummaryModel>();
            return Build(cache.GetBySource(kind, sourceId), context, surface, kind, sourceId);
        }

        public static DiscoveryClueSummaryModel[] BuildForClue(DiscoveryClueLookupCache cache, string clueId, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface)
        {
            if (cache == null || !cache.TryGetById(clueId, out var clue)) return Array.Empty<DiscoveryClueSummaryModel>();
            return Build(new[] { clue }, context, surface, null, null);
        }

        public static DiscoveryClueSummaryModel[] BuildForSurface(DiscoveryClueLookupCache cache, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface)
        {
            if (cache == null) return Array.Empty<DiscoveryClueSummaryModel>();
            return Build(cache.All, context, surface, null, null);
        }

        private static DiscoveryClueSummaryModel[] Build(IReadOnlyList<DiscoveryClueDefinition> clues, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface, DiscoveryClueSourceKind? sourceKindFilter, string sourceIdFilter)
        {
            if (clues == null || context.ClueProgress == null) return Array.Empty<DiscoveryClueSummaryModel>();
            var result = new List<DiscoveryClueSummaryModel>();
            for (int i = 0; i < clues.Count; i++)
            {
                var clue = clues[i];
                if (clue == null || !clue.IsVisibleOn(surface) || !clue.ConditionsSatisfied(context)) continue;
                var record = context.ClueProgress.GetRecord(clue.Id);
                for (int sourceIndex = 0; sourceIndex < clue.Sources.Count; sourceIndex++)
                {
                    var source = clue.Sources[sourceIndex];
                    if (source == null) continue;
                    if (sourceKindFilter.HasValue && source.Kind != sourceKindFilter.Value) continue;
                    if (!string.IsNullOrEmpty(sourceIdFilter) && !string.Equals(source.Id, sourceIdFilter, StringComparison.Ordinal)) continue;
                    result.Add(new DiscoveryClueSummaryModel(
                        clue.Id,
                        source.Id,
                        source.Kind,
                        clue.DisplayNameKey,
                        source.HintTextKey,
                        clue.PublicTextKey,
                        clue.HiddenTextKey,
                        Math.Max(clue.Strength, source.Strength),
                        clue.SortPriority + source.Strength,
                        record.Seen,
                        record.Tracked,
                        record.Completed,
                        record.RewardClaimed));
                }
            }

            result.Sort((left, right) => right.SortPriority.CompareTo(left.SortPriority));
            return result.ToArray();
        }
    }

    public sealed class DiscoveryClueLookupCache
    {
        private readonly IReadOnlyList<DiscoveryClueDefinition> _clues;
        private Dictionary<string, DiscoveryClueDefinition> _byId;
        private Dictionary<string, List<DiscoveryClueDefinition>> _bySource;

        public DiscoveryClueLookupCache(IReadOnlyList<DiscoveryClueDefinition> clues)
        {
            _clues = clues ?? Array.Empty<DiscoveryClueDefinition>();
        }

        public IReadOnlyList<DiscoveryClueDefinition> All => _clues;
        public int BuildCount { get; private set; }
        public int LookupCount { get; private set; }

        public bool TryGetById(string id, out DiscoveryClueDefinition clue)
        {
            EnsureBuilt();
            LookupCount++;
            if (string.IsNullOrEmpty(id))
            {
                clue = null;
                return false;
            }

            return _byId.TryGetValue(id, out clue);
        }

        public DiscoveryClueDefinition[] GetBySource(DiscoveryClueSourceKind kind, string sourceId)
        {
            EnsureBuilt();
            LookupCount++;
            string key = SourceKey(kind, sourceId);
            if (!_bySource.TryGetValue(key, out var values)) return Array.Empty<DiscoveryClueDefinition>();
            return values.ToArray();
        }

        private void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, DiscoveryClueDefinition>(StringComparer.Ordinal);
            _bySource = new Dictionary<string, List<DiscoveryClueDefinition>>(StringComparer.Ordinal);
            for (int i = 0; i < _clues.Count; i++)
            {
                var clue = _clues[i];
                if (clue == null || string.IsNullOrEmpty(clue.Id)) continue;
                _byId[clue.Id] = clue;
                for (int sourceIndex = 0; sourceIndex < clue.Sources.Count; sourceIndex++)
                {
                    var source = clue.Sources[sourceIndex];
                    if (source == null || string.IsNullOrEmpty(source.Id)) continue;
                    string key = SourceKey(source.Kind, source.Id);
                    if (!_bySource.TryGetValue(key, out var values))
                    {
                        values = new List<DiscoveryClueDefinition>();
                        _bySource[key] = values;
                    }

                    if (!values.Contains(clue)) values.Add(clue);
                }
            }

            BuildCount++;
        }

        private static string SourceKey(DiscoveryClueSourceKind kind, string sourceId)
        {
            return kind.ToString() + ":" + (sourceId ?? string.Empty);
        }
    }
}
