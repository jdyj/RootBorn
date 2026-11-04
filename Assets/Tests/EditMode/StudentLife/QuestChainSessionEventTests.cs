using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.Quests;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainSessionEventTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _root;
        private LifeCalendarDefinition _calendar;
        private ItemDefinition _coin;
        private PartTimeWorkDefinition _work;
        private QuestChainRuntimeService _service;
        private AdultCareerSnapshot _source;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-quest-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root); SaveService.SetRootDirectoryForTests(_root);
            _calendar = Make<LifeCalendarDefinition>(); _calendar.ConfigureForTests(2, 3);
            _coin = Make<ItemDefinition>(); Set(_coin, "_id", "item.session-coin"); Set(_coin, "_maxStack", 999);
            _work = Make<PartTimeWorkDefinition>(); _work.ConfigureForTests("work.session", "Work", null, 0, 0, 0, 0, null, null, null);
            var objective = Make<PartTimeWorkQuestObjective>(); objective.ConfigureForRuntime("objective.work", 1); Set(objective, "_acceptedWorks", new[] { _work });
            var reward = Make<ItemQuestReward>(); Set(reward, "_item", _coin); Set(reward, "_count", 2);
            var step = Make<QuestStepDefinition>(); step.ConfigureForTests("step.first-job", "First job", "Complete work", new QuestObjectiveBase[] { objective }, null, null, new QuestRewardBase[] { reward }, null);
            var chain = Make<QuestChainDefinition>(); chain.ConfigureForTests("chain.first-job", "First job", "First job", "", new[] { step }, null, null, null, null, 0);
            var registry = Make<GameDataRegistry>(); Set(registry, "_questChains", new[] { chain });
            var go = new GameObject("QuestSessionFixture"); _objects.Add(go); _service = go.AddComponent<QuestChainRuntimeService>(); Set(_service, "_registry", registry);
            var student = new StudentLifeProgress("slot-session", "player-session", 100, 100); student.ConfigureCalendar(_calendar);
            _source = AdultCareerSnapshotPersistence.Capture(new AdultCareerProgress(student.SaveSlot, student.PlayerId), student, new Inventory(), new WorldStateProgress(student.SaveSlot, student.PlayerId));
            var log = new QuestChainLog(new[] { chain }); Assert.That(log.Accept(chain), Is.True);
            _source.QuestChains = log.ToSaveData();
            foreach (var record in _source.QuestChains.Chains) { record.SaveSlot = student.SaveSlot; record.PlayerId = student.PlayerId; }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void WorkPayoutAndQuestStepReward_CommitInSameSnapshot_AndQuestReceiptSurvivesReload()
        {
            var session = Restore(_source); Assert.That(session.Inventory.TryAddAll(new[] { new InventoryGrant(_coin, 5) }), Is.True);
            int notifications = 0; _service.ProgressChanged += _ => notifications++;
            Assert.That(Apply(session), Is.True);
            Assert.That(notifications, Is.Zero, "A detached command cannot notify live UI before persistence.");
            var snapshot = AdultCareerSnapshotPersistence.CaptureSession(session);
            Assert.That(AdultCareerSnapshotPersistence.SaveSession(new SaveService("slot-session"), snapshot), Is.True);
            var loaded = Restore(AdultCareerSnapshotPersistence.Load(new SaveService("slot-session"), "player-session"));
            Assert.That(loaded.Inventory.CountOf(_coin), Is.EqualTo(7));
            CollectionAssert.Contains(loaded.QuestChains.Chains[0].CompletedStepIds, "step.first-job");
            CollectionAssert.Contains(loaded.QuestChains.Chains[0].ProcessedEventKeys, "settlement-request");
            Assert.That(Apply(loaded), Is.True);
            Assert.That(loaded.Inventory.CountOf(_coin), Is.EqualTo(7), "Duplicate event cannot repay the step reward.");
            Assert.That(typeof(QuestChainRuntimeService).GetMethod("NotifyCommitted", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
        }

        [Test]
        public void FailedCombinedWrite_PublishesNeitherIncomeQuestReceiptNorUiNotification()
        {
            var storage = new SaveService("slot-session");
            Directory.Delete(storage.DirectoryPath); File.WriteAllText(storage.DirectoryPath, "blocked target");
            string before = JsonUtility.ToJson(_source); int notifications = 0; bool published = false;
            _service.ProgressChanged += _ => notifications++;
            var coordinator = new AdultCareerCommandCoordinator(Array.Empty<FacilityDefinition>(), new[] { _coin }, _calendar, storage);
            var result = coordinator.Execute(_source, (session, service) =>
            {
                Assert.That(session.Inventory.TryAddAll(new[] { new InventoryGrant(_coin, 5) }), Is.True);
                return new AdultCareerResult(Apply(session) ? AdultCareerResultKind.Applied : AdultCareerResultKind.InvalidRequest);
            }, _ => published = true);
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed));
            Assert.That(published, Is.False); Assert.That(notifications, Is.Zero);
            Assert.That(JsonUtility.ToJson(_source), Is.EqualTo(before));
        }

        private bool Apply(AdultCareerSession session)
        {
            var method = typeof(QuestChainRuntimeService).GetMethod("ApplyEventToSession", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Work settlement needs quest credit inside the same detached player command.");
            return (bool)method.Invoke(_service, new object[] { session, new QuestEvent(QuestEventKind.PartTimeWork, "settlement-request", work: _work) });
        }
        private AdultCareerSession Restore(AdultCareerSnapshot snapshot) => AdultCareerSnapshotPersistence.Restore(snapshot, "slot-session", "player-session", new[] { _coin }, _calendar);
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue; field.SetValue(target, value); return;
            }
            Assert.Fail("Missing field: " + name);
        }
    }
}
