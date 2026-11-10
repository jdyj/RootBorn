using System;
using System.Collections.Generic;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Tests
{
    // 다른 영역(지식, 스토리 플래그, 월드 상태, 가구 해금)이 Core로 오기 전까지 퀘스트 테스트가 쓰는 메모리 구현.
    internal sealed class FakeStoryFlags : IQuestStoryFlags
    {
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
        public bool IsSet(string flagId) => !string.IsNullOrEmpty(flagId) && _flags.Contains(flagId);
        public bool Set(string flagId) => !string.IsNullOrEmpty(flagId) && _flags.Add(flagId);
    }

    internal sealed class FakeKnowledge : IQuestKnowledge
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>(StringComparer.Ordinal);
        public bool IsUnlocked(string knowledgeId) => !string.IsNullOrEmpty(knowledgeId) && _unlocked.Contains(knowledgeId);
        public void Seed(string knowledgeId) { if (!string.IsNullOrEmpty(knowledgeId)) _unlocked.Add(knowledgeId); }
    }

    internal sealed class FakeFurnitureUnlocks : IQuestFurnitureUnlocks
    {
        private readonly HashSet<string> _tokens = new HashSet<string>(StringComparer.Ordinal);
        public int Count => _tokens.Count;
        public bool IsUnlocked(string unlockToken) => !string.IsNullOrEmpty(unlockToken) && _tokens.Contains(unlockToken);
        public bool TryUnlock(string unlockToken) => !string.IsNullOrEmpty(unlockToken) && _tokens.Add(unlockToken);
    }

    internal sealed class FakeWorldState : IQuestWorldState
    {
        public readonly Dictionary<string, QuestWorldStateSource> Sources = new Dictionary<string, QuestWorldStateSource>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> AppliedVersions = new Dictionary<string, int>(StringComparer.Ordinal);
        public bool IsActive(string flagId) => !string.IsNullOrEmpty(flagId) && Sources.ContainsKey(flagId);
        public bool TryActivate(string flagId, QuestWorldStateSource source)
        {
            if (string.IsNullOrEmpty(flagId) || Sources.ContainsKey(flagId)) return false;
            Sources.Add(flagId, source);
            return true;
        }
        public bool MarkEffectVersionApplied(string flagId, int version)
        {
            if (!Sources.ContainsKey(flagId)) return false;
            AppliedVersions.TryGetValue(flagId, out int applied);
            int normalized = Math.Max(0, version);
            if (applied >= normalized) return false;
            AppliedVersions[flagId] = normalized;
            return true;
        }
    }

    /// <summary>Matches every event, like the test-only objective the Unity tests used.</summary>
    internal sealed class AnyEventObjective : QuestObjective
    {
        public AnyEventObjective(int requiredCount = 1, string displayKey = "objective.any") : base(displayKey, requiredCount) { }
        public override bool Matches(in QuestEvent questEvent) => true;
    }

    /// <summary>A condition the test switches on and off.</summary>
    internal sealed class SwitchCondition : QuestCondition
    {
        private readonly string _reasonId;
        private readonly string _summary;
        public SwitchCondition(string reasonId, string summary, bool isOn = false) { _reasonId = reasonId; _summary = summary; IsOn = isOn; }
        public bool IsOn { get; set; }
        public override string BlockedReasonId => _reasonId;
        public override string BlockedSummary => _summary;
        public override bool IsSatisfied(in QuestRuntimeContext context) => IsOn;
    }
}
