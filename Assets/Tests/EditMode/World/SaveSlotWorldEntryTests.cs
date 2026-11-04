using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using Rootborn.UI.MainMenu;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class SaveSlotWorldEntryTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _root;
        private GameDataRegistry _registry, _oldRegistry;
        private SaveSlotMetadata _oldMetadata;
        private SaveSlotSelectPanel _panel;
        private GameObject _panelRoot;
        private bool _initialized;
        [SetUp] public void Setup()
        {
            Assert.That(typeof(SaveSlotSelectPanel).GetMethod("TryEnterSlot"), Is.Not.Null, "Save slot entry must resolve registered new-game/continue destinations before hiding selection.");
            GameDataRegistryProvider.TryGet(out _oldRegistry); _oldMetadata = ActiveSaveContext.Metadata; _initialized = true;
            _root = Path.Combine(Path.GetTempPath(), "rb-entry-ui-" + Guid.NewGuid().ToString("N")); SaveSlotSelectPanel.SetSaveRootForTests(_root);
            _registry = New<GameDataRegistry>(); GameDataRegistryProvider.SetOverrideForTests(_registry);
            var destinationType = typeof(WorldPortal).Assembly.GetType("Rootborn.Game.World.WorldDestinationDefinition"); var catalogType = typeof(WorldPortal).Assembly.GetType("Rootborn.Game.World.WorldTravelCatalogDefinition");
            Assert.That(destinationType, Is.Not.Null); Assert.That(catalogType, Is.Not.Null);
            var home = Make(destinationType); Set(home, "_sceneName", "House"); Set(home, "_spawnIds", new[] { "house-entry" });
            var school = Make(destinationType); Set(school, "_sceneName", "School"); Set(school, "_spawnIds", new[] { "school-entry" });
            var destinations = Array.CreateInstance(destinationType, 2); destinations.SetValue(home, 0); destinations.SetValue(school, 1);
            var catalog = Make(catalogType); Set(catalog, "_destinations", destinations); Set(catalog, "_newGameDestination", home); Set(catalog, "_newGameSpawnId", "house-entry"); Set(_registry, "_worldTravelCatalog", catalog);
            var go = new GameObject("SlotSelection"); _objects.Add(go); _panel = go.AddComponent<SaveSlotSelectPanel>(); _panelRoot = new GameObject("SelectionRoot"); _objects.Add(_panelRoot); Set(_panel, "_root", _panelRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "previous" });
        }
        [TearDown] public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (!_initialized) return;
            SaveSlotSelectPanel.SetSaveRootForTests(null); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry); ActiveSaveContext.Set(_oldMetadata);
            if (Directory.Exists(_root)) Directory.Delete(_root, true); _initialized = false;
        }
        [Test] public void SAVE_WORLD_001_NewGameRequestsOwnRoomAndHidesOnlyAfterAcceptance()
        {
            string scene = null, spawn = null; int calls = 0; var metadata = new SaveSlotMetadata { SlotId = "slot-0" };
            Configure((selected, destination, entry) => { Assert.That(selected, Is.SameAs(metadata)); Assert.That(_panelRoot.activeSelf, Is.True); scene = destination; spawn = entry; calls++; return true; });
            Assert.That(Enter(metadata, true), Is.True); Assert.That(scene, Is.EqualTo("House")); Assert.That(spawn, Is.EqualTo("house-entry")); Assert.That(calls, Is.EqualTo(1)); Assert.That(_panelRoot.activeSelf, Is.False);
        }
        [Test] public void SAVE_WORLD_002_ContinueRequestsSavedRegisteredSchoolEntry()
        {
            new SaveService("slot-0", _root).WriteJson("world-progress.json", JsonUtility.ToJson(new WorldProgressSaveData { CurrentScene = "School", CurrentEntryPointId = "school-entry" }));
            string scene = null, spawn = null; Configure((selected, destination, entry) => { scene = destination; spawn = entry; return true; });
            Assert.That(Enter(new SaveSlotMetadata { SlotId = "slot-0" }, false), Is.True); Assert.That(scene, Is.EqualTo("School")); Assert.That(spawn, Is.EqualTo("school-entry"));
        }
        [Test] public void SAVE_WORLD_003_CorruptContinueKeepsModalContextAndDisplaysFailure()
        {
            new SaveService("slot-0", _root).WriteJson("world-progress.json", "{ broken"); int calls = 0; Configure((selected, destination, entry) => { calls++; return true; });
            Assert.That(Enter(new SaveSlotMetadata { SlotId = "slot-0" }, false), Is.False); Assert.That(calls, Is.Zero); AssertRetainedFailure();
        }
        [Test] public void SAVE_WORLD_004_RejectedDriverKeepsSelectionAndExistingContext()
        {
            Configure((selected, destination, entry) => false); Assert.That(Enter(new SaveSlotMetadata { SlotId = "slot-0" }, true), Is.False); AssertRetainedFailure();
        }
        [Test] public void SAVE_WORLD_005_MissingCatalogCannotFallBackToHardcodedTown()
        {
            Set(_registry, "_worldTravelCatalog", null); int calls = 0; Configure((selected, destination, entry) => { calls++; return true; });
            Assert.That(Enter(new SaveSlotMetadata { SlotId = "slot-0" }, true), Is.False); Assert.That(calls, Is.Zero); AssertRetainedFailure();
        }
        [Test] public void SAVE_WORLD_006_ExistingLoadButtonPathUsesValidatedContinue()
        {
            new SaveService("slot-0", _root).WriteJson("world-progress.json", JsonUtility.ToJson(new WorldProgressSaveData { CurrentScene = "School", CurrentEntryPointId = "school-entry" }));
            string scene = null; Configure((selected, destination, entry) => { scene = destination; return true; });
            typeof(SaveSlotSelectPanel).GetMethod("LoadSlot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_panel, new object[] { new SaveSlotMetadata { SlotId = "slot-0" } }); Assert.That(scene, Is.EqualTo("School"));
        }
        private bool Enter(SaveSlotMetadata metadata, bool newGame) => (bool)typeof(SaveSlotSelectPanel).GetMethod("TryEnterSlot").Invoke(_panel, new object[] { metadata, newGame });
        private void Configure(Func<SaveSlotMetadata, string, string, bool> handler) { var method = typeof(SaveSlotSelectPanel).GetMethod("ConfigureEntryHandler"); Assert.That(method, Is.Not.Null); method.Invoke(_panel, new object[] { handler }); }
        private void AssertRetainedFailure() { Assert.That(_panelRoot.activeSelf, Is.True); Assert.That(ActiveSaveContext.SlotId, Is.EqualTo("previous")); var property = typeof(SaveSlotSelectPanel).GetProperty("LastEntryError"); Assert.That(property, Is.Not.Null); Assert.That((string)property.GetValue(_panel), Is.Not.Empty); }
        private T New<T>() where T : ScriptableObject { var result = ScriptableObject.CreateInstance<T>(); _objects.Add(result); return result; }
        private ScriptableObject Make(Type type) { var result = ScriptableObject.CreateInstance(type); _objects.Add(result); return result; }
        private static void Set(object target, string field, object value) { var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(info, Is.Not.Null, field); info.SetValue(target, value); }
    }
}
