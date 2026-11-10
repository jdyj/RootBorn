using System;
using System.Collections.Generic;

namespace Rootborn.Core.Minigames
{
    /// <summary>보여 준 순서를 외워 고른다. 보여 주는 동안의 입력은 거절한다.</summary>
    public sealed class SequenceChallenge : ChallengeBase
    {
        private readonly int[] _answers;
        private readonly string[] _symbols;

        public SequenceChallenge(string instructionKey, int[] answers, string[] symbols, float revealSeconds = 3f, float timeLimitSeconds = 30f, int mistakeAllowance = 3)
            : base(instructionKey, timeLimitSeconds, mistakeAllowance)
        {
            _answers = answers ?? Array.Empty<int>();
            _symbols = symbols ?? Array.Empty<string>();
            RevealSeconds = revealSeconds;
        }

        public float RevealSeconds { get; }

        public override bool IsValid
        {
            get
            {
                if (!base.IsValid || _answers.Length == 0 || _symbols.Length < 2 || RevealSeconds < 0 || RevealSeconds >= TimeLimitSeconds) return false;
                foreach (int a in _answers) if (a < 0 || a >= _symbols.Length) return false;
                return true;
            }
        }

        public override ChallengeSession Create(MinigameTuning tuning, int seed)
        {
            var s = new SequenceSession(_answers, _symbols, RevealSeconds);
            Bind(s, tuning);
            return s;
        }

        private sealed class SequenceSession : ChallengeSession
        {
            private readonly int[] _answers;
            private readonly string[] _symbols;
            private readonly float _reveal;
            private int _index;

            public SequenceSession(int[] answers, string[] symbols, float reveal)
            {
                _answers = (int[])answers.Clone();
                _symbols = (string[])symbols.Clone();
                _reveal = reveal;
            }

            public override MinigameMechanic Mechanic => MinigameMechanic.Sequence;
            public override string[] Symbols => _symbols;
            public override bool PreviewVisible => Elapsed < _reveal;
            public override int[] PreviewSequence => PreviewVisible ? (int[])_answers.Clone() : Array.Empty<int>();
            public override int ProgressIndex => _index;
            public override int TotalSteps => _answers.Length;
            public override bool IsHintChoice(int index) => HintCount > 0 && !PreviewVisible && !IsComplete && index == _answers[_index];

            public override bool Choose(int index)
            {
                if (IsComplete || PreviewVisible || index < 0 || index >= _symbols.Length) return false;
                if (index != _answers[_index]) return Wrong();
                _index++;
                if (_index == _answers.Length) Complete(Percent(_index, _answers.Length + Mistakes));
                return true;
            }
        }
    }

    /// <summary>질문마다 답을 고른다. 틀려도 다음 질문으로 넘어간다.</summary>
    public sealed class ChoiceChallenge : ChallengeBase
    {
        private readonly int[] _answers;
        private readonly string[] _prompts;
        private readonly string[] _symbols;

        public ChoiceChallenge(string instructionKey, int[] answers, string[] prompts, string[] symbols, float timeLimitSeconds = 30f, int mistakeAllowance = 3)
            : base(instructionKey, timeLimitSeconds, mistakeAllowance)
        {
            _answers = answers ?? Array.Empty<int>();
            _prompts = prompts ?? Array.Empty<string>();
            _symbols = symbols ?? Array.Empty<string>();
        }

        public override bool IsValid
        {
            get
            {
                if (!base.IsValid || _answers.Length == 0 || _prompts.Length != _answers.Length || _symbols.Length < 2) return false;
                foreach (int a in _answers) if (a < 0 || a >= _symbols.Length) return false;
                return true;
            }
        }

        public override ChallengeSession Create(MinigameTuning tuning, int seed)
        {
            var s = new ChoiceSession(_answers, _prompts, _symbols);
            Bind(s, tuning);
            return s;
        }

        private sealed class ChoiceSession : ChallengeSession
        {
            private readonly int[] _answers;
            private readonly string[] _prompts, _symbols;
            private int _index, _correct;

            public ChoiceSession(int[] answers, string[] prompts, string[] symbols)
            {
                _answers = (int[])answers.Clone();
                _prompts = (string[])prompts.Clone();
                _symbols = (string[])symbols.Clone();
            }

