using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
using Rootborn.UI.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class LegacyDestinationPlaceholderTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private Scene _scene;
        private AdultCareerRuntimeComponent _owner;
        private GameObject _actor;
        private string _root;
        private AppConfig _config;
        private SaveSlotMetadata _metadata;
        private INetworkSession _network;
        private GameDataRegistry _registry, _oldRegistry;
        private FieldInfo _policyField;
        private object _previousPolicy;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-placeholder-" + Guid.NewGuid().ToString("N")); SaveService.SetRootDirectoryForTests(_root);
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _oldRegistry);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single }); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            string slot = "placeholder-" + Guid.NewGuid().ToString("N").Substring(0, 8); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = slot });
            var calendar = New<LifeCalendarDefinition>(); calendar.ConfigureForTests(3, 3); _registry = New<GameDataRegistry>(); Set(_registry, "_studentLifeCalendar", calendar); GameDataRegistryProvider.SetOverrideForTests(_registry);
            _scene = EditorSceneManager.NewPreviewScene(); _scene.name = "PlaceholderDestination";
            var spawn = Go("Spawn"); SceneManager.MoveGameObjectToScene(spawn, _scene); spawn.AddComponent<WorldSpawnPoint>().Bind("entry");
            _actor = Go("CanonicalTraveller"); _actor.AddComponent<PlayerController>(); _actor.AddComponent<PlayerIdentity>().Configure("actor"); var student = _actor.AddComponent<StudentLifeProgressComponent>(); student.ConfigureForTests(slot, "actor", 12, 10, 0, 480); student.Progress.ConfigureCalendar(calendar); _actor.AddComponent<PlayerInventory>();
            _owner = AdultCareerRuntimeComponent.Ensure(_actor, _registry); Assert.That(_owner.TryBeginSceneTransfer(), Is.True);
            _policyField = typeof(WorldEntryService).GetField("_legacyPlaceholderPolicy", BindingFlags.Static | BindingFlags.NonPublic); _previousPolicy = _policyField?.GetValue(null);
            // Use the real production bootstrap registration rather than a permissive test predicate.
            typeof(PlayerWorldTravelAdapter).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        [TearDown] public void Cleanup()
        {
            if (_owner != null) _owner.CancelSceneTransfer(); AdultCareerPersistenceRouter.Unregister(_owner); _policyField?.SetValue(null, _previousPolicy);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
            SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(_metadata); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry); typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [Test] public void WORLD_PLACEHOLDER_001_CanonicalTravellerRemovesUnidentifiedSavedPlayerWithoutReplacingItself()
        {
            var placeholder = Candidate("Player"); var controller = _actor.GetComponent<PlayerController>(); var student = _owner.StudentComponent;
            Assert.That(WorldEntryService.TryPlaceActor(_scene, _actor, "entry", null), Is.True);
            Assert.That(placeholder == null || !placeholder.activeSelf, Is.True, "Saved House controller-only Player must not survive as a second visible avatar.");
            Assert.That(_actor.GetComponent<PlayerController>(), Is.SameAs(controller)); Assert.That(_actor.GetComponent<AdultCareerRuntimeComponent>(), Is.SameAs(_owner)); Assert.That(_owner.StudentComponent, Is.SameAs(student)); Assert.That(_actor.activeSelf, Is.True);
        }
        [Test] public void WORLD_PLACEHOLDER_002_ForeignNamedIdentifiedAndNetworkActorsArePreserved()
        {
            var foreign = Candidate("VisitingActor"); var identified = Candidate("Player"); identified.AddComponent<PlayerIdentity>().Configure("another-player");
            var networkActor = Candidate("Player"); Type networkType = null; foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { networkType = assembly.GetType("Unity.Netcode.NetworkObject"); if (networkType != null) break; } Assert.That(networkType, Is.Not.Null); networkActor.AddComponent(networkType);
            Assert.That(WorldEntryService.TryPlaceActor(_scene, _actor, "entry", null), Is.True); Assert.That(foreign.activeSelf, Is.True); Assert.That(identified.activeSelf, Is.True); Assert.That(networkActor.activeSelf, Is.True);
        }
        [TestCase(SessionMode.Client)] [TestCase(SessionMode.Host)] [TestCase(SessionMode.Server)]
        public void WORLD_PLACEHOLDER_003_NetworkModeNeverAuthorizesPlaceholderRemoval(SessionMode mode)
        {
            var placeholder = Candidate("Player"); GameBootstrap.Config.Mode = mode;
            Assert.That(WorldEntryService.TryPlaceActor(_scene, _actor, "entry", null), Is.True); Assert.That(placeholder.activeSelf, Is.True);
        }
        [Test] public void WORLD_PLACEHOLDER_004_UnboundTravellerCannotRemoveTheOfflinePlaceholder()
        {
            var placeholder = Candidate("Player"); var unbound = Go("UnboundTraveller"); unbound.AddComponent<PlayerController>(); unbound.AddComponent<PlayerIdentity>().Configure("unbound");
            Assert.That(WorldEntryService.TryPlaceActor(_scene, unbound, "entry", null), Is.True); Assert.That(placeholder.activeSelf, Is.True);
        }
        private GameObject Candidate(string name) { var go = Go(name); go.AddComponent<PlayerController>(); SceneManager.MoveGameObjectToScene(go, _scene); return go; }
        private GameObject Go(string name) { var value = new GameObject(name); _objects.Add(value); return value; }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
