using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;

namespace Rootborn.Core.Workspaces
{
    /// <summary>공간 점수가 일 정산에 더해 주는 이득. 0 이상만 낸다.</summary>
    public interface IWorkspaceBonus
    {
        int RewardPercent(SpaceScores scores);
        int Reputation(SpaceScores scores);
    }

    internal static class StepBonus
    {
        /// <summary>점수 scorePerStep마다 amountPerStep, 한도 maximum. scorePerStep가 0 이하면 0. 음수는 0으로 다듬는다.</summary>
        public static int Compute(int score, int scorePerStep, int amountPerStep, int maximum)
        {
            if (scorePerStep <= 0 || score <= 0) return 0;
            long value = (long)(score / scorePerStep) * Math.Max(0, amountPerStep);
            return (int)Math.Min(value, Math.Max(0, maximum));
        }
    }

    /// <summary>성질 점수가 보상 비율(%)을 올린다.</summary>
    public sealed class QualityRewardPercentBonus : IWorkspaceBonus
    {
        private readonly string _qualityId;
        private readonly int _scorePerStep;
        private readonly int _percentPerStep;
        private readonly int _maximumPercent;

        public QualityRewardPercentBonus(string qualityId, int scorePerStep, int percentPerStep, int maximumPercent)
        {
            _qualityId = qualityId;
            _scorePerStep = scorePerStep;
            _percentPerStep = percentPerStep;
            _maximumPercent = maximumPercent;
        }

        public int RewardPercent(SpaceScores scores)
            => StepBonus.Compute(scores?.Get(_qualityId) ?? 0, _scorePerStep, _percentPerStep, _maximumPercent);

        public int Reputation(SpaceScores scores) => 0;
    }

    /// <summary>성질 점수가 명성을 올린다.</summary>
    public sealed class QualityReputationBonus : IWorkspaceBonus
    {
        private readonly string _qualityId;
        private readonly int _scorePerStep;
        private readonly int _reputationPerStep;
        private readonly int _maximumReputation;

        public QualityReputationBonus(string qualityId, int scorePerStep, int reputationPerStep, int maximumReputation)
        {
            _qualityId = qualityId;
            _scorePerStep = scorePerStep;
            _reputationPerStep = reputationPerStep;
            _maximumReputation = maximumReputation;
        }

        public int RewardPercent(SpaceScores scores) => 0;

        public int Reputation(SpaceScores scores)
            => StepBonus.Compute(scores?.Get(_qualityId) ?? 0, _scorePerStep, _reputationPerStep, _maximumReputation);
    }

    /// <summary>일 하나에 붙는 공간 이득 묶음.</summary>
    public sealed class WorkBonusBinding
    {
        public WorkBonusBinding(string id, string workId, IWorkspaceBonus[] bonuses)
        {
            Id = id;
            WorkId = workId;
            Bonuses = bonuses ?? Array.Empty<IWorkspaceBonus>();
        }

        public string Id { get; }
        public string WorkId { get; }
        public IReadOnlyList<IWorkspaceBonus> Bonuses { get; }
    }

    /// <summary>일을 하면 오르는 기술.</summary>
    public sealed class WorkSkillGrowth
    {
        public WorkSkillGrowth(string id, string workId, string skillId, int amount)
        {
            Id = id;
            WorkId = workId;
            SkillId = skillId;
            Amount = Math.Max(0, amount);
        }

        public string Id { get; }
        public string WorkId { get; }
        public string SkillId { get; }
        public int Amount { get; }
    }

    /// <summary>일 id별 공간 이득과 기술 성장 표.</summary>
    public sealed class WorkLinks
    {
        private readonly Dictionary<string, List<WorkBonusBinding>> _bonuses = new Dictionary<string, List<WorkBonusBinding>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<WorkSkillGrowth>> _growth = new Dictionary<string, List<WorkSkillGrowth>>(StringComparer.Ordinal);

        public WorkLinks(IEnumerable<WorkBonusBinding> bonuses, IEnumerable<WorkSkillGrowth> growth)
        {
            if (bonuses != null)
                foreach (var b in bonuses)
                {
                    if (b?.WorkId == null) continue;
                    if (!_bonuses.TryGetValue(b.WorkId, out var list)) _bonuses[b.WorkId] = list = new List<WorkBonusBinding>();
                    list.Add(b);
                }
            if (growth != null)
                foreach (var g in growth)
                {
                    if (g?.WorkId == null) continue;
                    if (!_growth.TryGetValue(g.WorkId, out var list)) _growth[g.WorkId] = list = new List<WorkSkillGrowth>();
                    list.Add(g);
                }
        }

        /// <summary>그 일에 붙은 이득을 모두 더한다. 연결이 없으면 default.</summary>
        public WorkExtras ExtrasFor(string workId, SpaceScores scores)
        {
            if (workId == null) return default;
            bool hasBonus = _bonuses.TryGetValue(workId, out var bindings);
            bool hasGrowth = _growth.TryGetValue(workId, out var growth);
            if (!hasBonus && !hasGrowth) return default;

            long reward = 0, reputation = 0;
            if (hasBonus)
                for (int i = 0; i < bindings.Count; i++)
                    for (int j = 0; j < bindings[i].Bonuses.Count; j++)
                    {
                        var bonus = bindings[i].Bonuses[j];
                        if (bonus == null) continue;
                        reward += Math.Max(0, bonus.RewardPercent(scores));
                        reputation += Math.Max(0, bonus.Reputation(scores));
                    }

            SkillGain[] gains = null;
            if (hasGrowth)
            {
                gains = new SkillGain[growth.Count];
                for (int i = 0; i < gains.Length; i++) gains[i] = new SkillGain(growth[i].SkillId, growth[i].Amount);
            }

            return new WorkExtras((int)Math.Min(reward, int.MaxValue), (int)Math.Min(reputation, int.MaxValue), gains);
        }
    }
}
