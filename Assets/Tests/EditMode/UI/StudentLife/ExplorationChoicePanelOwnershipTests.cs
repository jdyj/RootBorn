using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    // Presentation ownership only. No real exploration choice/outcome is executed.
    public sealed class ExplorationChoicePanelOwnershipTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<Scene> _scenes = new List<Scene>();
        private readonly List<string> _scenePaths = new List<string>();
        private readonly Dictionary<FieldInfo, object> _priorStatics = new Dictionary<FieldInfo, object>();
        private Scene _hostScene, _ownScene, _foreignScene;
        private SaveSlotMetadata _priorMetadata;
        private string _root, _slot, _player;
        private StudentLifeProgressComponent _student;
        private ExplorationProgress _progress;

        [SetUp]
        public void SetUp()
        {
            _hostScene = SceneManager.GetActiveScene();
            _priorMetadata = ActiveSaveContext.Metadata;
            Snapshot(typeof(SaveService), "s_rootDirectoryOverride");
            foreach (string name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" }) Snapshot(typeof(GameDataRegistryProvider), name);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False,
                "Isolation precondition: no active modal lease. Never clear or release another owner's lease.");
            _root = Path.Combine(Path.GetTempPath(), "rootborn-exploration-ui-" + Guid.NewGuid().ToString("N"));
            _slot = "exploration-" + Guid.NewGuid().ToString("N");
            _player = "fixture-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(_root);
            SaveService.SetRootDirectoryForTests(_root);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot });
            var registry = Own(ScriptableObject.CreateInstance<GameDataRegistry>());
            GameDataRegistryProvider.SetOverrideForTests(registry);
            _ownScene = SavedEmptyScene();
            _foreignScene = SavedEmptyScene();
            var actor = Go("FixtureStudent", _ownScene);
            _student = actor.AddComponent<StudentLifeProgressComponent>();
            _student.ConfigureForTests(_slot, _player, 12, 10, 0, 480);
            _progress = new ExplorationProgress(_slot, _player);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // Release and destroy only fixture-owned objects, including a sentinel owner on failure.
                foreach (var value in _objects) UiGameplayInputLock.Release(value);
                for (int i = _objects.Count - 1; i >= 0; i--)
                    if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
                _objects.Clear();
            }
            finally
            {
                try
                {
                    foreach (var scene in _scenes)
                    {
                        if (!scene.IsValid() || !scene.isLoaded) continue;
                        if (SceneManager.GetActiveScene() == scene && _hostScene.IsValid() && _hostScene.isLoaded)
                            SceneManager.SetActiveScene(_hostScene);
                        Assert.That(_scenePaths, Does.Contain(scene.path), "Close only owned saved scenes.");
                        Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
                    }
                    foreach (string path in _scenePaths)
                        if (File.Exists(path)) Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
                }
                finally
                {
                    _scenes.Clear(); _scenePaths.Clear();
                    ActiveSaveContext.Set(_priorMetadata);
                    foreach (var pair in _priorStatics) pair.Key.SetValue(null, pair.Value);
                    _priorStatics.Clear();
                    if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
                    {
                        string absolute = Path.GetFullPath(_root);
                        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        Assert.That(absolute.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                            && Path.GetFileName(absolute).StartsWith("rootborn-exploration-ui-", StringComparison.Ordinal), Is.True);
                        Directory.Delete(absolute, true);
                    }
                }
            }
        }

        [Test]
        public void EnsureRequestedCanvasDoesNotReturnAnotherHiddenCanvasPanelInSameScene()
        {
            var hidden = CanvasIn(_ownScene, "HiddenCanvas");
            var foreign = BarePanel(hidden);
            hidden.gameObject.SetActive(false);
            var requested = CanvasIn(_ownScene, "RequestedCanvas");
            var actual = ExplorationChoicePanel.EnsureInScene(requested);
            Assert.That(actual, Is.Not.Null);
            Assert.That(ReferenceEquals(actual, foreign), Is.False);
            Assert.That(actual.transform.IsChildOf(requested.transform), Is.True,
                "Ensure must resolve ownership under the requested canvas, never global first-panel order.");
        }

        [Test]
        public void EnsureRequestedCanvasDoesNotReturnPanelOwnedByAnotherScene()
        {
            var other = CanvasIn(_foreignScene, "OtherSceneCanvas");
            var foreign = BarePanel(other);
            var requested = CanvasIn(_ownScene, "RequestedCanvas");
            var actual = ExplorationChoicePanel.EnsureInScene(requested);
            Assert.That(actual, Is.Not.Null);
            Assert.That(ReferenceEquals(actual, foreign), Is.False);
            Assert.That(actual.gameObject.scene, Is.EqualTo(_ownScene));
            Assert.That(actual.transform.IsChildOf(requested.transform), Is.True);
        }

        [Test]
        public void RepeatedEnsureReusesOnlyRequestedCanvasPanelAndMakesItsButtonsRaycastable()
        {
            var canvas = CanvasIn(_ownScene, "RequestedCanvas");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Null, "Fixture deliberately starts without a raycaster.");
            var first = OwnedPanel(canvas); // Assert ownership before any Open can alter an external legacy panel.
            var second = ExplorationChoicePanel.EnsureInScene(canvas);
            Assert.That(ReferenceEquals(first, second), Is.True);
            Assert.That(canvas.GetComponentsInChildren<ExplorationChoicePanel>(true).Length, Is.EqualTo(1));
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(canvas.GetComponent<GraphicRaycaster>().isActiveAndEnabled, Is.True);
        }

        [Test]
        public void InteractionCanvasSelectionSkipsHiddenNestedWorldSpaceCameraAndOtherSceneRoots()
        {
            // Pure candidate selection avoids executing the legacy global TryInteract against host UI.
            // Whole interaction/input behavior is proved separately in the actual native run.
            var hidden = CanvasIn(_ownScene, "Hidden"); hidden.gameObject.SetActive(false);
            var world = CanvasIn(_ownScene, "World"); world.renderMode = RenderMode.WorldSpace;
            var outside = CanvasIn(_foreignScene, "Foreign"); outside.sortingOrder = 9000;
            var parent = CanvasIn(_ownScene, "Parent");
            var nested = CanvasIn(_ownScene, "Nested"); nested.transform.SetParent(parent.transform, false);
            var cameraCanvas = CanvasIn(_ownScene, "CameraCanvas");
            cameraCanvas.worldCamera = Go("CanvasCamera", _ownScene).AddComponent<Camera>();
            cameraCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            Assert.That(cameraCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera), "Real camera is required for effective render mode.");
            var disabled = CanvasIn(_ownScene, "Disabled"); disabled.enabled = false;
            var visible = CanvasIn(_ownScene, "Visible");
            Assert.That(nested.isRootCanvas, Is.False);
            var method = typeof(ExplorationInteractionPointInteractor).GetMethod("SelectInteractionCanvas",
                BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Canvas[]), typeof(Scene) }, null);
            Assert.That(method, Is.Not.Null, "Pure scene-aware interaction canvas selector required.");
            var rejected = new[] { outside, hidden, world, nested, cameraCanvas, disabled };
            var selected = (Canvas)method.Invoke(null, new object[] { rejected.Concat(new[] { visible }).ToArray(), _ownScene });
            Assert.That(ReferenceEquals(selected, visible), Is.True);
            Assert.That(method.Invoke(null, new object[] { rejected, _ownScene }), Is.Null);
            Assert.That(hidden.gameObject.activeSelf, Is.False, "Selection cannot activate a hidden root.");
            Assert.That(disabled.enabled, Is.False, "Selection cannot enable another root.");
        }

        [Test]
        public void ActualCloseButtonReleasesRepeatedOpenWithoutSelectingOrChangingProgressOrFiles()
        {
            var panel = OwnedPanel(CanvasIn(_ownScene, "RequestedCanvas"));
            int callbacks = 0;
            string studentBefore = JsonUtility.ToJson(_student.Progress.ToSaveData());
            string progressBefore = JsonUtility.ToJson(_progress.ToSaveData());
            string filesBefore = Files();
            panel.Open(Model(), _progress, id => callbacks++);
            panel.Open(Model(), _progress, id => callbacks++);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.True);
            var close = panel.GetComponentsInChildren<Button>(true).SingleOrDefault(b => !b.name.StartsWith("ChoiceButton_", StringComparison.Ordinal));
            Assert.That(close, Is.Not.Null, "A real Close button separate from authored choices is required.");
            Assert.That(close.gameObject.activeInHierarchy && close.interactable, Is.True);
            close.onClick.Invoke();
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            close.onClick.Invoke(); panel.Close();
            Assert.That(callbacks, Is.Zero, "Close must never choose Leave or replay a choice callback.");
            Assert.That(JsonUtility.ToJson(_student.Progress.ToSaveData()), Is.EqualTo(studentBefore));
            Assert.That(JsonUtility.ToJson(_progress.ToSaveData()), Is.EqualTo(progressBefore));
            Assert.That(Files(), Is.EqualTo(filesBefore));
        }

        [Test]
        public void ClosingExplorationKeepsAnotherOwnersInputLease()
        {
            var panel = OwnedPanel(CanvasIn(_ownScene, "RequestedCanvas"));
            var other = Go("OtherModalOwner", _ownScene);
            UiGameplayInputLock.Acquire(other);
            panel.Open(Model(), _progress, id => Assert.Fail("Closing cannot choose an outcome."));
            panel.Close(); panel.Close();
            Assert.That(UiGameplayInputLock.IsBlocked, Is.True, "Exploration closes only its own lease.");
            UiGameplayInputLock.Release(other);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisablingOrDestroyingOpenPanelReleasesOnlyItsLeaseAndCleanupIsIdempotent(bool destroy)
        {
            var panel = OwnedPanel(CanvasIn(_ownScene, "RequestedCanvas"));
            int callbacks = 0;
            panel.Open(Model(), _progress, id => callbacks++);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.True);
            if (destroy) Object.DestroyImmediate(panel.gameObject);
            else
            {
                panel.gameObject.SetActive(false);
                Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
                panel.Close(); panel.Close();
                panel.Open(Model(), _progress, id => callbacks++);
                Assert.That(UiGameplayInputLock.IsBlocked, Is.True, "Reopened owned panel reacquires its lease.");
                panel.Close();
            }
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            Assert.That(callbacks, Is.Zero);
        }

        private ExplorationChoicePanel OwnedPanel(Canvas canvas)
        {
            var panel = ExplorationChoicePanel.EnsureInScene(canvas);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.gameObject.scene, Is.EqualTo(canvas.gameObject.scene));
            Assert.That(panel.transform.IsChildOf(canvas.transform), Is.True,
                "Do not Open/Close a panel returned outside this fixture's requested canvas.");
            return panel;
        }
        private ExplorationChoicePanel BarePanel(Canvas canvas)
        {
            var go = Go("ExistingOwnedPanel", canvas.gameObject.scene, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var panel = go.AddComponent<ExplorationChoicePanel>();
            go.SetActive(false);
            return panel;
        }
        private Canvas CanvasIn(Scene scene, string name)
        {
            var canvas = Go(name, scene, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }
        private GameObject Go(string name, Scene scene, params Type[] types)
        {
            var go = Own(new GameObject(name, types));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        private T Own<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static ExplorationSummaryModel Model() => new ExplorationSummaryModel("fixture.interaction", "Fixture exploration", "Description", false,
            new[] { new ExplorationChoiceSummaryModel("fixture.inspect", "Inspect", "Hint", true, string.Empty) });
        private string Files() => string.Join("\n", Directory.GetFiles(_root, "*", SearchOption.AllDirectories).OrderBy(f => f)
            .Select(f => f.Substring(_root.Length) + ":" + Convert.ToBase64String(File.ReadAllBytes(f))));
        private void Snapshot(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            _priorStatics.Add(field, field.GetValue(null));
        }
        private Scene SavedEmptyScene()
        {
            string path = "Assets/Temp/ExplorationUi-" + Guid.NewGuid().ToString("N") + ".unity";
            Directory.CreateDirectory("Assets/Temp");
            _scenePaths.Add(path);
            string bootYaml = File.ReadAllText("Assets/Scenes/Boot.unity");
            var settings = Regex.Split(bootYaml, @"(?m)(?=^--- !u!)").Where(block => Regex.IsMatch(block, @"^--- !u!(29|104|157|196) "));
            File.WriteAllText(path, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Concat(settings)
                + "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            _scenes.Add(scene);
            Assert.That(scene.GetRootGameObjects(), Is.Empty);
            if (_hostScene.IsValid() && _hostScene.isLoaded && SceneManager.GetActiveScene() == scene) SceneManager.SetActiveScene(_hostScene);
            return scene;
        }
    }
}
