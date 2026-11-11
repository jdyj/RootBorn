using System;
using System.Collections.Generic;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.DiscoveryClues
{
    /// <summary>단서의 관련 월드 상태가 켜져 있으면 참.</summary>
    public sealed class DiscoveryClueWorldStateActiveCondition : IDiscoveryClueCondition
    {
        public bool Evaluate(in DiscoveryClueContext context, DiscoveryClue clue)
        {
            return clue != null && context.WorldStateProgress != null && context.WorldStateProgress.IsActive(clue.RelatedWorldState);
        }
    }

    /// <summary>장소에 가면 끝난다. 장소를 비우면 단서의 관련 장소를 쓴다. 기대 장소가 없으면 맞지 않는다.</summary>
    public sealed class DiscoveryClueLocationVisitCompletion : IDiscoveryClueCompletion
    {
        public DiscoveryClueLocationVisitCompletion(string locationId) { LocationId = locationId ?? string.Empty; }

        public string LocationId { get; }

        public bool IsCompleted(in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context, DiscoveryClue clue)
        {
            if (completionEvent.Kind != DiscoveryClueCompletionKind.LocationVisited) return false;
            string expected = LocationId.Length > 0 ? LocationId : clue != null ? clue.RelatedLocationId : string.Empty;
            return expected.Length > 0 && string.Equals(expected, completionEvent.TargetId, StringComparison.Ordinal);
        }
    }

    /// <summary>도감 항목을 연다. 이미 연 항목은 기록에만 남는다.</summary>
    public sealed class DiscoveryClueEncyclopediaOutcome : IDiscoveryClueOutcome
    {
        private readonly EncyclopediaEntry[] _entries;

        public DiscoveryClueEncyclopediaOutcome(EncyclopediaEntry[] entries) { _entries = entries ?? Array.Empty<EncyclopediaEntry>(); }

        public IReadOnlyList<EncyclopediaEntry> Entries => _entries;
        public string OutcomeId => "encyclopedia";

        public bool Apply(in DiscoveryClueContext context, DiscoveryClue clue, DiscoveryClueGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                if (context.EncyclopediaProgress == null || context.EncyclopediaProgress.TryUnlock(entry.Id, entry.RevealStage, context.NowUtcTicks)) applied = true;
                DiscoveryClueGrants.AddUnique(grants.EncyclopediaEntryIds, entry.Id);
            }

            return applied;
        }
    }

    /// <summary>월드 상태를 켠다. 켠 원인 이벤트는 단서 id다. 이미 켜진 상태는 기록에만 남는다.</summary>
    public sealed class DiscoveryClueWorldStateRevealOutcome : IDiscoveryClueOutcome
    {
        private readonly WorldStateFlag[] _flags;

        public DiscoveryClueWorldStateRevealOutcome(WorldStateFlag[] flags) { _flags = flags ?? Array.Empty<WorldStateFlag>(); }

        public IReadOnlyList<WorldStateFlag> Flags => _flags;
        public string OutcomeId => "world-state";

        public bool Apply(in DiscoveryClueContext context, DiscoveryClue clue, DiscoveryClueGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (flag == null || string.IsNullOrEmpty(flag.Id)) continue;
                var source = new WorldStateActivationSource(string.Empty, string.Empty, clue != null ? clue.Id : string.Empty, context.CurrentDay);
                if (context.WorldStateProgress == null || context.WorldStateProgress.TryActivate(flag, source)) applied = true;
                DiscoveryClueGrants.AddUnique(grants.WorldStateFlagIds, flag.Id);
            }

            return applied;
        }
    }

    /// <summary>진로 힌트를 연다. 이미 열린 힌트는 기록에만 남는다.</summary>
    public sealed class DiscoveryClueCareerHintOutcome : IDiscoveryClueOutcome
    {
        private readonly string[] _careerIds;

        public DiscoveryClueCareerHintOutcome(string[] careerIds) { _careerIds = careerIds ?? Array.Empty<string>(); }

        public IReadOnlyList<string> CareerIds => _careerIds;
        public string OutcomeId => "career-hint";

        public bool Apply(in DiscoveryClueContext context, DiscoveryClue clue, DiscoveryClueGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _careerIds.Length; i++)
            {
                string careerId = _careerIds[i];
                if (string.IsNullOrEmpty(careerId)) continue;
                if (context.StudentLifeProgress == null || !context.StudentLifeProgress.IsCareerHintUnlocked(careerId)) applied = true;
                context.StudentLifeProgress?.UnlockCareerHint(careerId);
                DiscoveryClueGrants.AddUnique(grants.CareerHintIds, careerId);
            }

            return applied;
        }
    }

    /// <summary>이어지는 퀘스트를 연다. 이미 시작한 퀘스트는 건드리지 않는다(<see cref="FollowUpQuests"/>).</summary>
    public sealed class DiscoveryClueQuestUnlockOutcome : IDiscoveryClueOutcome
    {
        private readonly QuestDefinition[] _quests;

        public DiscoveryClueQuestUnlockOutcome(QuestDefinition[] quests) { _quests = quests ?? Array.Empty<QuestDefinition>(); }

        public IReadOnlyList<QuestDefinition> Quests => _quests;
        public string OutcomeId => "quest";

        public bool Apply(in DiscoveryClueContext context, DiscoveryClue clue, DiscoveryClueGrants grants)
        {
            bool applied = false;
            for (int i = 0; i < _quests.Length; i++)
            {
                var quest = _quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id)) continue;
                DiscoveryClueGrants.AddUnique(grants.QuestIds, quest.Id);
                if (FollowUpQuests.TryStart(context.QuestLog, quest)) applied = true;
            }

            return applied;
        }
    }
}
