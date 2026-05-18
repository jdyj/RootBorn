using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class TownQuestResourceInteractionRegressionTests
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-resource-regression", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QUEST_RESOURCE_TOWN_001_QuestResourceCanBeGatheredWithoutRouterMasking()
        {
            var service = new SaveService(TestSlotId, _saveRoot);
            var metadata = service.CreateUiMetadata(TestSlotId, new CharacterCustomization(), 2026051301, 2026051302);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);
            yield return LoadScene("Town");
            yield return WaitForTownRuntime(10f);

            var runtime = FindRuntime();
            var route = FindGatherRoute(runtime.Provider);
            var item = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);
            var node = FindTargetResourceNode(route.Objective.TargetResource);
            Assert.IsNotNull(node, "Town must install a quest resource node for the gather objective.");

            runtime.Player.transform.position = node.transform.position + new Vector3(0.35f, 0f, 0f);
            yield return null;
            runtime.Router.RefreshPromptNow();
            Assert.IsNull(runtime.Router.CurrentInteractable, "Quest resource position is masked by " + DescribeInteractable(runtime.Router.CurrentInteractable) + " prompt='" + runtime.Router.PromptText + "'.");

            int gathered = 0;
            int broken = 0;
            node.OnGathered += (_, __) => gathered++;
            node.OnBroken += _ => broken++;
            int before = runtime.Inventory.Inventory.CountOf(item);
            for (int i = 0; i < 80 && runtime.Inventory.Inventory.CountOf(item) <= before; i++)
            {
                runtime.Gather.TriggerInteract();
                yield return null;
            }

            Assert.Greater(gathered, 0, "GatherInteractor did not call ResourceNode.Hit for the nearest quest resource.");
            Assert.Greater(broken, 0, "Quest resource node was hit but never broke.");
            Assert.Greater(runtime.Inventory.Inventory.CountOf(item), before, "Broken quest resource did not add its configured drop to inventory.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op);
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var npc = GameObject.Find("GuideNpc");
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<GatherInteractor>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && npc != null && npc.GetComponent<QuestProvider>() != null && dialoguePanel != null && questLogPanel != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Town runtime did not expose resource interaction objects within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            var npcGo = GameObject.Find("GuideNpc");
            Assert.IsNotNull(player);
            Assert.IsNotNull(npcGo);
            return new RuntimeRefs(player, player.GetComponent<GatherInteractor>(), player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), npcGo.GetComponent<QuestProvider>());
        }

        private static GatherRoute FindGatherRoute(QuestProvider provider)
        {
            Assert.IsNotNull(provider);
            for (int i = 0; i < provider.Quests.Length; i++)
            {
                var quest = provider.Quests[i];
                if (quest == null || quest.Objectives == null) continue;
                for (int j = 0; j < quest.Objectives.Length; j++) if (quest.Objectives[j] is GatherQuestObjective objective && objective.TargetResource != null) return new GatherRoute(quest, objective);
            }
            Assert.Fail("No gather quest route found on GuideNpc.");
            return default;
        }

        private static ResourceNode FindTargetResourceNode(ResourceNodeDefinition targetResource)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++) if (nodes[i] != null && nodes[i].Definition == targetResource && !nodes[i].IsBroken) return nodes[i];
            return null;
        }

        private static ItemDefinition ResolveFirstDropItem(PlayerInventory inventory, ResourceNodeDefinition resource)
        {
            var item = inventory.FindById(resource.Drops[0].ResourceId);
            Assert.IsNotNull(item);
            return item;
        }

        private static string DescribeInteractable(IPlayerInteractable interactable)
        {
            return interactable == null ? "<none>" : interactable.GetType().FullName;
        }

        private readonly struct GatherRoute
        {
            public readonly QuestDefinition Quest;
            public readonly GatherQuestObjective Objective;
            public GatherRoute(QuestDefinition quest, GatherQuestObjective objective)
            {
                Quest = quest;
                Objective = objective;
            }
        }

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly GatherInteractor Gather;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly QuestProvider Provider;
            public RuntimeRefs(GameObject player, GatherInteractor gather, PlayerInteractionRouter router, PlayerInventory inventory, QuestProvider provider)
            {
                Player = player;
                Gather = gather;
                Router = router;
                Inventory = inventory;
                Provider = provider;
            }
        }
    }
}
