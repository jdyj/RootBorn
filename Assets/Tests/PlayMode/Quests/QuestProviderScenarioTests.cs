using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestProviderScenarioTests
    {
        [UnityTest]
        public IEnumerator QUEST_003_InteractableObject_ProvidesQuestDefinition()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            var providerGo = new GameObject("Quest Board");
            var provider = providerGo.AddComponent<QuestProvider>();
            provider.Bind(new[] { quest });

            yield return null;

            CollectionAssert.Contains(provider.Quests, quest);
            Object.Destroy(providerGo);
        }
    }
}
