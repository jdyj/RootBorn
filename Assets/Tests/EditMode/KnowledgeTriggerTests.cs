using NUnit.Framework;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Knowledge.Triggers;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // KNOW-001: 돌로 땅을 반복 타격하면 도구 개념이 해금된다.
    public sealed class KnowledgeTriggerTests
    {
        private static ToolDefinition NewTool(string id)
        {
            var t = ScriptableObject.CreateInstance<ToolDefinition>();
            typeof(ToolDefinition).GetField("_id",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(t, id);
            return t;
        }

        private static HitGroundWithRockTrigger NewHitGroundTrigger(ToolDefinition tool, int repeats)
        {
            var trig = ScriptableObject.CreateInstance<HitGroundWithRockTrigger>();
            var type = typeof(HitGroundWithRockTrigger);
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            type.GetField("_expectedTargetResourceId", bind).SetValue(trig, "Rock");
            type.GetField("_expectedSurface", bind).SetValue(trig, "ground");
            type.GetField("_expectedTool", bind).SetValue(trig, tool);
            typeof(KnowledgeTriggerBase).GetField("_requiredRepeats",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(trig, repeats);
            return trig;
        }

        [Test]
        public void Trigger_RequiresAllConditionsAndRepeatThreshold()
        {
            var bareHand = NewTool("BareHand");
            var trig = NewHitGroundTrigger(bareHand, 10);

            Assert.IsFalse(trig.Evaluate(new KnowledgeContext(KnowledgeAction.Gather, bareHand, "Rock", "ground", 10)));
            Assert.IsFalse(trig.Evaluate(new KnowledgeContext(KnowledgeAction.HitGround, bareHand, "Tree", "ground", 10)));
            Assert.IsFalse(trig.Evaluate(new KnowledgeContext(KnowledgeAction.HitGround, bareHand, "Rock", "water", 10)));
            Assert.IsFalse(trig.Evaluate(new KnowledgeContext(KnowledgeAction.HitGround, bareHand, "Rock", "ground", 9)));
            Assert.IsTrue(trig.Evaluate(new KnowledgeContext(KnowledgeAction.HitGround, bareHand, "Rock", "ground", 10)));
            Assert.IsTrue(trig.Evaluate(new KnowledgeContext(KnowledgeAction.HitGround, bareHand, "Rock", "ground", 99)));
        }

        [Test]
        public void GatherCountTrigger_OnlyFiresOnGather()
        {
            var trig = ScriptableObject.CreateInstance<GatherCountTrigger>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GatherCountTrigger).GetField("_expectedTargetResourceId", bind).SetValue(trig, "Wood");
            typeof(KnowledgeTriggerBase).GetField("_requiredRepeats", bind).SetValue(trig, 3);

            Assert.IsFalse(trig.Evaluate(new KnowledgeContext(KnowledgeAction.HitGround, null, "Wood", "ground", 5)));
            Assert.IsFalse(trig.Evaluate(new KnowledgeContext(KnowledgeAction.Gather, null, "Stone", "ground", 5)));
            Assert.IsTrue(trig.Evaluate(new KnowledgeContext(KnowledgeAction.Gather, null, "Wood", "ground", 3)));
        }

        [Test]
        public void Node_AllTriggersMustBeSatisfied()
        {
            var bareHand = NewTool("BareHand");
            var trig1 = NewHitGroundTrigger(bareHand, 5);
            var trig2 = ScriptableObject.CreateInstance<GatherCountTrigger>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GatherCountTrigger).GetField("_expectedTargetResourceId", bind).SetValue(trig2, "Stone");
            typeof(KnowledgeTriggerBase).GetField("_requiredRepeats", bind).SetValue(trig2, 1);

            var node = ScriptableObject.CreateInstance<KnowledgeNode>();
            typeof(KnowledgeNode).GetField("_triggers", bind)
                .SetValue(node, new KnowledgeTriggerBase[] { trig1, trig2 });

            // First context only satisfies trig1 → node not yet satisfied
            Assert.IsFalse(node.IsSatisfied(new KnowledgeContext(KnowledgeAction.HitGround, bareHand, "Rock", "ground", 5)));
            Assert.IsFalse(node.IsSatisfied(new KnowledgeContext(KnowledgeAction.Gather, null, "Stone", "ground", 1)));
        }
    }
}
