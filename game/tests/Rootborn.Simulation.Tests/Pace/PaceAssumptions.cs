using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rootborn.Simulation.Pace
{
    /// <summary>글 읽는 속도(축 값 하나). 분당 글자 수가 0이면 읽지 않고 넘긴다.</summary>
    public readonly struct PaceReading
    {
        public PaceReading(string name, double charsPerMinute) { Name = name; CharsPerMinute = charsPerMinute; }
        public string Name { get; }
        public double CharsPerMinute { get; }
    }

    /// <summary>한 번 고르는 데 드는 초(축 값 하나).</summary>
    public readonly struct PaceChoice
    {
        public PaceChoice(string name, double seconds) { Name = name; Seconds = seconds; }
        public string Name { get; }
        public double Seconds { get; }
    }

    /// <summary>사람 쪽 가정 한 벌: 읽기 속도, 고르기 시간, 미니게임에 쓰는 제한 시간의 몫.</summary>
    public readonly struct PaceAxis
    {
        public PaceAxis(PaceReading reading, PaceChoice choice, double minigameShare)
        {
            Reading = reading; Choice = choice; MinigameShare = minigameShare;
        }

        public PaceReading Reading { get; }
        public PaceChoice Choice { get; }
        public double MinigameShare { get; }

        public double ReadingSeconds(PaceStep s) => Reading.CharsPerMinute > 0 ? s.NewChars * 60.0 / Reading.CharsPerMinute : 0;
        public double ChoiceSeconds(PaceStep s) => s.Choices * Choice.Seconds;

        public string Label => "읽기 " + Reading.Name + " · 고르기 " + Choice.Name + " · 미니게임 " + MinigameShare.ToString("0.##", CultureInfo.InvariantCulture);
        public bool SameAs(PaceAxis o) => Reading.Name == o.Reading.Name && Choice.Name == o.Choice.Name && Math.Abs(MinigameShare - o.MinigameShare) < 1e-9;
    }

    /// <summary>
    /// 사람 쪽 시간의 가정 값(축). 모두 가정이다 — 사용자가 정한 값이 아니다(설계 2026-11-13-play-pace-simulation-design.md 9절 1번:
    /// 기준은 읽기 보통 분당 400자, 고르기 2초, 미니게임 몫 0.5. 기준은 표를 읽기 쉽게 하는 표시일 뿐이다).
    /// 다른 축 값(느림 250자, 빠름 700자, 고르기 1초·5초)은 이 세션이 대신 정한 것이다. 값을 바꾸려면 이 한 곳만 고친다.
    /// </summary>
    public static class PaceAssumptions
    {
        public static readonly IReadOnlyList<PaceReading> Reading = new[]
        {
            new PaceReading("느림", 250), new PaceReading("보통", 400), new PaceReading("빠름", 700), new PaceReading("넘김", 0),
        };

        public static readonly IReadOnlyList<PaceChoice> Choice = new[]
        {
            new PaceChoice("짧게", 1), new PaceChoice("보통", 2), new PaceChoice("오래", 5),
        };

        /// <summary>미니게임: 자동 풀이가 쓴 시간 대신 도전의 제한 시간 중 이만큼(균형 시뮬레이터와 같은 축).</summary>
        public static readonly IReadOnlyList<double> MinigameShares = new[] { 0.25, 0.5, 1.0 };

        public static PaceAxis Baseline => new PaceAxis(Reading[1], Choice[1], MinigameShares[1]);

        /// <summary>가장 빠른 사람(넘김·짧게·0.25)과 가장 느린 사람(느림·오래·1.0) — 범위의 양 끝.</summary>
        public static PaceAxis Fastest => new PaceAxis(Reading[3], Choice[0], MinigameShares[0]);
        public static PaceAxis Slowest => new PaceAxis(Reading[0], Choice[2], MinigameShares[2]);

        /// <summary>축 값의 모든 조합.</summary>
        public static List<PaceAxis> All()
        {
            var all = new List<PaceAxis>(Reading.Count * Choice.Count * MinigameShares.Count);
            foreach (PaceReading r in Reading)
                foreach (PaceChoice c in Choice)
                    foreach (double m in MinigameShares)
                        all.Add(new PaceAxis(r, c, m));
            return all;
        }

        /// <summary>기준에서 축 하나만 바꾼 것들(보고서의 "축 값마다 한 줄").</summary>
        public static List<PaceAxis> OneAtATime()
        {
            PaceAxis b = Baseline;
            var list = new List<PaceAxis>();
            foreach (PaceReading r in Reading) list.Add(new PaceAxis(r, b.Choice, b.MinigameShare));
            foreach (PaceChoice c in Choice) if (c.Name != b.Choice.Name) list.Add(new PaceAxis(b.Reading, c, b.MinigameShare));
            foreach (double m in MinigameShares) if (Math.Abs(m - b.MinigameShare) > 1e-9) list.Add(new PaceAxis(b.Reading, b.Choice, m));
            return list;
        }
    }

    /// <summary>걸음마다 실제 시간(초)을 어림한다: 게임이 돈 시간 + 사람이 읽고 고르는 시간, 미니게임은 도전마다 풀이 시간과 제한 시간 몫 중 큰 쪽.</summary>
    public static class PaceTime
    {
        public static double[] RealSeconds(IReadOnlyList<PaceStep> steps, PaceAxis axis, double secondsPerFrame)
        {
            var real = new double[steps.Count];
            var solved = new Dictionary<int, double>();
            var limit = new Dictionary<int, double>();
            var last = new Dictionary<int, int>();
            for (int i = 0; i < steps.Count; i++)
            {
                PaceStep s = steps[i];
                double game = s.GameSeconds(secondsPerFrame);
                real[i] = game + axis.ReadingSeconds(s) + axis.ChoiceSeconds(s);
                if (s.Challenge < 0) continue;
                solved[s.Challenge] = (solved.TryGetValue(s.Challenge, out double g) ? g : 0) + game;
                limit[s.Challenge] = (limit.TryGetValue(s.Challenge, out double l) ? l : 0) + s.MinigameLimitSeconds;
                last[s.Challenge] = i;
            }
            foreach (var pair in last)
            {
                double want = limit[pair.Key] * axis.MinigameShare;
                double extra = want - solved[pair.Key];
                if (extra > 0) real[pair.Value] += extra;
            }
            return real;
        }
    }
}
