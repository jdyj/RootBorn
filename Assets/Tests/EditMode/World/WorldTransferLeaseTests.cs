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
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class WorldTransferLeaseTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _root;
        private string _slot;
        private readonly List<AdultCareerRuntimeComponent> _owners = new List<AdultCareerRuntimeComponent>();
        private AppConfig _oldConfig;
        private SaveSlotMetadata _oldMetadata;
        private INetworkSession _oldSession;
        private GameDataRegistry _registry, _oldRegistry;
        private SaveService _storage;
        private LifeCalendarDefinition _calendar;
        private bool _initialized;
        [SetUp] public void Setup()
        {
            Assert.That(typeof(AdultCareerRuntimeComponent).GetMethod("TryBeginSceneTransfer"), Is.Not.Null, "Scene travel needs a saved, pinned owner lease before loading.");
            _oldConfig = GameBootstrap.Config; _oldMetadata = ActiveSaveContext.Metadata; _oldSession = NetworkBootstrap.ActiveSession;
            GameDataRegistryProvider.TryGet(out _oldRegistry); _initialized = true;
            _slot = "transfer-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _root = Path.Combine(Path.GetTempPath(), "rb-travel-" + Guid.NewGuid().ToString("N"));
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = _slot });
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot });
            _storage = new SaveService(_slot, _root); _calendar = New<LifeCalendarDefinition>(); _calendar.ConfigureForTests(3, 3);
            _registry = New<GameDataRegistry>(); Set(_registry, "_studentLifeCalendar", _calendar); GameDataRegistryProvider.SetOverrideForTests(_registry);
        }
        [TearDown] public void TearDown()
        {
            foreach (var owner in _owners) AdultCareerPersistenceRouter.Unregister(owner); _owners.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]); _objects.Clear();
            if (!_initialized) return;
            SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(_oldMetadata); GameDataRegistryProvider.SetOverrideForTests(_oldRegistry);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _oldConfig); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _oldSession);
            if (Directory.Exists(_root)) Directory.Delete(_root, true); _initialized = false;
        }
        [Test] public void WORLD_TRANSFER_001_PreparePersistsCurrentAggregateAndFreezesCommandsWithoutReplacingModels()
        {
            var owner = Owner(); var student = owner.StudentComponent.Progress; var inventory = owner.PlayerInventory.Inventory; student.TrySpendTimePoints(1);
            Assert.That(Begin(owner), Is.True); Assert.That(Pending(owner), Is.True); Assert.That(owner.CanMutate, Is.False);
            var snapshot = AdultCareerSnapshotPersistence.Load(_storage, "actor"); Assert.That(snapshot, Is.Not.Null); Assert.That(snapshot.Student.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(owner.StudentComponent.Progress, Is.SameAs(student)); Assert.That(owner.PlayerInventory.Inventory, Is.SameAs(inventory));
            Assert.That(owner.ExecuteSessionCommand((session, service) => { session.Student.TrySpendTimePoints(1); return new AdultCareerResult(AdultCareerResultKind.Applied); }).Applied, Is.False);
            Assert.That(student.TimePointsSpentToday, Is.EqualTo(1));
        }
        [Test] public void WORLD_TRANSFER_002_DestinationDuplicateCannotStealPinnedLeaseOrOverwriteSnapshot()
        {
            var original = Owner(); var student = original.StudentComponent.Progress; Assert.That(Begin(original), Is.True); var duplicate = Owner();
            Assert.That(AdultCareerPersistenceRouter.IsOwner(_slot, "actor", original), Is.True); Assert.That(duplicate.CanMutate, Is.False); Assert.That(duplicate.SaveCurrent(), Is.False);
            Assert.That(Complete(original), Is.True); Assert.That(original.CanMutate, Is.True); Assert.That(original.StudentComponent.Progress, Is.SameAs(student)); Assert.That(duplicate.CanMutate, Is.False);
        }
        [Test] public void WORLD_TRANSFER_003_FailedDepartureSaveDoesNotPinOrAuthorizeLoading()
        {
            var owner = Owner(); Directory.Delete(_storage.DirectoryPath, true); File.WriteAllText(_storage.DirectoryPath, "blocked");
            Assert.That(Begin(owner), Is.False); Assert.That(Pending(owner), Is.False); Assert.That(owner.RecoveryRequired, Is.True);
        }
        [TestCase(SessionMode.Client)] [TestCase(SessionMode.Host)] [TestCase(SessionMode.Server)]
        public void WORLD_TRANSFER_004_ApplicationNetworkAuthorityDeniesLocalTransfer(SessionMode mode)
        {
            var owner = Owner(); GameBootstrap.Config.Mode = mode; Assert.That(Begin(owner), Is.False); Assert.That(Pending(owner), Is.False);
            Assert.That(AdultCareerSnapshotPersistence.HasCommittedFile(_storage, "actor"), Is.False);
        }
        [Test] public void WORLD_TRANSFER_005_PendingStudentAttemptBlocksTravelWithoutForfeitingIt()
        {
            var owner = Owner(); var student = owner.StudentComponent.Progress;
            typeof(StudentLifeProgress).GetMethod("ReserveMinigame", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(student,
                new object[] { new PendingStudentMinigameSaveData { RequestId = "reserved", DefinitionId = "practice", MinigameId = "game", Day = 1, ReservedTimePoints = 1 } });
            Assert.That(Begin(owner), Is.False); Assert.That(student.HasPendingMinigame, Is.True); Assert.That(student.HasAppliedRequest("reserved"), Is.False);
        }
        [Test] public void WORLD_TRANSFER_006_RepeatedPrepareAndCompleteAreNotAdditionalTransfers()
        {
            var owner = Owner(); Assert.That(Begin(owner), Is.True); Assert.That(Begin(owner), Is.False); Assert.That(Complete(owner), Is.True); Assert.That(Complete(owner), Is.False); Assert.That(Pending(owner), Is.False);
        }
        [Test] public void WORLD_TRANSFER_007_ChangedIdentityCannotCompleteAndCancelReleasesOnlyItsPin()
        {
            var owner = Owner(); Assert.That(Begin(owner), Is.True); owner.GetComponent<PlayerIdentity>().Configure("different"); Assert.That(Complete(owner), Is.False);
            Call(owner, "CancelSceneTransfer"); Assert.That(Pending(owner), Is.False); var arriving = Owner(); Assert.That(arriving.CanMutate, Is.True); Assert.That(owner.CanMutate, Is.False);
        }
        [Test] public void WORLD_TRANSFER_008_CancelRestoresSameOwnerAndOrdinaryLatestOwnerSemantics()
        {
            var owner = Owner(); Assert.That(Begin(owner), Is.True); Call(owner, "CancelSceneTransfer"); Assert.That(owner.CanMutate, Is.True);
            var next = Owner(); Assert.That(next.CanMutate, Is.True); Assert.That(owner.CanMutate, Is.False);
        }
        [Test] public void WORLD_TRANSFER_009_DestroyedUnityOwnerCannotKeepItsPinnedLease()
        {
            var owner = Owner(); Assert.That(Begin(owner), Is.True); Object.DestroyImmediate(owner.gameObject);
            Assert.That(owner == null, Is.True, "Unity object has been destroyed while its managed reference remains available");
            var next = Owner();
            Assert.That(next.CanMutate, Is.True, "Registration must discard a pinned owner whose Unity object is destroyed, even when EditMode omits lifecycle callbacks");
        }
        private AdultCareerRuntimeComponent Owner()
        {
            var go = new GameObject("TravellingActor"); _objects.Add(go); go.AddComponent<PlayerIdentity>().Configure("actor");
            var student = go.AddComponent<StudentLifeProgressComponent>(); student.ConfigureForTests(_slot, "actor", 12, 10, 0, 480); student.Progress.ConfigureCalendar(_calendar);
            go.AddComponent<PlayerInventory>(); var owner = go.AddComponent<AdultCareerRuntimeComponent>(); owner.Bind(_registry, _storage); Assert.That(owner.IsBound, Is.True); _owners.Add(owner); return owner;
        }
        private static bool Begin(AdultCareerRuntimeComponent owner) => (bool)Call(owner, "TryBeginSceneTransfer");
        private static bool Complete(AdultCareerRuntimeComponent owner) => (bool)Call(owner, "TryCompleteSceneTransfer");
        private static bool Pending(AdultCareerRuntimeComponent owner) { var property = owner.GetType().GetProperty("SceneTransferPending"); Assert.That(property, Is.Not.Null); return (bool)property.GetValue(owner); }
        private static object Call(object target, string name) { var method = target.GetType().GetMethod(name); Assert.That(method, Is.Not.Null, name); return method.Invoke(target, null); }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
