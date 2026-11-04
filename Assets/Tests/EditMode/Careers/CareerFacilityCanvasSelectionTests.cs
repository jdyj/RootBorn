using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Careers;
using Rootborn.UI.Modern;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class CareerFacilityCanvasSelectionTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();
        private Scene _scene, _foreign;

        [SetUp]
        public void SetUp()
        {
            // Preview scenes do not require saving or changing the runner's untitled scene.
            // Candidates are passed explicitly to the selector; global scene discovery is not exercised here.
            _scene = EditorSceneManager.NewPreviewScene();
            _foreign = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            if (_foreign.IsValid() && EditorSceneManager.IsPreviewScene(_foreign)) EditorSceneManager.ClosePreviewScene(_foreign);
            if (_scene.IsValid() && EditorSceneManager.IsPreviewScene(_scene)) EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void SelectManagementCanvasSkipsInactiveDisabledWorldSpaceForeignSceneAndChildCanvases()
        {
            var inactive = CanvasAt(9000); inactive.gameObject.SetActive(false);
            var disabled = CanvasAt(8000); disabled.enabled = false;
            var world = CanvasAt(7000); world.renderMode = RenderMode.WorldSpace;
            var foreign = CanvasAt(6000, true);
            var outer = CanvasAt(5000);
            var child = CanvasAt(4000); child.transform.SetParent(outer.transform, false);
            var valid = CanvasAt(10);
            Assert.That(Select(new[] { null, inactive, disabled, world, foreign, child, valid }), Is.SameAs(valid),
                "Facility management must be hosted by a visible root screen-space overlay in the facility's scene.");
        }

        [Test]
        public void SharedModernInventoryRootWinsOverHigherSortingDebugOverlay()
        {
            var debug = CanvasAt(5000);
            var shared = CanvasAt(1000);
            var inventory = Child(shared, typeof(ModernUiInventoryPanel));
            inventory.SetActive(false);
            Assert.That(Select(new[] { debug, shared }), Is.SameAs(shared),
                "A closed inventory still identifies the game's common modal UI root.");
        }

        [Test]
        public void SharedModernRouterRootIsPreferredWithoutAnInventoryComponent()
        {
            var higher = CanvasAt(5000);
            var shared = CanvasAt(1000);
            var router = Child(shared, typeof(ModernUiPanelInputRouter));
            router.SetActive(false);
            Assert.That(Select(new[] { higher, shared }), Is.SameAs(shared));
        }

        [Test]
        public void ActiveOverlayFallbackUsesHighestSortingOrder()
        {
            var low = CanvasAt(10);
            var high = CanvasAt(95);
            var middle = CanvasAt(90);
            Assert.That(Select(new[] { low, high, middle }), Is.SameAs(high));
            Assert.That(Select(new[] { middle, low, high }), Is.SameAs(high),
                "Fallback choice must not depend on FindObjects enumeration order when sorting differs.");
        }

        [Test]
        public void MissingEligibleCanvasReturnsNullInsteadOfSelectingAnInvisiblePanelHost()
        {
            var inactive = CanvasAt(1000); inactive.gameObject.SetActive(false);
            var world = CanvasAt(5000); world.renderMode = RenderMode.WorldSpace;
            var cameraRoot = new GameObject("CanvasFixtureCamera", typeof(Camera));
            _owned.Add(cameraRoot);
            SceneManager.MoveGameObjectToScene(cameraRoot, _scene);
            var camera = CanvasAt(2000);
            camera.worldCamera = cameraRoot.GetComponent<Camera>();
            camera.renderMode = RenderMode.ScreenSpaceCamera;
            Assert.That(camera.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera),
                "The rejected camera-space candidate must have a real camera rather than effectively falling back to overlay rendering.");
            Assert.That(Select(new[] { inactive, world, camera }), Is.Null);
            Assert.That(Select(Array.Empty<Canvas>()), Is.Null);
            Assert.That(Select(null), Is.Null);
        }

        private Canvas Select(Canvas[] candidates)
        {
            var method = typeof(CareerFacilityInteractor).GetMethod("SelectManagementCanvas", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Canvas[]), typeof(Scene) }, null);
            Assert.That(method, Is.Not.Null,
                "Career facility interaction needs explicit visible modal-canvas selection before creating or showing its management panel.");
            return (Canvas)method.Invoke(null, new object[] { candidates, _scene });
        }

        private Canvas CanvasAt(int sortingOrder, bool foreign = false)
        {
            var go = new GameObject("CanvasFixture_" + _owned.Count, typeof(RectTransform), typeof(Canvas));
            _owned.Add(go);
            SceneManager.MoveGameObjectToScene(go, foreign ? _foreign : _scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            return canvas;
        }

        private static GameObject Child(Canvas parent, Type component)
        {
            var child = new GameObject("SharedUiFixture", typeof(RectTransform), component);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
