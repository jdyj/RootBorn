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

        public void Apply(StudentLifeProgress progress)
        {
            if (progress == null) return;
            for (int i = 0; i < _rules.Length; i++) _rules[i]?.Apply(progress);
        }
    }

    public sealed class EnergyRestoreRule : IDayEndRule
    {
        public EnergyRestoreRule(int restoredEnergy) { RestoredEnergy = restoredEnergy; }
        public int RestoredEnergy { get; }
        public void Apply(StudentLifeProgress progress) => progress?.RestoreEnergyForDay(RestoredEnergy);
    }

    public sealed class FocusRestoreRule : IDayEndRule
    {
        public FocusRestoreRule(int restoredFocus) { RestoredFocus = restoredFocus; }
        public int RestoredFocus { get; }
        public void Apply(StudentLifeProgress progress) => progress?.RestoreFocusForDay(RestoredFocus);
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
