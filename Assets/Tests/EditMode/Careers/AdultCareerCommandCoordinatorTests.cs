using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    // Import after the snapshot types compile; only the coordinator is intentionally absent at RED.
    public sealed class AdultCareerCommandCoordinatorTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root;
        [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(), "rootborn-adult-command-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void Cleanup() { foreach (var value in _objects) UnityEngine.Object.DestroyImmediate(value); _objects.Clear(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test] public void ADULT_COMMAND_001_PersistenceFailureLeavesSourceBudgetItemsAndProgressUntouched()
        {
            var fixture = Fixture(); var coordinator = Coordinator(fixture); string before = JsonUtility.ToJson(fixture.Snapshot); bool published = false;
            Directory.Delete(fixture.Storage.DirectoryPath); File.WriteAllText(fixture.Storage.DirectoryPath, "not a directory");
            var result = Execute(coordinator, fixture.Snapshot, Command(fixture), _ => published = true);
            Assert.That(result.Kind.ToString(), Is.EqualTo("PersistenceFailed")); Assert.That(published, Is.False);
            Assert.That(JsonUtility.ToJson(fixture.Snapshot), Is.EqualTo(before)); Assert.That(fixture.Inventory.CountOf(fixture.Coin), Is.EqualTo(5));
            Assert.That(fixture.Student.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test] public void ADULT_COMMAND_002_PublishRunsAfterCompleteSnapshotIsOnDisk()
        {
            var fixture = Fixture(); var coordinator = Coordinator(fixture); int published = 0;
            var result = Execute(coordinator, fixture.Snapshot, Command(fixture), session =>
            {
                published++; var saved = AdultCareerSnapshotPersistence.Load(fixture.Storage, "player");
                Assert.That(saved, Is.Not.Null); Assert.That(saved.Student.TimePointsSpentToday, Is.EqualTo(1));
                Assert.That(saved.Inventory.Items[0].Count, Is.EqualTo(7)); Assert.That(session.Inventory.CountOf(fixture.Coin), Is.EqualTo(7));
            });
            Assert.That(result.Applied, Is.True); Assert.That(published, Is.EqualTo(1));
        }

        [Test] public void ADULT_COMMAND_003_ReentrantPublishCannotExecuteAnotherCommand()
        {
            var fixture = Fixture(); var coordinator = Coordinator(fixture); int published = 0; AdultCareerResult nested = default;
            var result = Execute(coordinator, fixture.Snapshot, Command(fixture), _ =>
            {
                published++; nested = Execute(coordinator, fixture.Snapshot, Command(fixture), __ => published++);
            });
            Assert.That(result.Applied, Is.True); Assert.That(nested.Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            Assert.That(published, Is.EqualTo(1)); Assert.That(AdultCareerSnapshotPersistence.Load(fixture.Storage, "player").Inventory.Items[0].Count, Is.EqualTo(7));
        }

        [Test] public void ADULT_COMMAND_004_RejectedCommandDoesNotPersistOrPublish()
        {
            var fixture = Fixture(); var coordinator = Coordinator(fixture); bool published = false;
            Func<AdultCareerSession, AdultCareerService, AdultCareerResult> reject = (session, service) => new AdultCareerResult(AdultCareerResultKind.RequirementFailed);
            var result = Execute(coordinator, fixture.Snapshot, reject, _ => published = true);
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed)); Assert.That(published, Is.False);
            Assert.That(AdultCareerSnapshotPersistence.Load(fixture.Storage, "player"), Is.Null);
        }

        [Test] public void ADULT_COMMAND_005_UnresolvableSnapshotCannotSpendOrPublish()
        {
            var fixture = Fixture(); var coordinator = Coordinator(fixture, Array.Empty<ItemDefinition>()); bool published = false;
            var result = Execute(coordinator, fixture.Snapshot, Command(fixture), _ => published = true);
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest)); Assert.That(published, Is.False);
            Assert.That(fixture.Inventory.CountOf(fixture.Coin), Is.EqualTo(5)); Assert.That(AdultCareerSnapshotPersistence.Load(fixture.Storage, "player"), Is.Null);
        }

        [Test] public void ADULT_COMMAND_006_EquipmentChoiceIsCarriedThroughCommandCopyAndCommit()
        {
            var fixture = Fixture(); fixture.Snapshot.Inventory.EquippedToolItemId = fixture.Coin.Id; var coordinator = Coordinator(fixture);
            var result = Execute(coordinator, fixture.Snapshot, Command(fixture), session => Assert.That(session.EquippedToolItemId, Is.EqualTo(fixture.Coin.Id)));
            Assert.That(result.Applied, Is.True);
            Assert.That(AdultCareerSnapshotPersistence.Load(fixture.Storage, "player").Inventory.EquippedToolItemId, Is.EqualTo(fixture.Coin.Id));
        }

        private sealed class Data
        {
            public AdultCareerSnapshot Snapshot; public LifeCalendarDefinition Calendar; public ItemDefinition Coin;
            public StudentLifeProgress Student; public Inventory Inventory; public SaveService Storage;
        }
        private Data Fixture()
        {
            var f = new Data(); f.Calendar = New<LifeCalendarDefinition>(); f.Calendar.ConfigureForTests(2, 3);
            f.Coin = New<ItemDefinition>(); typeof(ItemDefinition).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f.Coin, "currency.fixture");
            f.Student = new StudentLifeProgress("slot", "player", 10, 10); f.Student.ConfigureCalendar(f.Calendar); f.Inventory = new Inventory(); f.Inventory.Add(f.Coin, 5);
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = "slot", PlayerId = "player", FacilityId = "facility.fixture", FacilityLevel = 1 });
            f.Snapshot = AdultCareerSnapshotPersistence.Capture(adult, f.Student, f.Inventory, new WorldStateProgress("slot", "player"));
            f.Storage = new SaveService("slot", _root); return f;
        }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static Func<AdultCareerSession, AdultCareerService, AdultCareerResult> Command(Data f) => (session, service) => { session.Student.TrySpendTimePoints(1); session.Inventory.Add(f.Coin, 2); return new AdultCareerResult(AdultCareerResultKind.Applied); };
        private static object Coordinator(Data f, ItemDefinition[] items = null) { var type = typeof(AdultCareerProgress).Assembly.GetType("Rootborn.Game.Careers.AdultCareerCommandCoordinator"); Assert.That(type, Is.Not.Null, "AdultCareerCommandCoordinator missing"); return Activator.CreateInstance(type, new object[] { Array.Empty<FacilityDefinition>(), items ?? new[] { f.Coin }, f.Calendar, f.Storage }); }
        private static AdultCareerResult Execute(object coordinator, AdultCareerSnapshot current, Func<AdultCareerSession, AdultCareerService, AdultCareerResult> command, Action<AdultCareerSession> publish) => (AdultCareerResult)coordinator.GetType().GetMethod("Execute").Invoke(coordinator, new object[] { current, command, publish });
    }
}
