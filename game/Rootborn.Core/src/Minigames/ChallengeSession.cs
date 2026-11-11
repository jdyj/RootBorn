using System;
using System.Collections.Generic;

namespace Rootborn.Core.Minigames
{
    /// <summary>도전 하나의 정의. 값이 맞는지와, 보너스를 받아 진행 중인 판을 만드는 일을 한다.</summary>
    public interface IChallenge
    {
        bool IsValid { get; }
        ChallengeSession Create(MinigameTuning tuning, int seed);
    }

    /// <summary>도전 정의의 공통 값: 안내 문구 키, 제한 시간, 허용 실수, 기호 그림 주소.</summary>
    public abstract class ChallengeBase : IChallenge
    {
        private readonly string[] _symbolSprites;

        /// <param name="symbolSprites">기호마다의 그림 주소(Unity ChallengeDefinitionBase._symbolSprites). 화면 몫이고 규칙은 쓰지 않는다.</param>
        protected ChallengeBase(string instructionKey, float timeLimitSeconds, int mistakeAllowance, string[] symbolSprites = null)
        {
            InstructionKey = instructionKey;
            TimeLimitSeconds = timeLimitSeconds;
            MistakeAllowance = mistakeAllowance;
            _symbolSprites = symbolSprites == null ? Array.Empty<string>() : (string[])symbolSprites.Clone();
        }

        public string InstructionKey { get; }

        /// <summary>기호마다의 그림 주소(없으면 빈 목록). 기호 순서와 같다.</summary>
        public IReadOnlyList<string> SymbolSprites => _symbolSprites;
        public float TimeLimitSeconds { get; }
        public int MistakeAllowance { get; }

        public virtual bool IsValid => TimeLimitSeconds > 0 && MinigameMath.IsFinite(TimeLimitSeconds) && MistakeAllowance >= 0;

        public abstract ChallengeSession Create(MinigameTuning tuning, int seed);

        protected void Bind(ChallengeSession session, MinigameTuning tuning)
            => session.Initialize(InstructionKey, TimeLimitSeconds + tuning.ExtraSeconds, MistakeAllowance + tuning.ExtraMistakes, tuning.HintCount, _symbolSprites);
    }

    /// <summary>진행 중인 도전 하나. 입력을 받아 점수(0~100)를 낸다. 끝난 뒤의 입력은 거절한다.</summary>
    public abstract class ChallengeSession
    {
        public abstract MinigameMechanic Mechanic { get; }
        public string InstructionKey { get; private set; }

        /// <summary>도전 정의의 기호 그림 주소(Unity ChallengeSession.SymbolSprites).</summary>
        public IReadOnlyList<string> SymbolSprites { get; private set; } = Array.Empty<string>();
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public float RemainingSeconds => Math.Max(0, Duration - Elapsed);
        public int Mistakes { get; protected set; }
        public int MistakeAllowance { get; private set; }
        public int HintCount { get; private set; }
        public bool IsComplete { get; private set; }
        public int Score { get; private set; }
        public virtual float TimingWindow => 0;
        public virtual float[] BeatTimes => Array.Empty<float>();
        public virtual string[] Symbols => Array.Empty<string>();
        public virtual string PromptKey => InstructionKey;
        public virtual int ProgressIndex => 0;
        public virtual int TotalSteps => 1;
        public virtual bool PreviewVisible => false;
        public virtual int[] PreviewSequence => Array.Empty<int>();
        public virtual bool IsHintChoice(int index) => false;
        public virtual GridPoint BoardSize => new GridPoint(0, 0);
        public virtual GridPoint[] PieceSizes => Array.Empty<GridPoint>();
        public virtual GridPoint[] BlockedCells => Array.Empty<GridPoint>();
        public virtual bool TryGetPieceRect(int index, out GridRect rect) { rect = default; return false; }
        public virtual PlanePoint TargetPosition => new PlanePoint(0, 0);
        public virtual bool HasTarget => false;
        public virtual float TargetRadius => 0;
        public virtual int Lane => 0;
        public virtual int LaneCount => 0;
        public virtual int NextObstacleLane => -1;
        public virtual float NextObstacleFraction => 0;

        internal void Initialize(string instruction, float duration, int allowance, int hints, string[] symbolSprites = null)
        {
            InstructionKey = instruction;
            SymbolSprites = symbolSprites ?? Array.Empty<string>();
            Duration = Math.Max(.01f, duration);
            MistakeAllowance = Math.Max(0, allowance);
            HintCount = Math.Max(0, hints);
        }

        public bool AdvanceTime(float seconds)
        {
            if (IsComplete || seconds < 0 || !MinigameMath.IsFinite(seconds)) return false;
            float before = Elapsed;
            Elapsed = Math.Min(Duration, Elapsed + seconds);
            OnAdvanced(before, Elapsed);
            if (!IsComplete && Elapsed >= Duration) FinishTimeout();
            return true;
        }

        protected virtual void OnAdvanced(float before, float after) { }
        protected virtual void FinishTimeout() => Complete(0);

        protected void Complete(int score)
        {
            if (IsComplete) return;
            Score = MinigameMath.Clamp(score, 0, 100);
            IsComplete = true;
        }

        protected bool Wrong()
        {
            Mistakes++;
            if (Mistakes > MistakeAllowance) Complete(0);
            return true;
        }

        protected static int Percent(int correct, int total) => total <= 0 ? 0 : MinigameMath.Clamp(MinigameMath.RoundToInt(100f * correct / total), 0, 100);

        public virtual bool Choose(int index) => false;
        public virtual bool Tap() => false;
        public virtual bool Place(int piece, GridPoint cell, int rotation) => false;
        public virtual bool Aim(PlanePoint position) => false;
        public virtual bool MoveLane(int lane) => false;
    }
}
