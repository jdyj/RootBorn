using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class AdultCareerSnapshotTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root;
        [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(), "rootborn-adult-snapshot-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void Cleanup() { foreach (var value in _objects) UnityEngine.Object.DestroyImmediate(value); _objects.Clear(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test] public void ADULT_SNAPSHOT_001_CaptureIncludesConsistentFourDomainState()
        {
            var fixture = Fixture(); object snapshot = Capture(fixture);
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(Field<object>(snapshot, "Adult"), Is.Not.Null); Assert.That(Field<object>(snapshot, "Student"), Is.Not.Null);
            Assert.That(Field<PlayerInventorySaveData>(snapshot, "Inventory").Items[0].Count, Is.EqualTo(17));
            Assert.That(Field<WorldStateProgressSaveData>(snapshot, "World").PlayerId, Is.EqualTo("player"));
        }

        [Test] public void ADULT_SNAPSHOT_002_CaptureRejectsMixedPlayerIdentity()
        {
            var fixture = Fixture(); fixture.World = new WorldStateProgress("slot", "another-player");
            Assert.That(Capture(fixture), Is.Null);
        }

        [Test] public void ADULT_SNAPSHOT_003_RestorePreservesAdultClaimStudentTpInventoryAndWorldTogether()
        {
            var fixture = Fixture(); var snapshot = Capture(fixture);
            object restored = Static("Restore", snapshot, "slot", "player", new[] { fixture.Coin }, fixture.Calendar);
            Assert.That(restored, Is.Not.Null);
            Assert.That(Property<Inventory>(restored, "Inventory").CountOf(fixture.Coin), Is.EqualTo(17));
            Assert.That(Property<StudentLifeProgress>(restored, "Student").TimePointsRemaining, Is.EqualTo(1));
            var adultSave = Property<object>(restored, "Progress").GetType().GetMethod("ToSaveData").Invoke(Property<object>(restored, "Progress"), null);
            CollectionAssert.Contains(Field<string[]>(adultSave, "AppliedRequestIds"), "paid-work");
            CollectionAssert.Contains(Field<string[]>(adultSave, "EndingIds"), "facility.fixture");
            Assert.That(Property<WorldStateProgress>(restored, "World").IsActive("world.fixture"), Is.True);
        }

        [Test] public void ADULT_SNAPSHOT_004_UnknownItemRejectsWholeRestoreRatherThanLosingRewards()
        {
            var fixture = Fixture(); object snapshot = Capture(fixture);
            Assert.That(Static("Restore", snapshot, "slot", "player", Array.Empty<ItemDefinition>(), fixture.Calendar), Is.Null);
            Assert.That(fixture.Inventory.CountOf(fixture.Coin), Is.EqualTo(17));
        }

        [Test] public void ADULT_SNAPSHOT_005_RestoreRejectsWrongPlayerWithoutApplyingAnything()
        {
            var fixture = Fixture(); object snapshot = Capture(fixture);
            Assert.That(Static("Restore", snapshot, "slot", "other", new[] { fixture.Coin }, fixture.Calendar), Is.Null);
        }

        [Test] public void ADULT_SNAPSHOT_006_SingleFileReplacementLoadsNewestCompleteSnapshot()
        {
            var fixture = Fixture(); var storage = new SaveService("slot", _root);
            Assert.That((bool)Static("Save", storage, Capture(fixture)), Is.True);
            fixture.Inventory.Add(fixture.Coin, 5);
            Assert.That((bool)Static("Save", storage, Capture(fixture)), Is.True);
            var loaded = Static("Load", storage, "player"); Assert.That(loaded, Is.Not.Null);
            Assert.That(Field<PlayerInventorySaveData>(loaded, "Inventory").Items[0].Count, Is.EqualTo(22));
        }

        [Test] public void ADULT_SNAPSHOT_007_InterruptedTemporaryFileCannotOverrideCommittedSnapshot()
        {
            var fixture = Fixture(); var storage = new SaveService("slot", _root);
            Assert.That((bool)Static("Save", storage, Capture(fixture)), Is.True);
            string name = (string)Static("FileNameFor", "player"); File.WriteAllText(Path.Combine(storage.DirectoryPath, name + ".tmp"), "{ partial");
            Assert.That(Field<PlayerInventorySaveData>(Static("Load", storage, "player"), "Inventory").Items[0].Count, Is.EqualTo(17));
        }

        [Test] public void ADULT_SNAPSHOT_008_InvalidReplacementDoesNotDestroyPreviousSave()
        {
            var fixture = Fixture(); var storage = new SaveService("slot", _root);
            Assert.That((bool)Static("Save", storage, Capture(fixture)), Is.True);
            object invalid = Capture(fixture); Field<StudentLifeProgressSaveData>(invalid, "Student").PlayerId = "another-player";
            Assert.That((bool)Static("Save", storage, invalid), Is.False);
            Assert.That(Static("Load", storage, "player"), Is.Not.Null);
        }

        [Test] public void ADULT_SNAPSHOT_009_FileNamesDoNotCollideForDistinctPunctuationIds()
        {
            string first = (string)Static("FileNameFor", "a/b"); string second = (string)Static("FileNameFor", "a_b");
            Assert.That(first, Is.Not.EqualTo(second)); Assert.That(Path.GetFileName(first), Is.EqualTo(first));
        }

        [Test] public void ADULT_SNAPSHOT_010_LegacyMissingFileDoesNotFabricateAdultProgress()
        {
            Assert.That(Static("Load", new SaveService("slot", _root), "player"), Is.Null);
        }

        [Test] public void ADULT_SNAPSHOT_011_StudentCloneSupportsTransitionButIsNotAnAuthoritativeAdultSave()
        {
            var fixture = Fixture(); fixture.Adult = Activator.CreateInstance(Find("AdultCareerProgress"), "slot", "player");
            object snapshot = Capture(fixture); Assert.That(snapshot, Is.Not.Null);
            Assert.That(Static("Restore", snapshot, "slot", "player", new[] { fixture.Coin }, fixture.Calendar), Is.Not.Null);
            Assert.That((bool)Static("Save", new SaveService("slot", _root), snapshot), Is.False);
        }

        [Test] public void ADULT_SNAPSHOT_012_EquipmentSelectionSurvivesTheSameSnapshotAsInventory()
        {
            var fixture = Fixture(); object snapshot = Capture(fixture);
            Field<PlayerInventorySaveData>(snapshot, "Inventory").EquippedToolItemId = fixture.Coin.Id;
            var storage = new SaveService("slot", _root); Assert.That((bool)Static("Save", storage, snapshot), Is.True);
            object restored = Static("Restore", Static("Load", storage, "player"), "slot", "player", new[] { fixture.Coin }, fixture.Calendar);
            Assert.That(Property<string>(restored, "EquippedToolItemId"), Is.EqualTo(fixture.Coin.Id));
        }

        [Test] public void ADULT_SNAPSHOT_013_TemporaryWindowsReadLockStillCommitsCompleteNewSnapshot()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor&&Application.platform!=RuntimePlatform.WindowsPlayer)Assert.Ignore("Windows file sharing contract");
            var f=Fixture(); var storage=new SaveService("slot",_root); Assert.That((bool)Static("Save",storage,Capture(f)),Is.True);
            string file=Path.Combine(storage.DirectoryPath,(string)Static("FileNameFor","player"));f.Inventory.Add(f.Coin,5);
            using(var gate=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var release=new System.Threading.Thread(()=>{System.Threading.Thread.Sleep(25);gate.Dispose();});release.IsBackground=true;release.Start();
                bool saved=(bool)Static("Save",storage,Capture(f));release.Join();Assert.That(saved,Is.True,"A brief reader lock must not lose the requested durable commit");
            }
            Assert.That(Field<PlayerInventorySaveData>(Static("Load",storage,"player"),"Inventory").Items[0].Count,Is.EqualTo(22));
        }
        [Test] public void ADULT_SNAPSHOT_014_PersistentWindowsLockPreservesPriorSaveAndLaterRetry()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor&&Application.platform!=RuntimePlatform.WindowsPlayer)Assert.Ignore("Windows file sharing contract");
            var f=Fixture();var storage=new SaveService("slot",_root);Assert.That((bool)Static("Save",storage,Capture(f)),Is.True);
            string file=Path.Combine(storage.DirectoryPath,(string)Static("FileNameFor","player"));f.Inventory.Add(f.Coin,5);
            using(var gate=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Assert.That((bool)Static("Save",storage,Capture(f)),Is.False);
                Assert.That(Field<PlayerInventorySaveData>(Static("Load",storage,"player"),"Inventory").Items[0].Count,Is.EqualTo(17));
            }
            Assert.That((bool)Static("Save",storage,Capture(f)),Is.True);
            Assert.That(Field<PlayerInventorySaveData>(Static("Load",storage,"player"),"Inventory").Items[0].Count,Is.EqualTo(22));
        }
        private sealed class Data
        {
            public object Adult; public StudentLifeProgress Student; public Inventory Inventory; public WorldStateProgress World;
            public ItemDefinition Coin; public LifeCalendarDefinition Calendar;
        }
        private Data Fixture()
        {
            var f = new Data(); f.Calendar = New<LifeCalendarDefinition>(); f.Calendar.ConfigureForTests(2, 3);
            f.Student = new StudentLifeProgress("slot", "player", 10, 10); f.Student.ConfigureCalendar(f.Calendar); f.Student.TrySpendTimePoints(1);
            f.Coin = New<ItemDefinition>(); typeof(ItemDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(f.Coin, "currency.fixture");
            f.Inventory = new Inventory(); f.Inventory.Add(f.Coin, 17); f.World = new WorldStateProgress("slot", "player");
            var flag = New<WorldStateFlagDefinition>(); typeof(WorldStateFlagDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flag, "world.fixture");
            f.World.TryActivate(flag, new WorldStateActivationSource("", "", "fixture", 1));
            var type = Find("AdultCareerProgressSaveData"); var save = Activator.CreateInstance(type);
            Set(save, "SaveSlot", "slot"); Set(save, "PlayerId", "player"); Set(save, "FacilityId", "facility.fixture"); Set(save, "FacilityLevel", 3);
            Set(save, "AppliedRequestIds", new[] { "paid-work" }); Set(save, "EndingIds", new[] { "facility.fixture" });
            f.Adult = Find("AdultCareerProgress").GetMethod("FromSaveData").Invoke(null, new[] { save }); return f;
        }
        private object Capture(Data f) => Static("Capture", f.Adult, f.Student, f.Inventory, f.World);
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static Type Find(string name) { var type = typeof(StudentLifeProgress).Assembly.GetType("Rootborn.Game.Careers." + name); Assert.That(type, Is.Not.Null, name + " missing"); return type; }
        private static object Static(string method, params object[] args) { var info = Find("AdultCareerSnapshotPersistence").GetMethod(method); Assert.That(info, Is.Not.Null, method + " missing"); return info.Invoke(null, args); }
        private static T Field<T>(object target, string name) { Assert.That(target, Is.Not.Null); return (T)target.GetType().GetField(name).GetValue(target); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name).GetValue(target);
    }
}