            public override MinigameMechanic Mechanic => MinigameMechanic.Choice;
            public override string[] Symbols => _symbols;
            public override string PromptKey => _prompts[Math.Min(_index, _prompts.Length - 1)];
            public override int ProgressIndex => _index;
            public override int TotalSteps => _answers.Length;
            public override bool IsHintChoice(int index) => HintCount > 0 && !IsComplete && index == _answers[_index];

            public override bool Choose(int index)
            {
                if (IsComplete || index < 0 || index >= _symbols.Length) return false;
                if (index == _answers[_index]) _correct++;
                else Mistakes++;
                _index++;
                if (_index == _answers.Length) Complete(Percent(_correct, _answers.Length));
                return true;
            }

            protected override void FinishTimeout() => Complete(Percent(_correct, _answers.Length));
        }
    }

    /// <summary>박자에 맞춰 누른다. 박자 하나는 한 번만 인정한다.</summary>
    public sealed class TimingChallenge : ChallengeBase
    {
        private readonly float[] _beatTimes;

        public TimingChallenge(string instructionKey, float[] beatTimes, float windowSeconds = .15f, float timeLimitSeconds = 30f, int mistakeAllowance = 3)
            : base(instructionKey, timeLimitSeconds, mistakeAllowance)
        {
            _beatTimes = beatTimes ?? Array.Empty<float>();
            WindowSeconds = windowSeconds;
        }

        public float WindowSeconds { get; }

        public override bool IsValid
        {
            get
            {
                if (!base.IsValid || _beatTimes.Length == 0 || WindowSeconds <= 0) return false;
                float previous = 0;
                foreach (float b in _beatTimes)
                {
                    if (b <= previous || !MinigameMath.IsFinite(b)) return false;
                    previous = b;
                }
                return previous + WindowSeconds <= TimeLimitSeconds;
            }
        }

        public override ChallengeSession Create(MinigameTuning tuning, int seed)
        {
            var s = new TimingSession(_beatTimes, WindowSeconds + tuning.TimingWindow);
            Bind(s, tuning);
            return s;
        }

        private sealed class TimingSession : ChallengeSession
        {
            private readonly float[] _beats;
            private readonly bool[] _hit;
            private readonly float _window;
            private int _correct;

            public TimingSession(float[] beats, float window)
            {
                _beats = (float[])beats.Clone();
                _hit = new bool[beats.Length];
                _window = window;
            }

            public override MinigameMechanic Mechanic => MinigameMechanic.Timing;
            public override float TimingWindow => _window;
            public override int ProgressIndex => _correct;
            public override int TotalSteps => _beats.Length;
            public override int[] PreviewSequence => Array.Empty<int>();
            public override float[] BeatTimes => (float[])_beats.Clone();

            public override bool Tap()
            {
                if (IsComplete) return false;
                for (int i = 0; i < _beats.Length; i++)
                    if (!_hit[i] && Math.Abs(Elapsed - _beats[i]) <= _window)
                    {
                        _hit[i] = true;
                        _correct++;
                        return true;
                    }
                Mistakes++;
                return false;
            }

            protected override void OnAdvanced(float before, float after)
            {
                if (after >= _beats[_beats.Length - 1] + _window) Complete(Percent(_correct, _beats.Length + Mistakes));
            }

            protected override void FinishTimeout() => Complete(Percent(_correct, _beats.Length + Mistakes));
        }
    }

    /// <summary>조각을 판에 겹치지 않게 놓는다. 막힌 칸과 판 밖은 거절한다. 필요한 칸을 다 덮어야 끝난다.</summary>
    public sealed class PlacementChallenge : ChallengeBase
    {
        private readonly GridPoint[] _pieceSizes;
        private readonly GridPoint[] _blockedCells;
        private readonly GridPoint[] _requiredCells;

