using NUnit.Framework;
using Rootborn.Game.Crops;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // CROP-001: CropPlot.Tick은 stageDurations에 따라 결정론적으로 단계 전환한다.
    public sealed class CropGrowthTests
    {
        private static CropDefinition MakeCrop(float[] stageDurations)
        {
            var c = ScriptableObject.CreateInstance<CropDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CropDefinition).GetField("_id", bind).SetValue(c, "Wheat");
            typeof(CropDefinition).GetField("_stageDurationsSec", bind).SetValue(c, stageDurations);
            var sprites = new Sprite[stageDurations.Length];
            typeof(CropDefinition).GetField("_growthStageSprites", bind).SetValue(c, sprites);
            return c;
        }

        [Test]
        public void Tick_TransitionsStages()
        {
            var go = new GameObject("plot");
            try
            {
                var plot = go.AddComponent<CropPlot>();
                var crop = MakeCrop(new[] { 10f, 10f, 10f });
                plot.Plant(crop);
                Assert.AreEqual(0, plot.CurrentStage);

                plot.Tick(5f, 1f, false);
                Assert.AreEqual(0, plot.CurrentStage);

                plot.Tick(6f, 1f, false); // total 11s, stage 0 -> 1
                Assert.AreEqual(1, plot.CurrentStage);

                plot.Tick(20f, 1f, false); // skip to last stage
                Assert.AreEqual(2, plot.CurrentStage);
                Assert.IsTrue(plot.IsHarvestable);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
