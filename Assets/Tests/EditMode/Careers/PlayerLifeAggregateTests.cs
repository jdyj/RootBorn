using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class PlayerLifeAggregateTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root;
        [SetUp] public void Setup() { _root = Path.Combine(Path.GetTempPath(), "rootborn-player-aggregate-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void Cleanup() { foreach (var value in _objects) UnityEngine.Object.DestroyImmediate(value); _objects.Clear(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test] public void PLAYER_AGGREGATE_001_StudentClaimItemsAndFurniturePersistInOneAuthoritativeFile()
        {
            var f = Fixture(); SetExtras(f.Snapshot);
            Assert.That((bool)Call("SaveSession", f.Storage, f.Snapshot), Is.True);
            var loaded = AdultCareerSnapshotPersistence.Load(f.Storage, "player"); Assert.That(loaded, Is.Not.Null);
            Assert.That(Field<QuestChainLogSaveData>(loaded, "QuestChains").Chains[0].RewardClaimedIds, Does.Contain("reward.fixture"));
            Assert.That(Field<FurnitureUnlockProgressSaveData>(loaded, "FurnitureUnlocks").UnlockTokens, Does.Contain("furniture.fixture"));
            Assert.That(loaded.Inventory.Items[0].Count, Is.EqualTo(17)); Assert.That(loaded.Adult.FacilityLevel, Is.Zero);
        }

        [Test] public void PLAYER_AGGREGATE_002_QuestIdentityMismatchCannotReplaceCommittedSession()
        {
            var f = Fixture(); SetExtras(f.Snapshot); Assert.That((bool)Call("SaveSession", f.Storage, f.Snapshot), Is.True);
            Field<QuestChainLogSaveData>(f.Snapshot, "QuestChains").Chains[0].PlayerId = "other";
            Assert.That((bool)Call("SaveSession", f.Storage, f.Snapshot), Is.False);
            Assert.That(Field<QuestChainLogSaveData>(AdultCareerSnapshotPersistence.Load(f.Storage, "player"), "QuestChains").Chains[0].PlayerId, Is.EqualTo("player"));
        }

        [Test] public void PLAYER_AGGREGATE_003_FurnitureIdentityMismatchRejectsWholeSnapshot()
        {
            var f = Fixture(); SetExtras(f.Snapshot); Field<FurnitureUnlockProgressSaveData>(f.Snapshot, "FurnitureUnlocks").SaveSlot = "other";
            Assert.That((bool)Call("SaveSession", f.Storage, f.Snapshot), Is.False);
            Assert.That(AdultCareerSnapshotPersistence.Restore(f.Snapshot, "slot", "player", new[] { f.Coin }, f.Calendar), Is.Null);
        }

        [Test] public void PLAYER_AGGREGATE_004_PrimaryPresenceIsDistinctFromCorruptionAndMissing()
        {
            var f = Fixture(); Assert.That((bool)Call("HasCommittedFile", f.Storage, "player"), Is.False);
            File.WriteAllText(Path.Combine(f.Storage.DirectoryPath, AdultCareerSnapshotPersistence.FileNameFor("player")), "{ broken");
            Assert.That((bool)Call("HasCommittedFile", f.Storage, "player"), Is.True); Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, "player"), Is.Null);
        }

        [Test] public void PLAYER_AGGREGATE_005_QuestCommandCommitsStudentReceiptAndRewardTogether()
        {
            var f = Fixture(); SetExtras(f.Snapshot, false);
            var coordinator = new AdultCareerCommandCoordinator(Array.Empty<FacilityDefinition>(), new[] { f.Coin }, f.Calendar, f.Storage);
            var result = coordinator.Execute(f.Snapshot, (session, service) =>
            {
                session.Inventory.Add(f.Coin, 3); session.Student.TrySpendTimePoints(1);
                ReplaceChains(session, Chains()); Get<FurnitureUnlockProgress>(session, "FurnitureUnlocks").TryUnlock("furniture.fixture");
                return new AdultCareerResult(AdultCareerResultKind.Applied);
            }, session => { });
            Assert.That(result.Applied, Is.True); var loaded = AdultCareerSnapshotPersistence.Load(f.Storage, "player");
            Assert.That(loaded.Inventory.Items[0].Count, Is.EqualTo(20)); Assert.That(loaded.Student.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(Field<QuestChainLogSaveData>(loaded, "QuestChains").Chains[0].RewardClaimedIds, Does.Contain("reward.fixture"));
        }

        [Test] public void PLAYER_AGGREGATE_006_FailedQuestWriteCannotPublishReceiptOrReward()
        {
            var f = Fixture(); SetExtras(f.Snapshot, false); string original = JsonUtility.ToJson(f.Snapshot); bool published = false;
            var coordinator = new AdultCareerCommandCoordinator(Array.Empty<FacilityDefinition>(), new[] { f.Coin }, f.Calendar, f.Storage);
            Directory.Delete(f.Storage.DirectoryPath); File.WriteAllText(f.Storage.DirectoryPath, "blocked");
            var result = coordinator.Execute(f.Snapshot, (session, service) => { session.Inventory.Add(f.Coin, 3); ReplaceChains(session, Chains()); return new AdultCareerResult(AdultCareerResultKind.Applied); }, _ => published = true);
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed)); Assert.That(published, Is.False); Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(original));
        }

        [Test] public void PLAYER_AGGREGATE_007_CaptureSessionDeepCopiesQuestReceiptsAndPreservesEquipment()
        {
            var f = Fixture(); SetExtras(f.Snapshot); f.Snapshot.Inventory.EquippedToolItemId = f.Coin.Id;
            var session = AdultCareerSnapshotPersistence.Restore(f.Snapshot, "slot", "player", new[] { f.Coin }, f.Calendar); Assert.That(session, Is.Not.Null);
            var captured = (AdultCareerSnapshot)Call("CaptureSession", session); Get<QuestChainLogSaveData>(session, "QuestChains").Chains[0].RewardClaimedIds[0] = "mutated";
            Assert.That(Field<QuestChainLogSaveData>(captured, "QuestChains").Chains[0].RewardClaimedIds, Does.Contain("reward.fixture"));
            Assert.That(captured.Inventory.EquippedToolItemId, Is.EqualTo(f.Coin.Id));
        }

        [Test] public void PLAYER_AGGREGATE_008_MissingExtraDomainsInLegacyAdultSnapshotMigrateToEmptyIdentityScopedState()
        {
            var f = Fixture(); f.Snapshot.Adult.FacilityId = "facility.fixture"; f.Snapshot.Adult.FacilityLevel = 1;
            var legacy = new LegacySnapshot { Adult = f.Snapshot.Adult, Student = f.Snapshot.Student, Inventory = f.Snapshot.Inventory, World = f.Snapshot.World };
            f.Storage.WriteJson(AdultCareerSnapshotPersistence.FileNameFor("player"), JsonUtility.ToJson(legacy));
            var loaded = AdultCareerSnapshotPersistence.Load(f.Storage, "player"); Assert.That(loaded, Is.Not.Null);
            Assert.That(Field<QuestChainLogSaveData>(loaded, "QuestChains").Chains, Is.Empty);
            Assert.That(Field<FurnitureUnlockProgressSaveData>(loaded, "FurnitureUnlocks").PlayerId, Is.EqualTo("player"));
        }

        [Serializable] private sealed class LegacySnapshot { public int Version = 1; public AdultCareerProgressSaveData Adult; public StudentLifeProgressSaveData Student; public Rootborn.Game.Player.PlayerInventorySaveData Inventory; public WorldStateProgressSaveData World; }
        private sealed class Data { public AdultCareerSnapshot Snapshot; public SaveService Storage; public ItemDefinition Coin; public LifeCalendarDefinition Calendar; }
        private Data Fixture()
        {
            var f = new Data(); f.Calendar = New<LifeCalendarDefinition>(); f.Calendar.ConfigureForTests(2, 3); f.Coin = New<ItemDefinition>(); typeof(ItemDefinition).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f.Coin, "item.allowance");
            var student = new StudentLifeProgress("slot", "player", 12, 10); student.ConfigureCalendar(f.Calendar); var inventory = new Inventory(); inventory.Add(f.Coin, 17);
            f.Snapshot = AdultCareerSnapshotPersistence.Capture(new AdultCareerProgress("slot", "player"), student, inventory, new WorldStateProgress("slot", "player")); f.Storage = new SaveService("slot", _root); return f;
        }
        private static QuestChainLogSaveData Chains() => new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData { SaveSlot = "slot", PlayerId = "player", ChainId = "chain.fixture", ChainState = "Completed", RewardClaimedIds = new[] { "reward.fixture" }, ProcessedEventKeys = new[] { "event.fixture" } } } };
        private static void SetExtras(AdultCareerSnapshot snapshot, bool claimed = true) { Set(snapshot, "QuestChains", claimed ? Chains() : new QuestChainLogSaveData()); Set(snapshot, "FurnitureUnlocks", new FurnitureUnlockProgressSaveData { SaveSlot = "slot", PlayerId = "player", UnlockTokens = claimed ? new[] { "furniture.fixture" } : Array.Empty<string>() }); }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void ReplaceChains(object target, QuestChainLogSaveData value) { var method = target.GetType().GetMethod("ReplaceQuestChains"); Assert.That(method, Is.Not.Null, "ReplaceQuestChains missing"); method.Invoke(target, new object[] { value }); }
        private static object Call(string name, params object[] args) { var method = typeof(AdultCareerSnapshotPersistence).GetMethod(name); Assert.That(method, Is.Not.Null, name + " missing"); return method.Invoke(null, args); }
        private static T Field<T>(object target, string name) { var field = target.GetType().GetField(name); Assert.That(field, Is.Not.Null, name + " missing"); return (T)field.GetValue(target); }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name); Assert.That(field, Is.Not.Null, name + " missing"); field.SetValue(target, value); }
        private static T Get<T>(object target, string name) { var property = target.GetType().GetProperty(name); Assert.That(property, Is.Not.Null, name + " missing"); return (T)property.GetValue(target); }
    }
}
