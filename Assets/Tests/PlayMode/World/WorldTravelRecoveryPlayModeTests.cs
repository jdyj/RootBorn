using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.World
{
    public sealed class WorldTravelRecoveryPlayModeTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Scene _origin;
        private Scene _destination;
        private GameObject _actor;
        private Camera _camera;
        private WorldTravelRuntime _runtime;
        private SaveService _storage;
        private GameDataRegistry _oldRegistry;
        private SaveSlotMetadata _oldMetadata;
        private string _saveRoot;
        private string _priorTravelJson;
        private int _cancelCount;
        private FieldInfo _recoveryEvent;
        private object _previousRecoveryHandlers;
        private readonly Vector3 _departure = new Vector3(12, -7, 0);

        [SetUp]
        public void SetUp()
        {
            _cancelCount = 0;
            _recoveryEvent = typeof(WorldTravelRuntime).GetField("RecoveryFailed", BindingFlags.Static | BindingFlags.NonPublic);
            _previousRecoveryHandlers = _recoveryEvent?.GetValue(null);
            _recoveryEvent?.SetValue(null, null);
            GameDataRegistryProvider.TryGet(out _oldRegistry);
            _oldMetadata = ActiveSaveContext.Metadata;
            _saveRoot = Path.Combine(Path.GetTempPath(), "rb-travel-recovery-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "recovery-slot" });
            _storage = new SaveService("recovery-slot");
            _origin = SceneManager.CreateScene("RecoveryOrigin-" + Guid.NewGuid().ToString("N"));
            _destination = SceneManager.CreateScene("RecoveryDestination-" + Guid.NewGuid().ToString("N"));
            var originDefinition = Definition(_origin.name, "origin");
            var destinationDefinition = Definition(_destination.name, "entry");
            var catalog = Make<WorldTravelCatalogDefinition>();
            Set(catalog, "_destinations", new[] { originDefinition, destinationDefinition });
            Set(catalog, "_newGameDestination", originDefinition);
            Set(catalog, "_newGameSpawnId", "origin");
            var registry = Make<GameDataRegistry>();
            Set(registry, "_worldTravelCatalog", catalog);
            GameDataRegistryProvider.SetOverrideForTests(registry);
            Assert.That(WorldEntryService.SaveTravel(catalog, _storage, _origin.name, "origin", "previous"), Is.True);
            _priorTravelJson = _storage.ReadJson(WorldEntryService.FileName);
            _actor = Go("ExactCarriedActor", _origin);
            _actor.AddComponent<PlayerIdentity>().Configure("recovery-actor");
            _actor.transform.position = _departure;
            _camera = Go("DestinationCamera", _destination).AddComponent<Camera>();
            _camera.orthographic = true;
            var spawn = Go("DestinationSpawn", _destination);
            spawn.transform.position = new Vector3(4, 3, 0);
            spawn.AddComponent<WorldSpawnPoint>().Bind("entry");
            _runtime = Go("RecoveryDriver", SceneManager.GetActiveScene()).AddComponent<WorldTravelRuntime>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runtime != null) _runtime.CancelPendingEntry();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_origin.IsValid() && _origin.isLoaded) yield return SceneManager.UnloadSceneAsync(_origin);
            if (_destination.IsValid() && _destination.isLoaded) yield return SceneManager.UnloadSceneAsync(_destination);
            SaveService.SetRootDirectoryForTests(null);
            ActiveSaveContext.Set(_oldMetadata);
            GameDataRegistryProvider.SetOverrideForTests(_oldRegistry);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
            _recoveryEvent?.SetValue(null, _previousRecoveryHandlers);
        }

        private void Configure(Func<string, bool> load, Func<Scene, GameObject, Task<bool>> bind)
        {
            _runtime.Configure(load, actor => ReferenceEquals(actor, _actor), bind, actor =>
            {
                Assert.That(actor, Is.Not.Null);
                _cancelCount++;
            });
        }
        private bool Begin() => _runtime.BeginTravel(_actor, _destination.name, "entry", "destination");
        private IEnumerator WaitForCompletion()
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_runtime.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [Test]
        public void RejectedLoaderRestoresExactActorToDepartureScene()
        {
            Configure(scene => false, (scene, actor) => Task.FromResult(true));
            Assert.That(Begin(), Is.False);
            Assert.That(_actor.scene, Is.EqualTo(_origin), "Rejected loads must undo the real DontDestroyOnLoad scene move.");
            Assert.That(_actor.transform.position, Is.EqualTo(_departure));
            Assert.That(_cancelCount, Is.EqualTo(1));
            Assert.That(_runtime.IsBusy, Is.False);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            Assert.That(_storage.ReadJson(WorldEntryService.FileName), Is.EqualTo(_priorTravelJson));
        }

        [UnityTest]
        public IEnumerator TransientArrivalBindingFailureRecoversWithoutASecondExternalCompletionCall()
        {
            int attempts = 0;
            Configure(scene => true, (scene, actor) =>
            {
                Assert.That(actor, Is.SameAs(_actor));
                attempts++;
                return Task.FromResult(attempts > 1);
            });
            Assert.That(Begin(), Is.True);
            Assert.That(WorldTravelRuntime.TryCompleteEntry(_destination, _actor, _camera), Is.True);
            yield return WaitForCompletion();
            Assert.That(_runtime.IsBusy, Is.False, "The native baseline invokes completion only once; transient errors need driver-owned retry.");
            Assert.That(attempts, Is.GreaterThan(1));
            Assert.That(_actor.scene, Is.EqualTo(_destination));
            Assert.That(_actor.transform.position, Is.EqualTo(new Vector3(4, 3, 0)));
            Assert.That(_cancelCount, Is.Zero);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator PersistentArrivalBindingFailureRollsBackInsteadOfLockingForever()
        {
            Configure(scene => true, (scene, actor) => Task.FromResult(scene == _origin));
            Assert.That(Begin(), Is.True);
            Assert.That(WorldTravelRuntime.TryCompleteEntry(_destination, _actor, _camera), Is.True);
            yield return WaitForCompletion();
            Assert.That(_runtime.IsBusy, Is.False, "A failed handoff must terminate in recovery instead of leaving a silent pending entry.");
            Assert.That(_actor.scene, Is.EqualTo(_origin));
            Assert.That(_actor.transform.position, Is.EqualTo(_departure));
            Assert.That(_cancelCount, Is.EqualTo(1));
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            Assert.That(_runtime.LastError, Is.Not.Empty);
            Assert.That(_storage.ReadJson(WorldEntryService.FileName), Is.EqualTo(_priorTravelJson), "Failed arrival cannot replace the last usable Continue destination.");
        }

        [UnityTest]
        public IEnumerator FailedArrivalReloadsUnloadedSourceBeforeRestoringExactActor()
        {
            string sourceName = _origin.name;
            int sourceLoads = 0;
            Configure(name =>
            {
                if (name == sourceName)
                {
                    sourceLoads++;
                    _origin = SceneManager.CreateScene(sourceName);
                    var camera = Go("RecoveredSourceCamera", _origin).AddComponent<Camera>();
                    camera.orthographic = true;
                }
                return true;
            }, (scene, actor) => Task.FromResult(scene.name == sourceName));
            Assert.That(Begin(), Is.True);
            yield return SceneManager.UnloadSceneAsync(_origin);
            Assert.That(WorldTravelRuntime.TryCompleteEntry(_destination, _actor, _camera), Is.True);
            yield return WaitForCompletion();
            Assert.That(_runtime.IsBusy, Is.False);
            Assert.That(sourceLoads, Is.EqualTo(1), "Single-scene travel unloads the source; recovery must reload it.");
            Assert.That(_actor.scene, Is.EqualTo(_origin));
            Assert.That(_actor.transform.position, Is.EqualTo(_departure));
            Assert.That(_cancelCount, Is.EqualTo(1));
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            Assert.That(_storage.ReadJson(WorldEntryService.FileName), Is.EqualTo(_priorTravelJson));
        }

        [UnityTest]
        public IEnumerator MissingArrivalSpawnTriggersRecoveryInsteadOfBypassingIt()
        {
            Configure(scene => true, (scene, actor) => Task.FromResult(scene == _origin));
            Object.DestroyImmediate(WorldEntryService.FindUniqueSpawn(_destination, "entry").gameObject);
            Assert.That(WorldEntryService.FindUniqueSpawn(_destination, "entry"), Is.Null);
            Assert.That(Begin(), Is.True);
            Assert.That(WorldTravelRuntime.TryCompleteEntry(_destination, _actor, _camera), Is.True);
            yield return WaitForCompletion();
            Assert.That(_runtime.IsBusy, Is.False);
            Assert.That(_actor.scene, Is.EqualTo(_origin));
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator FailedInitialEntryRestoresPreviousContextAndRemovesOnlyItsNewActor()
        {
            SceneManager.SetActiveScene(_origin);
            var previous = ActiveSaveContext.Metadata;
            var selected = new SaveSlotMetadata { SlotId = "failed-initial-entry" };
            var selectedStorage = new SaveService(selected.SlotId);
            Configure(scene => true, (scene, actor) => Task.FromResult(false));
            Assert.That(_runtime.BeginEntry(selected, _destination.name, "entry"), Is.True);
            var createdActor = Go("InitialEntryActor", _destination);
            createdActor.AddComponent<PlayerIdentity>().Configure("initial-entry-actor");
            Assert.That(WorldTravelRuntime.TryCompleteEntry(_destination, createdActor, _camera), Is.True);
            yield return WaitForCompletion();
            yield return null;
            Assert.That(_runtime.IsBusy, Is.False);
            Assert.That(ActiveSaveContext.Metadata, Is.SameAs(previous));
            Assert.That(createdActor == null, Is.True);
            Assert.That(_actor != null, Is.True, "An initial-entry rollback must not destroy an unrelated source actor.");
            Assert.That(File.Exists(Path.Combine(selectedStorage.DirectoryPath, WorldEntryService.FileName)), Is.False);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator StaleBindingCompletionCannotRollBackNewSaveSelection()
        {
            var binding = new TaskCompletionSource<bool>();
            Configure(scene => true, (scene, actor) => binding.Task);
            Assert.That(Begin(), Is.True);
            Assert.That(WorldTravelRuntime.TryCompleteEntry(_destination, _actor, _camera), Is.True);
            _runtime.CancelPendingEntry();
            var selected = new SaveSlotMetadata { SlotId = "new-selected-save" };
            ActiveSaveContext.Set(selected);
            var storage = new SaveService(selected.SlotId);
            storage.WriteJson(WorldEntryService.FileName, "{\"newSelection\":true}");
            string expected = storage.ReadJson(WorldEntryService.FileName);
            binding.SetResult(false);
            yield return null;
            yield return null;
            Assert.That(ActiveSaveContext.Metadata, Is.SameAs(selected));
            Assert.That(storage.ReadJson(WorldEntryService.FileName), Is.EqualTo(expected));
            Assert.That(_runtime.IsBusy, Is.False);
            Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }

        private WorldDestinationDefinition Definition(string scene, string spawn)
        {
            var value = Make<WorldDestinationDefinition>();
            Set(value, "_sceneName", scene);
            Set(value, "_spawnIds", new[] { spawn });
            return value;
        }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private GameObject Go(string name, Scene scene)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            _objects.Add(value);
            return value;
        }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}

