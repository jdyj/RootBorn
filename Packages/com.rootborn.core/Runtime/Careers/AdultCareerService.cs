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
            var check = CanTransition(facility, student, candidates, adult);
            if (check != AdultCareerResultKind.Applied) return Result(check);
            adult.Select(facility, requestId);
            return Result(AdultCareerResultKind.Applied);
        }

        /// <summary>점수는 믿을 수 있는 끝난 미니게임 판에서 온 값이어야 한다. 여기서는 화면·클라이언트가 보낸 점수를 검증하지 않는다.</summary>
        public AdultCareerResult TryCompleteWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory, string requestId, int score, bool autoSkip)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            ForfeitWorkLeftFromEarlierDays(student, adult);
            var check = CheckCompleteWork(work, student, adult, inventory, score, autoSkip, out var rewards);
            if (check != AdultCareerResultKind.Applied) return Result(check);
            int effectiveScore = autoSkip ? 100 : score;
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
            var check = CheckUpgrade(facility, student, adult, inventory, out var level, out var costs);
            if (check != AdultCareerResultKind.Applied) return Result(check);
            return Exchange(inventory, costs, null, AdultCareerResultKind.InsufficientResources, () =>
            {
                adult.Upgrade(facility, level, requestId);
                for (int i = 0; i < level.WorldFlagIds.Count; i++) world.TryActivate(level.WorldFlagIds[i], requestId, student.CurrentDay);
            });
        }

        /// <summary>
        /// 미니게임으로 할 일을 예약한다. 시계는 건너뛰지 않고 끝나는 시각만 정한다 — 정산·포기 때 미니게임 동안 흐른 시간을 뺀 나머지만 쓴다
        /// (흐르는 시간 설계 3.2절).
        /// </summary>
        public AdultCareerResult TryReserveWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, string requestId, int seed)
        {
            if (_executing || !SameIdentity(student, adult) || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            ForfeitWorkLeftFromEarlierDays(student, adult);
            var check = CheckReserveWork(work, student, adult, out var level);
            if (check != AdultCareerResultKind.Applied) return Result(check);
            if (!student.TrySpendTimePoints(work.TimePointCost)) return Result(AdultCareerResultKind.InsufficientTime);
            student.ReserveActivityTime(work.TimeCostMinutes);
            adult.ReserveWork(work, requestId, student.CurrentDay, seed, level.RewardPercent);
            return Result(AdultCareerResultKind.Applied);
        }

        /// <summary>예약한 일을 끝낸 판의 점수로 정산한다. 보상 비율은 예약할 때의 레벨 것을 쓴다.</summary>
        public AdultCareerResult TrySettleReservedWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory, string requestId, int score)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || string.IsNullOrEmpty(requestId) || score < 0 || score > 100) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            var pending = adult.PendingWork;
            if (pending == null || pending.RequestId != requestId || pending.Day != student.CurrentDay)
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
                student.FinishReservedActivityTime();
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
            if (pending.Day == student.CurrentDay) student.FinishReservedActivityTime();
            return Result(AdultCareerResultKind.Applied);
        }

        /// <summary>
        /// 앞선 날에 예약하고 정산하지 않은 일을 포기 처리한다(쓴 시간은 돌려주지 않고 요청은 쓴 것으로 남긴다).
        /// 다음 날을 시작할 때 부른다. 부르지 않았더라도 새 일을 예약할 때 같은 처리를 한다. Unity 판은 예약이 남으면 날을 넘기지 못하게 막았다.
        /// </summary>
        public bool ForfeitWorkLeftFromEarlierDays(StudentLifeProgress student, AdultCareerProgress adult)
        {
            if (_executing || !SameIdentity(student, adult)) return false;
            var pending = adult.PendingWork;
            if (pending == null || pending.Day >= student.CurrentDay) return false;
            adult.ForfeitPendingWork();
            return true;
        }

        // ---------- 할 수 있는지(명령과 같은 판정, 아무것도 바꾸지 않는다) ----------
        // 돌려주는 값이 Applied면 할 수 있다. 그 밖은 명령이 지금 상태에서 돌려줄 거절 이유다(요청 id·중복 검사는 빼고 본다).
        // 앞선 날에 예약하고 정산하지 않은 일은 명령이 먼저 포기 처리하므로 여기서도 없는 것으로 본다.

        /// <summary>이 시설을 성인 진로로 고를 수 있는지(Unity AdultCareerRuntimeComponent.CanTransition. 하루가 끝난 뒤에는 막는다).</summary>
        public AdultCareerResultKind CanTransition(Facility facility, StudentLifeProgress student, CareerCandidateProgress candidates, AdultCareerProgress adult)
        {
            if (_executing || !SameIdentity(student, adult) || candidates == null || candidates.SaveSlot != adult.SaveSlot || candidates.PlayerId != adult.PlayerId)
                return AdultCareerResultKind.InvalidRequest;
            if (!Registered(facility)) return AdultCareerResultKind.InvalidDefinition;
            if (adult.IsAdult) return AdultCareerResultKind.AlreadyAdult;
            // 하루가 끝난 뒤(결과 화면)에는 시설을 고를 수 없다(Unity 판은 됐다).
            if (student.HasPendingMinigame || student.DayState != StudentDayState.InProgress) return AdultCareerResultKind.RequirementFailed;
            if (!facility.CanTransition(new FacilityTransitionContext(facility, student, candidates))) return AdultCareerResultKind.RequirementFailed;
            return AdultCareerResultKind.Applied;
        }

        /// <summary>
        /// 이 일을 직접 할 수 있는지(Unity CanWork). 미니게임이 있는 일은 예약(TryReserveWork)의 판정, 없는 일은 바로 하기(TryCompleteWork, 만점 보상 기준)의 판정이다.
        /// </summary>
        public AdultCareerResultKind CanWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory)
        {
            if (_executing || !SameIdentity(student, adult)) return AdultCareerResultKind.InvalidRequest;
            if (work != null && work.Minigame != null)
            {
                var check = CheckReserveWork(work, student, adult, out _);
                if (check != AdultCareerResultKind.Applied) return check;
                return student.CanSpendTimePoints(work.TimePointCost) ? AdultCareerResultKind.Applied : AdultCareerResultKind.InsufficientTime;
            }

            if (inventory == null) return AdultCareerResultKind.InvalidRequest;
            return CheckCompleteWork(work, student, adult, inventory, 100, false, out _);
        }

        /// <summary>이 일을 자동 완료할 수 있는지(Unity CanAutoSkip: 자동 완료 조건 + 만점 보상이 가방에 들어가는지).</summary>
        public AdultCareerResultKind CanAutoSkip(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null) return AdultCareerResultKind.InvalidRequest;
            return CheckCompleteWork(work, student, adult, inventory, 100, true, out _);
        }

        /// <summary>지금 시설을 한 단계 올릴 수 있는지(Unity CanUpgrade). 월드 상태는 보지 않는다(올릴 때 깃발을 켜기만 한다).</summary>
        public AdultCareerResultKind CanUpgrade(Facility facility, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null) return AdultCareerResultKind.InvalidRequest;
            return CheckUpgrade(facility, student, adult, inventory, out _, out _);
        }

        // 명령은 앞선 날의 예약을 먼저 포기 처리한 뒤 이 판정을 부른다. 조회는 포기 처리를 하지 않으므로 오늘 이후 예약만 막는다.
        private static bool HasPendingWorkFromToday(StudentLifeProgress student, AdultCareerProgress adult)
            => adult.PendingWork != null && adult.PendingWork.Day >= student.CurrentDay;

        private AdultCareerResultKind CheckCompleteWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory, int score, bool autoSkip,
            out IReadOnlyList<ItemAmount<string>> rewards)
        {
            rewards = null;
            if (HasPendingWorkFromToday(student, adult) || student.HasPendingMinigame) return AdultCareerResultKind.RequirementFailed;
            var facility = FacilityOf(work);
            if (facility == null) return AdultCareerResultKind.InvalidDefinition;
            if (!adult.IsAdult || adult.FacilityId != facility.Id || adult.FacilityLevel < work.MinimumFacilityLevel) return AdultCareerResultKind.RequirementFailed;
            if (score < 0 || score > 100) return AdultCareerResultKind.InvalidRequest;
            if (autoSkip && !work.CanAutoSkip(new AutoSkipContext(work, adult, inventory))) return AdultCareerResultKind.RequirementFailed;
            if (!student.CanSpendTimePoints(work.TimePointCost)) return AdultCareerResultKind.InsufficientTime;
            int effectiveScore = autoSkip ? 100 : score;
            var level = facility.GetLevel(adult.FacilityLevel);
            if (level == null || !work.TryBuildRewards(effectiveScore, level.RewardPercent, out var built)) return AdultCareerResultKind.InvalidDefinition;
            if (!inventory.CanExchange(null, built)) return AdultCareerResultKind.InsufficientCapacity;
            rewards = built;
            return AdultCareerResultKind.Applied;
        }

        private AdultCareerResultKind CheckReserveWork(FacilityWork work, StudentLifeProgress student, AdultCareerProgress adult, out FacilityLevel level)
        {
            level = null;
            if (HasPendingWorkFromToday(student, adult) || student.HasPendingMinigame) return AdultCareerResultKind.RequirementFailed;
            var facility = FacilityOf(work);
            if (facility == null || work.Minigame == null || !work.Minigame.IsValid) return AdultCareerResultKind.InvalidDefinition;
            if (!adult.IsAdult || adult.FacilityId != facility.Id || adult.FacilityLevel < work.MinimumFacilityLevel || student.DayState != StudentDayState.InProgress)
                return AdultCareerResultKind.RequirementFailed;
            level = facility.GetLevel(adult.FacilityLevel);
            if (level == null || !level.IsValid) return AdultCareerResultKind.InvalidDefinition;
            return AdultCareerResultKind.Applied;
        }

        private AdultCareerResultKind CheckUpgrade(Facility facility, StudentLifeProgress student, AdultCareerProgress adult, ICareerInventory inventory,
            out FacilityLevel level, out IReadOnlyList<ItemAmount<string>> costs)
        {
            level = null;
            costs = null;
            if (adult.HasPendingWork || student.HasPendingMinigame) return AdultCareerResultKind.RequirementFailed;
            if (!Registered(facility)) return AdultCareerResultKind.InvalidDefinition;
            if (!adult.IsAdult || adult.FacilityId != facility.Id) return AdultCareerResultKind.RequirementFailed;
            if (adult.FacilityLevel >= 3) return AdultCareerResultKind.AtMaximumLevel;
            level = facility.GetLevel(adult.FacilityLevel + 1);
            if (level == null || !level.IsValid) return AdultCareerResultKind.InvalidDefinition;
            costs = level.BuildCosts();
            if (!inventory.CanExchange(costs, null)) return AdultCareerResultKind.InsufficientResources;
            return AdultCareerResultKind.Applied;
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
