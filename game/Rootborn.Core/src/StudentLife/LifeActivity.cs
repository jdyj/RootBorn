using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public interface ILifeActivityRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    public interface ILifeActivityEffect
    {
        void Apply(StudentLifeProgress progress);
    }

    public sealed class LifeChoice
    {
        private readonly ILifeActivityEffect[] _effects;

        public LifeChoice(string id, ILifeActivityEffect[] effects)
        {
            Id = id ?? string.Empty;
            _effects = effects ?? Array.Empty<ILifeActivityEffect>();
        }

        public string Id { get; }
        public IReadOnlyList<ILifeActivityEffect> Effects => _effects;

        public void ApplyEffects(StudentLifeProgress progress)
        {
            for (int i = 0; i < _effects.Length; i++) _effects[i]?.Apply(progress);
        }
    }

    /// <summary>생활 활동 하나의 정의. 비용, 요구 조건, 효과, 선택지를 값으로 가진다.</summary>
    public sealed class LifeActivity
    {
        private readonly ILifeActivityRequirement[] _requirements;
        private readonly ILifeActivityEffect[] _effects;
        private readonly LifeChoice[] _choices;

        public LifeActivity(string id, LifeActivityCategory category, ActivityTimePolicy timePolicy, int timeCostMinutes, int energyCost, int focusCost, int stressDelta,
            ILifeActivityRequirement[] requirements, ILifeActivityEffect[] effects, LifeChoice[] choices)
        {
            Id = id ?? string.Empty;
            Category = category;
            TimePolicy = timePolicy;
            TimeCostMinutes = Math.Max(0, timeCostMinutes);
            EnergyCost = Math.Max(0, energyCost);
            FocusCost = Math.Max(0, focusCost);
            StressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<ILifeActivityRequirement>();
            _effects = effects ?? Array.Empty<ILifeActivityEffect>();
            _choices = choices ?? Array.Empty<LifeChoice>();
        }

        public string Id { get; }
        public LifeActivityCategory Category { get; }
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public IReadOnlyList<LifeChoice> Choices => _choices;

        public LifeChoice GetChoice(int index) => index >= 0 && index < _choices.Length ? _choices[index] : null;

        public bool HasSatisfiedRequirements(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                ILifeActivityRequirement requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress)) return false;
            }
            return true;
        }

        public void ApplyEffects(StudentLifeProgress progress)
        {
            for (int i = 0; i < _effects.Length; i++) _effects[i]?.Apply(progress);
        }
    }
}
