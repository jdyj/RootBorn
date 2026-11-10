using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Careers
{
    public enum AdultCareerResultKind
    {
        Applied, InvalidRequest, InvalidDefinition, DuplicateRequest, RequirementFailed, AlreadyAdult, InsufficientTime,
        InsufficientCapacity, InsufficientResources, AtMaximumLevel, PersistenceFailed, AuthorityDenied, RecoveryRequired
    }

    public readonly struct AdultCareerResult
    {
        public AdultCareerResult(AdultCareerResultKind kind) { Kind = kind; }
        public AdultCareerResultKind Kind { get; }
        public bool Applied => Kind == AdultCareerResultKind.Applied;
    }

    /// <summary>
    /// 성인 직업 명령: 시설 고르기, 바로 하는 일, 일 예약·정산·포기, 시설 올리기.
    /// 등록한 시설(같은 객체, id가 겹치지 않는 것)만 받는다. 같은 요청은 한 번만 적용한다.
    /// 거절하면 아무것도 바꾸지 않는다. 인벤토리 교환 안에서 다시 명령을 부르면 거절한다.
    /// </summary>
    public sealed class AdultCareerService
    {
        private readonly Dictionary<string, Facility> _facilities = new Dictionary<string, Facility>(StringComparer.Ordinal);
        private bool _executing;

        public AdultCareerService(Facility[] registeredFacilities)
        {
            if (registeredFacilities == null) return;
            for (int i = 0; i < registeredFacilities.Length; i++)
            {
                var facility = registeredFacilities[i];
                if (facility == null || string.IsNullOrEmpty(facility.Id)) continue;
                // id가 겹치면 어느 쪽도 등록하지 않는다.
                if (_facilities.ContainsKey(facility.Id)) _facilities[facility.Id] = null;
                else _facilities.Add(facility.Id, facility);
            }
        }

        public AdultCareerResult TryTransition(Facility facility, StudentLifeProgress student, CareerCandidateProgress candidates, AdultCareerProgress adult, string requestId)
        {
            if (_executing || !SameIdentity(student, adult) || candidates == null || candidates.SaveSlot != adult.SaveSlot || candidates.PlayerId != adult.PlayerId || string.IsNullOrEmpty(requestId))
                return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (!Registered(facility)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (adult.IsAdult) return Result(AdultCareerResultKind.AlreadyAdult);
            if (student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            if (!facility.CanTransition(new FacilityTransitionContext(facility, student, candidates))) return Result(AdultCareerResultKind.RequirementFailed);
            adult.Select(facility, requestId);
            return Result(AdultCareerResultKind.Applied);
        }

        /// <summary>점수는 믿을 수 있는 끝난 미니게임 판에서 온 값이어야 한다. 여기서는 화면·클라이언트가 보낸 점수를 검증하지 않는다.</summary>
        public AdultCareerResult TryCompleteWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory, string requestId, int score, bool autoSkip)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.HasPendingWork || student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            var facility = FacilityOf(work);
            if (facility == null) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!adult.IsAdult || adult.FacilityId != facility.Id || adult.FacilityLevel < work.MinimumFacilityLevel) return Result(AdultCareerResultKind.RequirementFailed);
            if (score < 0 || score > 100) return Result(AdultCareerResultKind.InvalidRequest);
            if (autoSkip && !work.CanAutoSkip(new AutoSkipContext(work, adult, inventory))) return Result(AdultCareerResultKind.RequirementFailed);
            if (!student.CanSpendTimePoints(work.TimePointCost)) return Result(AdultCareerResultKind.InsufficientTime);
            int effectiveScore = autoSkip ? 100 : score;
            var level = facility.GetLevel(adult.FacilityLevel);
            if (level == null || !work.TryBuildRewards(effectiveScore, level.RewardPercent, out var rewards)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!inventory.CanExchange(null, rewards)) return Result(AdultCareerResultKind.InsufficientCapacity);
            return Exchange(inventory, null, rewards, AdultCareerResultKind.InsufficientCapacity, () =>
            {
                if (!student.TrySpendTimePoints(work.TimePointCost)) throw new InvalidOperationException("Prevalidated facility work budget changed during settlement.");
                student.Spend(work.TimeCostMinutes, 0, 0, 0);
                adult.CompleteWork(work, requestId, effectiveScore >= work.MinimumSuccessScore);
                student.RecordActivityCompleted(work.Id);
            });
        }

        public AdultCareerResult TryUpgrade(Facility facility, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory, ICareerWorldState world, string requestId)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || world == null || world.SaveSlot != adult.SaveSlot || world.PlayerId != adult.PlayerId || string.IsNullOrEmpty(requestId))
                return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.HasPendingWork || student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            if (!Registered(facility)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!adult.IsAdult || adult.FacilityId != facility.Id) return Result(AdultCareerResultKind.RequirementFailed);
            if (adult.FacilityLevel >= 3) return Result(AdultCareerResultKind.AtMaximumLevel);
            var level = facility.GetLevel(adult.FacilityLevel + 1);
            if (level == null || !level.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            var costs = level.BuildCosts();
            if (!inventory.CanExchange(costs, null)) return Result(AdultCareerResultKind.InsufficientResources);
            return Exchange(inventory, costs, null, AdultCareerResultKind.InsufficientResources, () =>
            {
                adult.Upgrade(facility, level, requestId);
                for (int i = 0; i < level.WorldFlagIds.Count; i++) world.TryActivate(level.WorldFlagIds[i], requestId, student.CurrentDay);
            });
        }

        /// <summary>미니게임으로 할 일을 예약한다. 시간 포인트와 시계는 지금 쓴다(정산 때 다시 쓰지 않는다).</summary>
        public AdultCareerResult TryReserveWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, string requestId, int seed)
        {
            if (_executing || !SameIdentity(student, adult) || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.HasPendingWork || student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            var facility = FacilityOf(work);
            if (facility == null || work.Minigame == null || !work.Minigame.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!adult.IsAdult || adult.FacilityId != facility.Id || adult.FacilityLevel < work.MinimumFacilityLevel || student.DayState != StudentDayState.InProgress)
                return Result(AdultCareerResultKind.RequirementFailed);
            var level = facility.GetLevel(adult.FacilityLevel);
            if (level == null || !level.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!student.TrySpendTimePoints(work.TimePointCost)) return Result(AdultCareerResultKind.InsufficientTime);
            student.Spend(work.TimeCostMinutes, 0, 0, 0);
            adult.ReserveWork(work, requestId, student.CurrentDay, seed, level.RewardPercent);
            return Result(AdultCareerResultKind.Applied);
        }

        /// <summary>예약한 일을 끝낸 판의 점수로 정산한다. 보상 비율은 예약할 때의 레벨 것을 쓴다.</summary>
        public AdultCareerResult TrySettleReservedWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory, string requestId, int score)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || string.IsNullOrEmpty(requestId) || score < 0 || score > 100) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            var pending = adult.PendingWork;
            if (pending == null || pending.RequestId != requestId || pending.Day != student.CurrentDay || pending.ReservedTimePoints > student.TimePointsSpentToday)
                return Result(AdultCareerResultKind.InvalidRequest);
            var facility = FacilityOf(work);
            if (facility == null || work.Minigame == null || pending.WorkId != work.Id || pending.MinigameId != work.Minigame.Id || adult.FacilityId != facility.Id || pending.FacilityLevel != adult.FacilityLevel)
                return Result(AdultCareerResultKind.InvalidDefinition);
            if (!work.TryBuildRewards(score, pending.RewardPercent, out var rewards)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!inventory.CanExchange(null, rewards)) return Result(AdultCareerResultKind.InsufficientCapacity);
            return Exchange(inventory, null, rewards, AdultCareerResultKind.InsufficientCapacity, () =>
            {
                adult.CompleteWork(work, requestId, score >= work.MinimumSuccessScore);
                student.RecordActivityCompleted(work.Id);
            });
        }

        /// <summary>예약한 일을 포기한다. 쓴 시간은 돌려주지 않고 요청은 쓴 것으로 남긴다.</summary>
        public AdultCareerResult TryForfeitReservedWork(StudentLifeProgress student, AdultCareerProgress adult, string requestId)
        {
            if (_executing || !SameIdentity(student, adult) || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            var pending = adult.PendingWork;
            if (pending == null || pending.RequestId != requestId) return Result(AdultCareerResultKind.InvalidRequest);
            adult.ForfeitPendingWork();
            return Result(AdultCareerResultKind.Applied);
        }

        private AdultCareerResult Exchange(ICareerInventory inventory, IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards, AdultCareerResultKind failure, Action commit)
        {
            _executing = true;
            try { return Result(inventory.TryExchange(costs, rewards, commit) ? AdultCareerResultKind.Applied : failure); }
            finally { _executing = false; }
        }

        private bool Registered(Facility facility) => facility != null && facility.IsValid && _facilities.TryGetValue(facility.Id, out var registered) && ReferenceEquals(registered, facility);

        /// <summary>올바른 일이고, 그 일이 가리키는 등록 시설에 바로 그 일이 들어 있을 때만 시설을 준다.</summary>
        private Facility FacilityOf(FacilityWork work)
        {
            if (work == null || !work.IsValid || !_facilities.TryGetValue(work.FacilityId, out var facility) || !Registered(facility) || !facility.ContainsWork(work)) return null;
            return facility;
        }

        private static bool SameIdentity(StudentLifeProgress student, AdultCareerProgress adult) => student != null && adult != null && student.SaveSlot == adult.SaveSlot && student.PlayerId == adult.PlayerId;
        private static AdultCareerResult Result(AdultCareerResultKind kind) => new AdultCareerResult(kind);
    }
}
