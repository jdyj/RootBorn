using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Careers
{
    /// <summary>
    /// 조건이 무엇을 증명하는지 나타내는 이름. 시설·일은 정해진 이름들이 모두 덮여야 열린다.
    /// 새 조건 종류는 이 중 하나를 대거나(null이면 아무것도 덮지 않는다) 클래스 하나로 더하면 된다.
    /// </summary>
    public static class CareerCoverage
    {
        public const string CareerHint = "career-hint";
        public const string CandidateReady = "candidate-ready";
        public const string Mastery = "mastery";
        public const string SuccessfulWork = "successful-work";
        public const string FacilityEquipment = "facility-equipment";

        internal static bool CoversAll(HashSet<string> covered, string[] required)
        {
            for (int i = 0; i < required.Length; i++) if (!covered.Contains(required[i])) return false;
            return true;
        }
    }

    public readonly struct FacilityTransitionContext
    {
        public FacilityTransitionContext(Facility facility, StudentLifeProgress student, CareerCandidateProgress candidates)
        {
            Facility = facility;
            Student = student;
            Candidates = candidates;
        }

        public Facility Facility { get; }
        public StudentLifeProgress Student { get; }
        public CareerCandidateProgress Candidates { get; }
    }

    public interface IFacilityTransitionRequirement
    {
        /// <summary><see cref="CareerCoverage"/>의 이름 또는 null.</summary>
        string Coverage { get; }
        bool IsSatisfied(FacilityTransitionContext context);
    }

    /// <summary>
    /// 성인이 되어 맡는 시설. 레벨 1~3이 하나씩 있고 레벨 3이 길을 끝낸다.
    /// 들어가려면 모든 전환 조건을 만족하고, 진로 힌트와 후보 준비 조건이 둘 다 있어야 한다.
    /// </summary>
    public sealed class Facility
    {
        private static readonly string[] RequiredTransitionCoverage = { CareerCoverage.CareerHint, CareerCoverage.CandidateReady };
        private readonly IFacilityTransitionRequirement[] _transitionRequirements;
        private readonly FacilityLevel[] _levels;
        private readonly FacilityWork[] _works;

        public Facility(string id, string displayNameKey, string locationId, string practiceId, string careerId, CareerCandidate candidate,
            IFacilityTransitionRequirement[] transitionRequirements, FacilityLevel[] levels, FacilityWork[] works)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            LocationId = locationId ?? string.Empty;
            PracticeId = practiceId ?? string.Empty;
            CareerId = careerId ?? string.Empty;
            Candidate = candidate;
            _transitionRequirements = transitionRequirements ?? Array.Empty<IFacilityTransitionRequirement>();
            _levels = levels ?? Array.Empty<FacilityLevel>();
            _works = works ?? Array.Empty<FacilityWork>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string LocationId { get; }
        public string PracticeId { get; }
        public string CareerId { get; }
        public CareerCandidate Candidate { get; }
        public IReadOnlyList<FacilityLevel> Levels => _levels;
        public IReadOnlyList<FacilityWork> Works => _works;

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || CareerId.Length == 0 || Candidate == null || _levels.Length != 3) return false;
                for (int level = 1; level <= 3; level++)
                {
                    int matches = 0;
                    for (int i = 0; i < _levels.Length; i++) if (_levels[i] != null && _levels[i].IsValid && _levels[i].Level == level) matches++;
                    if (matches != 1) return false;
                }
                return GetLevel(3).CompletesRoute;
            }
        }

        public FacilityLevel GetLevel(int level)
        {
            for (int i = 0; i < _levels.Length; i++) if (_levels[i] != null && _levels[i].Level == level) return _levels[i];
            return null;
        }

        public bool ContainsWork(FacilityWork work)
        {
            for (int i = 0; i < _works.Length; i++) if (ReferenceEquals(_works[i], work)) return true;
            return false;
        }

        /// <summary>빈 조건이 끼어 있으면 들어갈 수 없다.</summary>
        public bool CanTransition(FacilityTransitionContext context)
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _transitionRequirements.Length; i++)
            {
                var rule = _transitionRequirements[i];
                if (rule == null || !rule.IsSatisfied(context)) return false;
                if (!string.IsNullOrEmpty(rule.Coverage)) covered.Add(rule.Coverage);
            }
            return CareerCoverage.CoversAll(covered, RequiredTransitionCoverage);
        }
    }

    /// <summary>시설 레벨. 올릴 때의 비용, 켜는 세계 깃발, 일 보상 비율(100 = 100%), 길을 끝내는지.</summary>
    public sealed class FacilityLevel
    {
        private readonly ItemAmount<string>[] _costs;
        private readonly string[] _worldFlagIds;

        public FacilityLevel(string id, string displayNameKey, int level, ItemAmount<string>[] costs, string[] worldFlagIds, int rewardPercent = 100, bool completesRoute = false)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            Level = level;
            _costs = costs ?? Array.Empty<ItemAmount<string>>();
            _worldFlagIds = worldFlagIds ?? Array.Empty<string>();
            RewardPercent = rewardPercent;
            CompletesRoute = completesRoute;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public int Level { get; }
        public int RewardPercent { get; }
        public bool CompletesRoute { get; }
        public IReadOnlyList<string> WorldFlagIds => _worldFlagIds;

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || Level < 1 || Level > 3 || RewardPercent < 0 || RewardPercent > 10000 || (CompletesRoute && Level != 3)) return false;
                for (int i = 0; i < _costs.Length; i++) if (string.IsNullOrEmpty(_costs[i].Item) || _costs[i].Count <= 0) return false;
                for (int i = 0; i < _worldFlagIds.Length; i++) if (string.IsNullOrEmpty(_worldFlagIds[i])) return false;
                return true;
            }
        }

        /// <summary>올바르지 않은 레벨은 비용을 내지 않는다. 같은 아이템 비용은 인벤토리가 합쳐서 본다.</summary>
        public ItemAmount<string>[] BuildCosts() => IsValid ? (ItemAmount<string>[])_costs.Clone() : Array.Empty<ItemAmount<string>>();
    }

    public readonly struct AutoSkipContext
    {
        public AutoSkipContext(FacilityWork work, AdultCareerProgress progress, ICareerInventory inventory)
        {
            Work = work;
            Progress = progress;
            Inventory = inventory;
        }

        public FacilityWork Work { get; }
        public AdultCareerProgress Progress { get; }
        public ICareerInventory Inventory { get; }
    }

    public interface IAutoSkipRequirement
    {
        /// <summary><see cref="CareerCoverage"/>의 이름 또는 null.</summary>
        string Coverage { get; }
        bool IsSatisfied(AutoSkipContext context);
    }

    /// <summary>
    /// 시설에서 하는 일. 점수(0~100) × 레벨 보상 비율로 최대 보상을 나눠 준다. 최소 성공 점수 아래면 보상이 없다.
    /// 건너뛰기는 숙련·성공 횟수·시설 장비 조건이 모두 있어야 한다.
    /// </summary>
    public sealed class FacilityWork
    {
        private static readonly string[] RequiredAutoSkipCoverage = { CareerCoverage.Mastery, CareerCoverage.SuccessfulWork, CareerCoverage.FacilityEquipment };
        private readonly ItemAmount<string>[] _maximumRewards;
        private readonly IAutoSkipRequirement[] _autoSkipRequirements;
        private readonly int _timeCostMinutes;

        public FacilityWork(string id, string displayNameKey, string facilityId, Minigame minigame, ItemAmount<string>[] maximumRewards, IAutoSkipRequirement[] autoSkipRequirements,
            int minimumFacilityLevel = 1, int timePointCost = 1, int timeCostMinutes = 60, int maximumMastery = 100, int masteryPerSuccess = 1, int minimumSuccessScore = 1, int reputationPerSuccess = 1)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            FacilityId = facilityId ?? string.Empty;
            Minigame = minigame;
            _maximumRewards = maximumRewards ?? Array.Empty<ItemAmount<string>>();
            _autoSkipRequirements = autoSkipRequirements ?? Array.Empty<IAutoSkipRequirement>();
            MinimumFacilityLevel = minimumFacilityLevel;
            TimePointCost = timePointCost;
            _timeCostMinutes = timeCostMinutes;
            MaximumMastery = maximumMastery;
            MasteryPerSuccess = masteryPerSuccess;
            MinimumSuccessScore = minimumSuccessScore;
            ReputationPerSuccess = reputationPerSuccess;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string FacilityId { get; }
        public Minigame Minigame { get; }
        public int MinimumFacilityLevel { get; }
        /// <summary>오늘 한 큰 활동 수에 더하는 몫(옛 이름 시간 포인트 비용, 데이터 timePointCost, 1 이상). 막는 데는 쓰지 않는다 — 시간은 <see cref="TimeCostMinutes"/>.</summary>
        public int TimePointCost { get; }

        /// <summary>일이 하루 시계를 움직이는 분. 학생 활동의 시간 비용과 같다.</summary>
        public int TimeCostMinutes => Math.Max(0, _timeCostMinutes);
        public int MaximumMastery { get; }
        public int MasteryPerSuccess { get; }
        public int MinimumSuccessScore { get; }
        public int ReputationPerSuccess { get; }

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || FacilityId.Length == 0 || MinimumFacilityLevel < 1 || MinimumFacilityLevel > 3 || TimePointCost <= 0 || MaximumMastery <= 0
                    || MasteryPerSuccess <= 0 || MinimumSuccessScore < 1 || MinimumSuccessScore > 100 || ReputationPerSuccess < 0) return false;
                for (int i = 0; i < _maximumRewards.Length; i++) if (string.IsNullOrEmpty(_maximumRewards[i].Item) || _maximumRewards[i].Count <= 0) return false;
                return true;
            }
        }

        public bool CanAutoSkip(AutoSkipContext context)
        {
            var covered = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _autoSkipRequirements.Length; i++)
            {
                var requirement = _autoSkipRequirements[i];
                if (requirement == null || !requirement.IsSatisfied(context)) return false;
                if (!string.IsNullOrEmpty(requirement.Coverage)) covered.Add(requirement.Coverage);
            }
            return CareerCoverage.CoversAll(covered, RequiredAutoSkipCoverage);
        }

        public bool TryBuildRewards(int score, int levelRewardPercent, out ItemAmount<string>[] rewards)
        {
            rewards = Array.Empty<ItemAmount<string>>();
            if (!IsValid || score < 0 || score > 100 || levelRewardPercent < 0 || levelRewardPercent > 10000) return false;
            if (score < MinimumSuccessScore) return true;
            var values = new List<ItemAmount<string>>(_maximumRewards.Length);
            for (int i = 0; i < _maximumRewards.Length; i++)
            {
                long count = (long)_maximumRewards[i].Count * score * levelRewardPercent / 10000L;
                if (count > int.MaxValue) return false;
                if (count > 0) values.Add(new ItemAmount<string>(_maximumRewards[i].Item, (int)count));
            }
            rewards = values.ToArray();
            return true;
        }
    }

    public sealed class CareerHintTransitionRequirement : IFacilityTransitionRequirement
    {
        public string Coverage => CareerCoverage.CareerHint;
        public bool IsSatisfied(FacilityTransitionContext context)
            => context.Student != null && context.Facility != null && context.Facility.CareerId.Length > 0 && context.Student.IsCareerHintUnlocked(context.Facility.CareerId);
    }

    /// <summary>시설의 진로 후보가 선택 준비 상태이고 후보 조건을 만족해야 한다.</summary>
    public sealed class CandidateReadyTransitionRequirement : IFacilityTransitionRequirement
    {
        public string Coverage => CareerCoverage.CandidateReady;
        public bool IsSatisfied(FacilityTransitionContext context)
        {
            var candidate = context.Facility != null ? context.Facility.Candidate : null;
            return candidate != null && context.Student != null && context.Candidates != null
                && candidate.GetState(context.Candidates) == CareerCandidateState.ReadyForChoice && candidate.AreRequirementsSatisfied(context.Candidates, context.Student);
        }
    }

    public sealed class MinimumDayTransitionRequirement : IFacilityTransitionRequirement
    {
        public MinimumDayTransitionRequirement(int minimumDay = 1) { MinimumDay = minimumDay; }
        public int MinimumDay { get; }
        public string Coverage => null;
        public bool IsSatisfied(FacilityTransitionContext context) => context.Student != null && context.Student.CurrentDay >= Math.Max(1, MinimumDay);
    }

    public sealed class MasteryAutoSkipRequirement : IAutoSkipRequirement
    {
        public string Coverage => CareerCoverage.Mastery;
        public bool IsSatisfied(AutoSkipContext context) => context.Work != null && context.Progress != null && context.Progress.GetMastery(context.Work) >= context.Work.MaximumMastery;
    }

    public sealed class SuccessCountAutoSkipRequirement : IAutoSkipRequirement
    {
        public SuccessCountAutoSkipRequirement(int minimumSuccesses = 3) { MinimumSuccesses = minimumSuccesses; }
        public int MinimumSuccesses { get; }
        public string Coverage => CareerCoverage.SuccessfulWork;
        public bool IsSatisfied(AutoSkipContext context) => context.Work != null && context.Progress != null && context.Progress.GetSuccessfulCompletions(context.Work) >= Math.Max(1, MinimumSuccesses);
    }

    /// <summary>시설 레벨이 충분하고, 장비를 정했으면 그 장비를 가지고 있어야 한다.</summary>
    public sealed class FacilityEquipmentAutoSkipRequirement : IAutoSkipRequirement
    {
        public FacilityEquipmentAutoSkipRequirement(int minimumLevel = 1, string equipmentItemId = null) { MinimumLevel = minimumLevel; EquipmentItemId = equipmentItemId ?? string.Empty; }
        public int MinimumLevel { get; }
        public string EquipmentItemId { get; }
        public string Coverage => CareerCoverage.FacilityEquipment;
        public bool IsSatisfied(AutoSkipContext context)
            => context.Progress != null && context.Progress.FacilityLevel >= Math.Max(1, MinimumLevel)
               && (EquipmentItemId.Length == 0 || (context.Inventory != null && context.Inventory.CountOf(EquipmentItemId) > 0));
    }
}
