using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;

namespace Rootborn.Game.Careers
{
    public enum AdultCareerResultKind
    { Applied, InvalidRequest, InvalidDefinition, DuplicateRequest, RequirementFailed, AlreadyAdult, InsufficientTime, InsufficientCapacity, InsufficientResources, AtMaximumLevel, PersistenceFailed, AuthorityDenied, RecoveryRequired }
    public readonly struct AdultCareerResult
    {
        public AdultCareerResult(AdultCareerResultKind kind) { Kind = kind; }
        public AdultCareerResultKind Kind { get; }
        public bool Applied => Kind == AdultCareerResultKind.Applied;
    }

    public sealed class AdultCareerService
    {
        private readonly Dictionary<string, FacilityDefinition> _facilities = new Dictionary<string, FacilityDefinition>(StringComparer.Ordinal);
        private bool _executing;
        public AdultCareerService(FacilityDefinition[] registeredFacilities)
        {
            if (registeredFacilities == null) return;
            for (int i = 0; i < registeredFacilities.Length; i++)
            {
                var facility = registeredFacilities[i]; if (facility == null || string.IsNullOrEmpty(facility.Id)) continue;
                if (_facilities.ContainsKey(facility.Id)) _facilities[facility.Id] = null;
                else _facilities.Add(facility.Id, facility);
            }
        }
        public AdultCareerResult TryTransition(FacilityDefinition facility, StudentLifeProgress student, CareerCandidateProgress candidates, AdultCareerProgress adult, string requestId)
        {
            if (_executing || !SameIdentity(student, adult) || candidates == null || candidates.SaveSlot != adult.SaveSlot || candidates.PlayerId != adult.PlayerId || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (!Registered(facility)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (adult.IsAdult) return Result(AdultCareerResultKind.AlreadyAdult);
            if (student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            var context = new FacilityTransitionContext(facility, student, candidates);
            if (!facility.CanTransition(in context)) return Result(AdultCareerResultKind.RequirementFailed);
            adult.Select(facility, requestId); return Result(AdultCareerResultKind.Applied);
        }
        // The caller supplies a score from an authoritative completed minigame session.
        // This domain method does not authenticate UI/client-reported scores.
        public AdultCareerResult TryCompleteWork(FacilityWorkDefinition work, StudentLifeProgress student, AdultCareerProgress adult, Inventory inventory, string requestId, int score, bool autoSkip)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.HasPendingWork || student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            if (work == null || !work.IsValid || !Registered(work.Facility) || !work.Facility.ContainsWork(work)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!adult.IsAdult || adult.FacilityId != work.Facility.Id || adult.FacilityLevel < work.MinimumFacilityLevel) return Result(AdultCareerResultKind.RequirementFailed);
            if (score < 0 || score > 100) return Result(AdultCareerResultKind.InvalidRequest);
            var skipContext = new AutoSkipContext(work, adult, inventory);
            if (autoSkip && !work.CanAutoSkip(in skipContext)) return Result(AdultCareerResultKind.RequirementFailed);
            if (!student.CanSpendTimePoints(work.TimePointCost)) return Result(AdultCareerResultKind.InsufficientTime);
            int effectiveScore = autoSkip ? 100 : score;
            var level = work.Facility.GetLevel(adult.FacilityLevel);
            if (level == null || !work.TryBuildRewards(effectiveScore, level.RewardPercent, out var rewards)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!inventory.CanExchange(null, rewards)) return Result(AdultCareerResultKind.InsufficientCapacity);
            _executing = true;
            try
            {
                bool applied = inventory.TryExchange(null, rewards, () =>
                {
                    if (!student.TrySpendTimePoints(work.TimePointCost)) throw new InvalidOperationException("Prevalidated facility work budget changed during settlement.");
                    student.Spend(work.TimeCostMinutes, 0, 0, 0);
                    adult.CompleteWork(work, requestId, effectiveScore >= work.MinimumSuccessScore);
                    student.RecordActivityCompleted(work.Id);
                });
                return Result(applied ? AdultCareerResultKind.Applied : AdultCareerResultKind.InsufficientCapacity);
            }
            finally { _executing = false; }
        }
        public AdultCareerResult TryUpgrade(FacilityDefinition facility, StudentLifeProgress student, AdultCareerProgress adult, Inventory inventory, WorldStateProgress world, string requestId)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || world == null || world.SaveSlot != adult.SaveSlot || world.PlayerId != adult.PlayerId || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.HasPendingWork || student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            if (!Registered(facility)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!adult.IsAdult || adult.FacilityId != facility.Id) return Result(AdultCareerResultKind.RequirementFailed);
            if (adult.FacilityLevel >= 3) return Result(AdultCareerResultKind.AtMaximumLevel);
            var level = facility.GetLevel(adult.FacilityLevel + 1);
            if (level == null || !level.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            var costs = level.BuildCosts();
            if (!inventory.CanExchange(costs, null)) return Result(AdultCareerResultKind.InsufficientResources);
            _executing = true;
            try
            {
                bool applied = inventory.TryExchange(costs, null, () =>
                {
                    adult.Upgrade(facility, level, requestId);
                    var source = new WorldStateActivationSource(string.Empty, string.Empty, requestId, student.CurrentDay);
                    for (int i = 0; i < level.WorldFlags.Count; i++) world.TryActivate(level.WorldFlags[i], source);
                });
                return Result(applied ? AdultCareerResultKind.Applied : AdultCareerResultKind.InsufficientResources);
            }
            finally { _executing = false; }
        }
        public AdultCareerResult TryReserveWork(FacilityWorkDefinition work, StudentLifeProgress student, AdultCareerProgress adult, string requestId, int seed)
        {
            if (_executing || !SameIdentity(student, adult) || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.HasPendingWork || student.HasPendingMinigame) return Result(AdultCareerResultKind.RequirementFailed);
            if (work == null || !work.IsValid || !Registered(work.Facility) || !work.Facility.ContainsWork(work) || work.Minigame == null || !work.Minigame.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!adult.IsAdult || adult.FacilityId != work.Facility.Id || adult.FacilityLevel < work.MinimumFacilityLevel || student.DayState != StudentDayState.InProgress) return Result(AdultCareerResultKind.RequirementFailed);
            var level = work.Facility.GetLevel(adult.FacilityLevel);
            if (level == null || !level.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!student.TrySpendTimePoints(work.TimePointCost)) return Result(AdultCareerResultKind.InsufficientTime);
            student.Spend(work.TimeCostMinutes, 0, 0, 0);
            adult.ReserveWork(work, requestId, student.CurrentDay, seed, level.RewardPercent);
            return Result(AdultCareerResultKind.Applied);
        }
        // Runtime validates the exact owner-held completed minigame session before calling this settlement.
        public AdultCareerResult TrySettleReservedWork(FacilityWorkDefinition work, StudentLifeProgress student, AdultCareerProgress adult, Inventory inventory, string requestId, int score)
        {
            if (_executing || !SameIdentity(student, adult) || inventory == null || string.IsNullOrEmpty(requestId) || score < 0 || score > 100) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            var pending = adult.PendingWork;
            if (pending == null || pending.RequestId != requestId || pending.Day != student.CurrentDay || pending.ReservedTimePoints > student.TimePointsSpentToday) return Result(AdultCareerResultKind.InvalidRequest);
            if (work == null || !work.IsValid || !Registered(work.Facility) || !work.Facility.ContainsWork(work) || work.Minigame == null || pending.WorkId != work.Id || pending.MinigameId != work.Minigame.Id || adult.FacilityId != work.Facility.Id || pending.FacilityLevel != adult.FacilityLevel) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!work.TryBuildRewards(score, pending.RewardPercent, out var rewards)) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!inventory.CanExchange(null, rewards)) return Result(AdultCareerResultKind.InsufficientCapacity);
            _executing = true;
            try
            {
                bool applied = inventory.TryExchange(null, rewards, () => { adult.CompleteWork(work, requestId, score >= work.MinimumSuccessScore); student.RecordActivityCompleted(work.Id); });
                return Result(applied ? AdultCareerResultKind.Applied : AdultCareerResultKind.InsufficientCapacity);
            }
            finally { _executing = false; }
        }
        public AdultCareerResult TryForfeitReservedWork(StudentLifeProgress student, AdultCareerProgress adult, string requestId)
        {
            if (_executing || !SameIdentity(student, adult) || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (adult.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.PendingWork == null || adult.PendingWork.RequestId != requestId) return Result(AdultCareerResultKind.InvalidRequest);
            adult.ForfeitPendingWork(); return Result(AdultCareerResultKind.Applied);
        }
        private bool Registered(FacilityDefinition facility) => facility != null && facility.IsValid && _facilities.TryGetValue(facility.Id, out var registered) && ReferenceEquals(registered, facility);
        private static bool SameIdentity(StudentLifeProgress student, AdultCareerProgress adult) => student != null && adult != null && student.SaveSlot == adult.SaveSlot && student.PlayerId == adult.PlayerId;
        private static AdultCareerResult Result(AdultCareerResultKind kind) => new AdultCareerResult(kind);
    }
}
