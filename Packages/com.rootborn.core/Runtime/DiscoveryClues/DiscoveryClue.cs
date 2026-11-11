using System;
using System.Collections.Generic;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.DiscoveryClues
{
    public enum DiscoveryClueSourceKind
    {
        NpcDialogue,
        BoardPost,
        MapHint,
        EncyclopediaUnknown,
        LocationTrace,
        DailyResult
    }

    public enum DiscoveryClueSummarySurface
    {
        NpcDialogue,
        Board,
        Map,
        Encyclopedia,
        LocationPanel,
        WorldLog,
        DayResult,
        Hud
    }

    public enum DiscoveryClueCompletionKind
    {
        LocationVisited,
        ObjectInteracted,
        NpcTalked,
        MapPositionChecked,
        EncyclopediaEntryFound,
        ActivityCompleted
    }

    public enum DiscoveryClueResultKind
    {
        Revealed,
        Completed,
        InvalidRequest,
        ConditionFailed,
        AlreadyCompleted,
        CompletionNotMatched
    }

    /// <summary>단서를 알게 되는 곳 하나(NPC 대화, 게시판 등). NPC는 id로, 관련 월드 상태는 정의로 가진다.</summary>
    public sealed class DiscoveryClueSource
    {
        public DiscoveryClueSource(string id, DiscoveryClueSourceKind kind, string displayNameKey, string hintTextKey, string relatedNpcId, WorldStateFlag relatedWorldState, int strength)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            HintTextKey = string.IsNullOrEmpty(hintTextKey) ? DisplayNameKey : hintTextKey;
            RelatedNpcId = relatedNpcId ?? string.Empty;
            RelatedWorldState = relatedWorldState;
            Strength = Math.Max(0, strength);
        }

        public string Id { get; }
        public DiscoveryClueSourceKind Kind { get; }
        public string DisplayNameKey { get; }
        public string HintTextKey { get; }
        public string RelatedNpcId { get; }
        public WorldStateFlag RelatedWorldState { get; }
        public int Strength { get; }
    }

    /// <summary>발견 단서 하나. 출처 여러 개에서 알게 되고, 완료 방법 중 하나를 맞추면 결과를 한 번 준다.</summary>
    public sealed class DiscoveryClue
    {
        private readonly DiscoveryClueSource[] _sources;
        private readonly IDiscoveryClueCondition[] _conditions;
        private readonly IDiscoveryClueCompletion[] _completions;
        private readonly IDiscoveryClueOutcome[] _outcomes;
        private readonly DiscoveryClueSummarySurface[] _visibleSurfaces;

        public DiscoveryClue(string id, string displayNameKey, string descriptionKey, WorldStateFlag relatedWorldState, string relatedQuestId, string relatedLocationId, int strength,
            string publicTextKey, string hiddenTextKey, DiscoveryClueSource[] sources, IDiscoveryClueCondition[] conditions, IDiscoveryClueCompletion[] completions,
            IDiscoveryClueOutcome[] outcomes, DiscoveryClueSummarySurface[] visibleSurfaces, int sortPriority)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            RelatedWorldState = relatedWorldState;
            RelatedQuestId = relatedQuestId ?? string.Empty;
            RelatedLocationId = relatedLocationId ?? string.Empty;
            Strength = Math.Max(0, strength);
            PublicTextKey = string.IsNullOrEmpty(publicTextKey) ? DescriptionKey : publicTextKey;
            HiddenTextKey = string.IsNullOrEmpty(hiddenTextKey) ? "???" : hiddenTextKey;
            _sources = sources ?? Array.Empty<DiscoveryClueSource>();
            _conditions = conditions ?? Array.Empty<IDiscoveryClueCondition>();
            _completions = completions ?? Array.Empty<IDiscoveryClueCompletion>();
            _outcomes = outcomes ?? Array.Empty<IDiscoveryClueOutcome>();
            _visibleSurfaces = visibleSurfaces ?? Array.Empty<DiscoveryClueSummarySurface>();
            SortPriority = sortPriority;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public WorldStateFlag RelatedWorldState { get; }
        public string RelatedQuestId { get; }
        public string RelatedLocationId { get; }
        public int Strength { get; }
        public string PublicTextKey { get; }
        public string HiddenTextKey { get; }
        public IReadOnlyList<DiscoveryClueSource> Sources => _sources;
        public IReadOnlyList<IDiscoveryClueCondition> Conditions => _conditions;
        public IReadOnlyList<IDiscoveryClueCompletion> Completions => _completions;
        public IReadOnlyList<IDiscoveryClueOutcome> Outcomes => _outcomes;
        public int SortPriority { get; }

        public bool IsVisibleOn(DiscoveryClueSummarySurface surface)
        {
            if (_visibleSurfaces.Length == 0) return true;
            for (int i = 0; i < _visibleSurfaces.Length; i++) if (_visibleSurfaces[i] == surface) return true;
            return false;
        }

        public bool ConditionsSatisfied(in DiscoveryClueContext context)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.Evaluate(in context, this)) return false;
            }

            return true;
        }

        /// <summary>완료 방법이 하나도 없으면 어떤 사건이든 맞는다. 있으면 하나만 맞으면 된다.</summary>
        public bool MatchesCompletion(in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context)
        {
            if (_completions.Length == 0) return true;
            for (int i = 0; i < _completions.Length; i++)
            {
                var completion = _completions[i];
                if (completion != null && completion.IsCompleted(in completionEvent, in context, this)) return true;
            }

            return false;
        }
    }

    public interface IDiscoveryClueCondition
    {
        bool Evaluate(in DiscoveryClueContext context, DiscoveryClue clue);
    }

    public interface IDiscoveryClueCompletion
    {
        bool IsCompleted(in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context, DiscoveryClue clue);
    }

    /// <summary>결과 하나. 무엇이 바뀌었으면 true를 돌려주고, 줬거나 이미 가진 것의 id는 기록용 목록에 넣는다.</summary>
    public interface IDiscoveryClueOutcome
    {
        string OutcomeId { get; }
        bool Apply(in DiscoveryClueContext context, DiscoveryClue clue, DiscoveryClueGrants grants);
    }

    /// <summary>단서 하나를 끝내며 기록에 남길 것들. 같은 id는 한 번만 들어간다.</summary>
    public sealed class DiscoveryClueGrants
    {
        public readonly List<string> EncyclopediaEntryIds = new List<string>();
        public readonly List<string> CareerHintIds = new List<string>();
        public readonly List<string> QuestIds = new List<string>();
        public readonly List<string> WorldStateFlagIds = new List<string>();

        internal static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }

    public readonly struct DiscoveryClueContext
    {
        public readonly WorldStateProgress WorldStateProgress;
        public readonly DiscoveryClueProgress ClueProgress;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly QuestLog QuestLog;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly int CurrentDay;

        /// <summary>도감에 처음 발견한 시각으로 남는다. 원본은 DateTime.UtcNow.Ticks를 직접 읽었다.</summary>
        public readonly long NowUtcTicks;

        public DiscoveryClueContext(WorldStateProgress worldStateProgress, DiscoveryClueProgress clueProgress, StudentLifeProgress studentLifeProgress, QuestLog questLog,
            EncyclopediaProgress encyclopediaProgress, int currentDay, long nowUtcTicks = 0)
        {
            WorldStateProgress = worldStateProgress;
            ClueProgress = clueProgress;
            StudentLifeProgress = studentLifeProgress;
            QuestLog = questLog;
            EncyclopediaProgress = encyclopediaProgress;
            CurrentDay = Math.Max(1, currentDay);
            NowUtcTicks = nowUtcTicks;
        }
    }

    public readonly struct DiscoveryClueCompletionEvent
    {
        public readonly DiscoveryClueCompletionKind Kind;
        public readonly string TargetId;

        public DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind kind, string targetId)
        {
            Kind = kind;
            TargetId = targetId ?? string.Empty;
        }
    }

    public readonly struct DiscoveryClueResult
    {
        public readonly DiscoveryClueResultKind Kind;
        public readonly string ClueId;
        public readonly string Reason;
        public readonly string[] OutcomeIds;

        public DiscoveryClueResult(DiscoveryClueResultKind kind, string clueId, string reason, string[] outcomeIds)
        {
            Kind = kind;
            ClueId = clueId ?? string.Empty;
            Reason = reason ?? string.Empty;
            OutcomeIds = outcomeIds ?? Array.Empty<string>();
        }
    }

    /// <summary>출처를 보거나(드러남) 완료 사건을 맞춰 결과를 받는다(완료). 막히면 아무것도 바꾸지 않는다.</summary>
    public sealed class DiscoveryClueRunner
    {
        public bool TryRevealSource(DiscoveryClue clue, DiscoveryClueSource source, in DiscoveryClueContext context, out DiscoveryClueResult result)
        {
            string clueId = clue != null ? clue.Id : string.Empty;
            result = new DiscoveryClueResult(DiscoveryClueResultKind.InvalidRequest, clueId, "missing clue", Array.Empty<string>());
            if (clue == null || source == null || context.ClueProgress == null) return false;
            if (!clue.ConditionsSatisfied(in context))
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.ConditionFailed, clue.Id, "conditions failed", Array.Empty<string>());
                return false;
            }

            context.ClueProgress.MarkSourceSeen(clue, source, context.CurrentDay);
            result = new DiscoveryClueResult(DiscoveryClueResultKind.Revealed, clue.Id, string.Empty, Array.Empty<string>());
            return true;
        }

        public bool TryComplete(DiscoveryClue clue, in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context, out DiscoveryClueResult result)
        {
            string clueId = clue != null ? clue.Id : string.Empty;
            result = new DiscoveryClueResult(DiscoveryClueResultKind.InvalidRequest, clueId, "missing clue", Array.Empty<string>());
            if (clue == null || context.ClueProgress == null) return false;
            if (!clue.ConditionsSatisfied(in context))
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.ConditionFailed, clue.Id, "conditions failed", Array.Empty<string>());
                return false;
            }

            if (context.ClueProgress.GetRecord(clue.Id).Completed)
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.AlreadyCompleted, clue.Id, "already completed", Array.Empty<string>());
                return false;
            }

            if (!clue.MatchesCompletion(in completionEvent, in context))
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.CompletionNotMatched, clue.Id, "completion not matched", Array.Empty<string>());
                return false;
            }

            var outcomeIds = new List<string>();
            var grants = new DiscoveryClueGrants();
            for (int i = 0; i < clue.Outcomes.Count; i++)
            {
                var outcome = clue.Outcomes[i];
                if (outcome == null) continue;
                if (outcome.Apply(in context, clue, grants)) DiscoveryClueGrants.AddUnique(outcomeIds, outcome.OutcomeId);
            }

            context.ClueProgress.MarkCompleted(clue, context.CurrentDay, outcomeIds.ToArray(), grants.EncyclopediaEntryIds.ToArray(), grants.CareerHintIds.ToArray(),
                grants.QuestIds.ToArray(), grants.WorldStateFlagIds.ToArray());
            result = new DiscoveryClueResult(DiscoveryClueResultKind.Completed, clue.Id, string.Empty, outcomeIds.ToArray());
            return true;
        }
    }
}
