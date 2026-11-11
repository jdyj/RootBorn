using System;
using System.Collections.Generic;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.DiscoveryClues
{
    public enum ClueInterpretationSourceKind { NpcDialogue, LocationInvestigation, ObjectInteraction, ActivityReview, EncyclopediaReview, BoardRecord, MapCheck }

    public enum ClueInterpretationSummarySurface { ClueDetail, NpcDialogue, LocationPanel, ObjectPanel, Encyclopedia, Journal }

    public enum ClueInterpretationPolicyKind { Exclusive, NonExclusive, Sequential, Repeatable }

    public enum ClueInterpretationResultKind { Completed, InvalidRequest, ConditionFailed, AlreadyCompleted, LockedByPolicy, CooldownActive }

    /// <summary>해석을 해 볼 수 있는 곳 하나. 대상(NPC·장소·물체 등)은 id로 가리킨다. 대상을 비우면 출처 id를 쓴다.</summary>
    public sealed class ClueInterpretationSource
    {
        public ClueInterpretationSource(string id, ClueInterpretationSourceKind kind, string targetId, string displayNameKey, string hintTextKey, int strength)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            TargetId = string.IsNullOrEmpty(targetId) ? Id : targetId;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            HintTextKey = string.IsNullOrEmpty(hintTextKey) ? DisplayNameKey : hintTextKey;
            Strength = Math.Max(0, strength);
        }

        public string Id { get; }
        public ClueInterpretationSourceKind Kind { get; }
        public string TargetId { get; }
        public string DisplayNameKey { get; }
        public string HintTextKey { get; }
        public int Strength { get; }
    }

    /// <summary>
    /// 같은 그룹 해석들 사이의 규칙. 배타(그룹에서 하나만), 비배타, 순서(바로 앞 순서가 끝나야), 반복. 쿨다운은 모든 종류에 걸린다.
    /// ConsumeItem은 원본에서도 읽지 않는 값이다. 데이터 호환을 위해 둔다.
    /// </summary>
    public sealed class ClueInterpretationPolicy
    {
        public ClueInterpretationPolicy(string id, ClueInterpretationPolicyKind kind, string groupId, int sequenceOrder, int cooldownDays, bool consumeItem)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            GroupId = string.IsNullOrEmpty(groupId) ? Id : groupId;
            SequenceOrder = Math.Max(0, sequenceOrder);
            CooldownDays = Math.Max(0, cooldownDays);
            ConsumeItem = consumeItem;
        }

        public string Id { get; }
        public ClueInterpretationPolicyKind Kind { get; }
        public string GroupId { get; }
        public int SequenceOrder { get; }
        public int CooldownDays { get; }
        public bool ConsumeItem { get; }
    }

    /// <summary>발견 단서를 풀어내는 방법 하나. 단서는 id로 가리킨다. 정책이 없으면 비배타·한 번.</summary>
    public sealed class ClueInterpretation
    {
        private readonly ClueInterpretationSource[] _sources;
        private readonly IClueInterpretationCondition[] _conditions;
        private readonly IClueInterpretationOutcome[] _outcomes;

        public ClueInterpretation(string id, string displayNameKey, string descriptionKey, string clueId, string methodKey, string publicTextKey, string hiddenTextKey,
            ClueInterpretationSource[] sources, IClueInterpretationCondition[] conditions, IClueInterpretationOutcome[] outcomes, ClueInterpretationPolicy policy, int sortPriority)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            ClueId = clueId ?? string.Empty;
            MethodKey = string.IsNullOrEmpty(methodKey) ? DisplayNameKey : methodKey;
            PublicTextKey = string.IsNullOrEmpty(publicTextKey) ? DescriptionKey : publicTextKey;
            HiddenTextKey = string.IsNullOrEmpty(hiddenTextKey) ? "???" : hiddenTextKey;
            _sources = sources ?? Array.Empty<ClueInterpretationSource>();
            _conditions = conditions ?? Array.Empty<IClueInterpretationCondition>();
            _outcomes = outcomes ?? Array.Empty<IClueInterpretationOutcome>();
            Policy = policy;
            SortPriority = sortPriority;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string ClueId { get; }
        public string MethodKey { get; }
        public string PublicTextKey { get; }
        public string HiddenTextKey { get; }
        public IReadOnlyList<ClueInterpretationSource> Sources => _sources;
        public IReadOnlyList<IClueInterpretationCondition> Conditions => _conditions;
        public IReadOnlyList<IClueInterpretationOutcome> Outcomes => _outcomes;
        public ClueInterpretationPolicy Policy { get; }
        public int SortPriority { get; }

        /// <summary>처음 실패한 조건의 잠김 이유를 돌려준다.</summary>
        public bool ConditionsSatisfied(in ClueInterpretationContext context, out string reason)
        {
            reason = string.Empty;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition == null || condition.Evaluate(in context, this)) continue;
                reason = condition.LockedReasonKey;
                return false;
            }

            return true;
        }
    }

    public interface IClueInterpretationCondition
    {
        string LockedReasonKey { get; }
        bool Evaluate(in ClueInterpretationContext context, ClueInterpretation interpretation);
    }

    /// <summary>결과 하나. 무엇이 바뀌었으면 true를 돌려주고, 줬거나 이미 가진 것의 id는 기록용 목록에 넣는다.</summary>
    public interface IClueInterpretationOutcome
    {
        string OutcomeId { get; }
        bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants);
    }

    /// <summary>해석 하나를 끝내며 기록에 남길 것들. 같은 id는 한 번만 들어간다.</summary>
    public sealed class ClueInterpretationGrants
    {
        public readonly List<string> EncyclopediaEntryIds = new List<string>();
        public readonly List<string> CareerHintIds = new List<string>();
        public readonly List<string> QuestIds = new List<string>();
        public readonly List<string> WorldStateIds = new List<string>();
        public readonly List<string> RelationshipIds = new List<string>();
        public readonly List<string> StatusIds = new List<string>();

        internal static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }

    public readonly struct ClueInterpretationContext
    {
        public readonly WorldStateProgress WorldStateProgress;
        public readonly DiscoveryClueProgress ClueProgress;
        public readonly ClueInterpretationProgress InterpretationProgress;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly QuestLog QuestLog;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly int CurrentDay;

        /// <summary>순서형 정책이 같은 그룹의 앞 순서를 찾는 데 쓴다. 없으면 순서 1 이상의 순서형 해석은 막힌다.</summary>
        public readonly ClueInterpretationLookupCache Interpretations;

        /// <summary>도감에 처음 발견한 시각으로 남는다. 원본은 DateTime.UtcNow.Ticks를 직접 읽었다.</summary>
        public readonly long NowUtcTicks;

        public ClueInterpretationContext(WorldStateProgress worldStateProgress, DiscoveryClueProgress clueProgress, ClueInterpretationProgress interpretationProgress, StudentLifeProgress studentLifeProgress,
            QuestLog questLog, EncyclopediaProgress encyclopediaProgress, int currentDay, ClueInterpretationLookupCache interpretations = null, long nowUtcTicks = 0)
        {
            WorldStateProgress = worldStateProgress;
            ClueProgress = clueProgress;
            InterpretationProgress = interpretationProgress;
            StudentLifeProgress = studentLifeProgress;
            QuestLog = questLog;
            EncyclopediaProgress = encyclopediaProgress;
            CurrentDay = Math.Max(1, currentDay);
            Interpretations = interpretations;
            NowUtcTicks = nowUtcTicks;
        }
    }

    public readonly struct ClueInterpretationResult
    {
        public readonly ClueInterpretationResultKind Kind;
        public readonly string InterpretationId;
        public readonly string Reason;
        public readonly string[] OutcomeIds;

        public ClueInterpretationResult(ClueInterpretationResultKind kind, string interpretationId, string reason, string[] outcomeIds)
        {
            Kind = kind;
            InterpretationId = interpretationId ?? string.Empty;
            Reason = reason ?? string.Empty;
            OutcomeIds = outcomeIds ?? Array.Empty<string>();
        }
    }

    /// <summary>조건 → 정책 → 결과 적용 → 기록 순으로 처리한다. 막히면 아무것도 바꾸지 않는다.</summary>
    public sealed class ClueInterpretationRunner
    {
        public bool TryComplete(ClueInterpretation interpretation, in ClueInterpretationContext context, out ClueInterpretationResult result)
        {
            string id = interpretation != null ? interpretation.Id : string.Empty;
            result = new ClueInterpretationResult(ClueInterpretationResultKind.InvalidRequest, id, "missing interpretation", Array.Empty<string>());
            if (interpretation == null || context.InterpretationProgress == null) return false;
            if (!interpretation.ConditionsSatisfied(in context, out string conditionReason))
            {
                result = new ClueInterpretationResult(ClueInterpretationResultKind.ConditionFailed, interpretation.Id, conditionReason, Array.Empty<string>());
                return false;
            }

            if (!context.InterpretationProgress.CanComplete(interpretation, context.CurrentDay, context.Interpretations, out string policyReason, out var policyResult))
            {
                result = new ClueInterpretationResult(policyResult, interpretation.Id, policyReason, Array.Empty<string>());
                return false;
            }

            var grants = new ClueInterpretationGrants();
            var outcomeIds = new List<string>();
            for (int i = 0; i < interpretation.Outcomes.Count; i++)
            {
                var outcome = interpretation.Outcomes[i];
                if (outcome != null && outcome.Apply(in context, interpretation, grants)) ClueInterpretationGrants.AddUnique(outcomeIds, outcome.OutcomeId);
            }

            context.InterpretationProgress.MarkCompleted(interpretation, context.CurrentDay, outcomeIds.ToArray(), grants.EncyclopediaEntryIds.ToArray(), grants.CareerHintIds.ToArray(),
                grants.QuestIds.ToArray(), grants.WorldStateIds.ToArray(), grants.RelationshipIds.ToArray(), grants.StatusIds.ToArray());
            result = new ClueInterpretationResult(ClueInterpretationResultKind.Completed, interpretation.Id, string.Empty, outcomeIds.ToArray());
            return true;
        }
    }
}
