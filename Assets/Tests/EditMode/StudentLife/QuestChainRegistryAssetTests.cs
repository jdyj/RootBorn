using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainRegistryAssetTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void QUEST_CHAIN_EDIT_002_TestQuestChainLoadsFromRegistryAssetWithoutCodeChanges()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, "QUEST-CHAIN-EDIT-002 failed: GameDataRegistry asset was not found.");
            Assert.IsNotNull(registry.QuestChains, "QUEST-CHAIN-EDIT-002 failed: QuestChains registry array must exist.");
            Assert.IsNotEmpty(registry.QuestChains, "QUEST-CHAIN-EDIT-002 failed: at least one test quest chain must be registered as ScriptableObject data.");

            bool found = false;
            for (int i = 0; i < registry.QuestChains.Length; i++)
            {
                var chain = registry.QuestChains[i];
                if (chain == null || chain.Id != "questchain.test.career-interest.library") continue;
                found = true;
                Assert.AreEqual("interest.learning", chain.RelatedCareerInterestId);
                Assert.IsNotNull(chain.Steps);
                Assert.IsNotEmpty(chain.Steps);
                Assert.IsNotNull(chain.Steps[0].Objectives);
                Assert.GreaterOrEqual(chain.Steps[0].Objectives.Length, 2, "QUEST-CHAIN-EDIT-002 failed: test chain must prove multiple objectives can be data-authored.");
            }

            Assert.IsTrue(found, "QUEST-CHAIN-EDIT-002 failed: questchain.test.career-interest.library was not registered.");
        }
    }
}
