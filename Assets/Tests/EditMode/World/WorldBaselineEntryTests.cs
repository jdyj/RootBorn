using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class WorldBaselineEntryTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Scene _scene;
        private WorldTravelRuntime _runtime;
        private GameDataRegistry _oldRegistry;
        private SaveSlotMetadata _oldMetadata;
        private string _root;
        private WorldTravelCatalogDefinition _catalog;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-baseline-" + Guid.NewGuid().ToString("N")); SaveService.SetRootDirectoryForTests(_root);
            _oldMetadata = ActiveSaveContext.Metadata; GameDataRegistryProvider.TryGet(out _oldRegistry); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "baseline" });
            _scene = EditorSceneManager.NewPreviewScene();
            _scene.name = "BaselineEntry-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var destination = New<WorldDestinationDefinition>(); Set(destination, "_sceneName", _scene.name); Set(destination, "_spawnIds", new[] { "entry" });
            _catalog = New<WorldTravelCatalogDefinition>(); Set(_catalog, "_destinations", new[] { destination }); Set(_catalog, "_newGameDestination", destination); Set(_catalog, "_newGameSpawnId", "entry");
            var registry = New<GameDataRegistry>(); Set(registry, "_worldTravelCatalog", _catalog); GameDataRegistryProvider.SetOverrideForTests(registry);
            _runtime = Go("TravelDriver").AddComponent<WorldTravelRuntime>(); _runtime.Configure(scene => true, actor => true, (scene, actor) => Task.FromResult(true), actor => { });
        }
        [TearDown] public void Cleanup()
        {
            if (_runtime != null) _runtime.CancelPendingEntry();
            foreach (var root in _scene.GetRootGameObjects()) if (root != null) Object.DestroyImmediate(root);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
            SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(_oldMetadata); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry); if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [Test] public void WORLD_BASELINE_001_PendingRegisteredEntryStartsBaselineInANonTownScene()
        {
            BeginEntry(); Call("HandleSceneLoaded", _scene, LoadSceneMode.Single); Assert.That(Find("[TownPlayableBaselineRuntimeInstaller]"), Is.Not.Null);
        }
        [Test] public void WORLD_BASELINE_002_AuthoredSceneStartsBaselineWithoutANameBranch()
        {
            var config = Go("AuthoredWorld"); SceneManager.MoveGameObjectToScene(config, _scene); config.AddComponent<AuthoredWorldScene>();
            Call("HandleSceneLoaded", _scene, LoadSceneMode.Single); Assert.That(Find("[TownPlayableBaselineRuntimeInstaller]"), Is.Not.Null);
        }
        [Test] public void WORLD_BASELINE_003_PendingTravellerWinsOverDestinationDuplicate()
        {
            var actor = Go("NamedTraveller"); actor.AddComponent<PlayerIdentity>().Configure("actor"); actor.AddComponent<PlayerController>();
            var duplicate = Go("Player"); duplicate.AddComponent<PlayerController>(); duplicate.AddComponent<PlayerIdentity>().Configure("actor"); SceneManager.MoveGameObjectToScene(duplicate, _scene);
            Assert.That(_runtime.BeginTravel(actor, _scene.name, "entry", "door"), Is.True); var players = new List<GameObject>(); Call("CollectPlayerRoots", _scene, players);
            Assert.That(players, Is.EqualTo(new[] { actor }), "Baseline must reinforce the carried actor, never initialize a destination duplicate owner.");
        }
        [Test] public void WORLD_BASELINE_004_NewActorUsesExactNestedConfiguredSpawn()
        {
            BeginEntry(); Spawn("entry", new Vector3(11.5f, -16.5f, 0)); var player = (GameObject)Call("CreateLocalPlayer", _scene); if (player != null) _objects.Add(player);
            Assert.That(player, Is.Not.Null); Assert.That(player.transform.position, Is.EqualTo(new Vector3(11.5f, -16.5f, 0)));
        }
        [Test] public void WORLD_BASELINE_005_MissingOrAmbiguousEntryCannotCreateActorAtTownFallback()
        {
            BeginEntry(); var missing = (GameObject)Call("CreateLocalPlayer", _scene); if (missing != null) _objects.Add(missing); Assert.That(missing, Is.Null);
            Spawn("entry", Vector3.zero); Spawn("entry", Vector3.one); var duplicate = (GameObject)Call("CreateLocalPlayer", _scene); if (duplicate != null) _objects.Add(duplicate); Assert.That(duplicate, Is.Null);
        }
        [Test] public void WORLD_BASELINE_006_FinalCameraHookCompletesEntryWithTheExactActor()
        {
            BeginEntry(); Spawn("entry", new Vector3(2, -3, 0)); var player = Go("Player"); player.AddComponent<PlayerIdentity>(); SceneManager.MoveGameObjectToScene(player, _scene);
            var camera = Go("MainCamera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.orthographic = true; SceneManager.MoveGameObjectToScene(camera.gameObject, _scene);
            var bounds = Go("CameraBounds").AddComponent<BoxCollider2D>(); bounds.isTrigger = true; bounds.size = new Vector2(30, 20); SceneManager.MoveGameObjectToScene(bounds.gameObject, _scene);
            Assert.That((bool)Call("FinalizeWorldEntry", _scene, player), Is.True); Assert.That(_runtime.IsBusy, Is.False); Assert.That(player.transform.position, Is.EqualTo(new Vector3(2, -3, 0))); Assert.That(camera.GetComponent<CameraFollow>(), Is.Not.Null);
        }
        private void BeginEntry() => Assert.That(_runtime.BeginEntry(ActiveSaveContext.Metadata, _scene.name, "entry"), Is.True);
        private void Spawn(string id, Vector3 position) { var root = Go("Spawns"); SceneManager.MoveGameObjectToScene(root, _scene); var go = Go("Spawn"); go.transform.SetParent(root.transform, false); go.transform.position = position; go.AddComponent<WorldSpawnPoint>().Bind(id); }
        private GameObject Find(string name) { foreach (var root in _scene.GetRootGameObjects()) if (root.name == name) return root; return null; }
        private static object Call(string name, params object[] args) { var method = typeof(TownPlayableBaselineRuntimeInstaller).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic); Assert.That(method, Is.Not.Null, name); return method.Invoke(null, args); }
        private GameObject Go(string name) { var value = new GameObject(name); _objects.Add(value); return value; }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
