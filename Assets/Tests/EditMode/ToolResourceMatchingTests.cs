using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    /// <summary>
    /// 도구 ↔ 자원 매칭 시나리오. ResourceNodeDefinition._preferredTool 데이터 와이어링이
    /// 의도대로 풀파워(매칭) / 0.5x(불일치) / 0.3x(맨손) 를 반환하는지 EditMode 검증.
    /// 코드 분기 없이 데이터(SO 필드)로만 도구-자원 매칭이 결정되는 헌법 §엔티티 데이터드리븐 절대 원칙 회귀 가드.
    /// </summary>
    public sealed class ToolResourceMatchingTests
    {
        private const string PathToolStoneAxe      = "Assets/Data/Tools/Tool_StoneAxe.asset";
        private const string PathToolStonePickaxe  = "Assets/Data/Tools/Tool_StonePickaxe.asset";
        private const string PathItemToolPickaxe   = "Assets/Data/Items/Item_Tool_StonePickaxe.asset";
        private const string PathResourceTree      = "Assets/Data/Resources/Resource_Tree.asset";
        private const string PathResourceRock      = "Assets/Data/Resources/Resource_Rock.asset";

        // === 합성 SO (격리 단위 검증) ===

        private static ToolDefinition MakeTool(string id, float powerMul)
        {
            var t = ScriptableObject.CreateInstance<ToolDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ToolDefinition).GetField("_id", bind).SetValue(t, id);
            typeof(ToolDefinition).GetField("_powerMultiplier", bind).SetValue(t, powerMul);
            return t;
        }

        private static ResourceNodeDefinition MakeResourceDef(string id, ToolDefinition preferred, float bareHandMul = 0.3f)
        {
            var r = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ResourceNodeDefinition).GetField("_id", bind).SetValue(r, id);
            typeof(ResourceNodeDefinition).GetField("_preferredTool", bind).SetValue(r, preferred);
            typeof(ResourceNodeDefinition).GetField("_bareHandPenaltyMul", bind).SetValue(r, bareHandMul);
            return r;
        }

        [Test]
        public void TOOL_MATCH_001_PreferredTool_AppliesFullPower()
        {
            var axe = MakeTool("Axe", 1.2f);
            var tree = MakeResourceDef("Tree", axe);
            try
            {
                Assert.AreEqual(1.2f, tree.ComputeEffectivePower(axe), 0.0001f,
                    "선호 도구로 자원을 치면 PowerMultiplier 그대로 적용 (패널티 0)");
            }
            finally
            {
                Object.DestroyImmediate(axe);
                Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void TOOL_MATCH_002_MismatchTool_AppliesHalfPenalty()
        {
            var axe = MakeTool("Axe", 1.2f);
            var pickaxe = MakeTool("Pickaxe", 1.2f);
            var rock = MakeResourceDef("Rock", pickaxe);
            try
            {
                Assert.AreEqual(0.6f, rock.ComputeEffectivePower(axe), 0.0001f,
                    "선호 도구가 곡괭이인데 도끼로 치면 PowerMul × 0.5 = 0.6");
            }
            finally
            {
                Object.DestroyImmediate(axe);
                Object.DestroyImmediate(pickaxe);
                Object.DestroyImmediate(rock);
            }
        }

        [Test]
        public void TOOL_MATCH_003_BareHand_AppliesBareHandPenalty()
        {
            var pickaxe = MakeTool("Pickaxe", 1.2f);
            var rock = MakeResourceDef("Rock", pickaxe, bareHandMul: 0.3f);
            try
            {
                Assert.AreEqual(0.3f, rock.ComputeEffectivePower(null), 0.0001f,
                    "맨손(usedTool=null)이면 _bareHandPenaltyMul 반환 (선호 도구와 무관)");
            }
            finally
            {
                Object.DestroyImmediate(pickaxe);
                Object.DestroyImmediate(rock);
            }
        }

        [Test]
        public void TOOL_MATCH_004_NoPreferredTool_AnyToolFullPower()
        {
            var axe = MakeTool("Axe", 1.2f);
            var noPref = MakeResourceDef("Generic", null);
            try
            {
                Assert.AreEqual(1.2f, noPref.ComputeEffectivePower(axe), 0.0001f,
                    "선호 도구 미설정 시 모든 도구 풀파워 (패널티 0)");
            }
            finally
            {
                Object.DestroyImmediate(axe);
                Object.DestroyImmediate(noPref);
            }
        }

        // === 디스크 자산 와이어링 검증 (통합) ===

        [Test]
        public void WIRING_001_Tool_StonePickaxe_AssetExists()
        {
            var pick = AssetDatabase.LoadAssetAtPath<ToolDefinition>(PathToolStonePickaxe);
            Assert.IsNotNull(pick, $"{PathToolStonePickaxe} 미존재. Unity Editor 에서 Tool_StoneAxe 복제 후 ID 변경 필요.");
            Assert.AreEqual("StonePickaxe", pick.Id, "Tool_StonePickaxe._id 는 'StonePickaxe' 여야 함 (Item._id 와 일치).");
            Assert.AreEqual(1.2f, pick.PowerMultiplier, 0.0001f, "곡괭이 PowerMultiplier 1.2 (도끼와 동일 등급).");
        }

        [Test]
        public void WIRING_002_Item_Tool_StonePickaxe_AssetExists()
        {
            var item = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.ItemDefinition>(PathItemToolPickaxe);
            Assert.IsNotNull(item, $"{PathItemToolPickaxe} 미존재.");
            Assert.AreEqual("StonePickaxe", item.Id, "Item._id 는 ToolDefinition._id 와 동일해야 PlayerInventory.EquipTool 의 ToolById lookup 작동.");
            Assert.AreEqual(Rootborn.Game.Common.ItemCategory.Tool, item.Category, "도구 아이템 Category=Tool.");
        }

        [Test]
        public void WIRING_003_Resource_Rock_PreferredTool_IsStonePickaxe()
        {
            var rock = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(PathResourceRock);
            var pick = AssetDatabase.LoadAssetAtPath<ToolDefinition>(PathToolStonePickaxe);
            Assert.IsNotNull(rock, "Resource_Rock.asset 미발견");
            Assert.IsNotNull(pick, "Tool_StonePickaxe.asset 미발견 — 먼저 자산 생성 필요");
            Assert.AreEqual(pick, rock.PreferredTool,
                "Resource_Rock._preferredTool 이 Tool_StonePickaxe 로 와이어링돼야 곡괭이만 풀파워 (다른 도구는 0.5x).");
        }

        [Test]
        public void WIRING_004_Resource_Tree_PreferredTool_IsStoneAxe()
        {
            var tree = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(PathResourceTree);
            var axe = AssetDatabase.LoadAssetAtPath<ToolDefinition>(PathToolStoneAxe);
            Assert.IsNotNull(tree, "Resource_Tree.asset 미발견");
            Assert.IsNotNull(axe, "Tool_StoneAxe.asset 미발견");
            Assert.AreEqual(axe, tree.PreferredTool,
                "Resource_Tree._preferredTool 이 Tool_StoneAxe 와이어링 회귀 가드.");
        }

        [Test]
        public void WIRING_005_GameDataRegistry_ContainsPickaxeToolAndItem()
        {
            // Resources/GameDataRegistry.asset 은 PlayerInventory.Bind 의 lookup Dictionary 소스.
            // 곡괭이 SO 를 등록하지 않으면 Inspector 에서 장착 시 ToolById lookup 실패.
            var registry = UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>("GameDataRegistry");
            Assert.IsNotNull(registry, "Resources/GameDataRegistry.asset 미발견");

            var pick = AssetDatabase.LoadAssetAtPath<ToolDefinition>(PathToolStonePickaxe);
            var pickItem = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.ItemDefinition>(PathItemToolPickaxe);
            Assert.IsNotNull(pick, "Tool_StonePickaxe.asset 선행 생성 필요");
            Assert.IsNotNull(pickItem, "Item_Tool_StonePickaxe.asset 선행 생성 필요");

            CollectionAssert.Contains(registry.Tools, pick,
                "GameDataRegistry._tools 에 Tool_StonePickaxe 등록 필요.");
            CollectionAssert.Contains(registry.Items, pickItem,
                "GameDataRegistry._items 에 Item_Tool_StonePickaxe 등록 필요.");
        }

        // === 통합 — 디스크 자산 기준 효과 계산 ===

        [Test]
        public void INTEGRATION_PickaxeOnRock_FullPower_AxeOnRock_HalfPenalty()
        {
            var rock = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(PathResourceRock);
            var pick = AssetDatabase.LoadAssetAtPath<ToolDefinition>(PathToolStonePickaxe);
            var axe = AssetDatabase.LoadAssetAtPath<ToolDefinition>(PathToolStoneAxe);
            Assert.IsNotNull(rock); Assert.IsNotNull(pick); Assert.IsNotNull(axe);

            float rockByPick = rock.ComputeEffectivePower(pick);
            float rockByAxe = rock.ComputeEffectivePower(axe);

            Assert.AreEqual(pick.PowerMultiplier, rockByPick, 0.0001f,
                "곡괭이로 돌 → PowerMul 1.2 풀파워.");
            Assert.AreEqual(axe.PowerMultiplier * 0.5f, rockByAxe, 0.0001f,
                "도끼로 돌 → PowerMul × 0.5 = 0.6 (mismatch).");
            Assert.Less(rockByAxe, rockByPick,
                "도구 매칭 정합성: 적합 도구 > 부적합 도구.");
        }
    }
}
