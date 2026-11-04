using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainProgressStoreTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _tempRoot;
        private GameDataRegistry _registry;
        private QuestChainDefinition _chain;
        private NpcDefinition _guide;
        private NpcDefinition _teacher;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "rootborn-chain-store", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            SaveService.SetRootDirectoryForTests(_tempRoot);
            _registry = Own(ScriptableObject.CreateInstance<GameDataRegistry>());
            _guide = Own(ScriptableObject.CreateInstance<NpcDefinition>());
            _teacher = Own(ScriptableObject.CreateInstance<NpcDefinition>());
            _chain = MakeChain("chain.fixture", _teacher, _teacher);
            Set(_registry, "_questChains", new[] { _chain });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }

        [Test]
        public void Store_ReusesLogForSameIdentity_AndIsolatesPlayersAndSaveSlots()
        {
            var store = NewStore();
            var a = Progress("slot-a", "player-a");
            var log = Get(store, a);
            Assert.That(Get(store, Progress("slot-a", "player-a")), Is.SameAs(log));
            log.Accept(_chain);
            Assert.That(Get(store, Progress("slot-a", "player-b")), Is.Not.SameAs(log));
            Assert.That(Get(store, Progress("slot-b", "player-a")), Is.Not.SameAs(log));
            Assert.That(Get(store, Progress("slot-a", "player-b")).GetState(_chain), Is.EqualTo(QuestChainState.Available));
            Assert.That(Get(store, Progress("slot-b", "player-a")).GetState(_chain), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void MatchingStartNpc_AcceptsAndTracksOnlyItsBoundChain_ThenCreditsThatSameEvent()
        {
            var store = NewStore();
            var other = MakeChain("chain.other", _teacher);
            Set(_registry, "_questChains", new[] { _chain, other });
            Set(_chain, "_startNpc", _teacher);
            Set(other, "_startNpc", _guide);
            var student = Progress("slot-a", "player-a");
            Record(store, student, new QuestEvent(QuestEventKind.Talk, "teacher-first", npc: _teacher));
            var log = Get(store, student);
            Assert.That(log.IsTracked(_chain), Is.True);
            Assert.That(log.GetCurrentStep(_chain), Is.SameAs(_chain.Steps[1]), "The starting talk itself may satisfy the first objective.");
            Assert.That(log.GetState(other), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void SaveReload_KeepsCurrentStepAndEventDedupe_WithoutCreditingAnOldTalkAgain()
        {
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            Get(store, student).Accept(_chain);
            Record(store, student, new QuestEvent(QuestEventKind.Talk, "same-talk", npc: _teacher));
            Call(store, "Save", student);
            var restoredStore = NewStore();
            var restored = Get(restoredStore, student);
            Assert.That(restored.GetCurrentStep(_chain), Is.SameAs(_chain.Steps[1]));
            Record(restoredStore, student, new QuestEvent(QuestEventKind.Talk, "same-talk", npc: _teacher));
            Assert.That(restored.GetState(_chain), Is.Not.EqualTo(QuestChainState.Completed));
            Record(restoredStore, student, new QuestEvent(QuestEventKind.Talk, "fresh-talk", npc: _teacher));
            Assert.That(restored.GetState(_chain), Is.EqualTo(QuestChainState.Completed));
        }

        [Test]
        public void PersistedRecords_CarryActualIdentity_AndAnotherPlayerCannotLoadThem()
        {
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            var log = Get(store, student);
            log.Accept(_chain);
            Call(store, "Save", student);
            var saved = log.ToSaveData().Chains;
            Assert.That(saved.Length, Is.EqualTo(1));
            Assert.That(saved[0].SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(saved[0].PlayerId, Is.EqualTo("player-a"));
            var restarted = NewStore();
            Assert.That(Get(restarted, student).GetState(_chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(Get(restarted, Progress("slot-a", "player-b")).GetState(_chain), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void Clear_RemovesMemoryStateAndReloadsOnlySavedState()
        {
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            var original = Get(store, student);
            original.Accept(_chain);
            Call(store, "Clear");
            var reloaded = Get(store, student);
            Assert.That(reloaded, Is.Not.SameAs(original));
            Assert.That(reloaded.GetState(_chain), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void DistinctPlayerIdsWithInvalidFilenameCharacters_DoNotCollideOnReload()
        {
            var store = NewStore();
            var first = Progress("slot-a", "player:a");
            var second = Progress("slot-a", "player?a");
            Get(store, first).Accept(_chain);
            Call(store, "Save", first);
            Call(store, "Save", second);
            var restarted = NewStore();
            Assert.That(Get(restarted, first).GetState(_chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(Get(restarted, second).GetState(_chain), Is.EqualTo(QuestChainState.Available));
        }

        private object NewStore()
        {
            var type = typeof(QuestChainLog).Assembly.GetType("Rootborn.Game.Quests.QuestChainProgressStore");
            Assert.That(type, Is.Not.Null, "A per-player/save-slot chain store is required for Town/House progress.");
            return Activator.CreateInstance(type);
        }
        private QuestChainLog Get(object store, StudentLifeProgress student) => (QuestChainLog)Call(store, "GetOrCreate", _registry, student);
        private void Record(object store, StudentLifeProgress student, QuestEvent questEvent)
        {
            var rewards = new RewardRuntimeContext(null, new Inventory(), null, null, student);
            Call(store, "Record", _registry, student, questEvent, rewards);
        }
        private static object Call(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, target.GetType().Name + "." + methodName);
            return method.Invoke(target, args);
        }
        private QuestChainDefinition MakeChain(string id, params NpcDefinition[] targets)
        {
            var steps = new ScriptableObject[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                var objective = Own(ScriptableObject.CreateInstance<TalkQuestObjective>());
                objective.ConfigureForRuntime("talk", 1);
                Set(objective, "_targetNpc", targets[i]);
                var step = Own(ScriptableObject.CreateInstance<QuestStepDefinition>());
                step.ConfigureForTests(id + ".step-" + i, "step", "step", new QuestObjectiveBase[] { objective }, null, null, null, null);
                steps[i] = step;
            }
            var chain = Own(ScriptableObject.CreateInstance<QuestChainDefinition>());
            chain.ConfigureForTests(id, "chain", "chain", null, steps, null, null, null, null, 0);
            return chain;
        }
        private static StudentLifeProgress Progress(string slot, string player) => new StudentLifeProgress(slot, player, 20, 20);
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
        private T Own<T>(T value) where T : Object { _objects.Add(value); return value; }
    }
}
