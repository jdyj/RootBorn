using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 미니게임 알바의 보상 확인과 정산(아직 Core에 없는 알바 영역이 맡는다). 정산은 보상·결과·하루 기록을 한 번에 하고,
    /// 보상이 들어가지 않으면 아무것도 바꾸지 않고 false를 준다.
    /// </summary>
    public interface IPartTimeWorkPayroll
    {
        bool CanReceiveRewards(IMinigamePartTimeWork work);
        bool TryPerformReserved(IMinigamePartTimeWork work, StudentLifeProgress student, string requestId);
    }

    /// <summary>
    /// 학생의 진로 연습·알바를 미니게임 한 판으로 진행한다. 시작할 때 시간·체력·집중을 먼저 쓰고 예약을 남긴다.
    /// 성공하면 성장·진로 힌트·후보 보상을 정산하고, 실패·취소하면 쓴 비용은 돌려주지 않는다. 한 번에 한 판만 예약한다.
    /// 등록한 정의(같은 객체)만 받는다.
    /// </summary>
    public sealed class StudentMinigameService
    {
        private readonly IReadOnlyList<CareerPractice> _practices;
        private readonly IReadOnlyList<IMinigamePartTimeWork> _works;
        private readonly IReadOnlyList<CareerHint> _hints;
        private readonly IReadOnlyList<CareerCandidate> _candidates;

        public StudentMinigameService(IReadOnlyList<CareerPractice> practices, IReadOnlyList<IMinigamePartTimeWork> works, IReadOnlyList<CareerHint> hints, IReadOnlyList<CareerCandidate> candidates)
        {
            _practices = practices ?? Array.Empty<CareerPractice>();
            _works = works ?? Array.Empty<IMinigamePartTimeWork>();
            _hints = hints ?? Array.Empty<CareerHint>();
            _candidates = candidates ?? Array.Empty<CareerCandidate>();
        }

        public AdultCareerResult TryBeginPractice(CareerPractice practice, StudentLifeProgress student, AdultCareerProgress adult, string requestId, int seed)
        {
            var invalid = CheckStart(student, adult, requestId);
            if (!invalid.Applied) return invalid;
            if (!Registered(_practices, practice) || practice.Activity == null || practice.Minigame == null || !practice.Minigame.IsValid) return Result(AdultCareerResultKind.InvalidDefinition);
            var activity = practice.Activity;
            if (!activity.HasSatisfiedRequirements(student)) return Result(AdultCareerResultKind.RequirementFailed);
            if (!CanPay(student, activity.TimePolicy, activity.EnergyCost, activity.FocusCost)) return Result(AdultCareerResultKind.InsufficientTime);
            student.TryUseTimePolicy(activity.TimePolicy);
            student.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            student.ReserveMinigame(Reservation(StudentMinigameKind.Practice, practice.Id, practice.Minigame, student, requestId, seed, activity.TimePolicy.TimePointCost));
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult TryBeginPartTimeWork(IMinigamePartTimeWork work, StudentLifeProgress student, AdultCareerProgress adult, IPartTimeWorkPayroll payroll, string requestId, int seed)
        {
            var invalid = CheckStart(student, adult, requestId);
            if (!invalid.Applied) return invalid;
            if (!Registered(_works, work) || work.Minigame == null || !work.Minigame.IsValid || payroll == null) return Result(AdultCareerResultKind.InvalidDefinition);
            if (!work.HasSatisfiedRequirements(student)) return Result(AdultCareerResultKind.RequirementFailed);
            if (!CanPay(student, work.TimePolicy, work.EnergyCost, work.FocusCost)) return Result(AdultCareerResultKind.InsufficientTime);
            if (!payroll.CanReceiveRewards(work)) return Result(AdultCareerResultKind.InsufficientCapacity);
            student.TryUseTimePolicy(work.TimePolicy);
            student.Spend(work.TimeCostMinutes, work.EnergyCost, work.FocusCost, work.StressDelta);
            student.ReserveMinigame(Reservation(StudentMinigameKind.PartTimeWork, work.Id, work.Minigame, student, requestId, seed, work.TimePolicy.TimePointCost));
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult TrySettlePractice(CareerPractice practice, StudentLifeProgress student, string requestId, bool succeeded)
        {
            if (!Registered(_practices, practice) || practice.Activity == null || !Matches(student, StudentMinigameKind.Practice, practice.Id, practice.Minigame, requestId))
                return Result(AdultCareerResultKind.InvalidRequest);
            if (!succeeded) return Forfeit(student, requestId);
            var activity = practice.Activity;
            ReservedActivitySettlement.Apply(activity, student, requestId);
            // 이번 연습에서 나온 기록만 힌트 출처로 본다. 예전 기록이 다른 연습의 힌트를 가져가지 않게 한다.
            var logs = student.GetTodayResultLogIds();
            var currentLogs = new List<string> { activity.Id };
            for (int i = 0; i < logs.Length; i++)
                if (logs[i].StartsWith(activity.Id + ":", StringComparison.Ordinal)) currentLogs.Add(logs[i]);
            CreditCareer(student, requestId, currentLogs.ToArray());
            student.ClearReservedMinigame(false);
            return Result(AdultCareerResultKind.Applied);
        }

        public AdultCareerResult TrySettlePartTimeWork(IMinigamePartTimeWork work, StudentLifeProgress student, IPartTimeWorkPayroll payroll, string requestId, bool succeeded)
        {
            if (!Registered(_works, work) || !Matches(student, StudentMinigameKind.PartTimeWork, work.Id, work.Minigame, requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (!succeeded) return Forfeit(student, requestId);
            if (payroll == null || !payroll.TryPerformReserved(work, student, requestId)) return Result(AdultCareerResultKind.InsufficientCapacity);
            // 실제 알바도 성공한 연습처럼 진로에 센다.
            CreditCareer(student, requestId, new[] { work.Id });
            // 정산한 요청은 알바 쪽이 표시하지 않았더라도 여기서 표시한다(다시 정산하지 못하게).
            student.ClearReservedMinigame(true);
            return Result(AdultCareerResultKind.Applied);
        }

        /// <summary>예약을 포기한다. 쓴 비용은 돌려주지 않고 요청은 쓴 것으로 남긴다.</summary>
        public AdultCareerResult Forfeit(StudentLifeProgress student, string requestId)
        {
            if (student == null || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (student.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            var pending = student.PendingMinigame;
            if (pending == null || pending.RequestId != requestId) return Result(AdultCareerResultKind.InvalidRequest);
            student.ClearReservedMinigame(true);
            return Result(AdultCareerResultKind.Applied);
        }

        private void CreditCareer(StudentLifeProgress student, string requestId, string[] resultLogs)
        {
            student.CareerCandidates.ApplyFirstMatchingHint(_hints, student, requestId, resultLogs);
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i] != null) student.CareerCandidates.TryClaimRewards(_candidates[i], student);
        }

        private static AdultCareerResult CheckStart(StudentLifeProgress student, AdultCareerProgress adult, string requestId)
        {
            if (student == null || adult == null || student.SaveSlot != adult.SaveSlot || student.PlayerId != adult.PlayerId || string.IsNullOrEmpty(requestId)) return Result(AdultCareerResultKind.InvalidRequest);
            if (student.HasAppliedRequest(requestId)) return Result(AdultCareerResultKind.DuplicateRequest);
            if (adult.IsAdult || adult.HasPendingWork || student.HasPendingMinigame || student.DayState != StudentDayState.InProgress) return Result(AdultCareerResultKind.RequirementFailed);
            return Result(AdultCareerResultKind.Applied);
        }

        private static bool CanPay(StudentLifeProgress student, ActivityTimePolicy policy, int energy, int focus)
            => policy != null && policy.TimePointCost > 0 && student.CanUseTimePolicy(policy) && student.CanSpend(energy, focus);

        private static PendingStudentMinigameSaveData Reservation(StudentMinigameKind kind, string definitionId, Minigame minigame, StudentLifeProgress student, string requestId, int seed, int cost)
            => new PendingStudentMinigameSaveData { Kind = kind, DefinitionId = definitionId, MinigameId = minigame.Id, RequestId = requestId, Day = student.CurrentDay, ReservedTimePoints = cost, Seed = seed };

        private static bool Matches(StudentLifeProgress student, StudentMinigameKind kind, string definitionId, Minigame minigame, string requestId)
        {
            var pending = student?.PendingMinigame;
            return pending != null && minigame != null && pending.Kind == kind && pending.DefinitionId == definitionId && pending.MinigameId == minigame.Id
                && pending.RequestId == requestId && pending.Day == student.CurrentDay && student.TimePointsSpentToday >= pending.ReservedTimePoints && !student.HasAppliedRequest(requestId);
        }

        private static bool Registered<T>(IReadOnlyList<T> definitions, T target) where T : class
        {
            if (target == null) return false;
            for (int i = 0; i < definitions.Count; i++) if (ReferenceEquals(definitions[i], target)) return true;
            return false;
        }

        private static AdultCareerResult Result(AdultCareerResultKind kind) => new AdultCareerResult(kind);
    }

    /// <summary>
    /// 비용을 이미 낸 예약 활동의 효과를 적용하고, 활동 실행기와 같은 모양의 변화 기록("활동:+id=변화량", "활동:unlock=진로")을 남긴다.
    /// 활동 실행기의 기록 만들기와 같은 규칙이다(그쪽이 공개되면 합친다).
    /// </summary>
    internal static class ReservedActivitySettlement
    {
        public static void Apply(LifeActivity activity, StudentLifeProgress progress, string requestId)
        {
            var traits = Snapshot(progress.GetTraitIds(), progress.GetTraitValue);
            var skills = Snapshot(progress.GetSkillIds(), progress.GetSkillValue);
            var relationships = Snapshot(progress.GetRelationshipIds(), progress.GetRelationshipValue);
            var statuses = Snapshot(progress.GetStatusIds(), progress.GetStatusValue);
            var careers = new HashSet<string>(progress.GetCareerHintIds());

            activity.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);

            var logs = new List<string>();
            string prefix = string.IsNullOrEmpty(activity.Id) ? "activity" : activity.Id;
            AddDeltas(logs, prefix, progress.GetTraitIds(), progress.GetTraitValue, traits);
            AddDeltas(logs, prefix, progress.GetSkillIds(), progress.GetSkillValue, skills);
            AddDeltas(logs, prefix, progress.GetRelationshipIds(), progress.GetRelationshipValue, relationships);
            AddDeltas(logs, prefix, progress.GetStatusIds(), progress.GetStatusValue, statuses);
            foreach (string careerId in progress.GetCareerHintIds())
                if (!careers.Contains(careerId)) logs.Add(prefix + ":unlock=" + careerId);
            progress.RecordActivityCompleted(activity.Id, logs.ToArray());
        }

        private static Dictionary<string, int> Snapshot(string[] ids, Func<string, int> getter)
        {
            var values = new Dictionary<string, int>();
            for (int i = 0; i < ids.Length; i++) values[ids[i]] = getter(ids[i]);
            return values;
        }

        private static void AddDeltas(List<string> logs, string prefix, string[] ids, Func<string, int> getter, Dictionary<string, int> before)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                before.TryGetValue(ids[i], out int previous);
                int delta = getter(ids[i]) - previous;
                if (delta != 0) logs.Add(prefix + ":+" + ids[i] + "=" + delta.ToString());
            }
        }
    }
}
