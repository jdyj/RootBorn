using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using Rootborn.Game.Tools;
using Rootborn.Game.Tools.Effects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Farming
{
    /// <summary>
    /// PlayMode 시나리오: 갈기 → 심기 → 시간 가속 → 수확 → 인벤토리에 Wheat 누적.
    /// FarmGrid 를 직접 만들고 인벤토리/도구를 reflection 없이 조립하여 헤드리스로 검증.
    /// </summary>
    public sealed class FarmingScenarioTests
    {
        [UnityTest]
        public IEnumerator Farming_TillPlantWaitHarvest_ProducesHarvestItem()
        {
            // Setup ===========================================================
            var gridGo = new GameObject("FarmGrid");
            var grid = gridGo.AddComponent<FarmGrid>();
            grid.Configure(null, null, null, null);

            var playerGo = new GameObject("Player");
            var inv = playerGo.AddComponent<PlayerInventory>();

            var harvestItem = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(harvestItem, "_id", "Item_Wheat");
            SetField(harvestItem, "_category", ItemCategory.Resource);

            // 짧은 stage durations 로 빠른 시나리오 — 0.5s × 3 stage.
            var crop = ScriptableObject.CreateInstance<CropDefinition>();
            SetField(crop, "_id", "TestWheat");
            SetField(crop, "_stageDurationsSec", new[] { 0.5f, 0.5f, 0.5f });
            SetField(crop, "_growthStageSprites", new Sprite[3]);
            SetField(crop, "_harvestItem", harvestItem);
            SetField(crop, "_harvestYieldMin", 1);
            SetField(crop, "_harvestYieldMax", 1);

            var seedItem = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(seedItem, "_id", "Item_Seed_Wheat");
            SetField(seedItem, "_category", ItemCategory.Seed);
            SetField(seedItem, "_seedFor", crop);
            SetField(crop, "_seedItem", seedItem);

            // Effects
            var fxTill = ScriptableObject.CreateInstance<TillSoilEffect>();
            var fxPlant = ScriptableObject.CreateInstance<PlantSeedEffect>();
            var fxHarvest = ScriptableObject.CreateInstance<HarvestCropEffect>();

            var toolHoe = ScriptableObject.CreateInstance<ToolDefinition>();
            SetField(toolHoe, "_id", "Hoe");
            SetField(toolHoe, "_effects", new ToolEffectBase[] { fxHarvest, fxTill });
            var toolHand = ScriptableObject.CreateInstance<ToolDefinition>();
            SetField(toolHand, "_id", "Hand");
            SetField(toolHand, "_effects", new ToolEffectBase[] { fxPlant });

            // Step 1 — Hoe 로 4 cell 갈기
            for (int i = 0; i < 4; i++)
            {
                var cell = new Vector3Int(i, 0, 0);
                var ctx = new ToolUseContext(toolHoe, playerGo, "Soil", cell, null, grid, inv, null);
                toolHoe.ApplyEffects(in ctx);
                Assert.IsTrue(grid.IsTilled(cell), $"cell {i} not tilled");
            }

            // Step 2 — 씨앗 4개 추가 + 자동 EquippedSeed + 4 cell 심기
            inv.Inventory.Add(seedItem, 4);
            inv.RefreshEquippedSeed();
            Assert.AreEqual(seedItem, inv.EquippedSeed);
            for (int i = 0; i < 4; i++)
            {
                var cell = new Vector3Int(i, 0, 0);
                var ctx = new ToolUseContext(toolHand, playerGo, "Soil", cell, null, grid, inv, null);
                toolHand.ApplyEffects(in ctx);
                Assert.IsTrue(grid.HasPlot(cell), $"cell {i} no plot");
            }
            Assert.AreEqual(0, inv.Inventory.CountOf(seedItem));

            // Step 3 — water 적용 (Behavior 미사용 crop 이지만 안전을 위해)
            for (int i = 0; i < 4; i++)
            {
                grid.Water(new Vector3Int(i, 0, 0), 1, 1f);
            }

            // Step 4 — 시간 경과로 성장. CropPlot.Tick 직접 호출 (FarmGrid.Update 가 deltaTime 사용하지만 짧은 yield 대신 결정론).
            for (int i = 0; i < 4; i++)
            {
                var plot = grid.GetPlot(new Vector3Int(i, 0, 0));
                Assert.IsNotNull(plot);
                plot.Tick(2f, 1f, false, 1f); // 2s > 0.5*3 = 1.5s → 마지막 stage 도달.
                Assert.IsTrue(plot.IsHarvestable, $"cell {i} not harvestable");
            }

            yield return null; // PlayMode 한 프레임 yield.

            // Step 5 — Hoe 로 수확
            for (int i = 0; i < 4; i++)
            {
                var cell = new Vector3Int(i, 0, 0);
                var ctx = new ToolUseContext(toolHoe, playerGo, "Soil", cell, null, grid, inv, null);
                toolHoe.ApplyEffects(in ctx);
                Assert.IsFalse(grid.HasPlot(cell), $"cell {i} plot not destroyed");
                Assert.IsTrue(grid.IsTilled(cell), $"cell {i} tilled state lost");
            }

            // 단언 — 수확물 4개 (yield min=max=1).
            Assert.AreEqual(4, inv.Inventory.CountOf(harvestItem));

            // Cleanup
            Object.Destroy(gridGo);
            Object.Destroy(playerGo);
            yield return null;
        }

        private static void SetField(Object o, string name, object v)
        {
            var bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var f = o.GetType().GetField(name, bf);
            if (f == null) throw new System.Exception($"missing field {name} on {o.GetType().Name}");
            f.SetValue(o, v);
        }
    }
}
