using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Interiors;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class TownPlayerGroundDepthSortTests
    {
        private Scene _fixtureScene;

        [SetUp]
        public void SetUp()
        {
            // This Test Runner snapshots/restores the editor scene; its Untitled scene rejects additive NewScene.
            _fixtureScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.AreEqual(_fixtureScene, SceneManager.GetActiveScene());
            Assert.AreNotEqual("HouseInterior", _fixtureScene.name);
        }

        [TearDown]
        public void TearDown()
        {
            if (_fixtureScene.IsValid() && _fixtureScene.isLoaded) EditorSceneManager.CloseScene(_fixtureScene, true);
        }

        [Test]
        public void UnconfiguredSorter_RetainsExistingZeroBaseBehavior()
        {
            var go = new GameObject("ExistingSortBehaviorFixture");
            try
            {
                go.transform.position = new Vector3(0f, -6.5f, 0f);
                go.AddComponent<GroundDepthSort>().RefreshSorting();
                Assert.AreEqual(312, go.GetComponent<SortingGroup>().sortingOrder);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TownReinforcement_WithoutOriginalGroup_PreservesUngroupedPartOrderOutsideHouse()
        {
            var player = new GameObject("DepthSortPlayerFixture");
            try
            {
                player.SetActive(false);
                player.transform.position = new Vector3(7.25f, -5.5f, 0f);
                var body = new GameObject("PartBody", typeof(SpriteRenderer));
                body.transform.SetParent(player.transform, false);
                body.GetComponent<SpriteRenderer>().sortingOrder = 4;
                var hair = new GameObject("PartHair", typeof(SpriteRenderer));
                hair.transform.SetParent(player.transform, false);
                hair.GetComponent<SpriteRenderer>().sortingOrder = 8;
                Reinforce(player);
                Reinforce(player);
                Assert.AreEqual(1, player.GetComponents<GroundDepthSort>().Length);
                Assert.AreEqual(1, player.GetComponents<SortingGroup>().Length);
                var group = player.GetComponent<SortingGroup>();
                Assert.IsFalse(group.enabled, "Outside the scoped house, Town must retain the pre-existing ungrouped ordering.");
                Assert.AreEqual(0, group.sortingOrder);
                Assert.AreEqual(4, body.GetComponent<SpriteRenderer>().sortingOrder);
                Assert.AreEqual(8, hair.GetComponent<SpriteRenderer>().sortingOrder);
            }
            finally { Object.DestroyImmediate(player); }
        }

        [TestCase(0)]
        [TestCase(-48)]
        public void Configure_ClampsInvalidPrecisionAndImmediatelyRefreshesTheConfiguredFoot(int units)
        {
            var go = new GameObject("GroundDepthSortConfigurationFixture");
            try
            {
                go.transform.position = new Vector3(0f, -10.5f, 0f);
                var sorter = go.AddComponent<GroundDepthSort>();
                sorter.Configure(new Vector2(0f, -0.5f), units, 0);
                Assert.AreEqual(11, go.GetComponent<SortingGroup>().sortingOrder);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TownReinforcement_AtPositiveY_PreservesAnExistingGroupInsteadOfForcingDepthBand()
        {
            var player = new GameObject("PositiveYDepthFixture", typeof(SortingGroup));
            try
            {
                player.SetActive(false);
                player.transform.position = new Vector3(1.5f, 1.5f, 0f);
                var original = player.GetComponent<SortingGroup>();
                original.enabled = true;
                original.sortingOrder = 300;
                Reinforce(player);
                Reinforce(player);
                Assert.IsTrue(original.enabled);
                Assert.AreEqual(300, original.sortingOrder, "The original outside-house group order must survive reinforcement and positive Y.");
            }
            finally { Object.DestroyImmediate(player); }
        }

        private static void Reinforce(GameObject player)
        {
            var method = typeof(TownPlayableBaselineRuntimeInstaller).GetMethod("ReinforcePlayer", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { player });
        }
    }
}
