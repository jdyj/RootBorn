using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.World
{
    public sealed class PlayerWorldInteractionScenarioTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator INTERACT_PM_001_NearNpcShowsTalkPromptAndInteractOpensDialogue()
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            var router = player.AddComponent<PlayerInteractionRouter>();
            router.ConfigureForTests(2f);

            var dialoguePanelGo = new GameObject("DialoguePanel");
            var dialoguePanel = dialoguePanelGo.AddComponent<DialoguePanel>();
            dialoguePanelGo.SetActive(false);
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            SetField(npcDefinition, "_defaultDialogue", dialogue);

            var npc = new GameObject("GuideNpc");
            npc.transform.position = new Vector3(1f, 0f, 0f);
            npc.AddComponent<CircleCollider2D>().isTrigger = true;
            var interactor = npc.AddComponent<NpcInteractor>();
            interactor.Bind(npcDefinition);
            interactor.OnInteracted += _ => dialoguePanel.Open(dialogue, default);

            yield return null;
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible);
            StringAssert.Contains("Talk", router.PromptText);
            Assert.AreSame(interactor, router.CurrentInteractable);

            Assert.IsTrue(router.TryInteractWithNearest());
            yield return null;

            Assert.IsTrue(dialoguePanel.IsOpen);

            Object.Destroy(player);
            Object.Destroy(npc);
            Object.Destroy(dialoguePanelGo);
        }

        [UnityTest]
        public IEnumerator INTERACT_PM_002_NearPortalShowsEnterPromptAndCanBeSelectedOverFarNpc()
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            var router = player.AddComponent<PlayerInteractionRouter>();
            router.ConfigureForTests(2f);

            var portalGo = new GameObject("FarmPortal");
            portalGo.transform.position = new Vector3(0.5f, 0f, 0f);
            portalGo.AddComponent<CircleCollider2D>().isTrigger = true;
            var portal = portalGo.AddComponent<WorldPortal>();
            portal.Bind("Farm Gate", "Town", "town-entry");

            var npcDefinition = ScriptableObject.CreateInstance<NpcDefinition>();
            var npc = new GameObject("GuideNpc");
            npc.transform.position = new Vector3(1.5f, 0f, 0f);
            npc.AddComponent<CircleCollider2D>().isTrigger = true;
            var interactor = npc.AddComponent<NpcInteractor>();
            interactor.Bind(npcDefinition);

            yield return null;
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible);
            StringAssert.Contains("Enter", router.PromptText);
            Assert.AreSame(portal, router.CurrentInteractable);

            Object.Destroy(player);
            Object.Destroy(portalGo);
            Object.Destroy(npc);
        }

        [UnityTest]
        public IEnumerator INTERACT_PM_005_PromptStaysOnVisibleTwoDimensionalPlane()
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            var router = player.AddComponent<PlayerInteractionRouter>();
            router.ConfigureForTests(2f);

            var portalGo = new GameObject("FarmPortal");
            portalGo.transform.position = new Vector3(0.5f, 0f, 0f);
            portalGo.AddComponent<CircleCollider2D>().isTrigger = true;
            portalGo.AddComponent<WorldPortal>().Bind("Farm Gate", "Town", "town-entry");

            yield return null;
            router.RefreshPromptNow();

            var prompt = player.transform.Find("[InteractionPrompt]");
            Assert.IsNotNull(prompt);
            Assert.IsTrue(router.PromptVisible);
            Assert.AreEqual(0f, prompt.position.z, 0.001f, "Interaction prompt must remain on the 2D render plane so the gameplay camera can show it.");

            Object.Destroy(player);
            Object.Destroy(portalGo);
        }

        [UnityTest]
        public IEnumerator INTERACT_PM_003_FarmRuntimeNpcPromptAndGatherInteractorOpenDialogue()
        {
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "interact-runtime-npc", DisplayName = "Interact NPC", WorldSeed = 1, TileSeed = 2 });
            yield return LoadScene("Farm");
            yield return WaitForFarmInteractionRuntime();

            var player = GameObject.Find("Player");
            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            var router = player.GetComponent<PlayerInteractionRouter>();
            var gather = player.GetComponent<GatherInteractor>();

            Assert.IsNotNull(router);
            Assert.IsNotNull(gather);
            Assert.IsNotNull(npc);
            Assert.IsNotNull(dialoguePanel);

            player.transform.position = npc.transform.position + new Vector3(1f, 0f, 0f);
            yield return null;
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible);
            StringAssert.Contains("Talk", router.PromptText);

            gather.TriggerInteract();
            yield return null;

            Assert.IsTrue(dialoguePanel.IsOpen);
        }

        [UnityTest]
        public IEnumerator INTERACT_PM_004_FarmRuntimePortalPromptAndGatherInteractorTravelToTown()
        {
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "interact-runtime-portal", DisplayName = "Interact Portal", WorldSeed = 3, TileSeed = 4 });
            yield return LoadScene("Farm");
            yield return WaitForFarmInteractionRuntime();

            var player = GameObject.Find("Player");
            var portal = GameObject.Find("FarmPortal").GetComponent<WorldPortal>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var gather = player.GetComponent<GatherInteractor>();

            Assert.IsNotNull(router);
            Assert.IsNotNull(gather);
            Assert.IsNotNull(portal);

            player.transform.position = portal.transform.position + new Vector3(1.2f, 0f, 0f);
            yield return null;
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible);
            StringAssert.Contains("Enter", router.PromptText);

            string destinationScene = portal.DestinationScene;
            gather.TriggerInteract();
            yield return WaitForScene(destinationScene, 10f);

            Assert.AreEqual("Town", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, CountObjectsNamed("Player"));
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

        private static IEnumerator WaitForFarmInteractionRuntime()
        {
            for (int i = 0; i < 600; i++)
            {
                var player = GameObject.Find("Player");
                var portal = GameObject.Find("FarmPortal");
                var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                if (player != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    portal != null && portal.GetComponent<WorldPortal>() != null &&
                    npc != null &&
                    dialoguePanel != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Farm runtime did not expose PlayerInteractionRouter, NPC, dialogue panel, and FarmPortal within timeout.");
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

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}
