using System;
using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
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

    [CreateAssetMenu(fileName = "DiscoveryClue_New", menuName = "Rootborn/Discovery Clues/Definition")]
    public sealed class DiscoveryClueDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private WorldStateFlagDefinition _relatedWorldState;
        [SerializeField] private QuestDefinition _relatedQuest;
        [SerializeField] private LocationDefinition _relatedLocation;
        [SerializeField] private int _strength;
        [SerializeField] private string _publicTextKey;
        [SerializeField] private string _hiddenTextKey;
        [SerializeField] private DiscoveryClueSourceDefinition[] _sources = Array.Empty<DiscoveryClueSourceDefinition>();
        [SerializeField] private DiscoveryClueConditionBase[] _conditions = Array.Empty<DiscoveryClueConditionBase>();
        [SerializeField] private DiscoveryClueCompletionBase[] _completions = Array.Empty<DiscoveryClueCompletionBase>();
        [SerializeField] private DiscoveryClueOutcomeBase[] _outcomes = Array.Empty<DiscoveryClueOutcomeBase>();
        [SerializeField] private DiscoveryClueSummarySurface[] _visibleSurfaces = Array.Empty<DiscoveryClueSummarySurface>();
        [SerializeField] private int _sortPriority;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public WorldStateFlagDefinition RelatedWorldState => _relatedWorldState;
        public QuestDefinition RelatedQuest => _relatedQuest;
        public LocationDefinition RelatedLocation => _relatedLocation;
        public int Strength => Mathf.Max(0, _strength);
        public string PublicTextKey => string.IsNullOrEmpty(_publicTextKey) ? DescriptionKey : _publicTextKey;
        public string HiddenTextKey => string.IsNullOrEmpty(_hiddenTextKey) ? "???" : _hiddenTextKey;
        public IReadOnlyList<DiscoveryClueSourceDefinition> Sources => _sources;
        public IReadOnlyList<DiscoveryClueConditionBase> Conditions => _conditions;
        public IReadOnlyList<DiscoveryClueCompletionBase> Completions => _completions;
        public IReadOnlyList<DiscoveryClueOutcomeBase> Outcomes => _outcomes;
        public int SortPriority => _sortPriority;

        public bool IsVisibleOn(DiscoveryClueSummarySurface surface)
        {
            if (_visibleSurfaces == null || _visibleSurfaces.Length == 0) return true;
            for (int i = 0; i < _visibleSurfaces.Length; i++) if (_visibleSurfaces[i] == surface) return true;
            return false;
        }

        public bool ConditionsSatisfied(in DiscoveryClueContext context)
        {
            if (_conditions == null) return true;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.Evaluate(context, this)) return false;
            }

            return true;
        }

        public bool MatchesCompletion(in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context)
        {
            if (_completions == null || _completions.Length == 0) return true;
            for (int i = 0; i < _completions.Length; i++)
            {
                var completion = _completions[i];
                if (completion != null && completion.IsCompleted(completionEvent, context, this)) return true;
            }

            return false;
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, WorldStateFlagDefinition relatedWorldState, QuestDefinition relatedQuest, LocationDefinition relatedLocation, int strength, string publicTextKey, string hiddenTextKey, DiscoveryClueSourceDefinition[] sources, DiscoveryClueConditionBase[] conditions, DiscoveryClueCompletionBase[] completions, DiscoveryClueOutcomeBase[] outcomes, DiscoveryClueSummarySurface[] visibleSurfaces, int sortPriority)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _descriptionKey = descriptionKey;
            _relatedWorldState = relatedWorldState;
            _relatedQuest = relatedQuest;
            _relatedLocation = relatedLocation;
            _strength = Mathf.Max(0, strength);
            _publicTextKey = publicTextKey;
            _hiddenTextKey = hiddenTextKey;
            _sources = sources ?? Array.Empty<DiscoveryClueSourceDefinition>();
            _conditions = conditions ?? Array.Empty<DiscoveryClueConditionBase>();
            _completions = completions ?? Array.Empty<DiscoveryClueCompletionBase>();
            _outcomes = outcomes ?? Array.Empty<DiscoveryClueOutcomeBase>();
            _visibleSurfaces = visibleSurfaces ?? Array.Empty<DiscoveryClueSummarySurface>();
            _sortPriority = sortPriority;
        }
    }

    public abstract class DiscoveryClueConditionBase : ScriptableObject
    {
        public abstract bool Evaluate(in DiscoveryClueContext context, DiscoveryClueDefinition clue);
    }

    public abstract class DiscoveryClueCompletionBase : ScriptableObject
    {
        public abstract bool IsCompleted(in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context, DiscoveryClueDefinition clue);
    }

    public abstract class DiscoveryClueOutcomeBase : ScriptableObject
    {
        public abstract string OutcomeId { get; }
        public abstract bool Apply(in DiscoveryClueContext context, DiscoveryClueDefinition clue, List<string> discoveredEntries, List<string> careerHints, List<string> followUpQuests, List<string> worldStateFlags);
    }

    public readonly struct DiscoveryClueContext
    {
        public readonly WorldStateProgress WorldStateProgress;
        public readonly DiscoveryClueProgress ClueProgress;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly QuestLog QuestLog;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly int CurrentDay;

        public DiscoveryClueContext(WorldStateProgress worldStateProgress, DiscoveryClueProgress clueProgress, StudentLifeProgress studentLifeProgress, QuestLog questLog, EncyclopediaProgress encyclopediaProgress, int currentDay)
        {
            WorldStateProgress = worldStateProgress;
            ClueProgress = clueProgress;
            StudentLifeProgress = studentLifeProgress;
            QuestLog = questLog;
            EncyclopediaProgress = encyclopediaProgress;
            CurrentDay = Mathf.Max(1, currentDay);
        }
    }

    public readonly struct DiscoveryClueCompletionEvent
    {
        public readonly DiscoveryClueCompletionKind Kind;
        public readonly string TargetId;

        public DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind kind, string targetId)
        {
            Kind = kind;
            TargetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
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
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            Reason = string.IsNullOrEmpty(reason) ? string.Empty : reason;
            OutcomeIds = outcomeIds ?? Array.Empty<string>();
        }
    }

    public sealed class DiscoveryClueRunner
    {
        public bool TryRevealSource(DiscoveryClueDefinition clue, DiscoveryClueSourceDefinition source, in DiscoveryClueContext context, out DiscoveryClueResult result)
        {
            string clueId = clue != null ? clue.Id : string.Empty;
            result = new DiscoveryClueResult(DiscoveryClueResultKind.InvalidRequest, clueId, "missing clue", Array.Empty<string>());
            if (clue == null || source == null || context.ClueProgress == null) return false;
            if (!clue.ConditionsSatisfied(context))
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.ConditionFailed, clue.Id, "conditions failed", Array.Empty<string>());
                return false;
            }

            context.ClueProgress.MarkSourceSeen(clue, source, context.CurrentDay);
            result = new DiscoveryClueResult(DiscoveryClueResultKind.Revealed, clue.Id, string.Empty, Array.Empty<string>());
            return true;
        }

        public bool TryComplete(DiscoveryClueDefinition clue, in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context, out DiscoveryClueResult result)
        {
            string clueId = clue != null ? clue.Id : string.Empty;
            result = new DiscoveryClueResult(DiscoveryClueResultKind.InvalidRequest, clueId, "missing clue", Array.Empty<string>());
            if (clue == null || context.ClueProgress == null) return false;
            if (!clue.ConditionsSatisfied(context))
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.ConditionFailed, clue.Id, "conditions failed", Array.Empty<string>());
                return false;
            }

            if (context.ClueProgress.GetRecord(clue.Id).Completed)
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.AlreadyCompleted, clue.Id, "already completed", Array.Empty<string>());
                return false;
            }

            if (!clue.MatchesCompletion(completionEvent, context))
            {
                result = new DiscoveryClueResult(DiscoveryClueResultKind.CompletionNotMatched, clue.Id, "completion not matched", Array.Empty<string>());
                return false;
            }

            var outcomes = new List<string>();
            var entries = new List<string>();
            var careers = new List<string>();
            var quests = new List<string>();
            var worldFlags = new List<string>();
            for (int i = 0; i < clue.Outcomes.Count; i++)
            {
                var outcome = clue.Outcomes[i];
                if (outcome == null) continue;
                if (outcome.Apply(context, clue, entries, careers, quests, worldFlags)) AddUnique(outcomes, outcome.OutcomeId);
            }

            context.ClueProgress.MarkCompleted(clue, context.CurrentDay, outcomes.ToArray(), entries.ToArray(), careers.ToArray(), quests.ToArray(), worldFlags.ToArray());
            result = new DiscoveryClueResult(DiscoveryClueResultKind.Completed, clue.Id, string.Empty, outcomes.ToArray());
            return true;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }
}
