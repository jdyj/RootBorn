using System;
using System.Collections.Generic;

namespace Rootborn.Core.DiscoveryClues
{
    public readonly struct ClueInterpretationSummaryModel
    {
        public readonly string ClueId;
        public readonly string InterpretationId;
        public readonly string SourceId;
        public readonly ClueInterpretationSourceKind SourceKind;
        public readonly string SourceTargetId;
        public readonly string DisplayName;
        public readonly string MethodText;
        public readonly string HintText;
        public readonly string PublicText;
        public readonly string HiddenText;
        public readonly bool Available;
        public readonly bool Selected;
        public readonly bool Completed;
        public readonly bool RewardClaimed;
        public readonly string LockedReason;
        public readonly ClueInterpretationPolicyKind PolicyKind;
        public readonly string PolicyGroupId;
        public readonly int SortPriority;

        public ClueInterpretationSummaryModel(string clueId, string interpretationId, string sourceId, ClueInterpretationSourceKind sourceKind, string sourceTargetId, string displayName,
            string methodText, string hintText, string publicText, string hiddenText, bool available, bool selected, bool completed, bool rewardClaimed, string lockedReason,
            ClueInterpretationPolicyKind policyKind, string policyGroupId, int sortPriority)
        {
            ClueId = clueId ?? string.Empty;
            InterpretationId = interpretationId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            SourceKind = sourceKind;
            SourceTargetId = string.IsNullOrEmpty(sourceTargetId) ? SourceId : sourceTargetId;
            DisplayName = string.IsNullOrEmpty(displayName) ? InterpretationId : displayName;
            MethodText = string.IsNullOrEmpty(methodText) ? DisplayName : methodText;
            HintText = string.IsNullOrEmpty(hintText) ? MethodText : hintText;
            PublicText = string.IsNullOrEmpty(publicText) ? HintText : publicText;
            HiddenText = string.IsNullOrEmpty(hiddenText) ? "???" : hiddenText;
            Available = available;
            Selected = selected;
            Completed = completed;
            RewardClaimed = rewardClaimed;
            LockedReason = lockedReason ?? string.Empty;
            PolicyKind = policyKind;
            PolicyGroupId = policyGroupId ?? string.Empty;
            SortPriority = sortPriority;
        }
    }

    /// <summary>
    /// 출처마다 한 줄. 조건이나 정책에 막힌 해석도 잠김 이유와 함께 나온다. 정렬은 (해석 우선순위 + 출처 세기)가 큰 것부터.
    /// 원본처럼 화면 종류로는 거르지 않는다.
    /// </summary>
    public static class ClueInterpretationSummaryBuilder
    {
        public static ClueInterpretationSummaryModel[] BuildForClue(ClueInterpretationLookupCache cache, string clueId, in ClueInterpretationContext context, ClueInterpretationSummarySurface surface)
        {
            if (cache == null) return Array.Empty<ClueInterpretationSummaryModel>();
            return Build(cache.GetByClue(clueId), in context, null, null);
        }

        public static ClueInterpretationSummaryModel[] BuildForSource(ClueInterpretationLookupCache cache, ClueInterpretationSourceKind kind, string sourceTargetId, in ClueInterpretationContext context, ClueInterpretationSummarySurface surface)
        {
            if (cache == null) return Array.Empty<ClueInterpretationSummaryModel>();
            return Build(cache.GetBySource(kind, sourceTargetId), in context, kind, sourceTargetId);
        }

        public static ClueInterpretationSummaryModel[] BuildForSurface(ClueInterpretationLookupCache cache, in ClueInterpretationContext context, ClueInterpretationSummarySurface surface)
        {
            if (cache == null) return Array.Empty<ClueInterpretationSummaryModel>();
            return Build(cache.All, in context, null, null);
        }

