using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Resources;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    public sealed class FarmFlowTests
    {
        [SetUp]
        public void CleanupBeforeEach()
        {
            var leftover = GameObject.Find("Player");
            if (leftover != null) Object.DestroyImmediate(leftover);
            var resRoot = GameObject.Find("[Resources]");
            if (resRoot != null) Object.DestroyImmediate(resRoot);
            var grid = GameObject.Find("[Grid]");
            if (grid != null) Object.DestroyImmediate(grid);
            var farmFiller = GameObject.Find("[FarmAutoFiller]");
            if (farmFiller != null) Object.DestroyImmediate(farmFiller);
            var water = GameObject.Find("[WaterSurface]");
            if (water != null) Object.DestroyImmediate(water);
        }

        [UnityTest]
        public IEnumerator FarmScene_AutoFills_Tilemap_Resources_Player()
        {
            yield return LoadFarmAndBootstrap();

            var tilemaps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(tilemaps.Length, 1, "Ground tilemap should exist.");

            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            int trees = 0, rocks = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition == null) continue;
                if (nodes[i].Definition.Id == "Tree") trees++;
                else if (nodes[i].Definition.Id == "Rock") rocks++;
            }
            Assert.AreEqual(12, trees, "Expected 12 trees in Farm.");
            Assert.AreEqual(8, rocks, "Expected 8 rocks in Farm.");

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player should be spawned.");
            var pc = player.GetComponent<PlayerController>();
            Assert.IsNotNull(pc, "Player should have PlayerController.");
            var gi = player.GetComponent<GatherInteractor>();
            Assert.IsNotNull(gi, "Player should have GatherInteractor.");

            Assert.IsNotNull(gi.KnowledgeProgress, "GatherInteractor should have KnowledgeProgress bound.");
        }

        [UnityTest]
        public IEnumerator FarmWaterSurfaceInstaller_CreatesWaterSurfaceZoneForFishing()
        {
            yield return LoadFarmAndBootstrap();

            DestroyWaterSurfaceZones();
            FarmWaterSurfaceInstaller.EnsureWaterSurfaceForActiveScene();
            yield return null;

            var zone = Object.FindFirstObjectByType<SurfaceTagZone>();
            Assert.IsNotNull(zone, "Farm should include a surface tag zone for target-surface tool effects.");
            Assert.AreEqual("Water", zone.Surface);
            Assert.IsNotNull(zone.GetComponent<Collider2D>());
        }

        [UnityTest]
        public IEnumerator Player_Position_Persists_After_Manual_Translate()
        {
            yield return LoadFarmAndBootstrap();

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Vector3 start = player.transform.position;
            player.transform.position = start + new Vector3(3f, 0f, 0f);
            yield return null;
            Assert.AreEqual(start.x + 3f, player.transform.position.x, 0.001f);
        }

        [UnityTest]
        public IEnumerator Hit_Rock_Repeatedly_Unlocks_StoneTool_Knowledge()
        {
            yield return LoadFarmAndBootstrap();

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player should exist after auto-fill.");
            var gi = player.GetComponent<GatherInteractor>();
            Assert.IsNotNull(gi, "Player should have GatherInteractor.");
            Assert.IsNotNull(gi.KnowledgeProgress, "GatherInteractor should have KnowledgeProgress bound.");

            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode rock = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition != null && nodes[i].Definition.Id == "Rock")
                {
                    rock = nodes[i];
                    break;
                }
            }
            Assert.IsNotNull(rock, "At least one Rock should exist.");

            int unlockedCount = 0;
            KnowledgeNode lastUnlocked = null;
            gi.KnowledgeProgress.OnUnlocked += node =>
            {
                unlockedCount++;
                lastUnlocked = node;
            };

            for (int i = 0; i < 10; i++)
            {
                gi.KnowledgeProgress.RecordAction(
                    KnowledgeAction.HitGround,
                    gi.EquippedTool,
                    rock.Definition.Id,
                    rock.Definition.SurfaceTag);
            }
            yield return null;

            Assert.AreEqual(1, unlockedCount, "StoneTool knowledge should unlock exactly once.");
            Assert.IsNotNull(lastUnlocked);
            Assert.AreEqual("StoneTool", lastUnlocked.Id);
        }

        [UnityTest]
        public IEnumerator ResourceNode_Hit_Accumulates_And_Breaks()
        {
            yield return LoadFarmAndBootstrap();

            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode tree = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition != null && nodes[i].Definition.Id == "Tree")
                {
                    tree = nodes[i];
                    break;
                }
            }
            Assert.IsNotNull(tree);

            int brokenCount = 0;
            tree.OnBroken += _ => brokenCount++;

            for (int i = 0; i < 30 && !tree.IsBroken; i++)
            {
                tree.Hit(null);
            }
            yield return null;

            Assert.IsTrue(tree.IsBroken, "Tree should break after enough hits.");
            Assert.AreEqual(1, brokenCount);
        }

        private static IEnumerator LoadFarmAndBootstrap()
        {
            var asyncLoad = SceneManager.LoadSceneAsync("Farm");
            while (!asyncLoad.isDone) yield return null;

            var bootstrapTask = Managers.BootstrapAsync();
            float bootstrapTimeout = 10f;
            float elapsed = 0f;
            while (!bootstrapTask.IsCompleted && elapsed < bootstrapTimeout)
            {
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;
            }
            Assert.IsTrue(bootstrapTask.IsCompleted, "Managers.BootstrapAsync did not complete within timeout.");
            if (bootstrapTask.Exception != null) throw bootstrapTask.Exception;

            var go = new GameObject("[FarmAutoFiller]");
            go.AddComponent<FarmAutoFiller>();

            float timeout = 2f;
            elapsed = 0f;
            while (elapsed < timeout)
            {
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;
            }
        }

        private static void DestroyWaterSurfaceZones()
        {
            var zones = Object.FindObjectsByType<SurfaceTagZone>(FindObjectsSortMode.None);
            for (int i = 0; i < zones.Length; i++)
            {
                Object.DestroyImmediate(zones[i].gameObject);
            }
        }
    }
}
