using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseDesignPortalPlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseDesignPortals_LeftDoorTravelsToCompactCondominiumDesignScene()
        {
            yield return LoadScene("House");
            yield return WaitForHouseDesignPortalRuntime("HouseDesignPortal_CondominiumDesign2", 10f);

            yield return InteractWithPortal(
                "HouseDesignPortal_CondominiumDesign2",
                "House_Condominium_Design_2",
                84,
                new Vector3(0f, -1.2f, 0f));
        }

        [UnityTest]
        public IEnumerator HouseDesignPortals_RightDoorTravelsToApartmentCondominiumDesignScene()
        {
            yield return LoadScene("House");
            yield return WaitForHouseDesignPortalRuntime("HouseDesignPortal_CondominiumDesign", 10f);

            yield return InteractWithPortal(
                "HouseDesignPortal_CondominiumDesign",
                "House_Condominium_Design",
                154,
                new Vector3(0f, -1.2f, 0f));
        }

        private static IEnumerator InteractWithPortal(string portalName, string destinationScene, int expectedFloorTiles, Vector3 approachOffset)
        {
            var portal = GameObject.Find(portalName);
            var player = GameObject.Find("Player");
            Assert.IsNotNull(portal, "House should expose design portal " + portalName + ".");
            Assert.IsNotNull(player, "House should expose Player for user-facing portal interaction.");
            Assert.IsNotNull(portal.GetComponent<WorldPortal>(), portalName + " should use WorldPortal.");

            var router = player.GetComponent<PlayerInteractionRouter>();
            Assert.IsNotNull(router, "Portal travel must use the existing player interaction router.");

            player.transform.position = portal.transform.position + approachOffset;
            Physics2D.SyncTransforms();
            yield return null;

            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, "Portal prompt should be visible before travelling through " + portalName + ".");
            StringAssert.Contains("Enter", router.PromptText);
            Assert.IsTrue(router.TryInteractWithNearest(), "Portal interaction should be accepted for " + portalName + ".");

            yield return WaitForScene(destinationScene, 10f);
            yield return null;
            yield return null;

            var floor = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(floor, destinationScene + " should expose a visible floor Tilemap.");
            Assert.AreEqual(expectedFloorTiles, CountTiles(floor), destinationScene + " should show the expected Home Design layer_1 tile count.");

            var spawn = FindSpawnPoint("house-design-entry");
            Assert.IsNotNull(spawn, destinationScene + " should expose the design-entry spawn point.");
            Assert.LessOrEqual(Vector3.Distance(GameObject.Find("Player").transform.position, spawn.transform.position), 0.05f, "Player should arrive at the design scene spawn through the portal flow.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForHouseDesignPortalRuntime(string portalName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var portal = GameObject.Find(portalName);
                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && portal != null && portal.GetComponent<WorldPortal>() != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("House runtime did not expose Player, PlayerInteractionRouter, and " + portalName + " within timeout.");
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
                if (spawnPoints[i].SpawnId == spawnId)
                {
                    return spawnPoints[i];
                }
            }

            return null;
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
