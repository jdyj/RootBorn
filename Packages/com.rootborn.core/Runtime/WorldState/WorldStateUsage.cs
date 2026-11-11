using System;
using System.Collections.Generic;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.WorldState
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

    /// <summary>켜진 월드 상태를 플레이어가 쓰는 방법 하나(예: 열린 자료실 책상). 근거 상태와 장소는 정의 객체·id로 가진다.</summary>
    public sealed class WorldStateUsage
    {
        private readonly IWorldStateUsageCondition[] _conditions;
        private readonly IWorldStateUsageOutcome[] _outcomes;
        private readonly WorldStateUsageSummarySurface[] _visibleSurfaces;
        private readonly WorldStateUsageRepeatPolicy _repeatPolicy;

        public WorldStateUsage(string id, string displayNameKey, string descriptionKey, WorldStateFlag sourceFlag, string relatedLocationId, string interactionPromptKey, string nextActionKey,
            IWorldStateUsageCondition[] conditions, IWorldStateUsageOutcome[] outcomes, WorldStateUsageRepeatPolicy repeatPolicy, WorldStateUsageSummarySurface[] visibleSurfaces, int sortPriority)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            SourceFlag = sourceFlag;
            RelatedLocationId = relatedLocationId ?? string.Empty;
            InteractionPromptKey = string.IsNullOrEmpty(interactionPromptKey) ? DisplayNameKey : interactionPromptKey;
            NextActionKey = string.IsNullOrEmpty(nextActionKey) ? InteractionPromptKey : nextActionKey;
            _conditions = conditions ?? Array.Empty<IWorldStateUsageCondition>();
            _outcomes = outcomes ?? Array.Empty<IWorldStateUsageOutcome>();
            _repeatPolicy = repeatPolicy;
            _visibleSurfaces = visibleSurfaces ?? Array.Empty<WorldStateUsageSummarySurface>();
            SortPriority = sortPriority;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public WorldStateFlag SourceFlag { get; }
        public string SourceFlagId => SourceFlag != null ? SourceFlag.Id : string.Empty;
        public string RelatedLocationId { get; }
        public string InteractionPromptKey { get; }
        public string NextActionKey { get; }
        public IReadOnlyList<IWorldStateUsageCondition> Conditions => _conditions;
        public IReadOnlyList<IWorldStateUsageOutcome> Outcomes => _outcomes;

        /// <summary>정책을 정하지 않았으면 한 번만 쓸 수 있다.</summary>
        public WorldStateUsageRepeatPolicy RepeatPolicy => _repeatPolicy ?? WorldStateUsageRepeatPolicy.OncePolicy;
        public int SortPriority { get; }

        public bool IsVisibleOn(WorldStateUsageSummarySurface surface)
        {
            if (_visibleSurfaces.Length == 0) return true;
            for (int i = 0; i < _visibleSurfaces.Length; i++) if (_visibleSurfaces[i] == surface) return true;
            return false;
        }

        public bool ConditionsSatisfied(in WorldStateUsageContext context)
        {
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.Evaluate(in context, this)) return false;
            }

            return true;
        }
    }

    public interface IWorldStateUsageCondition
    {
        bool Evaluate(in WorldStateUsageContext context, WorldStateUsage usage);
    }

    /// <summary>
    /// 결과 하나. 무엇이 바뀌었으면 true를 돌려주고, 줬거나 이미 가진 것의 id는 기록용 목록에 넣는다.
    /// </summary>
    public interface IWorldStateUsageOutcome
    {
        string OutcomeId { get; }
        bool Apply(in WorldStateUsageContext context, WorldStateUsage usage, WorldStateUsageGrants grants);
    }

    /// <summary>한 번 사용하며 기록에 남길 것들. 같은 id는 한 번만 들어간다.</summary>
    public sealed class WorldStateUsageGrants
    {
        public readonly List<string> ActivityIds = new List<string>();
        public readonly List<string> QuestIds = new List<string>();
        public readonly List<string> EncyclopediaEntryIds = new List<string>();
        public readonly List<string> CareerHintIds = new List<string>();

        internal static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }

    public readonly struct WorldStateUsageContext
    {
        public readonly WorldStateProgress WorldStateProgress;
        public readonly WorldStateUsageProgress UsageProgress;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly QuestLog QuestLog;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly int CurrentDay;

        /// <summary>도감에 처음 발견한 시각으로 남는다. 원본은 DateTime.UtcNow.Ticks를 직접 읽었다.</summary>
        public readonly long NowUtcTicks;

        public WorldStateUsageContext(WorldStateProgress worldStateProgress, WorldStateUsageProgress usageProgress, StudentLifeProgress studentLifeProgress, QuestLog questLog,
            EncyclopediaProgress encyclopediaProgress, int currentDay, long nowUtcTicks = 0)
        {
            WorldStateProgress = worldStateProgress;
            UsageProgress = usageProgress;
            StudentLifeProgress = studentLifeProgress;
            QuestLog = questLog;
            EncyclopediaProgress = encyclopediaProgress;
            CurrentDay = Math.Max(1, currentDay);
            NowUtcTicks = nowUtcTicks;
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
            UsageId = usageId ?? string.Empty;
            Reason = reason ?? string.Empty;
            OutcomeIds = outcomeIds ?? Array.Empty<string>();
        }
    }

    /// <summary>조건 → 반복 정책 → 결과 적용 → 사용 기록 순으로 처리한다. 조건이나 정책에서 막히면 아무것도 바꾸지 않는다.</summary>
    public sealed class WorldStateUsageRunner
    {
        public bool TryUse(WorldStateUsage usage, in WorldStateUsageContext context, out WorldStateUsageResult result)
        {
            string usageId = usage != null ? usage.Id : string.Empty;
            result = new WorldStateUsageResult(WorldStateUsageResultKind.InvalidRequest, usageId, "missing usage", Array.Empty<string>());
            if (usage == null || context.UsageProgress == null) return false;
            if (!usage.ConditionsSatisfied(in context))
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

            var outcomeIds = new List<string>();
            var grants = new WorldStateUsageGrants();
            for (int i = 0; i < usage.Outcomes.Count; i++)
            {
                var outcome = usage.Outcomes[i];
                if (outcome == null) continue;
                if (outcome.Apply(in context, usage, grants)) WorldStateUsageGrants.AddUnique(outcomeIds, outcome.OutcomeId);
            }

            context.UsageProgress.MarkUsed(usage, context.CurrentDay, policy, outcomeIds.ToArray(), grants.ActivityIds.ToArray(), grants.QuestIds.ToArray(),
                grants.EncyclopediaEntryIds.ToArray(), grants.CareerHintIds.ToArray());
            result = new WorldStateUsageResult(WorldStateUsageResultKind.Applied, usage.Id, string.Empty, outcomeIds.ToArray());
            return true;
        }
    }

    /// <summary>근거 월드 상태가 켜져 있으면 참.</summary>
    public sealed class WorldStateUsageFlagActiveCondition : IWorldStateUsageCondition
    {
        public bool Evaluate(in WorldStateUsageContext context, WorldStateUsage usage)
        {
            return usage != null && context.WorldStateProgress != null && context.WorldStateProgress.IsActive(usage.SourceFlag);
        }
    }

    /// <summary>장소 활동을 연다. 활동은 id로 가리키며 기록에만 남는다(원본과 같다).</summary>
    public sealed class WorldStateUsageActivityUnlockOutcome : IWorldStateUsageOutcome
    {
        private readonly string[] _activityIds;

        public WorldStateUsageActivityUnlockOutcome(string[] activityIds) { _activityIds = activityIds ?? Array.Empty<string>(); }

        public IReadOnlyList<string> ActivityIds => _activityIds;
        public string OutcomeId => "activity";

        public bool Apply(in WorldStateUsageContext context, WorldStateUsage usage, WorldStateUsageGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _activityIds.Length; i++)
            {
                if (string.IsNullOrEmpty(_activityIds[i])) continue;
                WorldStateUsageGrants.AddUnique(grants.ActivityIds, _activityIds[i]);
                applied = true;
            }

            return applied;
        }
    }

    /// <summary>진로 힌트를 연다. 이미 열린 힌트는 기록에만 남는다.</summary>
    public sealed class WorldStateUsageCareerHintOutcome : IWorldStateUsageOutcome
    {
        private readonly string[] _careerIds;

        public WorldStateUsageCareerHintOutcome(string[] careerIds) { _careerIds = careerIds ?? Array.Empty<string>(); }

        public IReadOnlyList<string> CareerIds => _careerIds;
        public string OutcomeId => "career-hint";

        public bool Apply(in WorldStateUsageContext context, WorldStateUsage usage, WorldStateUsageGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _careerIds.Length; i++)
            {
                string careerId = _careerIds[i];
                if (string.IsNullOrEmpty(careerId)) continue;
                if (context.StudentLifeProgress == null || !context.StudentLifeProgress.IsCareerHintUnlocked(careerId)) applied = true;
                context.StudentLifeProgress?.UnlockCareerHint(careerId);
                WorldStateUsageGrants.AddUnique(grants.CareerHintIds, careerId);
            }

            return applied;
        }
    }

    /// <summary>도감 항목을 연다. 이미 연 항목은 기록에만 남는다.</summary>
    public sealed class WorldStateUsageEncyclopediaOutcome : IWorldStateUsageOutcome
    {
        private readonly EncyclopediaEntry[] _entries;

        public WorldStateUsageEncyclopediaOutcome(EncyclopediaEntry[] entries) { _entries = entries ?? Array.Empty<EncyclopediaEntry>(); }

        public IReadOnlyList<EncyclopediaEntry> Entries => _entries;
        public string OutcomeId => "encyclopedia";

        public bool Apply(in WorldStateUsageContext context, WorldStateUsage usage, WorldStateUsageGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                if (context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(entry.Id, entry.RevealStage, context.NowUtcTicks)) applied = true;
                WorldStateUsageGrants.AddUnique(grants.EncyclopediaEntryIds, entry.Id);
            }

            return applied;
        }
    }

    /// <summary>
    /// 이어지는 퀘스트를 기록에 넣고 받는다. 이미 받았거나 끝난 퀘스트는 건드리지 않는다
    /// (Unity 판은 매번 다시 넣어 진행도를 지웠다).
    /// </summary>
    public sealed class WorldStateUsageQuestUnlockOutcome : IWorldStateUsageOutcome
    {
        private readonly QuestDefinition[] _quests;

        public WorldStateUsageQuestUnlockOutcome(QuestDefinition[] quests) { _quests = quests ?? Array.Empty<QuestDefinition>(); }

        public IReadOnlyList<QuestDefinition> Quests => _quests;
        public string OutcomeId => "quest";

        public bool Apply(in WorldStateUsageContext context, WorldStateUsage usage, WorldStateUsageGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                WorldStateUsageGrants.AddUnique(grants.QuestIds, quest.Id);
                if (FollowUpQuests.TryStart(context.QuestLog, quest)) applied = true;
            }

            return applied;
        }
    }

    /// <summary>몇 번, 얼마 간격으로 쓸 수 있는지. 쿨다운은 DailyCooldown만 만든다(원본과 같다). MaxUses가 0이면 제한이 없다.</summary>
    public sealed class WorldStateUsageRepeatPolicy
    {
        public static readonly WorldStateUsageRepeatPolicy OncePolicy = new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.Once, 0, 1, false);

        public WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode mode, int cooldownDays, int maxUses, bool allowDuplicateRewards)
        {
            Mode = mode;
            CooldownDays = Math.Max(0, cooldownDays);
            MaxUses = Math.Max(0, maxUses);
            AllowDuplicateRewards = allowDuplicateRewards;
        }

        public WorldStateUsageRepeatMode Mode { get; }
        public int CooldownDays { get; }
        public int MaxUses { get; }

        /// <summary>원본에서도 어디에서도 읽지 않는 값이다. 데이터 호환을 위해 둔다.</summary>
        public bool AllowDuplicateRewards { get; }

        public bool CanUse(WorldStateUsage usage, WorldStateUsageProgress progress, int currentDay, out string reason)
        {
            reason = string.Empty;
            if (usage == null || progress == null)
            {
                reason = "missing usage progress";
                return false;
            }

            var record = progress.GetRecord(usage.Id);
            int day = Math.Max(1, currentDay);
            if (record.CooldownUntilDay > day)
            {
                reason = "cooldown until day " + record.CooldownUntilDay;
                return false;
            }

            if (Mode == WorldStateUsageRepeatMode.Once && record.UsedCount > 0)
            {
                reason = "already used";
                return false;
            }

            if (Mode == WorldStateUsageRepeatMode.OncePerDay && record.LastUsedDay == day)
            {
                reason = "already used today";
                return false;
            }

            if (Mode == WorldStateUsageRepeatMode.MaxUses && MaxUses > 0 && record.UsedCount >= MaxUses)
            {
                reason = "max uses reached";
                return false;
            }

            return true;
        }

        public int NextCooldownUntilDay(int currentDay)
        {
            if (Mode != WorldStateUsageRepeatMode.DailyCooldown || CooldownDays <= 0) return 0;
            return Math.Max(1, currentDay) + CooldownDays;
        }
    }
}
