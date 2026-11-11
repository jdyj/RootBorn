using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.DiscoveryClues
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

        public DiscoveryClueSummaryModel(string clueId, string sourceId, DiscoveryClueSourceKind sourceKind, string displayName, string hintText, string publicText, string hiddenText,
            int strength, int sortPriority, bool seen, bool tracked, bool completed, bool rewardClaimed)
        {
            ClueId = clueId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
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

    /// <summary>
    /// 출처마다 한 줄. 조건이 맞지 않는 단서는 빠진다. 정렬은 (단서 우선순위 + 출처 세기)가 큰 것부터.
    /// </summary>
    public static class DiscoveryClueSummaryBuilder
    {
        public static DiscoveryClueSummaryModel[] BuildForSource(DiscoveryClueLookupCache cache, DiscoveryClueSourceKind kind, string sourceId, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface)
        {
            if (cache == null) return Array.Empty<DiscoveryClueSummaryModel>();
            return Build(cache.GetBySource(kind, sourceId), in context, surface, kind, sourceId);
        }

        public static DiscoveryClueSummaryModel[] BuildForClue(DiscoveryClueLookupCache cache, string clueId, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface)
        {
            if (cache == null || !cache.TryGetById(clueId, out var clue)) return Array.Empty<DiscoveryClueSummaryModel>();
            return Build(new[] { clue }, in context, surface, null, null);
        }

        public static DiscoveryClueSummaryModel[] BuildForSurface(DiscoveryClueLookupCache cache, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface)
        {
            if (cache == null) return Array.Empty<DiscoveryClueSummaryModel>();
            return Build(cache.All, in context, surface, null, null);
        }

        private static DiscoveryClueSummaryModel[] Build(IReadOnlyList<DiscoveryClue> clues, in DiscoveryClueContext context, DiscoveryClueSummarySurface surface, DiscoveryClueSourceKind? sourceKindFilter, string sourceIdFilter)
        {
            if (clues == null || context.ClueProgress == null) return Array.Empty<DiscoveryClueSummaryModel>();
            var result = new List<DiscoveryClueSummaryModel>();
            for (int i = 0; i < clues.Count; i++)
            {
                var clue = clues[i];
                if (clue == null || !clue.IsVisibleOn(surface) || !clue.ConditionsSatisfied(in context)) continue;
                var record = context.ClueProgress.GetRecord(clue.Id);
                for (int s = 0; s < clue.Sources.Count; s++)
                {
                    var source = clue.Sources[s];
                    if (source == null) continue;
                    if (sourceKindFilter.HasValue && source.Kind != sourceKindFilter.Value) continue;
                    if (!string.IsNullOrEmpty(sourceIdFilter) && !string.Equals(source.Id, sourceIdFilter, StringComparison.Ordinal)) continue;
                    result.Add(new DiscoveryClueSummaryModel(clue.Id, source.Id, source.Kind, clue.DisplayNameKey, source.HintTextKey, clue.PublicTextKey, clue.HiddenTextKey,
                        Math.Max(clue.Strength, source.Strength), clue.SortPriority + source.Strength, record.Seen, record.Tracked, record.Completed, record.RewardClaimed));
                }
            }

            result.Sort((left, right) => right.SortPriority.CompareTo(left.SortPriority));
            return result.ToArray();
        }
    }

    /// <summary>id·(출처 종류, 출처 id) → 단서 조회. 처음 찾을 때 한 번만 색인한다.</summary>
    public sealed class DiscoveryClueLookupCache
    {
        private readonly IReadOnlyList<DiscoveryClue> _clues;
        private Dictionary<string, DiscoveryClue> _byId;
        private Dictionary<string, List<DiscoveryClue>> _bySource;

        public DiscoveryClueLookupCache(IReadOnlyList<DiscoveryClue> clues)
        {
            _clues = clues ?? Array.Empty<DiscoveryClue>();
        }

        public IReadOnlyList<DiscoveryClue> All => _clues;
        public int BuildCount { get; private set; }
        public int LookupCount { get; private set; }

        public bool TryGetById(string id, out DiscoveryClue clue)
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

        public DiscoveryClue[] GetBySource(DiscoveryClueSourceKind kind, string sourceId)
        {
            EnsureBuilt();
            LookupCount++;
            return _bySource.TryGetValue(SourceKey(kind, sourceId), out var values) ? values.ToArray() : Array.Empty<DiscoveryClue>();
        }

        private void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, DiscoveryClue>(StringComparer.Ordinal);
            _bySource = new Dictionary<string, List<DiscoveryClue>>(StringComparer.Ordinal);
            for (int i = 0; i < _clues.Count; i++)
            {
                var clue = _clues[i];
                if (clue == null || string.IsNullOrEmpty(clue.Id)) continue;
                _byId[clue.Id] = clue;
                for (int s = 0; s < clue.Sources.Count; s++)
                {
                    var source = clue.Sources[s];
                    if (source == null || string.IsNullOrEmpty(source.Id)) continue;
                    string key = SourceKey(source.Kind, source.Id);
                    if (!_bySource.TryGetValue(key, out var values))
                    {
                        values = new List<DiscoveryClue>();
                        _bySource[key] = values;
                    }

                    if (!values.Contains(clue)) values.Add(clue);
                }
            }

            BuildCount++;
        }

        private static string SourceKey(DiscoveryClueSourceKind kind, string sourceId) => kind.ToString() + ":" + (sourceId ?? string.Empty);
    }

    /// <summary>
    /// 탐색 결과가 id로 단서를 읽고 남기는 창구(main의 IExplorationClues). 출처 id가 비면 단서의 첫 출처를 쓴다(원본 ExplorationClueOutcome).
    /// 정의 목록에 없는 단서는 무시한다.
    /// </summary>
    public sealed class DiscoveryClueAccess : IExplorationClues
    {
        private readonly DiscoveryClueProgress _progress;
        private readonly DiscoveryClueLookupCache _clues;

        public DiscoveryClueAccess(DiscoveryClueProgress progress, DiscoveryClueLookupCache clues)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _clues = clues ?? new DiscoveryClueLookupCache(null);
        }

        public bool HasSeen(string clueId) => !string.IsNullOrEmpty(clueId) && _progress.GetRecord(clueId).Seen;

        public void MarkSourceSeen(string clueId, string sourceId, int day)
        {
            if (!_clues.TryGetById(clueId, out var clue)) return;
            string resolved = !string.IsNullOrEmpty(sourceId) ? sourceId : clue.Sources.Count > 0 && clue.Sources[0] != null ? clue.Sources[0].Id : string.Empty;
            _progress.MarkSourceSeen(clue.Id, resolved, day);
        }
    }

    /// <summary>
    /// 플레이어마다 파일 하나(원본 DiscoveryClueProgressPersistence). 새로 쓸 때는 정리한 플레이어 id 이름을 쓰고,
    /// 읽을 때 그 파일이 없으면 Unity 판의 정리하지 않은 이름을 찾는다.
    /// </summary>
    public static class DiscoveryClueSaveFiles
    {
        public static DiscoveryClueProgress LoadOrCreate(ISaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? SaveFileNames.LegacyPlayerId : playerId;
            return store.TryReadWithLegacyName(slot, SaveFileNames.DiscoveryClues(player), SaveFileNames.LegacyDiscoveryClues(player), out DiscoveryClueProgressSaveData data)
                ? DiscoveryClueProgress.FromSaveData(data)
                : new DiscoveryClueProgress(slot, player);
        }

        public static void Save(ISaveStore store, DiscoveryClueProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, SaveFileNames.DiscoveryClues(progress.PlayerId), progress.ToSaveData());
        }
    }
}
