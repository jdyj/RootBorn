using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
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

        private static ResourceNodeDefinition MakeResource(string id)
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            SetField(resource, "_id", id);
            SetField(resource, "_baseHitsToBreak", 1f);
            SetField(resource, "_bareHandPenaltyMul", 1f);
            return resource;
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
