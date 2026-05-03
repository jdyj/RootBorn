using NUnit.Framework;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Knowledge.Triggers;
using Rootborn.Game.Player;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // TOOL-001: GatherInteractor가 KnowledgeProgress에 액션을 기록하고
    // 충분히 반복하면 OnUnlocked가 발화한다.
    public sealed class GatherInteractorTests
    {
        private static ToolDefinition MakeTool(string id)
        {
            var t = ScriptableObject.CreateInstance<ToolDefinition>();
            typeof(ToolDefinition).GetField("_id",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(t, id);
            return t;
        }

        private static ResourceNodeDefinition MakeResourceDef(string id, string surface)
        {
            var r = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ResourceNodeDefinition).GetField("_id", bind).SetValue(r, id);
            typeof(ResourceNodeDefinition).GetField("_surfaceTag", bind).SetValue(r, surface);
            typeof(ResourceNodeDefinition).GetField("_baseHitsToBreak", bind).SetValue(r, 999f);
            return r;
        }

        [Test]
        public void RecordAction_AccumulatesAndUnlocksKnowledge()
        {
            var bareHand = MakeTool("BareHand");

            var trig = ScriptableObject.CreateInstance<HitGroundWithRockTrigger>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(HitGroundWithRockTrigger).GetField("_expectedTargetResourceId", bind).SetValue(trig, "Rock");
            typeof(HitGroundWithRockTrigger).GetField("_expectedSurface", bind).SetValue(trig, "ground");
            typeof(HitGroundWithRockTrigger).GetField("_expectedTool", bind).SetValue(trig, bareHand);
            typeof(KnowledgeTriggerBase).GetField("_requiredRepeats", bind).SetValue(trig, 3);

            var node = ScriptableObject.CreateInstance<KnowledgeNode>();
            typeof(KnowledgeNode).GetField("_triggers", bind).SetValue(node, new KnowledgeTriggerBase[] { trig });

            var progress = new KnowledgeProgress(new[] { node });

            // 직접 RecordAction을 호출해서 GatherInteractor의 본질 검증
            // (GatherInteractor 자체는 InputAction에 의존하므로 EditMode에서 직접 호출 어려움)
            int unlocks = 0;
            progress.OnUnlocked += _ => unlocks++;

            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            Assert.AreEqual(0, unlocks);
            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            Assert.AreEqual(1, unlocks);
        }

        [Test]
        public void ResourceNode_BindForRuntime_AssignsDefAndRenderer()
        {
            var go = new GameObject("node");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var node = go.AddComponent<ResourceNode>();
                var def = MakeResourceDef("Tree", "ground");

                node.BindForRuntime(def, sr);

                Assert.AreEqual(def, node.Definition);
                Assert.IsFalse(node.IsBroken);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResourceNode_AccumulatesPower_AndBreaks()
        {
            var go = new GameObject("rock");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var node = go.AddComponent<ResourceNode>();

                var def = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(ResourceNodeDefinition).GetField("_id", bind).SetValue(def, "Rock");
                typeof(ResourceNodeDefinition).GetField("_baseHitsToBreak", bind).SetValue(def, 3f);
                typeof(ResourceNodeDefinition).GetField("_bareHandPenaltyMul", bind).SetValue(def, 1f);

                node.BindForRuntime(def, sr);

                int brokenCount = 0;
                node.OnBroken += _ => brokenCount++;

                node.Hit(null);
                node.Hit(null);
                Assert.IsFalse(node.IsBroken);
                node.Hit(null);
                Assert.IsTrue(node.IsBroken);
                Assert.AreEqual(1, brokenCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