        public PlacementChallenge(string instructionKey, GridPoint boardSize, GridPoint[] pieceSizes, GridPoint[] blockedCells, GridPoint[] requiredCells, float timeLimitSeconds = 30f, int mistakeAllowance = 3)
            : base(instructionKey, timeLimitSeconds, mistakeAllowance)
        {
            BoardSize = boardSize;
            _pieceSizes = pieceSizes ?? Array.Empty<GridPoint>();
            _blockedCells = blockedCells ?? Array.Empty<GridPoint>();
            _requiredCells = requiredCells ?? Array.Empty<GridPoint>();
        }

        public GridPoint BoardSize { get; }

        public override bool IsValid
        {
            get
            {
                if (!base.IsValid || BoardSize.X < 1 || BoardSize.Y < 1 || BoardSize.X > 16 || BoardSize.Y > 16 || _pieceSizes.Length == 0) return false;
                foreach (var p in _pieceSizes) if (p.X < 1 || p.Y < 1) return false;
                return true;
            }
        }

        public override ChallengeSession Create(MinigameTuning tuning, int seed)
        {
            var s = new PlacementSession(BoardSize, _pieceSizes, _blockedCells, _requiredCells, tuning.Harmony);
            Bind(s, tuning);
            return s;
        }

        private sealed class PlacementSession : ChallengeSession
        {
            private readonly GridPoint _size;
            private readonly GridPoint[] _pieces, _blocked, _required;
            private readonly Dictionary<int, GridRect> _placed = new Dictionary<int, GridRect>();
            private readonly float _harmony;

            public PlacementSession(GridPoint size, GridPoint[] pieces, GridPoint[] blocked, GridPoint[] required, float harmony)
            {
                _size = size;
                _pieces = (GridPoint[])pieces.Clone();
                _blocked = (GridPoint[])blocked.Clone();
                _required = (GridPoint[])required.Clone();
                _harmony = harmony;
            }

            public override MinigameMechanic Mechanic => MinigameMechanic.Placement;
            public override GridPoint BoardSize => _size;
            public override GridPoint[] PieceSizes => (GridPoint[])_pieces.Clone();
            public override GridPoint[] BlockedCells => (GridPoint[])_blocked.Clone();
            public override int ProgressIndex => _placed.Count;
            public override int TotalSteps => _pieces.Length;
            public override bool TryGetPieceRect(int index, out GridRect rect) => _placed.TryGetValue(index, out rect);

            public override bool Place(int piece, GridPoint cell, int rotation)
            {
                if (IsComplete || piece < 0 || piece >= _pieces.Length || rotation < 0 || rotation > 3) return false;
                var size = _pieces[piece];
                if ((rotation & 1) != 0) size = new GridPoint(size.Y, size.X);
                var rect = new GridRect(cell, size);
                if (rect.XMin < 0 || rect.YMin < 0 || rect.XMax > _size.X || rect.YMax > _size.Y) return false;
                foreach (var b in _blocked) if (rect.Contains(b)) return false;
                foreach (var p in _placed) if (p.Key != piece && rect.Overlaps(p.Value)) return false;
                _placed[piece] = rect;
                if (_placed.Count == _pieces.Length)
                {
                    foreach (var required in _required)
                    {
                        bool covered = false;
                        foreach (var p in _placed) covered |= p.Value.Contains(required);
                        if (!covered) return true;
                    }
                    Complete(100);
                }
                return true;
            }

            protected override void FinishTimeout() => Complete(Math.Min(99, Percent(_placed.Count, _pieces.Length) + MinigameMath.RoundToInt(_harmony)));
        }
    }

    /// <summary>움직이는 과녁을 맞히고, 다가오는 장애물을 차선을 바꿔 피한다.</summary>
    public sealed class TargetDodgeChallenge : ChallengeBase
    {
        private readonly PlanePoint[] _targets;
        private readonly int[] _obstacleLanes;

        public TargetDodgeChallenge(string instructionKey, PlanePoint[] targets, int[] obstacleLanes, float obstacleIntervalSeconds = 2f, float targetRadius = .08f, float targetWobble = .03f, int laneCount = 3, float timeLimitSeconds = 30f, int mistakeAllowance = 3)
            : base(instructionKey, timeLimitSeconds, mistakeAllowance)
        {
            _targets = targets ?? Array.Empty<PlanePoint>();
            _obstacleLanes = obstacleLanes ?? Array.Empty<int>();
            ObstacleIntervalSeconds = obstacleIntervalSeconds;
            TargetRadius = targetRadius;
            TargetWobble = targetWobble;
            LaneCount = laneCount;
        }

