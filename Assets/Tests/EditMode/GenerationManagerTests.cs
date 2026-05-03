using NUnit.Framework;
using Rootborn.Game.Generation;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // GEN-001: lifetimeSec 경과 시 GenerationManager는 다음 세대로 진입하고 Lineage에 기록한다.
    public sealed class GenerationManagerTests
    {
        private static GenerationProfile MakeProfile(int idx, float lifetime, GenerationProfile next = null)
        {
            var p = ScriptableObject.CreateInstance<GenerationProfile>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GenerationProfile).GetField("_generationIndex", bind).SetValue(p, idx);
            typeof(GenerationProfile).GetField("_lifetimeSec", bind).SetValue(p, lifetime);
            typeof(GenerationProfile).GetField("_nextGeneration", bind).SetValue(p, next);
            return p;
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

                manager.Tick(6f); // total 11s > 10s lifetime
                Assert.AreEqual(gen2, manager.CurrentProfile);
                Assert.AreEqual(1, manager.Lineage.GenerationCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
