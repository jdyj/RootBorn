using System;
using System.Collections.Generic;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.DiscoveryClues
{
    internal static class ClueInterpretationConditionDefaults
    {
        public const string LockedReasonKey = "condition.missing";
        public static string Reason(string key) => string.IsNullOrEmpty(key) ? LockedReasonKey : key;
    }

    /// <summary>해석의 단서를 본 적이 있으면 참.</summary>
    public sealed class ClueInterpretationClueSeenCondition : IClueInterpretationCondition
    {
        public ClueInterpretationClueSeenCondition(string lockedReasonKey = null) { LockedReasonKey = ClueInterpretationConditionDefaults.Reason(lockedReasonKey); }

        public string LockedReasonKey { get; }

        public bool Evaluate(in ClueInterpretationContext context, ClueInterpretation interpretation)
        {
            return context.ClueProgress != null && interpretation != null && interpretation.ClueId.Length > 0 && context.ClueProgress.GetRecord(interpretation.ClueId).Seen;
        }
    }

    /// <summary>관계 수치가 최소값 이상이면 참. 최소값은 0 아래로 내려가지 않는다.</summary>
    public sealed class ClueInterpretationRelationshipThresholdCondition : IClueInterpretationCondition
    {
        public ClueInterpretationRelationshipThresholdCondition(string relationshipId, int minimumValue, string lockedReasonKey = null)
        {
            RelationshipId = relationshipId ?? string.Empty;
            MinimumValue = Math.Max(0, minimumValue);
            LockedReasonKey = ClueInterpretationConditionDefaults.Reason(lockedReasonKey);
        }

        public string RelationshipId { get; }
        public int MinimumValue { get; }
        public string LockedReasonKey { get; }

        public bool Evaluate(in ClueInterpretationContext context, ClueInterpretation interpretation)
        {
            return context.StudentLifeProgress != null && RelationshipId.Length > 0 && context.StudentLifeProgress.GetRelationshipValue(RelationshipId) >= MinimumValue;
        }
    }

    /// <summary>정한 월드 상태가 켜져 있으면 참.</summary>
    public sealed class ClueInterpretationWorldStateActiveCondition : IClueInterpretationCondition
    {
        public ClueInterpretationWorldStateActiveCondition(WorldStateFlag flag, string lockedReasonKey = null)
        {
            Flag = flag;
            LockedReasonKey = ClueInterpretationConditionDefaults.Reason(lockedReasonKey);
        }

        public WorldStateFlag Flag { get; }
        public string LockedReasonKey { get; }

        public bool Evaluate(in ClueInterpretationContext context, ClueInterpretation interpretation) => context.WorldStateProgress != null && Flag != null && context.WorldStateProgress.IsActive(Flag);
    }

    /// <summary>진로 힌트를 연다. 이미 열린 힌트는 기록에만 남는다.</summary>
    public sealed class ClueInterpretationCareerHintOutcome : IClueInterpretationOutcome
    {
        private readonly string[] _careerIds;

        public ClueInterpretationCareerHintOutcome(string[] careerIds) { _careerIds = careerIds ?? Array.Empty<string>(); }

        public IReadOnlyList<string> CareerIds => _careerIds;
        public string OutcomeId => "career-hint";

        public bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _careerIds.Length; i++)
            {
                string careerId = _careerIds[i];
                if (string.IsNullOrEmpty(careerId)) continue;
                if (context.StudentLifeProgress == null || !context.StudentLifeProgress.IsCareerHintUnlocked(careerId)) applied = true;
                context.StudentLifeProgress?.UnlockCareerHint(careerId);
                ClueInterpretationGrants.AddUnique(grants.CareerHintIds, careerId);
            }

            return applied;
        }
    }

    /// <summary>도감 항목을 연다. 이미 연 항목은 기록에만 남는다.</summary>
    public sealed class ClueInterpretationEncyclopediaOutcome : IClueInterpretationOutcome
    {
        private readonly EncyclopediaEntry[] _entries;

        public ClueInterpretationEncyclopediaOutcome(EncyclopediaEntry[] entries) { _entries = entries ?? Array.Empty<EncyclopediaEntry>(); }

        public IReadOnlyList<EncyclopediaEntry> Entries => _entries;
        public string OutcomeId => "encyclopedia";

        public bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                if (context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(entry.Id, entry.RevealStage, context.NowUtcTicks)) applied = true;
                ClueInterpretationGrants.AddUnique(grants.EncyclopediaEntryIds, entry.Id);
            }

            return applied;
        }
    }

    /// <summary>이어지는 퀘스트를 연다. 이미 시작한 퀘스트는 건드리지 않는다(<see cref="FollowUpQuests"/>).</summary>
    public sealed class ClueInterpretationQuestUnlockOutcome : IClueInterpretationOutcome
    {
        private readonly QuestDefinition[] _quests;

        public ClueInterpretationQuestUnlockOutcome(QuestDefinition[] quests) { _quests = quests ?? Array.Empty<QuestDefinition>(); }

        public IReadOnlyList<QuestDefinition> Quests => _quests;
        public string OutcomeId => "quest";

        public bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                ClueInterpretationGrants.AddUnique(grants.QuestIds, quest.Id);
                if (FollowUpQuests.TryStart(context.QuestLog, quest)) applied = true;
            }

            return applied;
        }
    }

    /// <summary>관계 수치를 더한다. 반복 정책이면 할 때마다 더한다(원본과 같다). 0이면 결과가 아니다.</summary>
    public sealed class ClueInterpretationRelationshipDeltaOutcome : IClueInterpretationOutcome
    {
        public ClueInterpretationRelationshipDeltaOutcome(string relationshipId, int delta)
        {
            RelationshipId = relationshipId ?? string.Empty;
            Delta = delta;
        }

        public string RelationshipId { get; }
        public int Delta { get; }
        public string OutcomeId => "relationship";

        public bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants)
        {
            if (RelationshipId.Length == 0 || Delta == 0) return false;
            context.StudentLifeProgress?.AddRelationship(RelationshipId, Delta);
            ClueInterpretationGrants.AddUnique(grants.RelationshipIds, RelationshipId);
            return true;
        }
    }

    /// <summary>상태 수치를 더한다. 0이면 결과가 아니다.</summary>
    public sealed class ClueInterpretationStatusDeltaOutcome : IClueInterpretationOutcome
    {
        public ClueInterpretationStatusDeltaOutcome(string statusId, int delta)
        {
            StatusId = statusId ?? string.Empty;
            Delta = delta;
        }

        public string StatusId { get; }
        public int Delta { get; }
        public string OutcomeId => "status";

        public bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants)
        {
            if (StatusId.Length == 0 || Delta == 0) return false;
            context.StudentLifeProgress?.AddStatus(StatusId, Delta);
            ClueInterpretationGrants.AddUnique(grants.StatusIds, StatusId);
            return true;
        }
    }

    /// <summary>월드 상태를 켠다. 켠 원인 이벤트는 해석 id다. 이미 켜진 상태는 기록에만 남는다.</summary>
    public sealed class ClueInterpretationWorldStateOutcome : IClueInterpretationOutcome
    {
        private readonly WorldStateFlag[] _flags;

        public ClueInterpretationWorldStateOutcome(WorldStateFlag[] flags) { _flags = flags ?? Array.Empty<WorldStateFlag>(); }

        public IReadOnlyList<WorldStateFlag> Flags => _flags;
        public string OutcomeId => "world-state";

        public bool Apply(in ClueInterpretationContext context, ClueInterpretation interpretation, ClueInterpretationGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (flag == null || string.IsNullOrEmpty(flag.Id)) continue;
                var source = new WorldStateActivationSource(string.Empty, string.Empty, interpretation != null ? interpretation.Id : string.Empty, context.CurrentDay);
                if (context.WorldStateProgress == null || context.WorldStateProgress.TryActivate(flag, source)) applied = true;
                ClueInterpretationGrants.AddUnique(grants.WorldStateIds, flag.Id);
            }

            return applied;
        }
    }
}
