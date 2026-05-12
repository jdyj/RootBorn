using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.World
{
    public sealed class WorldPortalTriggerTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Set(new SaveSlotMetadata
            {
                SlotId = "portal-trigger-test-slot",
                DisplayName = "Portal Trigger Test",
                WorldSeed = 2026050801,
                TileSeed = 2026050802
            });
            yield return LoadScene("Farm");
            yield return WaitForFarmRuntime(10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MAP_PORTAL_001_PlayerInteractsWithFarmPortalTravelsToTownSpawnAndGuideNpc()
        {
            var portal = GameObject.Find("FarmPortal").GetComponent<WorldPortal>();
            var player = GameObject.Find("Player");
            Assert.IsNotNull(portal);
            Assert.IsNotNull(player);
            Assert.IsTrue(portal.CanTravel);
            Assert.AreEqual("Town", portal.DestinationScene);

            var router = player.GetComponent<PlayerInteractionRouter>();
            Assert.IsNotNull(router, "Farm portal travel should use the same player interaction route as gameplay input.");

            string destinationScene = portal.DestinationScene;
            string destinationSpawnId = portal.DestinationSpawnId;
            player.transform.position = portal.transform.position + new Vector3(1.35f, 0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;

            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, "Portal interaction prompt should be visible when the player is in range.");
            StringAssert.Contains("Enter", router.PromptText);
            Assert.IsTrue(router.TryInteractWithNearest(), "Portal travel should happen through the player interaction router.");
            yield return WaitForScene(destinationScene, 10f);
            yield return WaitForTownNpcRuntime(10f);

            var spawn = FindSpawnPoint(destinationSpawnId);
            var guideNpc = GameObject.Find("GuideNpc");
            Assert.IsNotNull(spawn);
            Assert.IsNotNull(guideNpc, "Farm portal destination should expose the Town GuideNpc.");
            Assert.IsNotNull(guideNpc.GetComponent<NpcInteractor>(), "Town GuideNpc should be interactable after portal travel.");
            Assert.IsNotNull(guideNpc.GetComponent<QuestProvider>(), "Town GuideNpc should keep quest provider after portal travel.");
            Assert.AreEqual(1, CountObjectsNamed("Player"));
            Assert.LessOrEqual(Vector3.Distance(GameObject.Find("Player").transform.position, spawn.transform.position), 1.5f);
            Assert.LessOrEqual(Vector3.Distance(guideNpc.transform.position, spawn.transform.position), 3f, "Town portal spawn should place the player close enough to reach GuideNpc intentionally.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForFarmRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && GameObject.Find("FarmPortal") != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Farm runtime did not expose Player, PlayerInteractionRouter, and FarmPortal within timeout.");
        }

        private static IEnumerator WaitForTownNpcRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var guideNpc = GameObject.Find("GuideNpc");
                if (guideNpc != null && guideNpc.GetComponent<NpcInteractor>() != null && guideNpc.GetComponent<QuestProvider>() != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town runtime did not expose GuideNpc with NpcInteractor and QuestProvider within timeout.");
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

        private static WorldSpawnPoint FindSpawnPoint(string spawnId)
        {
            var spawnPoints = Object.FindObjectsByType<WorldSpawnPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i].SpawnId == spawnId) return spawnPoints[i];
            }

            return null;
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
    }
}
