using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>점수 단계 하나(1단계 보고): 시설 일을 이 점수로 끝낸다고 본다.</summary>
    internal sealed class ScoreTier
    {
        public string Id;
        public int Score;
    }

    /// <summary>전략 하나: 대상 종류(each), 시작 상태(start), 점수 단계와 곱하는지(scored), 차례대로 시도할 행동 종류(do), 보고에 붙일 설명 한 줄(note, 없으면 null).</summary>
    internal sealed class StrategySpec
    {
        public string Id, Each, Start, Note;
        public bool Scored;
        public string[] Do;
    }

    /// <summary>훑을 축의 값 목록(설계 13.1절, money-balance.json "axes"). 축마다 기준 값이 들어 있어야 한다.</summary>
    internal sealed class AxesSpec
    {
        public string[] Sleep, Score, PartTime;
        public int[] TravelMinutes;
        public float[] MinigameShare;
        public int Days;
        public int[] Checkpoints;
        public int FailScore, PassScore;   // fail-every 점수 모델의 실패·성공 점수
    }

    /// <summary>
    /// 균형 시뮬레이터의 전략과 가정(money-balance.json 한 곳). 게임은 읽지 않는다.
    /// 걷기·미니게임 몫의 기준 값은 여기 없고 day-balance.json(<see cref="DayAssumptions"/>)에서 온다.
    /// </summary>
    internal sealed class BalanceConfig
    {
        public int Days;
        public RunSetup BaselineSetup;            // "baseline" + day-balance.json의 걷기·몫
        public AxesSpec Axes;                     // "axes"
        public IReadOnlyList<ScoreTier> Tiers;    // 1단계 보고의 점수 단계
        public IReadOnlyList<StrategySpec> Strategies;
        public int BandCount;   // "bands"의 항목 수. 범위 검사는 아직 없어(3단계) 비어 있어야 한다

        public static string FilePath => Path.Combine(TestWorld.RepoRoot, "game", "tests", "Rootborn.Balance.Tests", "money-balance.json");

        public static BalanceConfig Read()
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(FilePath));
            var root = document.RootElement;
            var tiers = new List<ScoreTier>();
            foreach (var t in root.GetProperty("scoreTiers").EnumerateArray())
                tiers.Add(new ScoreTier { Id = t.GetProperty("id").GetString(), Score = t.GetProperty("score").GetInt32() });
            var strategies = new List<StrategySpec>();
            foreach (var s in root.GetProperty("strategies").EnumerateArray())
            {
                strategies.Add(new StrategySpec
                {
                    Id = s.GetProperty("id").GetString(),
                    Each = s.GetProperty("each").GetString(),
                    Start = s.GetProperty("start").GetString(),
                    Scored = s.GetProperty("scored").GetBoolean(),
                    Do = Strings(s.GetProperty("do")),
                    Note = s.TryGetProperty("note", out var note) ? note.GetString() : null,
                });
            }

            var baseline = root.GetProperty("baseline");
            var axes = root.GetProperty("axes");
            var assumptions = DayAssumptions.Current;
            return new BalanceConfig
            {
                Days = root.GetProperty("days").GetInt32(),
                BaselineSetup = new RunSetup
                {
                    Sleep = baseline.GetProperty("sleep").GetString(),
                    Score = baseline.GetProperty("score").GetString(),
                    PartTime = baseline.GetProperty("partTime").GetString(),
                    TravelMinutes = assumptions.TravelMinutes,
                    MinigameShare = assumptions.MinigameShare,
                },
                Axes = new AxesSpec
                {
                    Sleep = Strings(axes.GetProperty("sleep")),
                    Score = Strings(axes.GetProperty("score")),
                    PartTime = Strings(axes.GetProperty("partTime")),
                    TravelMinutes = axes.GetProperty("travelMinutes").EnumerateArray().Select(e => e.GetInt32()).ToArray(),
                    MinigameShare = axes.GetProperty("minigameShare").EnumerateArray().Select(e => e.GetSingle()).ToArray(),
                    Days = axes.GetProperty("days").GetInt32(),
                    Checkpoints = axes.GetProperty("checkpoints").EnumerateArray().Select(e => e.GetInt32()).ToArray(),
                    FailScore = axes.GetProperty("failScore").GetInt32(),
                    PassScore = axes.GetProperty("passScore").GetInt32(),
                },
                Tiers = tiers,
                Strategies = strategies,
                BandCount = root.TryGetProperty("bands", out var bands) ? bands.GetArrayLength() : 0,
            };
        }

        /// <summary>모르는 이름이 있으면 그 이름(과 전략 id·축 이름)을 말하며 실패한다.</summary>
        public void Validate()
        {
            if (Days <= 0) throw new InvalidOperationException("날 수는 1 이상이다: " + Days);
            if (BaselineSetup == null) throw new InvalidOperationException("기준 가정이 없다(baseline)");
            if (!SleepRules.Knows(BaselineSetup.Sleep)) throw new InvalidOperationException("모르는 잠 규칙이다(baseline.sleep): " + BaselineSetup.Sleep);
            if (!ScoreModels.Knows(BaselineSetup.Score)) throw new InvalidOperationException("모르는 점수 모델이다(baseline.score): " + BaselineSetup.Score);
            if (!SuccessModels.Knows(BaselineSetup.PartTime)) throw new InvalidOperationException("모르는 알바 성공 모델이다(baseline.partTime): " + BaselineSetup.PartTime);
            if (Tiers == null || Tiers.Count == 0) throw new InvalidOperationException("점수 단계가 없다");
            foreach (var tier in Tiers)
                if (tier.Score < 0 || tier.Score > 100) throw new InvalidOperationException("점수 단계는 0~100이다: " + tier.Id + " " + tier.Score);
            if (Strategies == null) throw new InvalidOperationException("전략이 없다");
            if (BandCount > 0) throw new InvalidOperationException("범위 검사는 아직 없다(3단계). bands는 비워 둔다: 항목 " + BandCount + "개");
            ValidateAxes();

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in Strategies)
            {
                if (string.IsNullOrEmpty(s.Id) || !ids.Add(s.Id)) throw new InvalidOperationException("전략 id가 비었거나 겹친다: " + s.Id);
                if (!RunTargets.Knows(s.Each)) throw new InvalidOperationException("전략 " + s.Id + ": 모르는 대상 종류다(each): " + s.Each);
                if (!RunStarts.Knows(s.Start)) throw new InvalidOperationException("전략 " + s.Id + ": 모르는 시작 상태다(start): " + s.Start);
                if (s.Do == null || s.Do.Length == 0) throw new InvalidOperationException("전략 " + s.Id + ": 행동이 없다(do)");
                foreach (var kind in s.Do)
                    if (!BalanceActions.Knows(kind)) throw new InvalidOperationException("전략 " + s.Id + ": 모르는 행동 종류다(do): " + kind);
            }
        }

        // 축마다: 비지 않고, 글자는 읽을 수 있고, 기준 값이 들어 있다. 날 수와 시점은 1 ≤ 시점(오름차순) ≤ 날 수.
        private void ValidateAxes()
        {
            var a = Axes ?? throw new InvalidOperationException("훑을 축이 없다(axes)");
            Axis("sleep", a.Sleep, BaselineSetup.Sleep, SleepRules.Knows);
            Axis("score", a.Score, BaselineSetup.Score, ScoreModels.Knows);
            Axis("partTime", a.PartTime, BaselineSetup.PartTime, SuccessModels.Knows);
            Axis("travelMinutes", a.TravelMinutes, BaselineSetup.TravelMinutes, m => m >= 0);
            Axis("minigameShare", a.MinigameShare, BaselineSetup.MinigameShare, s => s > 0f && !float.IsNaN(s) && !float.IsInfinity(s));
            if (a.FailScore < 0 || a.FailScore > 100 || a.PassScore < 0 || a.PassScore > 100)
                throw new InvalidOperationException("축의 실패·성공 점수는 0~100이다(axes.failScore, axes.passScore): " + a.FailScore + ", " + a.PassScore);
            if (a.Days <= 0) throw new InvalidOperationException("축의 날 수는 1 이상이다(axes.days): " + a.Days);
            if (a.Checkpoints == null || a.Checkpoints.Length == 0) throw new InvalidOperationException("시점이 없다(axes.checkpoints)");
            for (int i = 0; i < a.Checkpoints.Length; i++)
                if (a.Checkpoints[i] < 1 || a.Checkpoints[i] > a.Days || (i > 0 && a.Checkpoints[i] <= a.Checkpoints[i - 1]))
                    throw new InvalidOperationException("시점은 1~날 수 사이의 오름차순이다(axes.checkpoints): " + string.Join(", ", a.Checkpoints));
        }

        private static void Axis<T>(string name, T[] values, T baseline, Func<T, bool> valid)
        {
            if (values == null || values.Length == 0) throw new InvalidOperationException("축이 비었다(axes." + name + ")");
            foreach (var v in values)
                if (!valid(v)) throw new InvalidOperationException("축 axes." + name + "에 읽을 수 없는 값이 있다: " + v);
            if (Array.IndexOf(values, baseline) < 0)
                throw new InvalidOperationException("축 axes." + name + "에 기준 값이 없다: " + baseline);
            if (values.Distinct().Count() != values.Length) throw new InvalidOperationException("축 axes." + name + "에 겹친 값이 있다");
        }

        private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()).ToArray();
    }
}
