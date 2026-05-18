using System;
using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    public enum WorldStateUsageRepeatMode
    {
        Once,
        Repeatable,
        OncePerDay,
        DailyCooldown,
        MaxUses
    }

    public enum WorldStateUsageResultKind
    {
        Applied,
        InvalidRequest,
        ConditionFailed,
        RepeatBlocked
    }

    public enum WorldStateUsageSummarySurface
    {
        LocationPanel,
        WorldLog,
        DayResult,
        Hud
    }

    [CreateAssetMenu(fileName = "WorldStateUsage_New", menuName = "Rootborn/World State/Usage/Definition")]
    public sealed class WorldStateUsageDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private WorldStateFlagDefinition _sourceFlag;
        [SerializeField] private LocationDefinition _relatedLocation;
        [SerializeField] private string _interactionPromptKey;
        [SerializeField] private string _nextActionKey;
        [SerializeField] private WorldStateUsageConditionBase[] _conditions = Array.Empty<WorldStateUsageConditionBase>();
        [SerializeField] private WorldStateUsageOutcomeBase[] _outcomes = Array.Empty<WorldStateUsageOutcomeBase>();
        [SerializeField] private WorldStateUsageRepeatPolicyDefinition _repeatPolicy;
        [SerializeField] private WorldStateUsageSummarySurface[] _visibleSurfaces = Array.Empty<WorldStateUsageSummarySurface>();
        [SerializeField] private int _sortPriority;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public WorldStateFlagDefinition SourceFlag => _sourceFlag;
        public string SourceFlagId => _sourceFlag != null ? _sourceFlag.Id : string.Empty;
        public LocationDefinition RelatedLocation => _relatedLocation;
        public string InteractionPromptKey => string.IsNullOrEmpty(_interactionPromptKey) ? DisplayNameKey : _interactionPromptKey;
        public string NextActionKey => string.IsNullOrEmpty(_nextActionKey) ? InteractionPromptKey : _nextActionKey;
        public IReadOnlyList<WorldStateUsageConditionBase> Conditions => _conditions;
        public IReadOnlyList<WorldStateUsageOutcomeBase> Outcomes => _outcomes;
        public WorldStateUsageRepeatPolicyDefinition RepeatPolicy => _repeatPolicy != null ? _repeatPolicy : WorldStateUsageRepeatPolicyDefinition.OncePolicy;
        public int SortPriority => _sortPriority;

        public bool IsVisibleOn(WorldStateUsageSummarySurface surface)
        {
            if (_visibleSurfaces == null || _visibleSurfaces.Length == 0) return true;
            for (int i = 0; i < _visibleSurfaces.Length; i++) if (_visibleSurfaces[i] == surface) return true;
            return false;
        }

        public bool ConditionsSatisfied(in WorldStateUsageContext context)
        {
            if (_conditions == null) return true;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.Evaluate(context, this)) return false;
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, WorldStateFlagDefinition sourceFlag, LocationDefinition relatedLocation, string interactionPromptKey, string nextActionKey, WorldStateUsageConditionBase[] conditions, WorldStateUsageOutcomeBase[] outcomes, WorldStateUsageRepeatPolicyDefinition repeatPolicy, WorldStateUsageSummarySurface[] visibleSurfaces, int sortPriority)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _descriptionKey = descriptionKey;
            _sourceFlag = sourceFlag;
            _relatedLocation = relatedLocation;
            _interactionPromptKey = interactionPromptKey;
            _nextActionKey = nextActionKey;
            _conditions = conditions ?? Array.Empty<WorldStateUsageConditionBase>();
            _outcomes = outcomes ?? Array.Empty<WorldStateUsageOutcomeBase>();
            _repeatPolicy = repeatPolicy;
            _visibleSurfaces = visibleSurfaces ?? Array.Empty<WorldStateUsageSummarySurface>();
            _sortPriority = sortPriority;
        }

        public void ConfigureRepeatPolicyForTests(WorldStateUsageRepeatPolicyDefinition repeatPolicy)
        {
            _repeatPolicy = repeatPolicy;
        }
    }

    public abstract class WorldStateUsageConditionBase : ScriptableObject
    {
        public abstract bool Evaluate(in WorldStateUsageContext context, WorldStateUsageDefinition usage);
    }

    public abstract class WorldStateUsageOutcomeBase : ScriptableObject
    {
        public abstract string OutcomeId { get; }
        public abstract bool Apply(in WorldStateUsageContext context, WorldStateUsageDefinition usage, List<string> unlockedActivities, List<string> unlockedQuests, List<string> discoveredEntries, List<string> careerHints);
    }

    public readonly struct WorldStateUsageContext
    {
        public readonly WorldStateProgress WorldStateProgress;
        public readonly WorldStateUsageProgress UsageProgress;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly QuestLog QuestLog;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly object Inventory;
        public readonly int CurrentDay;

        public WorldStateUsageContext(WorldStateProgress worldStateProgress, WorldStateUsageProgress usageProgress, StudentLifeProgress studentLifeProgress, QuestLog questLog, EncyclopediaProgress encyclopediaProgress, object inventory, int currentDay)
        {
            WorldStateProgress = worldStateProgress;
            UsageProgress = usageProgress;
            StudentLifeProgress = studentLifeProgress;
            QuestLog = questLog;
            EncyclopediaProgress = encyclopediaProgress;
            Inventory = inventory;
            CurrentDay = Mathf.Max(1, currentDay);
        }
    }

    public readonly struct WorldStateUsageResult
    {
        public readonly WorldStateUsageResultKind Kind;
        public readonly string UsageId;
        public readonly string Reason;
        public readonly string[] OutcomeIds;

        public WorldStateUsageResult(WorldStateUsageResultKind kind, string usageId, string reason, string[] outcomeIds)
        {
            Kind = kind;
            UsageId = string.IsNullOrEmpty(usageId) ? string.Empty : usageId;
            Reason = string.IsNullOrEmpty(reason) ? string.Empty : reason;
            OutcomeIds = outcomeIds ?? Array.Empty<string>();
        }
    }

    public sealed class WorldStateUsageRunner
    {
        public bool TryUse(WorldStateUsageDefinition usage, in WorldStateUsageContext context, out WorldStateUsageResult result)
        {
            string usageId = usage != null ? usage.Id : string.Empty;
            result = new WorldStateUsageResult(WorldStateUsageResultKind.InvalidRequest, usageId, "missing usage", Array.Empty<string>());
            if (usage == null || context.UsageProgress == null) return false;
            if (!usage.ConditionsSatisfied(context))
            {
                result = new WorldStateUsageResult(WorldStateUsageResultKind.ConditionFailed, usage.Id, "conditions failed", Array.Empty<string>());
                return false;
            }

            var policy = usage.RepeatPolicy;
            if (!policy.CanUse(usage, context.UsageProgress, context.CurrentDay, out string reason))
            {
                result = new WorldStateUsageResult(WorldStateUsageResultKind.RepeatBlocked, usage.Id, reason, Array.Empty<string>());
                return false;
            }

            var outcomes = new List<string>();
            var activities = new List<string>();
            var quests = new List<string>();
            var entries = new List<string>();
            var careers = new List<string>();
            for (int i = 0; i < usage.Outcomes.Count; i++)
            {
                var outcome = usage.Outcomes[i];
                if (outcome == null) continue;
                if (outcome.Apply(context, usage, activities, quests, entries, careers)) AddUnique(outcomes, outcome.OutcomeId);
            }

            context.UsageProgress.MarkUsed(usage, context.CurrentDay, policy, outcomes.ToArray(), activities.ToArray(), quests.ToArray(), entries.ToArray(), careers.ToArray());
            result = new WorldStateUsageResult(WorldStateUsageResultKind.Applied, usage.Id, string.Empty, outcomes.ToArray());
            return true;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
        }
    }
}
