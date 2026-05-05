using NUnit.Framework;
using Rootborn.Game.Common;
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

        // 통행 가능 여부는 SO 데이터로만 결정 (코드 분기 없음).
        [Test]
        public void IsWalkable_DefaultsToTrue_OnNewlyCreatedSO()
        {
            var def = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            try
            {
                Assert.IsTrue(def.IsWalkable, "신규 ResourceNodeDefinition 의 기본 _isWalkable 은 true (통과 가능).");
                Assert.AreEqual(new Vector2(1f, 1f), def.ColliderSize);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void IsWalkable_ReflectsSerializedField()
        {
            var def = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            try
            {
                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(ResourceNodeDefinition).GetField("_isWalkable", bind).SetValue(def, false);
                Assert.IsFalse(def.IsWalkable);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void RegistryWiring_TreeWalkable_RockBlocked()
        {
            // 와이어링 회귀 테스트: Resource_Tree.asset / Resource_Rock.asset 로딩 검증.
            // AssetDatabase 는 EditMode 에서 사용 가능.
            var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(
                "Assets/Data/Resources/Resource_Tree.asset");
            var rock = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(
                "Assets/Data/Resources/Resource_Rock.asset");
            Assert.IsNotNull(tree, "Resource_Tree.asset 미발견");
            Assert.IsNotNull(rock, "Resource_Rock.asset 미발견");
            Assert.IsTrue(tree.IsWalkable, "Resource_Tree 는 통행 가능해야 함 (풀/덤불).");
            Assert.IsFalse(rock.IsWalkable, "Resource_Rock 은 통행 차단되어야 함 (돌).");
        }

        // ===== Inventory =====

        private static ItemDefinition MakeItem(string id, int maxStack = 99, ItemCategory cat = ItemCategory.Resource)
        {
            var it = ScriptableObject.CreateInstance<ItemDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ItemDefinition).GetField("_id", bind).SetValue(it, id);
            typeof(ItemDefinition).GetField("_maxStack", bind).SetValue(it, maxStack);
            typeof(ItemDefinition).GetField("_category", bind).SetValue(it, cat);
            return it;
        }

        [Test]
        public void Inventory_Add_NewItem_CreatesSlot()
        {
            var inv = new Inventory();
            var wood = MakeItem("Wood");
            int changed = 0;
            inv.OnChanged += () => changed++;

            inv.Add(wood, 5);

            Assert.AreEqual(1, inv.Slots.Count);
            Assert.AreEqual(5, inv.Slots[0].Count);
            Assert.AreEqual(wood, inv.Slots[0].Item);
            Assert.AreEqual(1, changed);
            Object.DestroyImmediate(wood);
        }

        [Test]
        public void Inventory_Add_Stackable_IncrementsExistingSlot()
        {
            var inv = new Inventory();
            var wood = MakeItem("Wood", maxStack: 99);
            inv.Add(wood, 3);
            inv.Add(wood, 4);

            Assert.AreEqual(1, inv.Slots.Count);
            Assert.AreEqual(7, inv.Slots[0].Count);
            Assert.AreEqual(7, inv.CountOf(wood));
            Object.DestroyImmediate(wood);
        }

        [Test]
        public void Inventory_Add_BeyondMaxStack_SplitsToNewSlot()
        {
            var inv = new Inventory();
            var stone = MakeItem("Stone", maxStack: 10);
            inv.Add(stone, 25);

            Assert.AreEqual(3, inv.Slots.Count, "10+10+5 = 3 슬롯");
            Assert.AreEqual(10, inv.Slots[0].Count);
            Assert.AreEqual(10, inv.Slots[1].Count);
            Assert.AreEqual(5, inv.Slots[2].Count);
            Assert.AreEqual(25, inv.CountOf(stone));
            Object.DestroyImmediate(stone);
        }

        [Test]
        public void Inventory_Remove_Insufficient_ReturnsFalse()
        {
            var inv = new Inventory();
            var wood = MakeItem("Wood");
            inv.Add(wood, 2);

            bool ok = inv.Remove(wood, 5);

            Assert.IsFalse(ok);
            Assert.AreEqual(2, inv.CountOf(wood), "실패 시 변동 없음");
            Object.DestroyImmediate(wood);
        }

        [Test]
        public void Inventory_Remove_DrainsAcrossSlots()
        {
            var inv = new Inventory();
            var stone = MakeItem("Stone", maxStack: 10);
            inv.Add(stone, 25); // 10/10/5 슬롯

            bool ok = inv.Remove(stone, 12);

            Assert.IsTrue(ok);
            Assert.AreEqual(13, inv.CountOf(stone));
            Object.DestroyImmediate(stone);
        }
    }
}
