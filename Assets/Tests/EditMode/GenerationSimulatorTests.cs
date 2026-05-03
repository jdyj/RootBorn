using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    /// <summary>
    /// 세대/F2P 래퍼 시뮬레이터 — Unity 없이 EditMode에서 1세대 1800초 ×
    /// 여러 세대를 빠르게 시뮬해서 결정론과 진행도를 검증.
    ///
    /// testing-discipline.md 의 META-008 (30일 F2P) 시나리오 축소판:
    /// - 1세대 lifetimeSec 진행 → 세대 교체 → 후계자 특성 누적
    /// - 시드 고정 → 동일 결과 보장 (parity)
    /// - 콘솔에 표 형식 리포트 출력
    /// </summary>
    [Category("Long")]
    public sealed class GenerationSimulatorTests
    {
        private const float TickStep = 1f / 60f;

        private static GenerationProfile MakeProfile(int idx, float lifetime, GenerationProfile next = null)
        {
            var p = ScriptableObject.CreateInstance<GenerationProfile>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GenerationProfile).GetField("_generationIndex", bind).SetValue(p, idx);
            typeof(GenerationProfile).GetField("_lifetimeSec", bind).SetValue(p, lifetime);
            typeof(GenerationProfile).GetField("_nextGeneration", bind).SetValue(p, next);
            return p;
        }

        private static HeirTrait MakeTrait(string id, bool inheritable = true,
            float hungerMul = 1f, float fatigueMul = 1f, float gatherMul = 1f, float learnMul = 1f)
        {
            var t = ScriptableObject.CreateInstance<HeirTrait>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(HeirTrait).GetField("_id", bind).SetValue(t, id);
            typeof(HeirTrait).GetField("_isInheritable", bind).SetValue(t, inheritable);
            typeof(HeirTrait).GetField("_hungerDecayMul", bind).SetValue(t, hungerMul);
            typeof(HeirTrait).GetField("_fatigueDecayMul", bind).SetValue(t, fatigueMul);
            typeof(HeirTrait).GetField("_gatherSpeedMul", bind).SetValue(t, gatherMul);
            typeof(HeirTrait).GetField("_learnSpeedMul", bind).SetValue(t, learnMul);
            return t;
        }

        // GEN-002: 5세대를 시뮬해서 lineage가 정확히 5개 ancestor 누적
        [Test]
        public void Simulate_5_Generations_Records_All_Ancestors()
        {
            // 1세대 → 2세대 → ... → 자기 자신 loop
            var gen = MakeProfile(1, 30f);
            // _nextGeneration = self (cycle) — 시뮬에서 같은 프로필을 5번 반복
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GenerationProfile).GetField("_nextGeneration", bind).SetValue(gen, gen);

            var go = new GameObject("gen-sim");
            try
            {
                var manager = go.AddComponent<GenerationManager>();
                manager.StartFromProfile(gen);

                int targetGenerations = 5;
                int transitions = 0;
                manager.OnGenerationChanged += (_, _, _) => transitions++;

                // 5세대만큼 시뮬 — 매번 lifetimeSec 30초 + 1초 여유
                for (int g = 0; g < targetGenerations; g++)
                {
                    SimulateSeconds(manager, 31f);
                }

                Assert.AreEqual(targetGenerations, transitions);
                Assert.AreEqual(targetGenerations, manager.Lineage.GenerationCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // HEIR-002: 동일 시드 → 동일 결과 (1000회 반복 분포)
        [Test]
        public void HeirGenerator_SeedDeterminism_1000_Iterations()
        {
            var pool = new List<HeirTrait>
            {
                MakeTrait("Hardy", hungerMul: 0.8f),
                MakeTrait("Green", gatherMul: 1.2f),
                MakeTrait("Quick", learnMul: 1.3f),
                MakeTrait("Lonely", fatigueMul: 0.9f),
                MakeTrait("Frail", inheritable: false)
            };

            var dist = new Dictionary<string, int>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var heir = HeirGenerator.Generate(
                    parentTraits: new[] { pool[0], pool[2] },
                    traitPool: pool,
                    seed: seed,
                    inheritProbability: 0.5f,
                    maxInherited: 2,
                    randomBonus: 1);

                foreach (var t in heir.Traits)
                {
                    if (!dist.ContainsKey(t.Id)) dist[t.Id] = 0;
                    dist[t.Id]++;
                }
            }

            // Frail은 비계승 + parent에 없으므로 randomBonus로만 등장 가능
            Assert.IsTrue(dist.ContainsKey("Hardy"));
            Assert.IsTrue(dist.ContainsKey("Quick"));
            // 동일 시드로 다시 시뮬 → 동일 분포
            var dist2 = new Dictionary<string, int>();
            for (int seed = 0; seed < 1000; seed++)
            {
                var heir = HeirGenerator.Generate(new[] { pool[0], pool[2] }, pool, seed, 0.5f, 2, 1);
                foreach (var t in heir.Traits)
                {
                    if (!dist2.ContainsKey(t.Id)) dist2[t.Id] = 0;
                    dist2[t.Id]++;
                }
            }
            foreach (var kv in dist)
            {
                Assert.AreEqual(kv.Value, dist2[kv.Key], $"Seed determinism failed for trait {kv.Key}.");
            }
        }

        // META-008 축약: 30일 F2P 시뮬 (GameClock 기반 일자 계산만, 실제 진행도 없음)
        // 1게임일 = 360 실시간초 (GameClock 기본). 30일 = 10800초.
        [Test]
        public void Simulate_30_Days_GameClock_Reaches_Day_30()
        {
            var go = new GameObject("clock-sim");
            try
            {
                var clock = go.AddComponent<Rootborn.Game.Time.GameClock>();
                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(Rootborn.Game.Time.GameClock).GetField("_realSecondsPerGameDay", bind).SetValue(clock, 360f);

                // 30일치 진행 — 30 × 360 = 10800초 = 648000 ticks @ 60Hz
                int ticks = 0;
                while (clock.Day < 30 && ticks < 1_000_000)
                {
                    clock.Tick(TickStep);
                    ticks++;
                }
                Assert.GreaterOrEqual(clock.Day, 30, $"Did not reach day 30 in {ticks} ticks.");
                // 결정론: 정확히 30일이 되는 시간은 30 × 360 / TickStep = 648000
                Assert.LessOrEqual(ticks, 648001, "Day 30 reached too late (non-deterministic?).");

                // 콘솔 리포트 — testing-discipline.md 형식
                var sb = new StringBuilder();
                sb.AppendLine("[ROOTBORN/Sim] === META-008 축약 — 30일 GameClock 진행 ===");
                sb.AppendLine($"Final day: {clock.Day}, ticks: {ticks}, tickStep: {TickStep:F4}s");
                sb.AppendLine($"Real seconds elapsed: {clock.ElapsedRealSeconds:F2}");
                Debug.Log(sb.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void SimulateSeconds(GenerationManager manager, float seconds)
        {
            int ticks = Mathf.CeilToInt(seconds / TickStep);
            for (int i = 0; i < ticks; i++)
            {
                manager.Tick(TickStep);
            }
        }
    }
}
