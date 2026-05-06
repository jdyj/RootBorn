using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using Rootborn.Game.Tools.Effects;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestEventAdapterTests
    {
        [Test]
        public void QUEST_007_GatherInteractor_RecordsGatherEventOnSuccessfulHit()
        {
            var player = new GameObject("Player");
            var nodeGo = new GameObject("Rock");
            try
            {
                player.transform.position = Vector3.zero;
                nodeGo.transform.position = new Vector3(0.5f, 0f, 0f);
                var interactor = player.AddComponent<GatherInteractor>();
                var node = nodeGo.AddComponent<ResourceNode>();
                var resource = MakeResource("Rock");
                var tool = MakeTool("Pickaxe", System.Array.Empty<ToolEffectBase>());
                node.BindForRuntime(resource, nodeGo.AddComponent<SpriteRenderer>());
                interactor.EquippedTool = tool;
                var sink = new RecordingQuestEventSink();
                interactor.BindQuestEvents(sink);

                interactor.TriggerInteract();

                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(QuestEventKind.Gather, sink.LastEvent.Kind);
                Assert.AreSame(resource, sink.LastEvent.Resource);
                Assert.AreSame(tool, sink.LastEvent.Tool);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(nodeGo);
            }
        }

        [Test]
        public void QUEST_009_HarvestCropEffect_RecordsHarvestEventAfterInventoryGrant()
        {
            var gridGo = new GameObject("FarmGrid");
            var player = new GameObject("Player");
            try
            {
                var grid = gridGo.AddComponent<FarmGrid>();
                grid.Configure(null, null, null, null);
                var inventory = player.AddComponent<PlayerInventory>();
                var harvestItem = MakeItem("Wheat");
                var crop = MakeCrop("Wheat", harvestItem);
                var effect = ScriptableObject.CreateInstance<HarvestCropEffect>();
                var tool = MakeTool("Hoe", new ToolEffectBase[] { effect });
                var cell = new Vector3Int(2, 0, 0);
                var sink = new RecordingQuestEventSink();
                grid.Till(cell);
                grid.TryPlant(cell, crop);

                effect.Apply(new ToolUseContext(tool, player, "Soil", cell, null, grid, inventory, null, sink));

                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(QuestEventKind.Harvest, sink.LastEvent.Kind);
                Assert.AreSame(crop, sink.LastEvent.Crop);
                Assert.AreSame(harvestItem, sink.LastEvent.Item);
                Assert.AreSame(tool, sink.LastEvent.Tool);
                Assert.AreEqual(1, inventory.Inventory.CountOf(harvestItem));
            }
            finally
            {
                Object.DestroyImmediate(gridGo);
                Object.DestroyImmediate(player);
            }
        }

        private static ResourceNodeDefinition MakeResource(string id)
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            SetField(resource, "_id", id);
            SetField(resource, "_baseHitsToBreak", 1f);
            SetField(resource, "_bareHandPenaltyMul", 1f);
            return resource;
        }

        private static ItemDefinition MakeItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_maxStack", 99);
            SetField(item, "_category", ItemCategory.Resource);
            return item;
        }

        private static CropDefinition MakeCrop(string id, ItemDefinition harvestItem)
        {
            var crop = ScriptableObject.CreateInstance<CropDefinition>();
            SetField(crop, "_id", id);
            SetField(crop, "_stageDurationsSec", new[] { 1f });
            SetField(crop, "_growthStageSprites", new Sprite[1]);
            SetField(crop, "_harvestItem", harvestItem);
            SetField(crop, "_harvestYieldMin", 1);
            SetField(crop, "_harvestYieldMax", 1);
            return crop;
        }

        private static ToolDefinition MakeTool(string id, ToolEffectBase[] effects)
        {
            var tool = ScriptableObject.CreateInstance<ToolDefinition>();
            SetField(tool, "_id", id);
            SetField(tool, "_effects", effects);
            return tool;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private sealed class RecordingQuestEventSink : IQuestEventSink
        {
            public QuestEvent LastEvent;
            public int Count;

            public void Record(in QuestEvent questEvent)
            {
                LastEvent = questEvent;
                Count++;
            }
        }
    }
}
