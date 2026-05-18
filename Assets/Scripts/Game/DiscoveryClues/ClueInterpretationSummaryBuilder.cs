using System;
using System.Collections.Generic;

namespace Rootborn.Game.DiscoveryClues
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

        public ClueInterpretationSummaryModel(string clueId, string interpretationId, string sourceId, ClueInterpretationSourceKind sourceKind, string sourceTargetId, string displayName, string methodText, string hintText, string publicText, string hiddenText, bool available, bool selected, bool completed, bool rewardClaimed, string lockedReason, ClueInterpretationPolicyKind policyKind, string policyGroupId, int sortPriority)
        {
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            InterpretationId = string.IsNullOrEmpty(interpretationId) ? string.Empty : interpretationId;
            SourceId = string.IsNullOrEmpty(sourceId) ? string.Empty : sourceId;
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
            LockedReason = string.IsNullOrEmpty(lockedReason) ? string.Empty : lockedReason;
            PolicyKind = policyKind;
            PolicyGroupId = string.IsNullOrEmpty(policyGroupId) ? string.Empty : policyGroupId;
            SortPriority = sortPriority;
        }
    }

    public static class ClueInterpretationSummaryBuilder
    {
        public static ClueInterpretationSummaryModel[] BuildForClue(ClueInterpretationLookupCache cache, string clueId, in ClueInterpretationContext context, ClueInterpretationSummarySurface surface)
        {
            if (cache == null) return Array.Empty<ClueInterpretationSummaryModel>();
            return Build(cache.GetByClue(clueId), context, null, null);
        }

        public static ClueInterpretationSummaryModel[] BuildForSource(ClueInterpretationLookupCache cache, ClueInterpretationSourceKind kind, string sourceTargetId, in ClueInterpretationContext context, ClueInterpretationSummarySurface surface)
        {
            if (cache == null) return Array.Empty<ClueInterpretationSummaryModel>();
            return Build(cache.GetBySource(kind, sourceTargetId), context, kind, sourceTargetId);
        }

        public static ClueInterpretationSummaryModel[] BuildForSurface(ClueInterpretationLookupCache cache, in ClueInterpretationContext context, ClueInterpretationSummarySurface surface)
        {
            if (cache == null) return Array.Empty<ClueInterpretationSummaryModel>();
            return Build(cache.All, context, null, null);
        }

        private static ClueInterpretationSummaryModel[] Build(IReadOnlyList<ClueInterpretationDefinition> interpretations, in ClueInterpretationContext context, ClueInterpretationSourceKind? sourceKindFilter, string sourceTargetIdFilter)
        {
            if (interpretations == null || context.InterpretationProgress == null) return Array.Empty<ClueInterpretationSummaryModel>();
            var result = new List<ClueInterpretationSummaryModel>();
            for (int i = 0; i < interpretations.Count; i++)
            {
                var interpretation = interpretations[i];
                if (interpretation == null) continue;
                bool conditions = interpretation.ConditionsSatisfied(context, out string conditionReason);
                bool policy = context.InterpretationProgress.CanComplete(interpretation, context.CurrentDay, out string policyReason, out _);
                var record = context.InterpretationProgress.GetRecord(interpretation.Id);
                var interpretationPolicy = interpretation.Policy;
                var policyKind = interpretationPolicy != null ? interpretationPolicy.Kind : ClueInterpretationPolicyKind.NonExclusive;
                string policyGroup = interpretationPolicy != null ? interpretationPolicy.GroupId : string.Empty;
                for (int sourceIndex = 0; sourceIndex < interpretation.Sources.Count; sourceIndex++)
                {
                    var source = interpretation.Sources[sourceIndex];
                    if (source == null) continue;
                    if (sourceKindFilter.HasValue && source.Kind != sourceKindFilter.Value) continue;
                    if (!string.IsNullOrEmpty(sourceTargetIdFilter) && !string.Equals(source.TargetId, sourceTargetIdFilter, StringComparison.Ordinal)) continue;
                    result.Add(new ClueInterpretationSummaryModel(
                        interpretation.ClueId,
                        interpretation.Id,
                        source.Id,
                        source.Kind,
                        source.TargetId,
                        interpretation.DisplayNameKey,
                        interpretation.MethodKey,
                        source.HintTextKey,
                        interpretation.PublicTextKey,
                        interpretation.HiddenTextKey,
                        conditions && policy,
                        record.Selected,
                        record.Completed,
                        record.RewardClaimed,
                        conditions ? policyReason : conditionReason,
                        policyKind,
                        policyGroup,
                        interpretation.SortPriority + source.Strength));
                }
            }

            result.Sort((left, right) => right.SortPriority.CompareTo(left.SortPriority));
            return result.ToArray();
        }
    }

    public sealed class ClueInterpretationLookupCache
    {
        private readonly IReadOnlyList<ClueInterpretationDefinition> _interpretations;
        private Dictionary<string, List<ClueInterpretationDefinition>> _byClue;
        private Dictionary<string, List<ClueInterpretationDefinition>> _bySource;

        public ClueInterpretationLookupCache(IReadOnlyList<ClueInterpretationDefinition> interpretations)
        {
            _interpretations = interpretations ?? Array.Empty<ClueInterpretationDefinition>();
        }

        public IReadOnlyList<ClueInterpretationDefinition> All => _interpretations;
        public int BuildCount { get; private set; }
        public int LookupCount { get; private set; }

        public ClueInterpretationDefinition[] GetByClue(string clueId)
        {
            EnsureBuilt();
            LookupCount++;
            if (string.IsNullOrEmpty(clueId) || !_byClue.TryGetValue(clueId, out var values)) return Array.Empty<ClueInterpretationDefinition>();
            return values.ToArray();
        }

        public ClueInterpretationDefinition[] GetBySource(ClueInterpretationSourceKind kind, string sourceTargetId)
        {
            EnsureBuilt();
            LookupCount++;
            string key = SourceKey(kind, sourceTargetId);
            if (!_bySource.TryGetValue(key, out var values)) return Array.Empty<ClueInterpretationDefinition>();
            return values.ToArray();
        }

        private void EnsureBuilt()
        {
            if (_byClue != null) return;
            _byClue = new Dictionary<string, List<ClueInterpretationDefinition>>(StringComparer.Ordinal);
            _bySource = new Dictionary<string, List<ClueInterpretationDefinition>>(StringComparer.Ordinal);
            for (int i = 0; i < _interpretations.Count; i++)
            {
                var interpretation = _interpretations[i];
                if (interpretation == null || string.IsNullOrEmpty(interpretation.Id)) continue;
                Add(_byClue, interpretation.ClueId, interpretation);
                for (int sourceIndex = 0; sourceIndex < interpretation.Sources.Count; sourceIndex++)
                {
                    var source = interpretation.Sources[sourceIndex];
                    if (source == null) continue;
                    Add(_bySource, SourceKey(source.Kind, source.TargetId), interpretation);
                }
            }

            BuildCount++;
        }

        private static void Add(Dictionary<string, List<ClueInterpretationDefinition>> map, string key, ClueInterpretationDefinition interpretation)
        {
            if (string.IsNullOrEmpty(key) || interpretation == null) return;
            if (!map.TryGetValue(key, out var values))
            {
                values = new List<ClueInterpretationDefinition>();
                map[key] = values;
            }

            if (!values.Contains(interpretation)) values.Add(interpretation);
        }

        private static string SourceKey(ClueInterpretationSourceKind kind, string sourceTargetId)
        {
            return kind.ToString() + ":" + (sourceTargetId ?? string.Empty);
        }
    }
}
