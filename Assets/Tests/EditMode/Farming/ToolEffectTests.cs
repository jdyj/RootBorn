using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using Rootborn.Game.Tools;
using Rootborn.Game.Tools.Effects;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Farming
{
    /// <summary>
    /// EFFECT-020~026: ToolEffectBase 서브클래스 5종의 precondition / 효과 / 인벤토리 변경 검증.
    /// </summary>
    public sealed class ToolEffectTests
    {
        private GameObject _gridGo;
        private FarmGrid _grid;
        private GameObject _playerGo;
        private PlayerInventory _inv;

        [SetUp]
        public void Setup()
        {
            _gridGo = new GameObject("FarmGrid");
            _grid = _gridGo.AddComponent<FarmGrid>();
            _grid.Configure(null, null, null, null);

            _playerGo = new GameObject("Player");
            _inv = _playerGo.AddComponent<PlayerInventory>();
            // GatherInteractor 의존이 없는 경로만 사용 — Inventory.Slots 직접 조작.
        }

        [TearDown]
        public void Teardown()
        {
            if (_gridGo != null) Object.DestroyImmediate(_gridGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        }

        private static ItemDefinition MakeItem(string id, ItemCategory cat, CropDefinition seedFor = null)
        {
            var it = ScriptableObject.CreateInstance<ItemDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ItemDefinition).GetField("_id", bind).SetValue(it, id);
            typeof(ItemDefinition).GetField("_category", bind).SetValue(it, cat);
            if (seedFor != null) typeof(ItemDefinition).GetField("_seedFor", bind).SetValue(it, seedFor);
            return it;
        }

        private static CropDefinition MakeCrop(string id, float[] durations, ItemDefinition seedItem, ItemDefinition harvestItem)
        {
            var c = ScriptableObject.CreateInstance<CropDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CropDefinition).GetField("_id", bind).SetValue(c, id);
            typeof(CropDefinition).GetField("_stageDurationsSec", bind).SetValue(c, durations);
            typeof(CropDefinition).GetField("_growthStageSprites", bind).SetValue(c, new Sprite[durations.Length]);
            typeof(CropDefinition).GetField("_seedItem", bind).SetValue(c, seedItem);
            typeof(CropDefinition).GetField("_harvestItem", bind).SetValue(c, harvestItem);
            typeof(CropDefinition).GetField("_harvestYieldMin", bind).SetValue(c, 1);
            typeof(CropDefinition).GetField("_harvestYieldMax", bind).SetValue(c, 1);
            return c;
        }

        private static ToolDefinition MakeTool(string id, ToolEffectBase[] effects)
        {
            var t = ScriptableObject.CreateInstance<ToolDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ToolDefinition).GetField("_id", bind).SetValue(t, id);
            typeof(ToolDefinition).GetField("_effects", bind).SetValue(t, effects);
            return t;
        }

        private ToolUseContext MakeCtx(ToolDefinition tool, Vector3Int cell)
        {
            return new ToolUseContext(tool, _playerGo, "Soil", cell, null, _grid, _inv, null);
        }

        [Test]
        public void EFFECT_020_TillSoilOnUntilled_Tills()
        {
            var fx = ScriptableObject.CreateInstance<TillSoilEffect>();
            var tool = MakeTool("Hoe", new ToolEffectBase[] { fx });
            var cell = new Vector3Int(0, 0, 0);
            Assert.IsFalse(_grid.IsTilled(cell));
            fx.Apply(MakeCtx(tool, cell));
            Assert.IsTrue(_grid.IsTilled(cell));
        }

        [Test]
        public void EFFECT_021_TillSoilOnTilled_NoOp()
        {
            var fx = ScriptableObject.CreateInstance<TillSoilEffect>();
            var tool = MakeTool("Hoe", new ToolEffectBase[] { fx });
            var cell = new Vector3Int(1, 0, 0);
            _grid.Till(cell);
            fx.Apply(MakeCtx(tool, cell)); // no-op
            Assert.IsTrue(_grid.IsTilled(cell));
        }

        [Test]
        public void EFFECT_022_PlantSeedRequiresSeedInInventory()
        {
            var fx = ScriptableObject.CreateInstance<PlantSeedEffect>();
            var tool = MakeTool("Hand", new ToolEffectBase[] { fx });
            var cell = new Vector3Int(2, 0, 0);
            _grid.Till(cell);
            // 씨앗 없음 → no-op.
            fx.Apply(MakeCtx(tool, cell));
            Assert.IsFalse(_grid.HasPlot(cell));
        }

        [Test]
        public void EFFECT_023_PlantSeedRemovesOneSeed()
        {
            var fx = ScriptableObject.CreateInstance<PlantSeedEffect>();
            var tool = MakeTool("Hand", new ToolEffectBase[] { fx });
            var harvestItem = MakeItem("Wheat", ItemCategory.Resource);
            // SeedFor=null 이면 PlantSeedEffect 가 거부 — 먼저 crop 만들고 seed.SeedFor 셋.
            var crop = MakeCrop("Wheat", new[] { 5f, 5f }, null, harvestItem);
            var seedItem = MakeItem("Seed_Wheat", ItemCategory.Seed, crop);
            // CropDefinition 의 _seedItem 도 채워야 일관성 (PlantSeedEffect 는 seed.SeedFor 만 봄).
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(CropDefinition).GetField("_seedItem", bind).SetValue(crop, seedItem);

            _inv.Inventory.Add(seedItem, 3);
            _inv.RefreshEquippedSeed();
            Assert.AreEqual(seedItem, _inv.EquippedSeed);

            var cell = new Vector3Int(3, 0, 0);
            _grid.Till(cell);
            fx.Apply(MakeCtx(tool, cell));
            Assert.IsTrue(_grid.HasPlot(cell));
            Assert.AreEqual(2, _inv.Inventory.CountOf(seedItem));
        }

        [Test]
        public void EFFECT_024_HarvestCropAddsHarvestItemAndDestroysPlot()
        {
            var harvestItem = MakeItem("Wheat", ItemCategory.Resource);
            var crop = MakeCrop("Wheat", new[] { 1f }, null, harvestItem); // StageCount=1 → 즉시 IsHarvestable.
            var fx = ScriptableObject.CreateInstance<HarvestCropEffect>();
            var tool = MakeTool("Hoe", new ToolEffectBase[] { fx });
            var cell = new Vector3Int(4, 0, 0);
            _grid.Till(cell);
            _grid.TryPlant(cell, crop);
            Assert.IsTrue(_grid.HasPlot(cell));

            fx.Apply(MakeCtx(tool, cell));
            Assert.IsFalse(_grid.HasPlot(cell));
            Assert.IsTrue(_grid.IsTilled(cell)); // 사용자 결정 #2 — 유지.
            Assert.GreaterOrEqual(_inv.Inventory.CountOf(harvestItem), 1);
        }

        [Test]
        public void EFFECT_025_WaterCropOnUntilledNoOp()
        {
            var fx = ScriptableObject.CreateInstance<WaterCropEffect>();
            var tool = MakeTool("Watering", new ToolEffectBase[] { fx });
            var cell = new Vector3Int(5, 0, 0);
            // Untilled → no-op.
            fx.Apply(MakeCtx(tool, cell));
            Assert.AreEqual(0f, _grid.GetWaterLevel01(cell));
        }

        [Test]
        public void EFFECT_026_FertilizeConsumesItem()
        {
            var fertItem = MakeItem("Fertilizer", ItemCategory.Resource);
            var fx = ScriptableObject.CreateInstance<FertilizeCropEffect>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FertilizeCropEffect).GetField("_consumesItem", bind).SetValue(fx, fertItem);
            typeof(FertilizeCropEffect).GetField("_multiplier", bind).SetValue(fx, 1.5f);
            typeof(FertilizeCropEffect).GetField("_durationDays", bind).SetValue(fx, 1);

            var tool = MakeTool("Spreader", new ToolEffectBase[] { fx });
            var cell = new Vector3Int(6, 0, 0);
            _grid.Till(cell);
            _inv.Inventory.Add(fertItem, 2);

            fx.Apply(MakeCtx(tool, cell));
            Assert.AreEqual(1, _inv.Inventory.CountOf(fertItem));
            Assert.AreEqual(1.5f, _grid.GetFertilizerMultiplier(cell, 1));
        }
    }
}
