using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public interface IDayEndRule
    {
        void Apply(StudentLifeProgress progress);
    }

    /// <summary>하루를 끝낼 때 차례로 적용하는 규칙 묶음과 다음 날 시작 지점.</summary>
    public sealed class DayEndRuleSet
    {
        private readonly IDayEndRule[] _rules;

        public DayEndRuleSet(string id, string nextDayEntryPointId, IDayEndRule[] rules)
        {
            Id = id ?? string.Empty;
            NextDayEntryPointId = string.IsNullOrEmpty(nextDayEntryPointId) ? string.Empty : nextDayEntryPointId;
            _rules = rules ?? Array.Empty<IDayEndRule>();
        }

        public string Id { get; }
        public string NextDayEntryPointId { get; }
        public IReadOnlyList<IDayEndRule> Rules => _rules;

        /// <summary>하루를 끝낼 때 채워지는 집중(규칙이 여럿이면 마지막 것). 없으면 0.</summary>
        public int DailyFocus
        {
            get
            {
                int focus = 0;
                for (int i = 0; i < _rules.Length; i++) if (_rules[i] is IDailyFocusRule rule) focus = Math.Max(0, rule.DailyFocus);
                return focus;
            }
        }

        public void Apply(StudentLifeProgress progress)
        {
            if (progress == null) return;
            for (int i = 0; i < _rules.Length; i++) _rules[i]?.Apply(progress);
        }
    }

    /// <summary>
    /// 잠으로 체력을 돌린다. 잔 시각(<see cref="StudentLifeProgress.LastNight"/>)이 입력이다: 취침 시각까지 자면 전부(옛 동작과 같다),
    /// 늦게 자면 시간 규칙(<see cref="Time.TimeRules.SleepRecovery"/>)만큼 덜 돌아오되 자서 체력이 줄지는 않는다. 자지 않고 날이 넘어갔으면 돌리지 않는다.
    /// 시간 규칙이 없으면 늘 전부 돌린다.
    /// </summary>
    public sealed class EnergyRestoreRule : IDayEndRule
    {
        public EnergyRestoreRule(int restoredEnergy) { RestoredEnergy = restoredEnergy; }
        public int RestoredEnergy { get; }

        public void Apply(StudentLifeProgress progress)
        {
            if (progress == null || !progress.LastNight.HasSlept) return;
            var rules = progress.TimeRules;
            int target = rules != null && progress.LastNight.SleptAtMinutes >= 0 ? rules.SleepRecovery(RestoredEnergy, progress.LastNight.SleptAtMinutes) : RestoredEnergy;
            progress.RestoreEnergyForDay(target >= RestoredEnergy ? RestoredEnergy : Math.Max(progress.Energy, target));
        }
    }

    /// <summary>하루 집중을 정하는 규칙이 밝히는 값.</summary>
    public interface IDailyFocusRule
    {
        int DailyFocus { get; }
    }

    public sealed class FocusRestoreRule : IDayEndRule, IDailyFocusRule
    {
        public FocusRestoreRule(int restoredFocus) { RestoredFocus = restoredFocus; }
        public int RestoredFocus { get; }
        public int DailyFocus => RestoredFocus;
        /// <summary>잤으면 돌린다(잔 시각과 무관). 자지 않고 날이 넘어갔으면 돌리지 않는다.</summary>
        public void Apply(StudentLifeProgress progress)
        {
            if (progress == null || !progress.LastNight.HasSlept) return;
            progress.RestoreFocusForDay(RestoredFocus);
        }
    }

    /// <summary>지정한 단계에 있을 때만(비어 있으면 언제나) 안내 단계를 다음으로 옮긴다.</summary>
    public sealed class TutorialStageAdvanceRule : IDayEndRule
    {
        public TutorialStageAdvanceRule(string fromStageId, TutorialStage targetStage, string nextObjectiveId)
        {
            FromStageId = string.IsNullOrEmpty(fromStageId) ? string.Empty : fromStageId;
            TargetStage = targetStage;
            NextObjectiveId = string.IsNullOrEmpty(nextObjectiveId) ? string.Empty : nextObjectiveId;
        }

        public string FromStageId { get; }
        public TutorialStage TargetStage { get; }
        public string NextObjectiveId { get; }

        public void Apply(StudentLifeProgress progress)
        {
            if (progress == null || TargetStage == null) return;
            if (FromStageId.Length > 0 && progress.TutorialStageId != FromStageId) return;
            progress.SetTutorialStage(TargetStage, NextObjectiveId);
        }
    }
}
