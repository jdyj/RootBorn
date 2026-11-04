using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class WorldTravelOwnerAdapterTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Type _adapter;
        private string _root;
        private SaveSlotMetadata _metadata;
        private AppConfig _config;
        private INetworkSession _session;
        private GameDataRegistry _registry, _oldRegistry;
        private bool _initialized;
        [SetUp] public void Setup()
        {
            _adapter = typeof(AdultCareerRuntimeComponent).Assembly.GetType("Rootborn.UI.World.PlayerWorldTravelAdapter"); Assert.That(_adapter, Is.Not.Null, "World handoff requires an actual aggregate-owner adapter.");
            _metadata = ActiveSaveContext.Metadata; _config = GameBootstrap.Config; _session = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _oldRegistry); _initialized = true;
            _root = Path.Combine(Path.GetTempPath(), "rb-owner-" + Guid.NewGuid().ToString("N")); SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" });
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "slot" }); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            var calendar = New<LifeCalendarDefinition>(); calendar.ConfigureForTests(3, 3); _registry = New<GameDataRegistry>(); Set(_registry, "_studentLifeCalendar", calendar); GameDataRegistryProvider.SetOverrideForTests(_registry);
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (!_initialized) return;
            ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _session);
            if (Directory.Exists(_root)) Directory.Delete(_root, true); _initialized = false;
        }
        [Test] public void WORLD_OWNER_001_PortalPreparationNeverCreatesAnOwnerFromAnArbitraryActor()
        {
            var actor = Actor(); Assert.That((bool)Call("Prepare", actor), Is.False); Assert.That(actor.GetComponent<AdultCareerRuntimeComponent>(), Is.Null);
        }
        [Test] public async Task WORLD_OWNER_002_TransferredOwnerAndStudentComponentRemainExactInstances()
        {
            var actor = Actor(); var owner = AdultCareerRuntimeComponent.Ensure(actor, _registry); var student = owner.StudentComponent; var model = student.Progress; Assert.That((bool)Call("Prepare", actor), Is.True);
            Assert.That(await (Task<bool>)Call("Complete", actor.scene, actor), Is.True); Assert.That(actor.GetComponent<AdultCareerRuntimeComponent>(), Is.SameAs(owner)); Assert.That(owner.StudentComponent, Is.SameAs(student)); Assert.That(student.Progress, Is.SameAs(model)); Assert.That(owner.CanMutate, Is.True);
        }
        [Test] public async Task WORLD_OWNER_003_InitialEntryBindsAndPersistsOnlyTheExplicitSceneActor()
        {
            var actor = Actor(); Assert.That(await (Task<bool>)Call("Complete", actor.scene, actor), Is.True); var owner = actor.GetComponent<AdultCareerRuntimeComponent>(); Assert.That(owner, Is.Not.Null); Assert.That(owner.CanMutate, Is.True); Assert.That(AdultCareerSnapshotPersistence.Load(new SaveService("slot"), "actor"), Is.Not.Null);
        }
        [Test] public async Task WORLD_OWNER_004_NetworkModeCannotCreateOrCompleteLocalWorldOwner()
        {
            var actor = Actor(); GameBootstrap.Config.Mode = SessionMode.Client; Assert.That(await (Task<bool>)Call("Complete", actor.scene, actor), Is.False); Assert.That(AdultCareerSnapshotPersistence.HasCommittedFile(new SaveService("slot"), "actor"), Is.False);
        }
        [Test] public void WORLD_OWNER_005_SaveSlotNetworkEntryIsDeniedBeforeSceneLoadingOrContextChange()
        {
            var destination = New<WorldDestinationDefinition>(); Set(destination, "_sceneName", "House"); Set(destination, "_spawnIds", new[] { "entry" });
            var catalog = New<WorldTravelCatalogDefinition>(); Set(catalog, "_destinations", new[] { destination }); Set(catalog, "_newGameDestination", destination); Set(catalog, "_newGameSpawnId", "entry"); Set(_registry, "_worldTravelCatalog", catalog);
            var runtimeObject = new GameObject("TravelDriver"); _objects.Add(runtimeObject); var runtime = runtimeObject.AddComponent<WorldTravelRuntime>(); int loads = 0;
            runtime.Configure(scene => { loads++; return true; }, actor => false, (scene, actor) => Task.FromResult(true), actor => { });
            var panelObject = new GameObject("SlotPanel"); _objects.Add(panelObject); var panel = panelObject.AddComponent<SaveSlotSelectPanel>(); GameBootstrap.Config.Mode = SessionMode.Client;
            try
            {
                Assert.That(panel.TryEnterSlot(new SaveSlotMetadata { SlotId = "other-slot" }, true), Is.False);
                Assert.That(loads, Is.Zero); Assert.That(ActiveSaveContext.SlotId, Is.EqualTo("slot"));
            }
            finally { runtime.CancelPendingEntry(); }
        }
        private GameObject Actor()
        {
            var go = new GameObject("ExactActor"); _objects.Add(go); go.AddComponent<PlayerIdentity>().Configure("actor"); var student = go.AddComponent<StudentLifeProgressComponent>(); student.ConfigureForTests("slot", "actor", 12, 10, 0, 480); student.Progress.ConfigureCalendar(_registry.StudentLifeCalendar); go.AddComponent<PlayerInventory>(); return go;
        }
        private object Call(string method, params object[] args) { var info = _adapter.GetMethod(method, BindingFlags.Static | BindingFlags.Public); Assert.That(info, Is.Not.Null, method); return info.Invoke(null, args); }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
