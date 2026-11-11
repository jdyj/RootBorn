using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/QuestChainProgressStoreTests.cs, Assets/Tests/EditMode/StudentLife/QuestChainOwnedStoreTests.cs
    // 파일 대신 메모리 저장소(InMemorySaveStore)를 쓴다. 저장소는 읽을 때마다 새 사본을 준다(JsonUtility가 매번 새 객체를 만들던 것과 같다).
    // 파일 이름 규칙 테스트는 SaveFileNamesTests로 옮겼다.
    public sealed class QuestChainProgressStoreTests
    {
        private InMemorySaveStore _storage;
        private QuestChainDefinition _chain;
        private QuestChainDefinition[] _chains;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStore();
            _chain = MakeChain("chain.fixture", null, "npc.teacher", "npc.teacher");
            _chains = new[] { _chain };
        }

        private QuestChainProgressStore NewStore() => new QuestChainProgressStore(_storage);
        private QuestChainLog Get(QuestChainProgressStore store, StudentLifeProgress student) => store.GetOrCreate(_chains, student);
        private void Record(QuestChainProgressStore store, StudentLifeProgress student, QuestEvent questEvent)
            => store.Record(_chains, student, questEvent, new RewardRuntimeContext(null, new ItemInventory(), null, null, student));
        private static StudentLifeProgress Progress(string slot, string player) => new StudentLifeProgress(slot, player, 20, 20);

        private static QuestChainDefinition MakeChain(string id, string startNpcId, params string[] targets)
        {
            var steps = new QuestStepDefinition[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                steps[i] = new QuestStepDefinition(id + ".step-" + i, "step", "step", new QuestObjective[] { new TalkQuestObjective("talk", 1, targets[i]) });
            return new QuestChainDefinition(id, "chain", "chain", null, steps, startNpcId: startNpcId);
        }

        [Test]
        public void Store_ReusesLogForSameIdentity_AndIsolatesPlayersAndSaveSlots()
        {
            var store = NewStore();
            var log = Get(store, Progress("slot-a", "player-a"));
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
            _chain = MakeChain("chain.fixture", "npc.teacher", "npc.teacher", "npc.teacher");
            var other = MakeChain("chain.other", "npc.guide", "npc.teacher");
            _chains = new[] { _chain, other };
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            Record(store, student, new QuestEvent(QuestEventKind.Talk, "teacher-first", "npc.teacher"));
            var log = Get(store, student);
            Assert.That(log.IsTracked(_chain), Is.True);
            Assert.That(log.GetCurrentStep(_chain), Is.SameAs(_chain.Steps[1]), "The starting talk itself may satisfy the first objective.");
            Assert.That(log.GetState(other), Is.EqualTo(QuestChainState.Available));
            Assert.That(_storage.Writes, Is.EqualTo(1), "a recorded event is saved");
        }

        [Test]
        public void SaveReload_KeepsCurrentStepAndEventDedupe_WithoutCreditingAnOldTalkAgain()
        {
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            Get(store, student).Accept(_chain);
            Record(store, student, new QuestEvent(QuestEventKind.Talk, "same-talk", "npc.teacher"));
            store.Save(student);
            var restoredStore = NewStore();
            var restored = Get(restoredStore, student);
            Assert.That(restored.GetCurrentStep(_chain), Is.SameAs(_chain.Steps[1]));
            Record(restoredStore, student, new QuestEvent(QuestEventKind.Talk, "same-talk", "npc.teacher"));
            Assert.That(restored.GetState(_chain), Is.Not.EqualTo(QuestChainState.Completed));
            Record(restoredStore, student, new QuestEvent(QuestEventKind.Talk, "fresh-talk", "npc.teacher"));
            Assert.That(restored.GetState(_chain), Is.EqualTo(QuestChainState.Completed));
        }

        [Test]
        public void PersistedRecords_CarryActualIdentity_AndAnotherPlayerCannotLoadThem()
        {
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            var log = Get(store, student);
            log.Accept(_chain);
            store.Save(student);
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
            store.Clear();
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
            store.Save(first);
            store.Save(second);
            var restarted = NewStore();
            Assert.That(Get(restarted, first).GetState(_chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(Get(restarted, second).GetState(_chain), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void BoundOwnerLog_IsReturnedDirectly_AndSaveUsesOnlyOwnerCallback()
        {
            var store = NewStore();
            var student = new StudentLifeProgress("owned-slot", "owned-player", 20, 20);
            var log = new QuestChainLog(Array.Empty<QuestChainDefinition>());
            int saves = 0;
            store.BindLog(student, log, p => { Assert.That(p, Is.SameAs(student)); saves++; });
            Assert.That(store.GetOrCreate(_chains, student), Is.SameAs(log));
            store.Save(student);
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(_storage.Writes, Is.Zero);
            Assert.That(_storage.Reads, Is.Zero);
        }

        [Test]
        public void BoundOwnerLog_DoesNotAcceptLegacyRecordCallsOutsideAnAggregateCommand()
        {
            var chain = new QuestChainDefinition("chain", "chain", "chain", null, new[] { new QuestStepDefinition("step", "step", "step", null) });
            _chains = new[] { chain };
            var student = new StudentLifeProgress("owned-slot", "owned-player", 20, 20);
            var log = new QuestChainLog(new[] { chain });
            log.Accept(chain);
            var store = NewStore();
            store.BindLog(student, log);
            store.Record(_chains, student, new QuestEvent(QuestEventKind.Talk, "unowned-write"), default);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(log.ToSaveData().Chains[0].ProcessedEventKeys, Is.Empty);
            store.Save(student);
            Assert.That(_storage.Writes, Is.Zero, "a bound log without a callback is not written to the legacy file");
        }

        [Test]
        public void LegacyRecordsWithoutIdentity_LoadOnlyForTheDefaultPlayer()
        {
            var legacy = new QuestChainLogSaveData { Chains = new[]
            {
                null,
                new QuestChainProgressSaveData { SaveSlot = "", PlayerId = "", ChainId = _chain.Id, ChainState = "Active", CurrentStepId = _chain.Steps[0].Id },
            } };
            _storage.Put("slot-a", "quest-chain-progress.json", legacy);
            var store = NewStore();
            var owner = Progress("slot-a", QuestChainProgressStore.DefaultPlayerId);
            var log = Get(store, owner);
            Assert.That(log.GetState(_chain), Is.EqualTo(QuestChainState.Active));
            var record = log.ToSaveData().Chains[0];
            Assert.That(record.SaveSlot, Is.EqualTo("slot-a"), "the loaded record takes the owner's identity");
            Assert.That(record.PlayerId, Is.EqualTo(QuestChainProgressStore.DefaultPlayerId));

            _storage.Put("slot-a", Save.SaveFileNames.QuestChain("player-b"), legacy);
            Assert.That(Get(store, Progress("slot-a", "player-b")).GetState(_chain), Is.EqualTo(QuestChainState.Available), "another player never takes a record without identity");
        }

        [Test]
        public void RecordsForAnotherIdentityInTheSameFile_AreIgnored()
        {
            _storage.Put("slot-a", Save.SaveFileNames.QuestChain("player-a"), new QuestChainLogSaveData { Chains = new[]
            {
                new QuestChainProgressSaveData { SaveSlot = "slot-b", PlayerId = "player-a", ChainId = _chain.Id, ChainState = "Active" },
            } });
            Assert.That(Get(NewStore(), Progress("slot-a", "player-a")).GetState(_chain), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void Record_IgnoresEmptyKeysMissingInputsAndAnotherPlayersRewards()
        {
            var store = NewStore();
            var student = Progress("slot-a", "player-a");
            store.Record(_chains, student, new QuestEvent(QuestEventKind.Talk, "", "npc.teacher"), default);
            store.Record(null, student, new QuestEvent(QuestEventKind.Talk, "k1", "npc.teacher"), default);
            store.Record(_chains, null, new QuestEvent(QuestEventKind.Talk, "k2", "npc.teacher"), default);
            var stranger = Progress("slot-a", "player-b");
            store.Record(_chains, student, new QuestEvent(QuestEventKind.Talk, "k3", "npc.teacher"), new RewardRuntimeContext(null, null, null, null, stranger));
            Assert.That(Get(store, student).GetState(_chain), Is.EqualTo(QuestChainState.Available));
            Assert.That(_storage.Writes, Is.Zero);
            Assert.That(store.GetOrCreate(null, Progress("slot-a", "player-new")), Is.Null, "nothing held and nothing to create from");
            Assert.That(store.GetOrCreate(_chains, null), Is.Null);
        }

        [Test]
        public void UnreadableFile_Throws_InsteadOfStartingOver()
        {
            // Unity: a file that could not be read was treated as no file, so the next save replaced it with a fresh log.
            var store = new QuestChainProgressStore(new UnreadableStore());
            var student = Progress("slot-a", "player-a");

            Assert.Throws<InvalidOperationException>(() => store.GetOrCreate(_chains, student));
            store.Save(student);
        }

        [Test]
        public void StepRewardPaidOnce_AcrossSaveAndReload()
        {
            var coin = new ItemDefinition("item.coin", 99);
            var chain = new QuestChainDefinition("chain.paid", "c", "c", null, new[]
            {
                new QuestStepDefinition("step.talk", "s", "s", new QuestObjective[] { new TalkQuestObjective("talk", 1, "npc.teacher") }, rewards: new QuestReward[] { new ItemQuestReward(coin, 3) }),
                new QuestStepDefinition("step.after", "s", "s", new QuestObjective[] { new TalkQuestObjective("talk", 1, "npc.other") }),
            }, startNpcId: "npc.teacher");
            _chains = new[] { chain };
            var inventory = new ItemInventory();
            var student = Progress("slot-a", "player-a");
            var store = NewStore();
            store.Record(_chains, student, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.teacher"), new RewardRuntimeContext(null, inventory, null, null, student));
            Assert.That(inventory.CountOf(coin), Is.EqualTo(3));

            var reloaded = NewStore();
            reloaded.Record(_chains, student, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.teacher"), new RewardRuntimeContext(null, inventory, null, null, student));
            reloaded.Record(_chains, student, new QuestEvent(QuestEventKind.Talk, "talk-2", "npc.teacher"), new RewardRuntimeContext(null, inventory, null, null, student));
            Assert.That(inventory.CountOf(coin), Is.EqualTo(3));
            Assert.That(reloaded.GetOrCreate(_chains, student).GetCurrentStep(chain).Id, Is.EqualTo("step.after"));
        }

        private sealed class UnreadableStore : Save.ISaveStore
        {
            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => throw new InvalidOperationException("corrupt");
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => Assert.Fail("an unreadable file must not be overwritten");
            public void Delete(string saveSlot, string fileName) => Assert.Fail("unexpected delete");
        }
    }
}
