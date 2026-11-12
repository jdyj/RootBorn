using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>점수 단계 하나: 시설 일을 이 점수로 끝낸다고 본다.</summary>
    internal sealed class ScoreTier
    {
        public string Id;
        public int Score;
    }

    /// <summary>전략 하나: 대상 종류(each), 시작 상태(start), 점수 단계와 곱하는지(scored), 차례대로 시도할 행동 종류(do).</summary>
    internal sealed class StrategySpec
    {
        public string Id, Each, Start;
        public bool Scored;
        public string[] Do;
    }

    /// <summary>
    /// 균형 시뮬레이터의 전략과 가정(money-balance.json 한 곳). 게임은 읽지 않는다.
    /// 걷기·미니게임 가정은 여기 없고 day-balance.json(<see cref="DayAssumptions"/>)에서 온다.
    /// </summary>
    internal sealed class BalanceConfig
    {
        public int Days;
        public string Sleep;
        public IReadOnlyList<ScoreTier> Tiers;
        public IReadOnlyList<StrategySpec> Strategies;

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
                var steps = new List<string>();
                foreach (var d in s.GetProperty("do").EnumerateArray()) steps.Add(d.GetString());
                strategies.Add(new StrategySpec
                {
                    Id = s.GetProperty("id").GetString(),
                    Each = s.GetProperty("each").GetString(),
                    Start = s.GetProperty("start").GetString(),
                    Scored = s.GetProperty("scored").GetBoolean(),
                    Do = steps.ToArray(),
                });
            }

            return new BalanceConfig
            {
                Days = root.GetProperty("days").GetInt32(),
                Sleep = root.GetProperty("sleep").GetString(),
                Tiers = tiers,
                Strategies = strategies,
            };
        }

        /// <summary>모르는 이름이 있으면 그 이름(과 전략 id)을 말하며 실패한다.</summary>
        public void Validate()
        {
            if (Days <= 0) throw new InvalidOperationException("날 수는 1 이상이다: " + Days);
            if (!SleepRules.Knows(Sleep)) throw new InvalidOperationException("모르는 잠 규칙이다(sleep): " + Sleep);
            if (Tiers == null || Tiers.Count == 0) throw new InvalidOperationException("점수 단계가 없다");
            foreach (var tier in Tiers)
                if (tier.Score < 0 || tier.Score > 100) throw new InvalidOperationException("점수 단계는 0~100이다: " + tier.Id + " " + tier.Score);
            if (Strategies == null) throw new InvalidOperationException("전략이 없다");

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
    }
}
