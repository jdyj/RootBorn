using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Knowledge.Triggers;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // KNOW-002: KnowledgeProgress가 반복 액션을 누적하고 임계 도달 시 OnUnlocked를 발화한다.
    public sealed class KnowledgeProgressTests
    {
        [Test]
        public void Repeated_HitGround_With_BareHand_Unlocks_StoneTool()
        {
            var bareHand = ScriptableObject.CreateInstance<ToolDefinition>();
            typeof(ToolDefinition).GetField("_id",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(bareHand, "BareHand");

            var trig = ScriptableObject.CreateInstance<HitGroundWithRockTrigger>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(HitGroundWithRockTrigger).GetField("_expectedTargetResourceId", bind).SetValue(trig, "Rock");
            typeof(HitGroundWithRockTrigger).GetField("_expectedSurface", bind).SetValue(trig, "ground");
            typeof(HitGroundWithRockTrigger).GetField("_expectedTool", bind).SetValue(trig, bareHand);
            typeof(KnowledgeTriggerBase).GetField("_requiredRepeats", bind).SetValue(trig, 3);

            var node = ScriptableObject.CreateInstance<KnowledgeNode>();
            typeof(KnowledgeNode).GetField("_triggers", bind).SetValue(node, new KnowledgeTriggerBase[] { trig });

            var progress = new KnowledgeProgress(new[] { node });
            int unlocks = 0;
            progress.OnUnlocked += _ => unlocks++;

            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            Assert.IsFalse(progress.IsUnlocked(node));
            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            Assert.IsFalse(progress.IsUnlocked(node));
            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            Assert.IsTrue(progress.IsUnlocked(node));
            Assert.AreEqual(1, unlocks);

            // Further actions don't double-unlock.
            progress.RecordAction(KnowledgeAction.HitGround, bareHand, "Rock", "ground");
            Assert.AreEqual(1, unlocks);
        }
    }
}
