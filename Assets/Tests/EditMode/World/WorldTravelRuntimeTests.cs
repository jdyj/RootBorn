using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
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
    public sealed class WorldTravelRuntimeTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Type _type;
        private Component _runtime;
        private Scene _destination;
        private string _root;
        private GameDataRegistry _oldRegistry;
        private SaveSlotMetadata _oldMetadata;
        private bool _initialized;
        private GameObject _actor;
        private Camera _camera;
        private SaveService _storage;
        [SetUp] public void Setup()
        {
            _type = typeof(WorldPortal).Assembly.GetType("Rootborn.Game.World.WorldTravelRuntime");
            Assert.That(_type, Is.Not.Null, "A persistent world travel driver must own handoff instead of a destroyed portal component.");
            GameDataRegistryProvider.TryGet(out _oldRegistry); _oldMetadata = ActiveSaveContext.Metadata; _initialized = true;
            _root = Path.Combine(Path.GetTempPath(), "rb-handoff-" + Guid.NewGuid().ToString("N")); SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" }); _storage = new SaveService("slot");
            _destination = EditorSceneManager.NewPreviewScene();
            _destination.name = "TravelDestination-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var destinationType = typeof(WorldPortal).Assembly.GetType("Rootborn.Game.World.WorldDestinationDefinition"); var catalogType = typeof(WorldPortal).Assembly.GetType("Rootborn.Game.World.WorldTravelCatalogDefinition");
            var destination = Make(destinationType); Set(destination, "_sceneName", _destination.name); Set(destination, "_spawnIds", new[] { "entry" });
            var array = Array.CreateInstance(destinationType, 1); array.SetValue(destination, 0); var catalog = Make(catalogType); Set(catalog, "_destinations", array); Set(catalog, "_newGameDestination", destination); Set(catalog, "_newGameSpawnId", "entry");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>(); _objects.Add(registry); Set(registry, "_worldTravelCatalog", catalog); GameDataRegistryProvider.SetOverrideForTests(registry);
            _runtime = Go("PersistentTravelDriver").AddComponent(_type); _actor = Go("ExactTravellingActor"); _actor.AddComponent<PlayerIdentity>().Configure("actor"); _actor.transform.position = new Vector3(40, 50, 0);
            _camera = Go("DestinationCamera").AddComponent<Camera>(); _camera.orthographic = true; _camera.orthographicSize = 2; SceneManager.MoveGameObjectToScene(_camera.gameObject, _destination);
            var bounds = Go("CameraBounds").AddComponent<BoxCollider2D>(); bounds.isTrigger = true; bounds.size = new Vector2(30, 20); SceneManager.MoveGameObjectToScene(bounds.gameObject, _destination);
            var spawns = Go("NestedSpawns"); SceneManager.MoveGameObjectToScene(spawns, _destination); var spawn = Go("DoorSpawn"); spawn.transform.SetParent(spawns.transform, false); spawn.transform.position = new Vector3(4, -3, 0); spawn.AddComponent<WorldSpawnPoint>().Bind("entry");
        }
        [TearDown] public void Cleanup()
        {
            if (_runtime != null) Call("CancelPendingEntry");
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (!_initialized) return;
            if (_destination.IsValid() && _destination.isLoaded) EditorSceneManager.ClosePreviewScene(_destination);
            SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(_oldMetadata); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry); if (Directory.Exists(_root)) Directory.Delete(_root, true); _initialized = false;
        }
        [Test] public void WORLD_DRIVER_001_ExactActorIsPreparedBeforeLoadingAndRequestIsExclusive()
        {
            var order = new List<string>(); Configure(scene => { order.Add("load"); return true; }, actor => { Assert.That(actor, Is.SameAs(_actor)); order.Add("prepare"); return true; });
            Assert.That(Begin(), Is.True); Assert.That(order, Is.EqualTo(new[] { "prepare", "load" })); Assert.That(Get<GameObject>("Actor"), Is.SameAs(_actor)); Assert.That(Get<bool>("IsBusy"), Is.True);
            Assert.That(Begin(), Is.False); Assert.That(order.Count, Is.EqualTo(2)); Assert.That(UiGameplayInputLock.IsBlocked, Is.True);
        }
        [Test] public void WORLD_DRIVER_002_RejectedPreparationNeverLoadsOrWritesTravel()
        {
            int loads = 0; Configure(scene => { loads++; return true; }, actor => false); Assert.That(Begin(), Is.False); Assert.That(loads, Is.Zero); Assert.That(Get<bool>("IsBusy"), Is.False); Assert.That(File.Exists(Path.Combine(_storage.DirectoryPath, "world-progress.json")), Is.False);
        }
        [Test] public void WORLD_DRIVER_003_RejectedLoaderCancelsPreparedOwnerAndReleasesInput()
        {
            int cancels = 0; Configure(scene => false, actor => true, cancel: actor => { Assert.That(actor, Is.SameAs(_actor)); cancels++; });
            Assert.That(Begin(), Is.False); Assert.That(cancels, Is.EqualTo(1)); Assert.That(Get<bool>("IsBusy"), Is.False); Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }
        [Test] public async Task WORLD_DRIVER_004_CompletionPlacesExactActorBindsCameraAndPersistsOnce()
        {
            int bindings = 0; Configure(scene => true, actor => true, (scene, actor) => { Assert.That(scene, Is.EqualTo(_destination)); Assert.That(actor, Is.SameAs(_actor)); bindings++; return Task.FromResult(true); }); Assert.That(Begin(), Is.True);
            Assert.That(await Complete(_destination, _actor), Is.True); Assert.That(_actor.scene, Is.EqualTo(_destination)); Assert.That(_actor.transform.position, Is.EqualTo(new Vector3(4, -3, 0))); Assert.That(bindings, Is.EqualTo(1));
            var follow = _camera.GetComponent<CameraFollow>(); Assert.That(follow, Is.Not.Null); Assert.That(Field(follow, "_target"), Is.SameAs(_actor.transform)); Assert.That(Field(follow, "_bounds"), Is.Not.Null);
            var saved = JsonUtility.FromJson<WorldProgressSaveData>(_storage.ReadJson("world-progress.json")); Assert.That(saved.CurrentScene, Is.EqualTo(_destination.name)); Assert.That(saved.CurrentEntryPointId, Is.EqualTo("entry"));
            Assert.That(await Complete(_destination, _actor), Is.False); Assert.That(bindings, Is.EqualTo(1)); Assert.That(Get<bool>("IsBusy"), Is.False); Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }
        [Test] public async Task WORLD_DRIVER_005_WrongActorCannotCompletePendingTraveller()
        {
            int bindings = 0; Configure(scene => true, actor => true, (scene, actor) => { bindings++; return Task.FromResult(true); }); Begin(); var duplicate = Go("DuplicateActor"); duplicate.AddComponent<PlayerIdentity>().Configure("actor");
            Assert.That(await Complete(_destination, duplicate), Is.False); Assert.That(bindings, Is.Zero); Assert.That(Get<bool>("IsBusy"), Is.True); Assert.That(_actor.transform.position, Is.EqualTo(new Vector3(40, 50, 0)));
        }
        [Test] public async Task WORLD_DRIVER_006_FailedArrivalWriteLeavesActorUnmovedAndCanRetry()
        {
            int bindings = 0; Configure(scene => true, actor => true, (scene, actor) => { bindings++; return Task.FromResult(true); }); Begin(); Directory.Delete(_storage.DirectoryPath, true); File.WriteAllText(_storage.DirectoryPath, "blocked");
            Assert.That(await Complete(_destination, _actor), Is.False); Assert.That(_actor.transform.position, Is.EqualTo(new Vector3(40, 50, 0))); Assert.That(bindings, Is.Zero); Assert.That(Get<bool>("IsBusy"), Is.True);
            File.Delete(_storage.DirectoryPath); Directory.CreateDirectory(_storage.DirectoryPath); Assert.That(await Complete(_destination, _actor), Is.True); Assert.That(bindings, Is.EqualTo(1));
        }
        [Test] public void WORLD_DRIVER_007_InitialEntryActivatesSelectedContextOnlyWhenLoaderAccepts()
        {
            var selected = new SaveSlotMetadata { SlotId = "new-slot" }; Configure(scene => false, actor => { Assert.Fail("Initial entry has no departing actor"); return false; });
            Assert.That((bool)Call("BeginEntry", selected, _destination.name, "entry"), Is.False); Assert.That(ActiveSaveContext.SlotId, Is.EqualTo("slot"));
            Configure(scene => true, actor => false); Assert.That((bool)Call("BeginEntry", selected, _destination.name, "entry"), Is.True); Assert.That(ActiveSaveContext.Metadata, Is.SameAs(selected)); Assert.That(Get<GameObject>("Actor"), Is.Null);
        }
        [Test] public void WORLD_DRIVER_008_UnregisteredPairNeverPreparesOrLoads()
        {
            int calls = 0; Configure(scene => { calls++; return true; }, actor => { calls++; return true; });
            Assert.That((bool)Call("BeginTravel", _actor, _destination.name, "missing", "door"), Is.False); Assert.That(calls, Is.Zero); Assert.That(Get<bool>("IsBusy"), Is.False);
        }
        private void Configure(Func<string, bool> loader, Func<GameObject, bool> prepare, Func<Scene, GameObject, Task<bool>> bind = null, Action<GameObject> cancel = null) => Call("Configure", loader, prepare, bind ?? ((scene, actor) => Task.FromResult(true)), cancel ?? (actor => { }));
        private bool Begin() => (bool)Call("BeginTravel", _actor, _destination.name, "entry", "door");
        private Task<bool> Complete(Scene scene, GameObject actor) => (Task<bool>)Call("CompleteEntryAsync", scene, actor, _camera);
        private object Call(string name, params object[] args) { var method = _type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public); Assert.That(method, Is.Not.Null, name); return method.Invoke(_runtime, args); }
        private T Get<T>(string name) { var property = _type.GetProperty(name); Assert.That(property, Is.Not.Null, name); return (T)property.GetValue(_runtime); }
        private GameObject Go(string name) { var go = new GameObject(name); _objects.Add(go); return go; }
        private ScriptableObject Make(Type type) { Assert.That(type, Is.Not.Null); var value = ScriptableObject.CreateInstance(type); _objects.Add(value); return value; }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value); }
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
