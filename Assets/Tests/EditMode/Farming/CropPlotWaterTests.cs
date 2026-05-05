using NUnit.Framework;
using Rootborn.Game.Crops;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Farming
{
    /// <summary>
    /// CROP-002~005: 물·비료 거동 검증. RequiresDailyWaterBehavior + FertilizerSpeedupBehavior 합성.
    /// </summary>
    public sealed class CropPlotWaterTests
    {
        private static CropDefinition MakeCrop(float[] stageDurations, GrowthBehaviorBase[] behaviors)
        {
            var c = ScriptableObject.CreateInstance<CropDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CropDefinition).GetField("_id", bind).SetValue(c, "TestCrop");
            typeof(CropDefinition).GetField("_stageDurationsSec", bind).SetValue(c, stageDurations);
            var sprites = new Sprite[stageDurations.Length];
            typeof(CropDefinition).GetField("_growthStageSprites", bind).SetValue(c, sprites);
            typeof(CropDefinition).GetField("_behaviors", bind).SetValue(c, behaviors);
            return c;
        }

        [Test]
        public void CROP_002_NoWater_GrowthIsZero()
        {
            var water = ScriptableObject.CreateInstance<RequiresDailyWaterBehavior>();
            var crop = MakeCrop(new[] { 10f, 10f }, new GrowthBehaviorBase[] { water });
            var go = new GameObject("plot");
            try
            {
                var plot = go.AddComponent<CropPlot>();
                plot.Plant(crop);
                plot.Tick(50f, 0f, false, 1f); // water=0, 비료 없음
                Assert.AreEqual(0, plot.CurrentStage);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CROP_003_Watered_GrowthNormal()
        {
            var water = ScriptableObject.CreateInstance<RequiresDailyWaterBehavior>();
            var crop = MakeCrop(new[] { 10f, 10f }, new GrowthBehaviorBase[] { water });
            var go = new GameObject("plot");
            try
            {
                var plot = go.AddComponent<CropPlot>();
                plot.Plant(crop);
                plot.Tick(11f, 1f, false, 1f); // water=1
                Assert.AreEqual(1, plot.CurrentStage);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CROP_004_FertilizerStacks_NormalGrowthTimes1_5()
        {
            var water = ScriptableObject.CreateInstance<RequiresDailyWaterBehavior>();
            var fert = ScriptableObject.CreateInstance<FertilizerSpeedupBehavior>();
            var crop = MakeCrop(new[] { 10f, 10f, 10f }, new GrowthBehaviorBase[] { water, fert });
            var go = new GameObject("plot");
            try
            {
                var plot = go.AddComponent<CropPlot>();
                plot.Plant(crop);
                // 비료 ×1.5 + water=1 → multiplier 1.5. 7초 × 1.5 = 10.5초 effective → stage 0→1.
                plot.Tick(7f, 1f, false, 1.5f);
                Assert.AreEqual(1, plot.CurrentStage);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CROP_005_DryDayResetsWater_AfterFarmGridDayRoll()
        {
            // FarmGrid 의 OnDayRolled 는 water 를 0 으로 클리어. 이는 FarmGridTests 에서 직접 검증.
            // 본 테스트는 Behavior 단독 — water=0 이 되면 multiplier 0 되는지 확인.
            var water = ScriptableObject.CreateInstance<RequiresDailyWaterBehavior>();
            var ctx = new CropGrowthContext(null, 0, 0f, 0f, false, 1f);
            Assert.AreEqual(0f, water.ModifyGrowthRate(in ctx));
            var ctxWatered = new CropGrowthContext(null, 0, 0f, 1f, false, 1f);
            Assert.AreEqual(1f, water.ModifyGrowthRate(in ctxWatered));
        }
    }
}
