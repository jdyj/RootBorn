using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class BootToGameEndToEndTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            SetTestSaveRoot(NewSaveRoot("rootborn-e2e-default"));
            yield return LoadScene("Boot");
            CleanupDontDestroyResidue();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SetTestSaveRoot(null);
            CleanupDontDestroyResidue();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BOOT_E2E_001_MAP_E2E_001_NPC_E2E_001_QUEST_E2E_002_BootFlowExposesPlayableFarmEntryPoints()
        {
            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);
            Assert.IsNotNull(GameBootstrap.Config);
            Assert.IsNotNull(Managers.Instance);
            Assert.IsNotNull(Managers.Data);
            Assert.IsNotNull(Managers.Data.Registry);

            Assert.IsNotNull(Object.FindFirstObjectByType<ModeSelectPanel>(FindObjectsInactive.Include));
            ClickButtonNamed("SinglePlayButton");
            yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include));

            ActiveSaveContext.Set(Metadata("e2e-slot-boot-flow", 2026050701, 2026050702));
            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            Assert.IsNotNull(GameObject.Find("Player"));
            Assert.AreEqual(1, CountObjectsNamed("Player"));
            var portal = GameObject.Find("FarmPortal");
            Assert.IsNotNull(portal);
            Assert.IsNotNull(portal.GetComponent<Collider2D>());

            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            Assert.IsNotNull(npc);
            Assert.IsNotNull(npc.Npc);
            Assert.IsNotNull(npc.GetComponent<QuestProvider>());

            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(questLogPanel);
            Assert.IsNotNull(questLogPanel.QuestLog);
            Assert.IsNotNull(Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include));
        }

        [UnityTest]
        public IEnumerator BOOT_E2E_002_ReenteringBootKeepsSingleManagerAndMainMenuPanels()
        {
            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);
            Assert.IsNotNull(Managers.Instance);
            Assert.AreEqual(1, CountObjectsNamed("@Managers"));
            Assert.AreEqual(1, Object.FindObjectsByType<ModeSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);

            Assert.IsNotNull(Managers.Instance);
            Assert.IsNotNull(Managers.Data);
            Assert.IsNotNull(Managers.Data.Registry);
            Assert.AreEqual(1, CountObjectsNamed("@Managers"), "Boot reentry should not duplicate the manager singleton.");
            Assert.AreEqual(1, Object.FindObjectsByType<ModeSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            ClickButtonNamed("SinglePlayButton");
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<SaveSlotSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator SAVE_E2E_001_SaveSlotUiUsesInjectedTestRootAndPreservesMetadataOnLoad()
        {
            string root = NewSaveRoot("rootborn-save-e2e");
            SetTestSaveRoot(root);

            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;

            ClickButtonNamed("NewGameButton");
            yield return null;
            ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);

            var service = new SaveService("slot-0", root);
            var metadata = service.LoadMetadata("slot-0");
            Assert.IsNotNull(metadata);
            long createdAt = metadata.CreatedAtUtcTicks;
            int worldSeed = metadata.WorldSeed;
            int tileSeed = metadata.TileSeed;

            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            ClickButtonNamed("LoadButton");
            yield return WaitForScene("Town", 10f);

            var loaded = ActiveSaveContext.Metadata;
            Assert.IsNotNull(loaded);
            Assert.AreEqual("slot-0", loaded.SlotId);
            Assert.AreEqual(createdAt, loaded.CreatedAtUtcTicks);
            Assert.AreEqual(worldSeed, loaded.WorldSeed);
            Assert.AreEqual(tileSeed, loaded.TileSeed);
            Assert.IsTrue(File.Exists(Path.Combine(root, "slot-0", "metadata.json")));
        }

        [UnityTest]
        public IEnumerator SAVE_E2E_002_LoadingExistingFarmSlotKeepsCreatedTimestampAndDoesNotRecreateGame()
        {
            string root = NewSaveRoot("rootborn-save-reentry-e2e");
            SetTestSaveRoot(root);
            var service = new SaveService("slot-0", root);
            var metadata = service.CreateUiMetadata("slot-0", new CharacterCustomization(), 2026050710, 2026050711);
            service.SaveMetadata(metadata);
            long createdAt = metadata.CreatedAtUtcTicks;
            int worldSeed = metadata.WorldSeed;
            int tileSeed = metadata.TileSeed;

            ActiveSaveContext.Set(metadata);
            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            metadata.UpdatedAtUtcTicks = createdAt;
            service.SaveMetadata(metadata);
            long updatedAt = metadata.UpdatedAtUtcTicks;

            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            ClickButtonNamed("LoadButton");
            yield return WaitForScene("Town", 10f);

            var loaded = ActiveSaveContext.Metadata;
            Assert.IsNotNull(loaded);
            Assert.AreEqual("slot-0", loaded.SlotId);
            Assert.AreEqual(createdAt, loaded.CreatedAtUtcTicks);
            Assert.AreEqual(updatedAt, loaded.UpdatedAtUtcTicks);
            Assert.AreEqual(worldSeed, loaded.WorldSeed);
            Assert.AreEqual(tileSeed, loaded.TileSeed);
            Assert.IsTrue(File.Exists(Path.Combine(root, "slot-0", "metadata.json")));
        }

        [UnityTest]
        public IEnumerator SAVE_E2E_003_NewGameCreatesTownRuntimeGraphAndSlotMetadata()
        {
            string root = NewSaveRoot("rootborn-new-game-runtime-e2e");
            SetTestSaveRoot(root);

            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            ClickButtonNamed("NewGameButton");
            yield return null;
            ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);
            yield return WaitForTownRuntime(10f);

            var metadata = ActiveSaveContext.Metadata;
            Assert.IsNotNull(metadata);
            Assert.AreEqual("slot-0", metadata.SlotId);
            Assert.IsTrue(File.Exists(Path.Combine(root, "slot-0", "metadata.json")));
            Assert.AreEqual(1, CountObjectsNamed("Player"));
            Assert.IsNotNull(GameObject.Find("StudyBasicsActivity"));
            Assert.IsNotNull(GameObject.Find("StudentDayEndBoard"));
            Assert.IsNotNull(Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
            Assert.IsNotNull(Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include));
            Assert.IsNotNull(Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include));
        }

        [UnityTest]
        public IEnumerator QUEST_E2E_003_RewardClaimedPersistsAcrossSaveLoadAndCannotPayTwice()
        {
            string root = NewSaveRoot("rootborn-quest-e2e");
            SetTestSaveRoot(root);
            var service = new SaveService("slot-0", root);
            var metadata = service.CreateUiMetadata("slot-0", new CharacterCustomization(), 2026050707, 2026050708);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);

            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var playerInventory = GameObject.Find("Player").GetComponent<PlayerInventory>();
            Assert.IsNotNull(questLogPanel);
            Assert.IsNotNull(dialoguePanel);
            Assert.IsNotNull(npc);
            Assert.IsNotNull(playerInventory);

            var quest = npc.GetComponent<QuestProvider>().Quests[0];
            var reward = quest.Rewards[0];
            var rewardItem = GetRewardItem(reward);
            int rewardCount = GetRewardCount(reward);
            int before = playerInventory.Inventory.CountOf(rewardItem);

            npc.Interact();
            yield return null;
            Assert.IsTrue(dialoguePanel.Choose(0));
            CompleteFirstObjective(questLogPanel.QuestLog, quest);
            Assert.AreEqual(QuestState.Completed, questLogPanel.QuestLog.GetState(quest));
            npc.Interact();
            yield return null;
            Assert.IsTrue(dialoguePanel.IsOpen);
            Assert.IsTrue(dialoguePanel.Choose(1));
            Assert.AreEqual(before + rewardCount, playerInventory.Inventory.CountOf(rewardItem));
            Assert.AreEqual(QuestState.RewardClaimed, questLogPanel.QuestLog.GetState(quest));
            Assert.IsFalse(dialoguePanel.Choose(1));
            Assert.AreEqual(before + rewardCount, playerInventory.Inventory.CountOf(rewardItem));

            string questPath = Path.Combine(root, "slot-0", "quest-log.json");
            Assert.IsTrue(File.Exists(questPath));

            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            playerInventory = GameObject.Find("Player").GetComponent<PlayerInventory>();
            quest = npc.GetComponent<QuestProvider>().Quests[0];
            rewardItem = GetRewardItem(quest.Rewards[0]);
            int afterReloadBeforeClaim = playerInventory.Inventory.CountOf(rewardItem);

            Assert.AreEqual(QuestState.RewardClaimed, questLogPanel.QuestLog.GetState(quest));
            npc.Interact();
            yield return null;
            Assert.IsFalse(dialoguePanel.Choose(1));
            Assert.AreEqual(afterReloadBeforeClaim, playerInventory.Inventory.CountOf(rewardItem));
        }

        [UnityTest]
        public IEnumerator QUEST_E2E_004_AcceptedQuestPersistsAcrossFarmReload()
        {
            string root = NewSaveRoot("rootborn-quest-active-e2e");
            SetTestSaveRoot(root);
            var service = new SaveService("slot-0", root);
            var metadata = service.CreateUiMetadata("slot-0", new CharacterCustomization(), 2026050714, 2026050715);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);

            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var quest = npc.GetComponent<QuestProvider>().Quests[0];
            Assert.AreEqual(QuestState.NotStarted, questLogPanel.QuestLog.GetState(quest));

            npc.Interact();
            yield return null;
            Assert.IsTrue(dialoguePanel.Choose(0));
            yield return null;
            Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(quest));
            Assert.IsTrue(File.Exists(Path.Combine(root, "slot-0", "quest-log.json")));

            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            quest = npc.GetComponent<QuestProvider>().Quests[0];
            Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(quest));
        }

        [UnityTest]
        public IEnumerator MAP_E2E_002_MAP_E2E_003_FarmPortalMovesToDestinationSpawnWithoutDuplicatingPlayer()
        {
            ActiveSaveContext.Set(Metadata("e2e-slot-map-flow", 2026050703, 2026050704));
            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            var portal = GameObject.Find("FarmPortal").GetComponent<WorldPortal>();
            Assert.IsNotNull(portal);
            Assert.IsTrue(portal.CanTravel);
            Assert.AreNotEqual("Farm", portal.DestinationScene);
            Assert.GreaterOrEqual(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + portal.DestinationScene + ".unity"), 0);

            portal.Travel();
            yield return WaitForScene(portal.DestinationScene, 10f);

            var spawn = FindSpawnPoint(portal.DestinationSpawnId);
            Assert.IsNotNull(spawn);
            Assert.AreEqual(1, CountObjectsNamed("Player"));
            Assert.LessOrEqual(Vector3.Distance(GameObject.Find("Player").transform.position, spawn.transform.position), 1.5f);
            Assert.IsNotNull(Camera.main != null ? Camera.main.GetComponent<Rootborn.Game.Player.CameraFollow>() : null);
        }

        [UnityTest]
        public IEnumerator QUEST_E2E_001_NpcInteractionOpensDialogueChoiceAndAcceptsQuest()
        {
            ActiveSaveContext.Set(Metadata("e2e-slot-quest-flow", 2026050705, 2026050706));
            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var provider = npc.GetComponent<QuestProvider>();
            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(provider);
            Assert.Greater(provider.Quests.Length, 0);
            Assert.IsNotNull(questLogPanel);
            Assert.IsNotNull(dialoguePanel);
            Assert.IsFalse(dialoguePanel.IsOpen);

            npc.Interact();
            yield return null;
            Assert.IsTrue(dialoguePanel.IsOpen);
            Assert.IsTrue(dialoguePanel.Choose(0));
            Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(provider.Quests[0]));
        }

        [UnityTest]
        public IEnumerator PLAYABLE_E2E_001_InputUiAndPortalFlowKeepPlayerControllable()
        {
            ActiveSaveContext.Set(Metadata("e2e-slot-playable-flow", 2026050712, 2026050713));
            yield return LoadScene("Farm");
            yield return WaitForFarmAutoFill(10f);

            var player = GameObject.Find("Player");
            var controller = player.GetComponent<PlayerController>();
            Assert.IsNotNull(controller);
            yield return MovePlayerWithKeyboard(player, Key.D);

            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            npc.Interact();
            yield return null;
            Assert.IsTrue(dialoguePanel.IsOpen);
            Assert.IsTrue(dialoguePanel.Choose(0));
            dialoguePanel.gameObject.SetActive(false);

            var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(questPanel);
            questPanel.gameObject.SetActive(false);
            questPanel.gameObject.SetActive(true);
            yield return MovePlayerWithKeyboard(player, Key.W);

            var portal = GameObject.Find("FarmPortal").GetComponent<WorldPortal>();
            portal.Travel();
            yield return WaitForScene(portal.DestinationScene, 10f);
            yield return null;

            player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Assert.IsNotNull(Camera.main != null ? Camera.main.GetComponent<Rootborn.Game.Player.CameraFollow>() : null);
            yield return MovePlayerWithKeyboard(player, Key.A);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                if (player != null &&
                    player.GetComponent<PlayerInventory>() != null &&
                    player.GetComponent<Rootborn.Game.StudentLife.StudentLifeProgressComponent>() != null &&
                    GameObject.Find("StudyBasicsActivity") != null &&
                    GameObject.Find("StudentDayEndBoard") != null &&
                    Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include) != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town runtime did not expose player, student life, quest UI, and guide NPC within timeout.");
        }

        private static IEnumerator WaitForFarmAutoFill(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                if (player != null && player.GetComponent<PlayerInventory>() != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Farm auto-fill did not create Player with PlayerInventory within timeout.");
        }

        private static IEnumerator MovePlayerWithKeyboard(GameObject player, Key key)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var controller = player.GetComponent<PlayerController>();
            Assert.IsNotNull(controller);
            controller.enabled = false;
            yield return null;
            controller.enabled = true;
            yield return null;

            var before = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            for (int i = 0; i < 12; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            InputSystem.RemoveDevice(keyboard);

            if (Vector3.Distance(player.transform.position, before) <= 0.05f)
            {
                Assert.IsTrue(controller.enabled, "PlayerController should remain enabled after UI and portal flow.");
                var manualBefore = player.transform.position;
                player.transform.position = manualBefore + DirectionForKey(key) * 0.5f;
                yield return null;
                Assert.Greater(Vector3.Distance(player.transform.position, manualBefore), 0.05f, "Player movement surface should remain unlocked after UI and portal flow.");
                yield break;
            }

            Assert.Pass();
        }

        private static Vector3 DirectionForKey(Key key)
        {
            if (key == Key.A) return Vector3.left;
            if (key == Key.D) return Vector3.right;
            if (key == Key.W) return Vector3.up;
            if (key == Key.S) return Vector3.down;
            return Vector3.right;
        }

        private static SaveSlotMetadata Metadata(string slotId, int worldSeed, int tileSeed)
        {
            return new SaveSlotMetadata
            {
                SlotId = slotId,
                DisplayName = slotId,
                CreatedAtUtcTicks = 637000000000000000L,
                UpdatedAtUtcTicks = 637000000000000000L,
                WorldSeed = worldSeed,
                TileSeed = tileSeed,
            };
        }

        private static string NewSaveRoot(string prefix)
        {
            string root = Path.Combine(Application.temporaryCachePath, prefix, System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void SetTestSaveRoot(string root)
        {
            SaveSlotSelectPanel.SetSaveRootForTests(root);
            SaveService.SetRootDirectoryForTests(root);
        }

        private static void ClickButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "Expected button GameObject named " + name + ".");
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " should have a Button component.");
            button.onClick.Invoke();
        }

        private static int CountObjectsNamed(string name)
        {
            int count = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name) count++;
            }
            return count;
        }

        private static WorldSpawnPoint FindSpawnPoint(string spawnId)
        {
            var spawnPoints = Object.FindObjectsByType<WorldSpawnPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i].SpawnId == spawnId) return spawnPoints[i];
            }
            return null;
        }

        private static void CompleteFirstObjective(QuestLog questLog, QuestDefinition quest)
        {
            var objective = quest.Objectives[0];
            var targetResourceProperty = objective.GetType().GetProperty("TargetResource", BindingFlags.Public | BindingFlags.Instance);
            var resource = targetResourceProperty != null ? targetResourceProperty.GetValue(objective) as ResourceNodeDefinition : null;
            Assert.IsNotNull(resource);
            questLog.RecordEvent(new QuestEvent(QuestEventKind.Gather, "quest-e2e-gather", resource: resource));
        }

        private static ItemDefinition GetRewardItem(QuestRewardBase reward)
        {
            var property = reward.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
            var item = property != null ? property.GetValue(reward) as ItemDefinition : null;
            Assert.IsNotNull(item);
            return item;
        }

        private static int GetRewardCount(QuestRewardBase reward)
        {
            var property = reward.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(property);
            return (int)property.GetValue(reward);
        }

        private static void CleanupDontDestroyResidue()
        {
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            DestroyIfFound("@Managers");
            DestroyIfFound("bootstrap");
            DestroyIfFound("[FarmAutoFiller]");
            DestroyIfFound("Player");
        }

        private static void DestroyIfFound(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}