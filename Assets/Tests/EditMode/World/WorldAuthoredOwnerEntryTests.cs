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
    public sealed class WorldAuthoredOwnerEntryTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Scene _scene;
        private GameObject _actor, _authored;
        private WorldTravelRuntime _runtime;
        private GameDataRegistry _oldRegistry;
        private SaveSlotMetadata _oldMetadata;
        private string _root;
        private int _loads, _preparations, _bindings;
        [SetUp] public void Setup()
        {
            _loads = 0; _preparations = 0; _bindings = 0;
            _root = Path.Combine(Path.GetTempPath(), "rb-direct-" + Guid.NewGuid().ToString("N")); SaveService.SetRootDirectoryForTests(_root);
            _oldMetadata = ActiveSaveContext.Metadata; ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "direct" }); GameDataRegistryProvider.TryGet(out _oldRegistry);
            _scene = EditorSceneManager.NewPreviewScene(); _scene.name = "Authored-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _authored = Go("AuthoredWorld"); SceneManager.MoveGameObjectToScene(_authored, _scene); _authored.AddComponent<AuthoredWorldScene>();
            _actor = Go("ExplicitSceneActor"); _actor.AddComponent<PlayerIdentity>(); SceneManager.MoveGameObjectToScene(_actor, _scene);
            var destination = New<WorldDestinationDefinition>(); Set(destination, "_sceneName", _scene.name); Set(destination, "_spawnIds", new[] { "entry" });
            var catalog = New<WorldTravelCatalogDefinition>(); Set(catalog, "_destinations", new[] { destination }); Set(catalog, "_newGameDestination", destination); Set(catalog, "_newGameSpawnId", "entry");
            var registry = New<GameDataRegistry>(); Set(registry, "_worldTravelCatalog", catalog); GameDataRegistryProvider.SetOverrideForTests(registry);
            _runtime = Go("TravelDriver").AddComponent<WorldTravelRuntime>(); Configure((scene, actor) => Task.FromResult(true));
        }
        [TearDown] public void Cleanup()
        {
            if (_runtime != null) _runtime.CancelPendingEntry();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
            SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(_oldMetadata); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry); if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [Test] public void AUTHORED_ENTRY_001_ActualBaselineFinalizerBindsExplicitOwnerWithoutInventingTravel()
        {
            var method = typeof(TownPlayableBaselineRuntimeInstaller).GetMethod("FinalizeWorldEntry", BindingFlags.Static | BindingFlags.NonPublic); Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(null, new object[] { _scene, _actor }), Is.True);
            Assert.That(_bindings, Is.EqualTo(1)); Assert.That(_loads, Is.Zero); Assert.That(_preparations, Is.Zero); Assert.That(WorldTravelRuntime.HasPendingTravel, Is.False); Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            Assert.That(File.Exists(Path.Combine(_root, "direct", "world-progress.json")), Is.False);
        }
        [Test] public async Task AUTHORED_ENTRY_002_ForeignActorAndUnconfiguredSceneAreRejected()
        {
            var foreign = Go("ForeignActor"); foreign.AddComponent<PlayerIdentity>(); Assert.That(await Direct(foreign), Is.False);
            Object.DestroyImmediate(_authored); Assert.That(await Direct(_actor), Is.False); Assert.That(_bindings, Is.Zero);
        }
        [Test] public async Task AUTHORED_ENTRY_003_PendingTravelMustUseItsExistingCompletionPath()
        {
            Assert.That(_runtime.BeginEntry(ActiveSaveContext.Metadata, _scene.name, "entry"), Is.True);
            Assert.That(await Direct(_actor), Is.False); Assert.That(_bindings, Is.Zero); Assert.That(_runtime.IsBusy, Is.True);
        }
        [Test] public async Task AUTHORED_ENTRY_004_DirectBindingAwaitsOwnerViewsWithoutCreatingAPendingEntry()
        {
            var ready = new TaskCompletionSource<bool>(); Configure((scene, actor) => ready.Task);
            var binding = Direct(_actor); Assert.That(_bindings, Is.EqualTo(1)); Assert.That(binding.IsCompleted, Is.False); Assert.That(WorldTravelRuntime.HasPendingTravel, Is.False); Assert.That(_loads, Is.Zero);
            ready.SetResult(true); Assert.That(await binding, Is.True); Assert.That(_actor.scene, Is.EqualTo(_scene));
        }
        private Task<bool> Direct(GameObject actor)
        {
            var method = typeof(WorldTravelRuntime).GetMethod("BindSceneOwnerAsync", BindingFlags.Static | BindingFlags.Public); Assert.That(method, Is.Not.Null, "Direct authored scene starts need owner binding without a fabricated travel request.");
            return (Task<bool>)method.Invoke(null, new object[] { _scene, actor });
        }
        private void Configure(Func<Scene, GameObject, Task<bool>> binder)
        {
            _runtime.Configure(scene => { _loads++; return true; }, actor => { _preparations++; return true; }, (scene, actor) => { Assert.That(scene, Is.EqualTo(_scene)); Assert.That(actor, Is.SameAs(_actor)); _bindings++; return binder(scene, actor); }, actor => { });
        }
        private GameObject Go(string name) { var value = new GameObject(name); _objects.Add(value); return value; }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
