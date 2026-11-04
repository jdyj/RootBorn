using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class WorldEntryRoutingTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Type _service, _destinationType, _catalogType;
        private ScriptableObject _house, _school, _town, _catalog;
        private string _root;
        private Scene _scene;
        private GameDataRegistry _previousRegistry;

        [SetUp] public void Setup()
        {
            var assembly = typeof(WorldPortal).Assembly;
            _service = assembly.GetType("Rootborn.Game.World.WorldEntryService");
            Assert.That(_service, Is.Not.Null, "WorldEntryService must validate registered destinations and unique spawn points.");
            _destinationType = assembly.GetType("Rootborn.Game.World.WorldDestinationDefinition"); _catalogType = assembly.GetType("Rootborn.Game.World.WorldTravelCatalogDefinition");
            Assert.That(_destinationType, Is.Not.Null); Assert.That(_catalogType, Is.Not.Null);
            _root = Path.Combine(Path.GetTempPath(), "rootborn-world-entry-" + Guid.NewGuid().ToString("N"));
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameDataRegistryProvider.TryGet(out _previousRegistry);
            _house = Destination("House", "house-entry"); _school = Destination("School", "school-entry"); _town = Destination("Town", "town-entry", "town-home-entry", "town-school-entry");
            _catalog = Make(_catalogType); Set(_catalog, "_destinations", TypedArray(_destinationType, _house, _school, _town)); Set(_catalog, "_newGameDestination", _house); Set(_catalog, "_newGameSpawnId", "house-entry");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>(); _objects.Add(registry); Set(registry, "_worldTravelCatalog", _catalog); GameDataRegistryProvider.SetOverrideForTests(registry);
        }
        [TearDown] public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (_root == null) return;
            GameDataRegistryProvider.SetOverrideForTests(_previousRegistry);
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test] public void WORLD_ENTRY_001_NewGameUsesConfiguredOwnRoom()
        {
            var entry = Call("ResolveNewGame", _catalog); AssertEntry(entry, "House", "house-entry");
        }
        [Test] public void WORLD_ENTRY_002_ContinueRestoresExactRegisteredSceneAndSpawn()
        {
            var storage = Storage("slot-a"); Assert.That(Save(storage, "Town", "town-school-entry"), Is.True);
            AssertEntry(Call("ResolveContinue", _catalog, storage), "Town", "town-school-entry");
        }
        [Test] public void WORLD_ENTRY_003_MissingLegacyTravelUsesOpeningWithoutWritingOrResettingProgress()
        {
            var storage = Storage("slot-a"); storage.WriteJson("student-life-progress.json", "retained-progress-marker");
            AssertEntry(Call("ResolveContinue", _catalog, storage), "House", "house-entry");
            Assert.That(storage.ReadJson("student-life-progress.json"), Is.EqualTo("retained-progress-marker")); Assert.That(File.Exists(Path.Combine(storage.DirectoryPath, "world-progress.json")), Is.False);
        }
        [Test] public void WORLD_ENTRY_004_CorruptOrUnregisteredSavedPairIsRejected()
        {
            var storage = Storage("slot-a"); storage.WriteJson("world-progress.json", "{ broken"); Assert.That(Call("ResolveContinue", _catalog, storage), Is.Null);
            storage.WriteJson("world-progress.json", JsonUtility.ToJson(new WorldProgressSaveData { CurrentScene = "Unknown", CurrentEntryPointId = "house-entry" })); Assert.That(Call("ResolveContinue", _catalog, storage), Is.Null);
            storage.WriteJson("world-progress.json", JsonUtility.ToJson(new WorldProgressSaveData { CurrentScene = "House", CurrentEntryPointId = "school-entry" })); Assert.That(Call("ResolveContinue", _catalog, storage), Is.Null);
        }
        [Test] public void WORLD_ENTRY_005_InvalidSavePreservesPriorTravelAndSlotsAreIsolated()
        {
            var first = Storage("slot-a"); var second = Storage("slot-b"); Assert.That(Save(first, "School", "school-entry"), Is.True); string before = first.ReadJson("world-progress.json");
            Assert.That(Save(first, "School", "town-entry"), Is.False); Assert.That(first.ReadJson("world-progress.json"), Is.EqualTo(before));
            AssertEntry(Call("ResolveContinue", _catalog, second), "House", "house-entry"); AssertEntry(Call("ResolveContinue", _catalog, first), "School", "school-entry");
        }
        [Test] public void WORLD_ENTRY_006_DuplicateSceneOrSpawnDefinitionsInvalidateCatalog()
        {
            Set(_catalog, "_destinations", TypedArray(_destinationType, _house, _school, Destination("House", "different-entry"))); Assert.That(Call("ResolveNewGame", _catalog), Is.Null);
            Set(_catalog, "_destinations", TypedArray(_destinationType, _house, _school, _town)); Set(_house, "_spawnIds", new[] { "house-entry", "house-entry" }); Assert.That(Call("ResolveNewGame", _catalog), Is.Null);
        }
        [Test] public void WORLD_ENTRY_007_NestedSpawnLookupFindsLaterSiblingUnderSharedRoot()
        {
            var root = Go("SpawnPoints"); Spawn(root.transform, "first", Vector2.zero); var wanted = Spawn(root.transform, "house-entry", new Vector2(4, 5));
            Assert.That(Call("FindUniqueSpawn", _scene, "house-entry"), Is.SameAs(wanted));
        }
        [Test] public void WORLD_ENTRY_008_DuplicateSpawnIdsAreRejectedBeforeActorOrCameraMoves()
        {
            var root = Go("SpawnPoints"); Spawn(root.transform, "house-entry", Vector2.zero); Spawn(root.transform, "house-entry", Vector2.one);
            var actor = Go("SuppliedActor"); actor.transform.position = new Vector3(7, 8, 0); var camera = Go("Camera").AddComponent<Camera>(); camera.transform.position = new Vector3(9, 10, -10);
            Assert.That(Call("FindUniqueSpawn", _scene, "house-entry"), Is.Null); Assert.That((bool)Call("TryPlaceActor", _scene, actor, "house-entry", camera), Is.False);
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(7, 8, 0))); Assert.That(camera.transform.position, Is.EqualTo(new Vector3(9, 10, -10)));
        }
        [Test] public void WORLD_ENTRY_009_PlacementPreservesSuppliedActorAndModelAndBindsAuthoredCameraBounds()
        {
            var root = Go("SpawnPoints"); Spawn(root.transform, "house-entry", new Vector2(4, -5));
            var actor = Go("NamedTraveller"); actor.AddComponent<PlayerIdentity>().Configure("traveller"); var component = actor.AddComponent<StudentLifeProgressComponent>(); var progress = component.EnsureProgress();
            var duplicate = Go("Player"); duplicate.AddComponent<PlayerIdentity>().Configure("traveller"); var other = Go("OtherPlayer"); other.AddComponent<PlayerIdentity>().Configure("another-player");
            var camera = Go("Camera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 2;
            var bounds = Go("CameraBounds").AddComponent<BoxCollider2D>(); bounds.isTrigger = true; bounds.size = new Vector2(30, 20);
            Assert.That((bool)Call("TryPlaceActor", _scene, actor, "house-entry", camera), Is.True);
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(4, -5, 0))); Assert.That(actor.GetComponent<StudentLifeProgressComponent>(), Is.SameAs(component)); Assert.That(component.Progress, Is.SameAs(progress));
            Assert.That(duplicate == null || !duplicate.activeSelf, Is.True); Assert.That(other.activeSelf, Is.True);
            var follow = camera.GetComponent<CameraFollow>(); Assert.That(follow, Is.Not.Null); Assert.That(Field(follow, "_target"), Is.SameAs(actor.transform)); Assert.That(Field(follow, "_bounds"), Is.SameAs(bounds));
        }
        [Test] public void WORLD_ENTRY_010_PortalPassesExactActorAndPairToDriverWithoutLoadingSceneInTheTest()
        {
            var portal = Go("HomeDoor").AddComponent<WorldPortal>(); portal.Bind("Home", "House", "house-entry"); var actor = Go("ProvidedActor"); actor.AddComponent<PlayerIdentity>().Configure("provided"); Go("Player").AddComponent<PlayerIdentity>().Configure("wrong-actor");
            GameObject received = null; int calls = 0; Func<GameObject, string, string, string, bool> handler = (selected, scene, spawn, label) => { received = selected; calls++; Assert.That(scene, Is.EqualTo("House")); Assert.That(spawn, Is.EqualTo("house-entry")); Assert.That(label, Is.EqualTo("Home")); return true; };
            typeof(WorldPortal).GetMethod("ConfigureTravelHandler").Invoke(portal, new object[] { handler });
            Assert.That(portal.TryInteract(actor), Is.True); Assert.That(received, Is.SameAs(actor)); Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void WORLD_ENTRY_011_PortalRejectsNullActorUnknownPairAndRejectedDriver()
        {
            var portal = Go("Door").AddComponent<WorldPortal>(); var actor = Go("Actor"); actor.AddComponent<PlayerIdentity>(); int calls = 0;
            Func<GameObject, string, string, string, bool> handler = (selected, scene, spawn, label) => { calls++; return false; }; typeof(WorldPortal).GetMethod("ConfigureTravelHandler").Invoke(portal, new object[] { handler });
            portal.Bind("Unknown", "Unknown", "entry"); Assert.That(portal.TryInteract(actor), Is.False); Assert.That(calls, Is.Zero);
            portal.Bind("Home", "House", "house-entry"); Assert.That(portal.TryInteract(null), Is.False); Assert.That(portal.TryInteract(actor), Is.False); Assert.That(calls, Is.EqualTo(1));
        }
        [Test] public void WORLD_ENTRY_012_ReciprocalHomeAndSchoolReturnEntriesRemainDistinct()
        {
            var storage = Storage("slot-a"); Assert.That(Save(storage, "Town", "town-home-entry"), Is.True); AssertEntry(Call("ResolveContinue", _catalog, storage), "Town", "town-home-entry");
            Assert.That(Save(storage, "Town", "town-school-entry"), Is.True); AssertEntry(Call("ResolveContinue", _catalog, storage), "Town", "town-school-entry");
        }
        private bool Save(SaveService storage, string scene, string spawn) => (bool)Call("SaveTravel", _catalog, storage, scene, spawn, "test doorway");
        private SaveService Storage(string slot) => new SaveService(slot, _root);
        private object Call(string name, params object[] args) { var method = _service.GetMethod(name, BindingFlags.Public | BindingFlags.Static); Assert.That(method, Is.Not.Null, name); return method.Invoke(null, args); }
        private static void AssertEntry(object entry, string scene, string spawn) { Assert.That(entry, Is.Not.Null); Assert.That(entry.GetType().GetProperty("SceneName").GetValue(entry), Is.EqualTo(scene)); Assert.That(entry.GetType().GetProperty("SpawnId").GetValue(entry), Is.EqualTo(spawn)); }
        private ScriptableObject Destination(string scene, params string[] spawns) { var result = Make(_destinationType); Set(result, "_sceneName", scene); Set(result, "_spawnIds", spawns); return result; }
        private ScriptableObject Make(Type type) { var value = ScriptableObject.CreateInstance(type); _objects.Add(value); return value; }
        private static Array TypedArray(Type type, params object[] values) { var array = Array.CreateInstance(type, values.Length); for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i); return array; }
        private GameObject Go(string name) { var go = new GameObject(name); _objects.Add(go); return go; }
        private WorldSpawnPoint Spawn(Transform root, string id, Vector2 position) { var go = Go("Spawn_" + id); go.transform.SetParent(root, false); go.transform.position = position; var spawn = go.AddComponent<WorldSpawnPoint>(); spawn.Bind(id); return spawn; }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value); }
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