        private static ClueInterpretationSummaryModel[] Build(IReadOnlyList<ClueInterpretation> interpretations, in ClueInterpretationContext context, ClueInterpretationSourceKind? sourceKindFilter, string sourceTargetIdFilter)
        {
            if (interpretations == null || context.InterpretationProgress == null) return Array.Empty<ClueInterpretationSummaryModel>();
            var result = new List<ClueInterpretationSummaryModel>();
            for (int i = 0; i < interpretations.Count; i++)
            {
                var interpretation = interpretations[i];
                if (interpretation == null) continue;
                bool conditions = interpretation.ConditionsSatisfied(in context, out string conditionReason);
                bool policy = context.InterpretationProgress.CanComplete(interpretation, context.CurrentDay, context.Interpretations, out string policyReason, out _);
                var record = context.InterpretationProgress.GetRecord(interpretation.Id);
                var policyKind = interpretation.Policy != null ? interpretation.Policy.Kind : ClueInterpretationPolicyKind.NonExclusive;
                string policyGroup = interpretation.Policy != null ? interpretation.Policy.GroupId : string.Empty;
                for (int s = 0; s < interpretation.Sources.Count; s++)
                {
                    var source = interpretation.Sources[s];
                    if (source == null) continue;
                    if (sourceKindFilter.HasValue && source.Kind != sourceKindFilter.Value) continue;
                    if (!string.IsNullOrEmpty(sourceTargetIdFilter) && !string.Equals(source.TargetId, sourceTargetIdFilter, StringComparison.Ordinal)) continue;
                    result.Add(new ClueInterpretationSummaryModel(interpretation.ClueId, interpretation.Id, source.Id, source.Kind, source.TargetId, interpretation.DisplayNameKey,
                        interpretation.MethodKey, source.HintTextKey, interpretation.PublicTextKey, interpretation.HiddenTextKey, conditions && policy, record.Selected, record.Completed,
                        record.RewardClaimed, conditions ? policyReason : conditionReason, policyKind, policyGroup, interpretation.SortPriority + source.Strength));
                }
            }

            result.Sort((left, right) => right.SortPriority.CompareTo(left.SortPriority));
            return result.ToArray();
        }
    }

    /// <summary>단서 id·(출처 종류, 대상 id)·정책 그룹 → 해석 조회. 처음 찾을 때 한 번만 색인한다.</summary>
    public sealed class ClueInterpretationLookupCache
    {
        private readonly IReadOnlyList<ClueInterpretation> _interpretations;
        private Dictionary<string, List<ClueInterpretation>> _byClue;
        private Dictionary<string, List<ClueInterpretation>> _bySource;
        private Dictionary<string, List<ClueInterpretation>> _byPolicyGroup;

        public ClueInterpretationLookupCache(IReadOnlyList<ClueInterpretation> interpretations)
        {
            _interpretations = interpretations ?? Array.Empty<ClueInterpretation>();
        }

        public IReadOnlyList<ClueInterpretation> All => _interpretations;
        public int BuildCount { get; private set; }
        public int LookupCount { get; private set; }

        public ClueInterpretation[] GetByClue(string clueId)
        {
            EnsureBuilt();
            LookupCount++;
            return Lookup(_byClue, clueId);
        }

        public ClueInterpretation[] GetBySource(ClueInterpretationSourceKind kind, string sourceTargetId)
        {
            EnsureBuilt();
            LookupCount++;
            return Lookup(_bySource, SourceKey(kind, sourceTargetId));
        }

        /// <summary>정책이 있는 해석만 들어 있다. 조회 횟수(LookupCount)에는 세지 않는다(정책 판정용).</summary>
        public ClueInterpretation[] GetByPolicyGroup(string groupId)
        {
            EnsureBuilt();
            return Lookup(_byPolicyGroup, groupId);
        }

        private static ClueInterpretation[] Lookup(Dictionary<string, List<ClueInterpretation>> map, string key)
        {
            return !string.IsNullOrEmpty(key) && map.TryGetValue(key, out var values) ? values.ToArray() : Array.Empty<ClueInterpretation>();
        }

        private void EnsureBuilt()
        {
            if (_byClue != null) return;
            _byClue = new Dictionary<string, List<ClueInterpretation>>(StringComparer.Ordinal);
            _bySource = new Dictionary<string, List<ClueInterpretation>>(StringComparer.Ordinal);
            _byPolicyGroup = new Dictionary<string, List<ClueInterpretation>>(StringComparer.Ordinal);
            for (int i = 0; i < _interpretations.Count; i++)
            {
                var interpretation = _interpretations[i];
                if (interpretation == null || string.IsNullOrEmpty(interpretation.Id)) continue;
                Add(_byClue, interpretation.ClueId, interpretation);
                if (interpretation.Policy != null) Add(_byPolicyGroup, interpretation.Policy.GroupId, interpretation);
                for (int s = 0; s < interpretation.Sources.Count; s++)
                {
                    var source = interpretation.Sources[s];
                    if (source != null) Add(_bySource, SourceKey(source.Kind, source.TargetId), interpretation);
                }
            }

            BuildCount++;
        }

        private static void Add(Dictionary<string, List<ClueInterpretation>> map, string key, ClueInterpretation interpretation)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!map.TryGetValue(key, out var values))
            {
                values = new List<ClueInterpretation>();
                map[key] = values;
            }

            if (!values.Contains(interpretation)) values.Add(interpretation);
        }

        private static string SourceKey(ClueInterpretationSourceKind kind, string sourceTargetId) => kind.ToString() + ":" + (sourceTargetId ?? string.Empty);
    }
}
