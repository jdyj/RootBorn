using NUnit.Framework;
using Rootborn.Game.Generation;
using Rootborn.Game.Time;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // GEN-001~003: 세대 전환은 기존 초 단위 호환을 유지하되 신규 밸런스는 lifetimeGameDays 기준으로 판단한다.
    public sealed class GenerationManagerTests
    {
        private const System.Reflection.BindingFlags Bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        private static GenerationProfile MakeProfile(int idx, float lifetimeSec, GenerationProfile next = null)
        {
            var p = ScriptableObject.CreateInstance<GenerationProfile>();
            typeof(GenerationProfile).GetField("_generationIndex", Bind).SetValue(p, idx);
            typeof(GenerationProfile).GetField("_lifetimeSec", Bind).SetValue(p, lifetimeSec);
            typeof(GenerationProfile).GetField("_lifetimeGameDays", Bind).SetValue(p, lifetimeSec);
            typeof(GenerationProfile).GetField("_nextGeneration", Bind).SetValue(p, next);
            return p;
        }

        private static GameClock MakeClock(float realSecondsPerGameDay)
        {
            var clockGo = new GameObject("clock");
            var clock = clockGo.AddComponent<GameClock>();
            typeof(GameClock).GetField("_realSecondsPerGameDay", Bind).SetValue(clock, realSecondsPerGameDay);
            clock.Configure(null);
            return clock;
        }

        [Test]
        public void Tick_LifetimeElapsed_AdvancesGenerationAndRecordsAncestor()
        {
            var go = new GameObject("gen");
            try
            {
                var gen2 = MakeProfile(2, 100f);
                var gen1 = MakeProfile(1, 10f, gen2);

                var manager = go.AddComponent<GenerationManager>();
                manager.StartFromProfile(gen1);

                Assert.AreEqual(0, manager.Lineage.GenerationCount);

                manager.Tick(5f);
                Assert.AreEqual(gen1, manager.CurrentProfile);

                manager.Tick(6f); // total 11s > 10s legacy lifetime
                Assert.AreEqual(gen2, manager.CurrentProfile);
                Assert.AreEqual(1, manager.Lineage.GenerationCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GEN_002_TickGameDays_AdvancesWhenLifetimeGameDaysElapsed()
        {
            var go = new GameObject("gen");
            try
            {
                var gen2 = MakeProfile(2, 14f);
                var gen1 = MakeProfile(1, 14f, gen2);

                var manager = go.AddComponent<GenerationManager>();
                manager.StartFromProfile(gen1);

                manager.TickGameDays(13.99f);
                Assert.AreEqual(gen1, manager.CurrentProfile);
                Assert.AreEqual(13.99f, manager.ElapsedGameDays, 0.001f);

                manager.TickGameDays(0.02f);
                Assert.AreEqual(gen2, manager.CurrentProfile);
                Assert.AreEqual(1, manager.Lineage.GenerationCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GEN_003_GameDayLifetimeMeaningSurvivesDifferentDayLengths()
        {
            var fastClock = MakeClock(300f);
            var slowClock = MakeClock(600f);
            var fastGo = new GameObject("fast-gen");
            var slowGo = new GameObject("slow-gen");
            try
            {
                var fastGen2 = MakeProfile(2, 2f);
                var fastGen1 = MakeProfile(1, 2f, fastGen2);
                var slowGen2 = MakeProfile(2, 2f);
                var slowGen1 = MakeProfile(1, 2f, slowGen2);

                var fastManager = fastGo.AddComponent<GenerationManager>();
                fastManager.ConfigureClock(fastClock);
                fastManager.StartFromProfile(fastGen1);

                var slowManager = slowGo.AddComponent<GenerationManager>();
                slowManager.ConfigureClock(slowClock);
                slowManager.StartFromProfile(slowGen1);

                fastManager.Tick(599.9f);
                slowManager.Tick(1199.9f);
                Assert.AreEqual(fastGen1, fastManager.CurrentProfile);
                Assert.AreEqual(slowGen1, slowManager.CurrentProfile);

                fastManager.Tick(0.2f);
                slowManager.Tick(0.2f);
                Assert.AreEqual(fastGen2, fastManager.CurrentProfile);
                Assert.AreEqual(slowGen2, slowManager.CurrentProfile);
            }
            finally
            {
                Object.DestroyImmediate(fastClock.gameObject);
                Object.DestroyImmediate(slowClock.gameObject);
                Object.DestroyImmediate(fastGo);
                Object.DestroyImmediate(slowGo);
            }
        }
    }
}
