using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Minigames
{
    /// <summary>학생의 성장 수치에 따라 도전을 조금 쉽게 만든다. 성장 수치는 바꾸지 않는다.</summary>
    public interface IMinigameBonusRule
    {
        void Apply(StudentLifeProgress progress, ref MinigameTuning tuning);
    }

    public enum MinigameBonusTarget { TimingWindow, TimeLimit, MistakeAllowance, HintCount, Harmony, Steadiness }

    /// <summary>특성·기술 중 높은 값 × 점당 보너스(최대치까지)를 정한 항목에 더한다.</summary>
    public sealed class GrowthMinigameBonusRule : IMinigameBonusRule
    {
        public GrowthMinigameBonusRule(string traitId, string skillId, MinigameBonusTarget target, float amountPerPoint = .01f, float maximumBonus = 1f)
        {
            TraitId = traitId;
            SkillId = skillId;
            Target = target;
            AmountPerPoint = amountPerPoint;
            MaximumBonus = maximumBonus;
        }

        public string TraitId { get; }
        public string SkillId { get; }
        public MinigameBonusTarget Target { get; }
        public float AmountPerPoint { get; }
        public float MaximumBonus { get; }

        public void Apply(StudentLifeProgress progress, ref MinigameTuning tuning)
        {
            if (progress == null) return;
            int points = Math.Max(!string.IsNullOrEmpty(TraitId) ? progress.GetTraitValue(TraitId) : 0, !string.IsNullOrEmpty(SkillId) ? progress.GetSkillValue(SkillId) : 0);
            float amount = MinigameMath.Clamp(points * Math.Max(0, AmountPerPoint), 0, Math.Max(0, MaximumBonus));
            switch (Target)
            {
                case MinigameBonusTarget.TimingWindow: tuning.TimingWindow += amount; break;
                case MinigameBonusTarget.TimeLimit: tuning.ExtraSeconds += amount; break;
                case MinigameBonusTarget.MistakeAllowance: tuning.ExtraMistakes += (int)Math.Floor(amount); break;
                case MinigameBonusTarget.HintCount: tuning.HintCount += (int)Math.Floor(amount); break;
                case MinigameBonusTarget.Harmony: tuning.Harmony += amount; break;
                case MinigameBonusTarget.Steadiness: tuning.Steadiness += amount; break;
            }
        }
    }

    /// <summary>미니게임 하나의 정의: 차례로 하는 도전들, 성장 보너스, 성공 점수(1~100).</summary>
    public sealed class Minigame
    {
        private readonly IChallenge[] _challenges;
        private readonly IMinigameBonusRule[] _bonuses;
        private readonly int _successScore;

        public Minigame(string id, string displayNameKey, IChallenge[] challenges, IMinigameBonusRule[] bonuses, int successScore = 60)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey;
            _challenges = challenges ?? Array.Empty<IChallenge>();
            _bonuses = bonuses ?? Array.Empty<IMinigameBonusRule>();
            _successScore = successScore;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public int SuccessScore => Math.Max(1, Math.Min(100, _successScore));
        public int ChallengeCount => _challenges.Length;
        public IReadOnlyList<IChallenge> Challenges => _challenges;

        public bool IsValid
        {
            get
            {
                if (_challenges.Length == 0) return false;
                foreach (var c in _challenges) if (c == null || !c.IsValid) return false;
                return true;
            }
        }

        internal ChallengeSession Create(int index, StudentLifeProgress progress, int seed)
        {
            var tuning = new MinigameTuning();
            foreach (var b in _bonuses) b?.Apply(progress, ref tuning);
            return _challenges[index].Create(tuning, seed);
        }
    }

    /// <summary>미니게임 한 판. 도전을 차례로 진행하고 평균 점수로 성공을 정한다. 취소하면 점수는 0이다.</summary>
    public sealed class MinigameSession
    {
        private readonly Minigame _definition;
        private readonly StudentLifeProgress _progress;
        private readonly int _seed;
        private int _scoreTotal;

        public MinigameSession(Minigame definition, StudentLifeProgress progress, int seed)
        {
            if (definition == null || !definition.IsValid) throw new ArgumentException("A valid authored minigame is required.");
            _definition = definition;
            _progress = progress;
            _seed = seed;
            Current = definition.Create(0, progress, seed);
        }

        public ChallengeSession Current { get; private set; }
        public int StageIndex { get; private set; }
        public int StageCount => _definition.ChallengeCount;
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public int Score => IsCancelled ? 0 : _scoreTotal / StageCount;
        public bool Succeeded => IsComplete && !IsCancelled && Score >= _definition.SuccessScore;

        public bool AdvanceTime(float seconds) => Input(() => Current.AdvanceTime(seconds));
        public bool Choose(int index) => Input(() => Current.Choose(index));
        public bool Tap() => Input(() => Current.Tap());
        public bool Place(int piece, GridPoint cell, int rotation) => Input(() => Current.Place(piece, cell, rotation));
        public bool Aim(PlanePoint position) => Input(() => Current.Aim(position));
        public bool MoveLane(int lane) => Input(() => Current.MoveLane(lane));

        public bool Cancel()
        {
            if (IsComplete) return false;
            IsCancelled = true;
            IsComplete = true;
            return true;
        }

        private bool Input(Func<bool> action)
        {
            if (IsComplete) return false;
            bool accepted = action();
            if (Current.IsComplete)
            {
                _scoreTotal += Current.Score;
                StageIndex++;
                if (StageIndex >= StageCount) IsComplete = true;
                else Current = _definition.Create(StageIndex, _progress, _seed + StageIndex);
            }
            return accepted;
        }
    }
}