        public float ObstacleIntervalSeconds { get; }
        public float TargetRadius { get; }
        public float TargetWobble { get; }
        public int LaneCount { get; }

        public override bool IsValid
        {
            get
            {
                if (!base.IsValid || _targets.Length + _obstacleLanes.Length == 0 || LaneCount < 2 || TargetRadius <= 0 || ObstacleIntervalSeconds <= 0
                    || _obstacleLanes.Length * ObstacleIntervalSeconds > TimeLimitSeconds) return false;
                foreach (int lane in _obstacleLanes) if (lane < 0 || lane >= LaneCount) return false;
                foreach (var p in _targets) if (p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1) return false;
                return true;
            }
        }

        public override ChallengeSession Create(MinigameTuning tuning, int seed)
        {
            var s = new TargetSession(_targets, _obstacleLanes, ObstacleIntervalSeconds, TargetRadius, TargetWobble / (1 + tuning.Steadiness), LaneCount, seed);
            Bind(s, tuning);
            return s;
        }

        private sealed class TargetSession : ChallengeSession
        {
            private readonly PlanePoint[] _targets;
            private readonly int[] _obstacles;
            private readonly float _interval, _radius, _wobble, _phase;
            private readonly int _laneCount;
            private int _targetIndex, _obstacleIndex, _avoided, _lane;

            public TargetSession(PlanePoint[] targets, int[] obstacles, float interval, float radius, float wobble, int laneCount, int seed)
            {
                _targets = (PlanePoint[])targets.Clone();
                _obstacles = (int[])obstacles.Clone();
                _interval = interval;
                _radius = radius;
                _wobble = wobble;
                _laneCount = laneCount;
                _phase = (seed & 255) / 255f;
            }

            public override MinigameMechanic Mechanic => MinigameMechanic.TargetDodge;
            public override bool HasTarget => _targetIndex < _targets.Length;

            public override PlanePoint TargetPosition
            {
                get
                {
                    if (!HasTarget) return new PlanePoint(0, 0);
                    var target = _targets[_targetIndex];
                    return new PlanePoint(target.X + (float)Math.Sin(Elapsed * 2 + _phase) * _wobble, target.Y);
                }
            }

            public override float TargetRadius => _radius;
            public override int Lane => _lane;
            public override int LaneCount => _laneCount;
            public override int NextObstacleLane => _obstacleIndex < _obstacles.Length ? _obstacles[_obstacleIndex] : -1;
            public override float NextObstacleFraction => NextObstacleLane < 0 ? 1 : MinigameMath.Clamp((Elapsed - _obstacleIndex * _interval) / _interval, 0f, 1f);
            public override int ProgressIndex => _targetIndex + _obstacleIndex;
            public override int TotalSteps => _targets.Length + _obstacles.Length;

            public override bool Aim(PlanePoint position)
            {
                if (IsComplete || !HasTarget || float.IsNaN(position.X) || float.IsNaN(position.Y)) return false;
                if (PlanePoint.Distance(position, TargetPosition) > _radius)
                {
                    Wrong();
                    return false;
                }
                _targetIndex++;
                CheckDone();
                return true;
            }

            public override bool MoveLane(int lane)
            {
                if (IsComplete || lane < 0 || lane >= _laneCount) return false;
                _lane = lane;
                return true;
            }

            protected override void OnAdvanced(float before, float after)
            {
                while (!IsComplete && _obstacleIndex < _obstacles.Length && after >= (_obstacleIndex + 1) * _interval)
                {
                    if (_lane == _obstacles[_obstacleIndex]) Wrong();
                    else _avoided++;
                    _obstacleIndex++;
                }
                CheckDone();
            }

            private void CheckDone()
            {
                if (!HasTarget && _obstacleIndex == _obstacles.Length) FinishTimeout();
            }

            protected override void FinishTimeout() => Complete(Percent(_targetIndex + _avoided, TotalSteps + Mistakes));
        }
    }
}
