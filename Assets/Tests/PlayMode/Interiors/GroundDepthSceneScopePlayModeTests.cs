using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Interiors;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class GroundDepthSceneScopePlayModeTests
    {
        [UnityTest]
        public IEnumerator PersistentTownPlayer_OnlyDepthSortsInHouse_AndRestoresUngroupedTownOrder()
        {
            var previous = SceneManager.GetActiveScene();
            var town = SceneManager.CreateScene("DepthScopeTownFixture");
            var house = SceneManager.CreateScene("HouseInterior");
            var player = new GameObject("ScopedDepthPlayer");
            GameObject reloaded = null;
            try
            {
                SceneManager.SetActiveScene(town);
                Assert.AreEqual(town, SceneManager.GetActiveScene());
                Object.DontDestroyOnLoad(player);
                var body = new GameObject("Body", typeof(SpriteRenderer));
                body.transform.SetParent(player.transform, false);
                body.GetComponent<SpriteRenderer>().sortingOrder = 0;
                var hair = new GameObject("Hair", typeof(SpriteRenderer));
                hair.transform.SetParent(player.transform, false);
                hair.GetComponent<SpriteRenderer>().sortingOrder = 4;
                player.transform.position = new Vector3(7.25f, -5.5f, 0f);
                Reinforce(player);
                var group = player.GetComponent<SortingGroup>();
                Assert.IsNotNull(group);
                Assert.IsFalse(group.enabled, "A newly introduced group must not lift the player above Town foregrounds.");
                Reinforce(player);
                Assert.IsFalse(group.enabled);

                SceneManager.SetActiveScene(house);
                Assert.AreEqual(house, SceneManager.GetActiveScene());
                yield return null;
                Assert.AreNotEqual(house, player.scene, "This test must exercise a persistent player, not its owning scene.");
                Assert.IsTrue(group.enabled);
                Assert.AreEqual(10288, group.sortingOrder);
                Assert.Less(group.sortingOrder, 10312, "Behind the toy kitchen.");
                string serializedScope = JsonUtility.ToJson(player.GetComponent<GroundDepthSort>());
                player.transform.position = new Vector3(7.25f, -7f, 0f);
                yield return null;
                Assert.AreEqual(10360, group.sortingOrder);
                Assert.Greater(group.sortingOrder, 10312, "In front of the toy kitchen.");

                SceneManager.SetActiveScene(town);
                Assert.AreEqual(town, SceneManager.GetActiveScene());
                yield return null;
                Assert.IsFalse(group.enabled);
                Assert.AreEqual(0, group.sortingOrder);
                Reinforce(player);
                Assert.IsFalse(group.enabled, "A second reinforcement must not overwrite the original fallback with the temporary house state.");
                player.transform.position = new Vector3(1.5f, 1.5f, 0f);
                yield return null;
                Assert.IsFalse(group.enabled, "Positive-Y destinations keep their original ordering rather than forcing a global band.");
                Assert.AreEqual(0, body.GetComponent<SpriteRenderer>().sortingOrder);
                Assert.AreEqual(4, hair.GetComponent<SpriteRenderer>().sortingOrder);

                SceneManager.SetActiveScene(house);
                reloaded = new GameObject("DirectHouseSerializedPlayer", typeof(SortingGroup));
                Object.DontDestroyOnLoad(reloaded);
                reloaded.transform.position = new Vector3(7.25f, -5.5f, 0f);
                var reloadedGroup = reloaded.GetComponent<SortingGroup>();
                reloadedGroup.enabled = true;
                reloadedGroup.sortingOrder = 10600;
                var reloadedSort = reloaded.AddComponent<GroundDepthSort>();
                JsonUtility.FromJsonOverwrite(serializedScope, reloadedSort);
                reloadedSort.RefreshSorting();
                Assert.IsTrue(reloadedGroup.enabled);
                Assert.AreEqual(10288, reloadedGroup.sortingOrder);
                SceneManager.SetActiveScene(town);
                yield return null;
                Reinforce(reloaded);
                Assert.IsFalse(reloadedGroup.enabled, "A serialized direct-house player must retain its original no-group fallback on Town return.");
                Assert.AreEqual(0, reloadedGroup.sortingOrder);
            }
            finally
            {
                Object.DestroyImmediate(player);
                if (reloaded != null) Object.DestroyImmediate(reloaded);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                SceneManager.UnloadSceneAsync(house);
                SceneManager.UnloadSceneAsync(town);
            }
        }

        [UnityTest]
        public IEnumerator ExistingSortingGroup_RestoresItsEnabledFlagAndOrder_AfterRepeatedHouseTransitions()
        {
            var previous = SceneManager.GetActiveScene();
            var outside = SceneManager.CreateScene("DepthScopeExistingGroupFixture");
            var house = SceneManager.CreateScene("HouseInterior");
            GameObject player = null;
            try
            {
                foreach (bool wasEnabled in new[] { true, false })
                {
                    SceneManager.SetActiveScene(outside);
                    Assert.AreEqual(outside, SceneManager.GetActiveScene());
                    player = new GameObject("ExistingGroupPlayer", typeof(SortingGroup));
                    Object.DontDestroyOnLoad(player);
                    player.transform.position = new Vector3(0f, -5.5f, 0f);
                    var group = player.GetComponent<SortingGroup>();
                    group.enabled = wasEnabled;
                    group.sortingOrder = 450;
                    Reinforce(player);
                    Assert.AreEqual(wasEnabled, group.enabled);
                    Assert.AreEqual(450, group.sortingOrder, "Capture must happen before AddComponent invokes GroundDepthSort.OnEnable.");
                    for (int visit = 0; visit < 2; visit++)
                    {
                        SceneManager.SetActiveScene(house);
                        yield return null;
                        Assert.IsTrue(group.enabled);
                        Assert.AreEqual(10288, group.sortingOrder);
                        Reinforce(player); // Reconfiguration inside the temporary band must not become the fallback.
                        SceneManager.SetActiveScene(outside);
                        yield return null;
                        Assert.AreEqual(wasEnabled, group.enabled);
                        Assert.AreEqual(450, group.sortingOrder);
                    }
                    Object.DestroyImmediate(player);
                    player = null;
                }
            }
            finally
            {
                if (player != null) Object.DestroyImmediate(player);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                SceneManager.UnloadSceneAsync(house);
                SceneManager.UnloadSceneAsync(outside);
            }
        }

        private static void Reinforce(GameObject player)
        {
            var method = typeof(TownPlayableBaselineRuntimeInstaller).GetMethod("ReinforcePlayer", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { player });
        }
    }
}
