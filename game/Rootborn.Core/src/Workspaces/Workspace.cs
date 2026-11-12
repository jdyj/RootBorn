using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Workspaces
{
    /// <summary>꾸미기 규칙 하나. 닿지 않은 단계의 규칙은 0을 준다. 보너스는 음수가 아니다.</summary>
    public interface IDecorationRule
    {
        int CapacityBonus(StudentLifeProgress student);
        int ScorePercentBonus(StudentLifeProgress student);
    }

    /// <summary>기술 단계에 닿으면 놓을 수 있는 가구 수 한도를 늘린다.</summary>
    public sealed class SkillTierCapacityRule : IDecorationRule
    {
        private readonly SkillTier _tier;
        private readonly int _capacityBonus;

        public SkillTierCapacityRule(SkillTier tier, int capacityBonus)
        {
            _tier = tier ?? throw new ArgumentNullException(nameof(tier));
            _capacityBonus = Math.Max(0, capacityBonus);
        }

        public int CapacityBonus(StudentLifeProgress student) => _tier.IsReached(student) ? _capacityBonus : 0;
        public int ScorePercentBonus(StudentLifeProgress student) => 0;
    }

    /// <summary>기술 단계에 닿으면 점수 배율(퍼센트)을 올린다.</summary>
    public sealed class SkillTierScoreRule : IDecorationRule
    {
        private readonly SkillTier _tier;
        private readonly int _scorePercentBonus;

        public SkillTierScoreRule(SkillTier tier, int scorePercentBonus)
        {
            _tier = tier ?? throw new ArgumentNullException(nameof(tier));
            _scorePercentBonus = Math.Max(0, scorePercentBonus);
        }

        public int CapacityBonus(StudentLifeProgress student) => 0;
        public int ScorePercentBonus(StudentLifeProgress student) => _tier.IsReached(student) ? _scorePercentBonus : 0;
    }

    /// <summary>시설 안에서 꾸미는 일터: 놓는 면, 받는 면, 레벨별 가구 한도, 기술 규칙.</summary>
    public sealed class Workspace
    {
        private const int LevelCount = 3;

        private readonly int[] _capacityByLevel;
        private readonly IDecorationRule[] _rules;

        public Workspace(string id, string facilityId, string surfaceId, string[] acceptsSurfaceIds,
            int[] capacityByLevel, int repeatPercent, IDecorationRule[] rules, string defaultTemplateId)
        {
            Id = id ?? string.Empty;
            FacilityId = facilityId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            AcceptsSurfaceIds = acceptsSurfaceIds ?? Array.Empty<string>();
            _capacityByLevel = capacityByLevel ?? Array.Empty<int>();
            RepeatPercent = repeatPercent;
            _rules = rules ?? Array.Empty<IDecorationRule>();
            DefaultTemplateId = defaultTemplateId ?? string.Empty;
        }

        public string Id { get; }
        public string FacilityId { get; }
        public string SurfaceId { get; }
        public string DefaultTemplateId { get; }
        public IReadOnlyList<string> AcceptsSurfaceIds { get; }
        public int RepeatPercent { get; }

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || FacilityId.Length == 0 || SurfaceId.Length == 0) return false;
                if (_capacityByLevel.Length != LevelCount) return false;
                foreach (int c in _capacityByLevel)
                    if (c < 0) return false;
                return RepeatPercent >= 0 && RepeatPercent <= 100;
            }
        }

        /// <summary>레벨 1~3 밖이면 0. 레벨 값에 닿은 규칙의 보너스를 더한다.</summary>
        public int Capacity(int facilityLevel, StudentLifeProgress student)
        {
            if (facilityLevel < 1 || facilityLevel > _capacityByLevel.Length) return 0;
            int total = Math.Max(0, _capacityByLevel[facilityLevel - 1]);
            foreach (var rule in _rules) total += rule.CapacityBonus(student);
            return total;
        }

        /// <summary>100에 닿은 규칙의 보너스를 더한다.</summary>
        public int ScorePercent(StudentLifeProgress student)
        {
            int total = 100;
            foreach (var rule in _rules) total += rule.ScorePercentBonus(student);
            return total;
        }

        public bool CanAdd(int placedCount, int facilityLevel, StudentLifeProgress student)
            => placedCount < Capacity(facilityLevel, student);
    }
}
