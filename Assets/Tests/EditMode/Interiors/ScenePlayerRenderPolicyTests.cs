using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class ScenePlayerRenderPolicyTests
    {
        private Type _type;
        private Scene _scene;
        [SetUp] public void Setup()
        {
            _type = typeof(CameraFollow).Assembly.GetType("Rootborn.Game.Player.ScenePlayerRenderPolicy");
            Assert.That(_type, Is.Not.Null, "An explicit scene player rendering policy must group parts above ground and bind authored camera bounds.");
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        [TearDown] public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
        }
        [Test] public void SCENE_RENDER_001_ExplicitPolicyOverridesStaleHouseFallbackWithoutChangingInternalPartOrders()
        {
            var player = new GameObject("Player"); player.transform.position = new Vector3(10.5f, -14.5f, 0);
            var body = new GameObject("Part_body").AddComponent<SpriteRenderer>(); body.transform.SetParent(player.transform, false); body.sortingOrder = 0;
            var hat = new GameObject("Part_accessory").AddComponent<SpriteRenderer>(); hat.transform.SetParent(player.transform, false); hat.sortingOrder = 5;
            var sorter = player.AddComponent<GroundDepthSort>(); sorter.SetActiveSceneScope("HouseInterior", false, 0); sorter.Configure(new Vector2(0, -.5f), 48, 10000);
            Assert.That(player.GetComponent<SortingGroup>().enabled, Is.False, "Reproduce the native Town probe's stale house-only scope.");
            var bounds = Bounds(); var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.orthographic = true;
            var policy = Policy(bounds); Assert.That(Apply(policy, player, camera), Is.True); sorter.RefreshSorting();
            var group = player.GetComponent<SortingGroup>(); Assert.That(group.enabled, Is.True); Assert.That(group.sortingOrder, Is.GreaterThan(100), "Ground2 is sorting order100 in saved Town.");
            Assert.That(body.sortingOrder, Is.Zero); Assert.That(hat.sortingOrder, Is.EqualTo(5)); Assert.That(player.GetComponents<SortingGroup>().Length, Is.EqualTo(1));
        }
        [Test] public void SCENE_RENDER_002_ApplyBindsTargetAndAuthoredBoundsAndSnapsInsideViewport()
        {
            var player = new GameObject("Player"); player.transform.position = new Vector3(1, -1, 0);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 9; camera.aspect = 16f / 9f;
            var bounds = Bounds(); Assert.That(Apply(Policy(bounds), player, camera), Is.True);
            var follow = camera.GetComponent<CameraFollow>(); Assert.That(follow, Is.Not.Null);
            Assert.That(Field(follow, "_target"), Is.SameAs(player.transform)); Assert.That(Field(follow, "_bounds"), Is.SameAs(bounds));
            float halfWidth = camera.orthographicSize * camera.aspect;
            Assert.That(camera.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(bounds.bounds.min.x - .001f));
            Assert.That(camera.transform.position.y + camera.orthographicSize, Is.LessThanOrEqualTo(bounds.bounds.max.y + .001f));
            Assert.That(camera.transform.position.z, Is.EqualTo(-10));
        }
        [Test] public void SCENE_RENDER_003_UnconfiguredPolicyDoesNotChangeLegacyRendererOrCamera()
        {
            var player = new GameObject("LegacyPlayer"); var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.position = new Vector3(7, 8, -10);
            var policy = new GameObject("UnconfiguredPolicy").AddComponent(_type);
            Assert.That(Apply(policy, player, camera), Is.False); Assert.That(player.GetComponent<SortingGroup>(), Is.Null); Assert.That(camera.GetComponent<CameraFollow>(), Is.Null); Assert.That(camera.transform.position, Is.EqualTo(new Vector3(7, 8, -10)));
        }
        private Component Policy(BoxCollider2D bounds)
        {
            var policy = new GameObject("ScenePlayerRenderPolicy").AddComponent(_type);
            _type.GetMethod("Configure").Invoke(policy, new object[] { bounds, 10000, 48 }); return policy;
        }
        private bool Apply(Component policy, GameObject player, Camera camera) => (bool)_type.GetMethod("Apply").Invoke(policy, new object[] { player, camera });
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static BoxCollider2D Bounds()
        {
            var go = new GameObject("CameraBounds"); go.transform.position = new Vector3(45, -31.5f, 0); var bounds = go.AddComponent<BoxCollider2D>(); bounds.isTrigger = true; bounds.size = new Vector2(90, 63); return bounds;
        }
    }
}
