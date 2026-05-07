using NUnit.Framework;
using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Time;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Farming
{
    /// <summary>
    /// FARM-010~015/CROP-006~007: FarmGrid 의 till/plant/water/fertilize/harvest/day-roll 거동.
    /// Tilemap 없이 (null 주입) 데이터 상태만 검증 — 사용자 결정 #2 준수 (수확 후 Tilled 유지).
    /// </summary>
    public sealed class FarmGridTests
    {
        private GameObject _go;
        private FarmGrid _grid;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("FarmGrid");
            _grid = _go.AddComponent<FarmGrid>();
            _grid.Configure(null, null, null, null); // 순수 데이터 모드.
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private static CropDefinition MakeCrop(float[] durations)
        {
            return MakeCrop(durations, System.Array.Empty<GrowthBehaviorBase>());
        }

        private static CropDefinition MakeCrop(float[] durations, GrowthBehaviorBase[] behaviors)
        {
            var c = ScriptableObject.CreateInstance<CropDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CropDefinition).GetField("_id", bind).SetValue(c, "TestCrop");
            typeof(CropDefinition).GetField("_stageDurationsSec", bind).SetValue(c, durations);
            typeof(CropDefinition).GetField("_growthStageSprites", bind).SetValue(c, new Sprite[durations.Length]);
            typeof(CropDefinition).GetField("_behaviors", bind).SetValue(c, behaviors);
            return c;
        }

        [Test]
        public void FARM_010_TillIdempotent()
        {
            var cell = new Vector3Int(0, 0, 0);
            Assert.IsFalse(_grid.IsTilled(cell));
            _grid.Till(cell);
            Assert.IsTrue(_grid.IsTilled(cell));
            _grid.Till(cell); // idempotent
            Assert.IsTrue(_grid.IsTilled(cell));
        }

        [Test]
        public void FARM_011_PlantOnlyOnTilled()
        {
            var cell = new Vector3Int(1, 0, 0);
            var crop = MakeCrop(new[] { 5f });
            // 갈지 않은 cell — 실패해야 함.
            Assert.IsFalse(_grid.TryPlant(cell, crop));
            Assert.IsFalse(_grid.HasPlot(cell));
            // 갈고 나서 — 성공해야 함.
            _grid.Till(cell);
            Assert.IsTrue(_grid.TryPlant(cell, crop));
            Assert.IsTrue(_grid.HasPlot(cell));
        }

        [Test]
        public void FARM_012_CannotReplantOccupied()
        {
            var cell = new Vector3Int(2, 0, 0);
            var crop = MakeCrop(new[] { 5f });
            _grid.Till(cell);
            Assert.IsTrue(_grid.TryPlant(cell, crop));
            // 이미 plot 이 있으면 실패.
            Assert.IsFalse(_grid.TryPlant(cell, crop));
        }

        [Test]
        public void FARM_013_HarvestRetainsTilled()
        {
            var cell = new Vector3Int(3, 0, 0);
            var crop = MakeCrop(new[] { 1f }); // 1 stage 만 — IsHarvestable=true.
            _grid.Till(cell);
            _grid.TryPlant(cell, crop);
            // CropPlot 의 stage 0 이 곧 마지막 stage (StageCount=1) → IsHarvestable=true.
            var plot = _grid.GetPlot(cell);
            Assert.IsNotNull(plot);
            Assert.IsTrue(plot.IsHarvestable);

            bool ok = _grid.TryHarvest(cell, null, out var harvested, out int yield);
            Assert.IsTrue(ok);
            Assert.AreEqual(crop, harvested);
            // 수확 후: plot 제거, Tilled 유지 (사용자 결정 #2).
            Assert.IsFalse(_grid.HasPlot(cell));
            Assert.IsTrue(_grid.IsTilled(cell));
        }

        [Test]
        public void FARM_014_DailyResetClearsWater()
        {
            var cell = new Vector3Int(4, 0, 0);
            _grid.Till(cell);
            _grid.Water(cell, currentDay: 1, level01: 1f);
            Assert.AreEqual(1f, _grid.GetWaterLevel01(cell));

            // day 3 으로 점프 (어제 = day 2 에 안 줬으므로 클리어돼야 함).
            _grid.TestForceDayRoll(3);
            Assert.AreEqual(0f, _grid.GetWaterLevel01(cell));
        }

        [Test]
        public void FARM_015_FertilizerExpiresAfterDuration()
        {
            var cell = new Vector3Int(5, 0, 0);
            _grid.Till(cell);
            _grid.Fertilize(cell, currentDay: 1, multiplier: 1.5f, durationDays: 1);
            Assert.AreEqual(1.5f, _grid.GetFertilizerMultiplier(cell, 1));
            Assert.AreEqual(1.5f, _grid.GetFertilizerMultiplier(cell, 2));
            // day 3 — 만료 (expiresOnDay=2, currentDay>2).
            Assert.AreEqual(1f, _grid.GetFertilizerMultiplier(cell, 3));
        }

        [Test]
        public void CROP_006_GameClockRollClearsWaterAndDailyWaterCropStopsGrowingWhenDry()
        {
            var clockGo = new GameObject("clock");
            try
            {
                var clock = clockGo.AddComponent<GameClock>();
                _grid.enabled = false;
                Object.DestroyImmediate(_go);
                _go = new GameObject("FarmGrid");
                _grid = _go.AddComponent<FarmGrid>();
                _grid.Configure(null, null, null, null);

                var cell = new Vector3Int(6, 0, 0);
                var waterBehavior = ScriptableObject.CreateInstance<RequiresDailyWaterBehavior>();
                var crop = MakeCrop(new[] { 10f, 10f }, new GrowthBehaviorBase[] { waterBehavior });

                _grid.Till(cell);
                Assert.IsTrue(_grid.TryPlant(cell, crop));
                _grid.Water(cell, currentDay: clock.Day, level01: 1f);

                clock.Tick(1200f);

                Assert.AreEqual(3, clock.Day);
                Assert.AreEqual(0f, _grid.GetWaterLevel01(cell));

                var plot = _grid.GetPlot(cell);
                plot.Tick(50f, _grid.GetWaterLevel01(cell), false, 1f);
                Assert.AreEqual(0, plot.CurrentStage);
            }
            finally
            {
                Object.DestroyImmediate(clockGo);
            }
        }

        [Test]
        public void CROP_007_FertilizerDurationExpiresByGameClockDay()
        {
            var clockGo = new GameObject("clock");
            try
            {
                var clock = clockGo.AddComponent<GameClock>();
                _grid.enabled = false;
                Object.DestroyImmediate(_go);
                _go = new GameObject("FarmGrid");
                _grid = _go.AddComponent<FarmGrid>();
                _grid.Configure(null, null, null, null);

                var cell = new Vector3Int(7, 0, 0);
                _grid.Till(cell);
                _grid.Fertilize(cell, currentDay: clock.Day, multiplier: 1.5f, durationDays: 1);

                clock.Tick(600f);
                Assert.AreEqual(2, clock.Day);
                Assert.AreEqual(1.5f, _grid.GetFertilizerMultiplier(cell, clock.Day));

                clock.Tick(600f);
                Assert.AreEqual(3, clock.Day);
                Assert.AreEqual(1f, _grid.GetFertilizerMultiplier(cell, clock.Day));
            }
            finally
            {
                Object.DestroyImmediate(clockGo);
            }
        }
    }
}
