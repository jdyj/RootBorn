using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.StudentLife
{
    /// <summary>다른 정의를 id와 표시 이름 키로 가리킨다. 표시 이름 키가 비면 id를 쓴다.</summary>
    public readonly struct DefinitionRef
    {
        public DefinitionRef(string id, string displayNameKey = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
    }

    public interface ICareerUnlockRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    /// <summary>진로 하나. 해금 조건을 모두 만족하면 해금된 것으로 본다.</summary>
    public sealed class Career
    {
        private readonly ICareerUnlockRequirement[] _requirements;

        public Career(string id, string displayNameKey, ICareerUnlockRequirement[] requirements)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            _requirements = requirements ?? Array.Empty<ICareerUnlockRequirement>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public IReadOnlyList<ICareerUnlockRequirement> Requirements => _requirements;

        public bool IsUnlocked(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress)) return false;
            }
            return true;
        }
    }

    public sealed class TraitThresholdCareerRequirement : ICareerUnlockRequirement
    {
        public TraitThresholdCareerRequirement(string traitId, int minimumValue) { TraitId = traitId; MinimumValue = minimumValue; }
        public string TraitId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && progress.GetTraitValue(TraitId) >= Math.Max(0, MinimumValue);
    }

    public sealed class SkillThresholdCareerRequirement : ICareerUnlockRequirement
    {
        public SkillThresholdCareerRequirement(string skillId, int minimumValue) { SkillId = skillId; MinimumValue = minimumValue; }
        public string SkillId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && progress.GetSkillValue(SkillId) >= Math.Max(0, MinimumValue);
    }

    public sealed class PracticeStep
    {
        public PracticeStep(string id, string displayNameKey = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
    }

    /// <summary>진로 연습. 생활 활동 하나를 실행한다. 미니게임이 있으면 예약·정산 경로로만 진행한다.</summary>
    public sealed class CareerPractice
    {
        private readonly PracticeStep[] _steps;

        public CareerPractice(string id, string displayNameKey, LifeActivity activity, string careerHintId, PracticeStep[] steps, Minigame minigame = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            Activity = activity;
            CareerHintId = careerHintId ?? string.Empty;
            _steps = steps ?? Array.Empty<PracticeStep>();
            Minigame = minigame;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public LifeActivity Activity { get; }
        public string CareerHintId { get; }
        public Minigame Minigame { get; }
        public IReadOnlyList<PracticeStep> Steps => _steps;
    }

    /// <summary>미니게임이 없는 진로 연습을 바로 실행한다. 미니게임이 있는 연습은 거절한다.</summary>
    public sealed class CareerPracticeRunner
    {
        private readonly LifeActivityRunner _activityRunner = new LifeActivityRunner();

        public bool TryPerform(CareerPractice practice, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
        {
            if (practice == null || practice.Minigame != null)
            {
                string saveSlot = progress == null ? "default" : progress.SaveSlot;
                string playerId = progress == null ? "player" : progress.PlayerId;
                result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, string.Empty, requestId);
                return false;
            }
            return _activityRunner.TryPerform(practice.Activity, progress, requestId, out result);
        }
    }
}
