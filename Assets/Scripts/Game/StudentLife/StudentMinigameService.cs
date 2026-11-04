using System;
using System.Collections.Generic;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;

namespace Rootborn.Game.StudentLife
{
    public sealed class StudentMinigameService
    {
        private readonly GameDataRegistry _registry;
        public StudentMinigameService(GameDataRegistry registry) => _registry = registry;

        public AdultCareerResult TryBeginPractice(CareerPracticeDefinition practice, StudentLifeProgress student, AdultCareerProgress adult, string requestId, int seed)
        {
            var invalid = CheckStart(student, adult, requestId);
            if (!invalid.Applied) return invalid;
            if (_registry == null || !Registered(_registry.CareerPractices, practice) || practice.Activity == null || practice.Minigame == null || !practice.Minigame.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            var activity = practice.Activity;
            if (!activity.HasSatisfiedRequirements(student)) return Result(AdultCareerResultKind.RequirementFailed);
            if (!CanPay(student, activity.TimePolicy, activity.EnergyCost, activity.FocusCost)) return Result(AdultCareerResultKind.InsufficientTime);
            student.TryUseTimePolicy(activity.TimePolicy);
            student.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            student.ReserveMinigame(Reservation(StudentMinigameKind.Practice, practice.Id, practice.Minigame, student, requestId, seed, activity.TimePolicy.TimePointCost));
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult TryBeginPartTimeWork(PartTimeWorkDefinition work, StudentLifeProgress student, AdultCareerProgress adult, Inventory inventory, string requestId, int seed)
        {
            var invalid = CheckStart(student, adult, requestId);
            if (!invalid.Applied) return invalid;
            if (_registry == null || !Registered(_registry.PartTimeWorks, work) || work.Minigame == null || !work.Minigame.IsValid || inventory == null) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!work.HasSatisfiedRequirements(student)) return Result(AdultCareerResultKind.RequirementFailed);
            if (!CanPay(student, work.TimePolicy, work.EnergyCost, work.FocusCost)) return Result(AdultCareerResultKind.InsufficientTime);
            if (!inventory.CanExchange(null, work.Rewards)) return Result(AdultCareerResultKind.InsufficientCapacity);
            student.TryUseTimePolicy(work.TimePolicy);
            student.Spend(work.TimeCostMinutes, work.EnergyCost, work.FocusCost, work.StressDelta);
            student.ReserveMinigame(Reservation(StudentMinigameKind.PartTimeWork, work.Id, work.Minigame, student, requestId, seed, work.TimePolicy.TimePointCost));
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult TrySettlePractice(CareerPracticeDefinition practice, StudentLifeProgress student, string requestId, bool succeeded)
        {
            if (_registry == null || !Registered(_registry.CareerPractices, practice) || practice.Activity == null || !Matches(student, StudentMinigameKind.Practice, practice.Id, practice.Minigame, requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (!succeeded) return Forfeit(student, requestId);
            if (!new LifeActivityRunner().TryPerformReserved(practice.Activity, student, requestId, out _)) return Result(AdultCareerResultKind.InvalidRequest);
            var logs = student.GetTodayResultLogIds();
            var currentLogs = new List<string> { practice.Activity.Id };
            for (int i = 0; i < logs.Length; i++)
                if (logs[i].StartsWith(practice.Activity.Id + ":", StringComparison.Ordinal)) currentLogs.Add(logs[i]);
            student.CareerCandidates.ApplyFirstMatchingHint(_registry.CareerHints, student, requestId, currentLogs.ToArray());
            foreach (var candidate in _registry.CareerCandidates)
                if (candidate != null) student.CareerCandidates.TryClaimRewards(candidate, student);
            student.ClearReservedMinigame(false);
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult TrySettlePartTimeWork(PartTimeWorkDefinition work, StudentLifeProgress student, Inventory inventory, string requestId, bool succeeded)
        {
            if (_registry == null || !Registered(_registry.PartTimeWorks, work) || !Matches(student, StudentMinigameKind.PartTimeWork, work.Id, work.Minigame, requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (!succeeded) return Forfeit(student, requestId);
            if (!new PartTimeWorkRunner().TryPerformReserved(work, student, inventory, requestId, out _)) return Result(AdultCareerResultKind.InsufficientCapacity);
            student.ClearReservedMinigame(false);
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult Forfeit(StudentLifeProgress student, string requestId)
        {
            if (student == null || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (student.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            var pending = student.PendingMinigame;
            if (pending == null || pending.RequestId != requestId) return Result(AdultCareerResultKind.InvalidRequest);
            student.ClearReservedMinigame(true); return Result(AdultCareerResultKind.Applied);
        }

        private static AdultCareerResult CheckStart(StudentLifeProgress student, AdultCareerProgress adult, string requestId)
        {
            if (student == null || adult == null || student.SaveSlot != adult.SaveSlot || student.PlayerId != adult.PlayerId || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (student.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.IsAdult || adult.HasPendingWork || student.HasPendingMinigame || student.DayState != StudentDayState.InProgress) return Result(AdultCareerResultKind.RequirementFailed);
            return Result(AdultCareerResultKind.Applied);
        }
        private static bool CanPay(StudentLifeProgress student, ActivityTimePolicyDefinition policy, int energy, int focus)
            => policy != null && policy.TimePointCost > 0 && student.CanUseTimePolicy(policy) && student.CanSpend(energy, focus);
        private static PendingStudentMinigameSaveData Reservation(StudentMinigameKind kind, string definitionId, MinigameDefinition minigame, StudentLifeProgress student, string requestId, int seed, int cost)
            => new PendingStudentMinigameSaveData { Kind = kind, DefinitionId = definitionId, MinigameId = minigame.Id, RequestId = requestId, Day = student.CurrentDay, ReservedTimePoints = cost, Seed = seed };
        private static bool Matches(StudentLifeProgress student, StudentMinigameKind kind, string definitionId, MinigameDefinition minigame, string requestId)
        {
            var pending = student?.PendingMinigame;
            return pending != null && minigame != null && pending.Kind == kind && pending.DefinitionId == definitionId && pending.MinigameId == minigame.Id
                && pending.RequestId == requestId && pending.Day == student.CurrentDay && student.TimePointsSpentToday >= pending.ReservedTimePoints && !student.HasAppliedRequest(requestId);
        }
        private static bool Registered<T>(T[] definitions, T target) where T : UnityEngine.Object
        {
            if (target == null || definitions == null) return false;
            for (int i = 0; i < definitions.Length; i++) if (ReferenceEquals(definitions[i], target)) return true;
            return false;
        }
        private static AdultCareerResult Result(AdultCareerResultKind kind) => new AdultCareerResult(kind);
    }
}
