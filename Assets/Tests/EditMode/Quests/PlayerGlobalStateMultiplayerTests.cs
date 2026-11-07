using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class PlayerGlobalStateMultiplayerTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerGlobalState.ClearForTests();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerGlobalState.ClearForTests();
        }

        [Test]
        public void PLAYER_STATE_NET_001_InventoryIsScopedBySaveSlotAndPlayerId()
        {
            var wood = CreateItem("wood");
            var playerOne = GetInventory("slot-a", "player-1");
            var playerTwo = GetInventory("slot-a", "player-2");

            playerOne.Add(wood, 3);
            playerTwo.Add(wood, 1);

            Assert.AreNotSame(playerOne, playerTwo);
            Assert.AreEqual(3, playerOne.CountOf(wood));
            Assert.AreEqual(1, playerTwo.CountOf(wood));
        }

        [Test]
        public void PLAYER_STATE_NET_002_QuestLogsAreScopedBySaveSlotAndPlayerId()
        {
            var quest = CreateQuest("quest.multiplayer.wood");
            var playerOne = GetQuestLog("slot-a", "player-1", new[] { quest });
            var playerTwo = GetQuestLog("slot-a", "player-2", new[] { quest });

            Assert.IsTrue(playerOne.Accept(quest));
            playerOne.RecordEvent(new QuestEvent(QuestEventKind.Gather, "p1-gather-1"));

            Assert.AreNotSame(playerOne, playerTwo);
            Assert.AreEqual(QuestState.Completed, playerOne.GetState(quest));
            Assert.AreEqual(1, playerOne.GetObjectiveCount(quest, 0));
            Assert.AreEqual(QuestState.NotStarted, playerTwo.GetState(quest));
            Assert.AreEqual(0, playerTwo.GetObjectiveCount(quest, 0));
        }

        [Test]
        public void PLAYER_STATE_NET_003_SamePlayerStateSurvivesFreshRuntimeObjectButDoesNotLeakToOtherPlayer()
        {
            var wood = CreateItem("wood");
            var playerOneFirstRuntime = GetInventory("slot-a", "player-1");
            playerOneFirstRuntime.Add(wood, 7);

            var playerOneSecondRuntime = GetInventory("slot-a", "player-1");
            var playerTwoRuntime = GetInventory("slot-a", "player-2");

            Assert.AreSame(playerOneFirstRuntime, playerOneSecondRuntime);
            Assert.AreEqual(7, playerOneSecondRuntime.CountOf(wood));
            Assert.AreEqual(0, playerTwoRuntime.CountOf(wood));
        }

        private static Inventory GetInventory(string saveSlot, string playerId)
        {
            var method = typeof(PlayerGlobalState).GetMethod("GetInventory", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
            Assert.IsNotNull(method, "PlayerGlobalState must expose GetInventory(saveSlot, playerId) for multiplayer-scoped inventory state.");
            return (Inventory)method.Invoke(null, new object[] { saveSlot, playerId });
        }

        private static QuestLog GetQuestLog(string saveSlot, string playerId, QuestDefinition[] quests)
        {
            var method = typeof(PlayerGlobalState).GetMethod("GetQuestLog", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string), typeof(QuestDefinition[]) }, null);
            Assert.IsNotNull(method, "PlayerGlobalState must expose GetQuestLog(saveSlot, playerId, quests) for multiplayer-scoped quest state.");
            return (QuestLog)method.Invoke(null, new object[] { saveSlot, playerId, quests });
        }

        private static ItemDefinition CreateItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_maxStack", 99);
            return item;
        }

        private static QuestDefinition CreateQuest(string id)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_id", id);
            SetField(quest, "_objectives", new QuestObjectiveBase[] { ScriptableObject.CreateInstance<AlwaysMatchObjective>() });
            return quest;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }
    }

    // QUEST-SESSION: the per-player quest runtime that replaced the hidden Town quest list panel.
    public sealed class PlayerQuestRuntimeTests
    {
        private const string Slot = "quest-session-slot";
        private const string Player = "player-a";
        private readonly System.Collections.Generic.List<UnityEngine.Object> _objects = new System.Collections.Generic.List<UnityEngine.Object>();
        private string _root;
        private QuestDefinition _quest;
        private ItemDefinition _stackedReward;
        private ItemDefinition _newReward;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rb-quest-session-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(_root);
            PlayerGlobalState.ClearForTests();
            _stackedReward = Item("item.session.coin", 99);
            _newReward = Item("item.session.badge", 99);
            _quest = Quest("quest.session.main", Reward(_stackedReward, 2), Reward(_newReward, 1));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            foreach (var item in _objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            _objects.Clear();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void QUEST_SESSION_001_LoadsTheSlotFileOnceAndKeepsTheSameLogAfterwards()
        {
            WriteSaved(Saved(_quest, QuestState.Active));
            var first = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });
            Assert.AreEqual(QuestState.Active, first.QuestLog.GetState(_quest), "The saved quest log file must load.");
            Assert.AreSame(PlayerGlobalState.GetQuestLog(Slot, Player, null), first.QuestLog, "One shared log per slot and player.");

            WriteSaved(Saved(_quest, QuestState.NotStarted));
            var again = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });
            Assert.AreSame(first, again);
            Assert.AreEqual(QuestState.Active, again.QuestLog.GetState(_quest), "A later scene must not reload the file over the live log.");
        }

        [Test]
        public void QUEST_SESSION_002_EveryStateChangeIsSavedWithTheExistingFileName()
        {
            var session = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });
            int changes = 0;
            Action<PlayerQuestSession> onChanged = _ => changes++;
            PlayerQuestRuntime.Changed += onChanged;
            try
            {
                Assert.IsTrue(session.QuestLog.Accept(_quest));
                Assert.AreEqual(QuestState.Active, SavedState(), "Accepting must be saved right away.");
                session.QuestLog.RecordEvent(new QuestEvent(QuestEventKind.Gather, "session-gather-1"));
                Assert.AreEqual(QuestState.Completed, SavedState(), "Objective progress must be saved right away.");
                Assert.Greater(changes, 0);
                Assert.IsTrue(File.Exists(Path.Combine(_root, Slot, "quest-log-player-a.json")), "Existing saves use this file name.");
                Assert.AreEqual("quest-log.json", PlayerQuestRuntime.LogFileName(PlayerIdentity.DefaultPlayerId));
            }
            finally
            {
                PlayerQuestRuntime.Changed -= onChanged;
            }
        }

        [Test]
        public void QUEST_SESSION_003_ClaimGrantsEveryRewardOnceEvenAfterReload()
        {
            var inventory = new Inventory();
            var session = CompletedSession();
            var context = session.RewardContext(inventory);

            Assert.IsTrue(session.TryClaimReward(_quest, in context));
            Assert.AreEqual(2, inventory.CountOf(_stackedReward));
            Assert.AreEqual(1, inventory.CountOf(_newReward));
            Assert.AreEqual(QuestState.RewardClaimed, SavedState(), "The claim must be saved right after claiming.");
            Assert.IsFalse(session.TryClaimReward(_quest, in context), "A repeated claim grants nothing.");

            PlayerGlobalState.ClearForTests();
            var reloaded = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });
            Assert.AreNotSame(session, reloaded);
            Assert.AreEqual(QuestState.RewardClaimed, reloaded.QuestLog.GetState(_quest));
            var reloadedContext = reloaded.RewardContext(inventory);
            Assert.IsFalse(reloaded.TryClaimReward(_quest, in reloadedContext), "A claim after reload grants nothing.");
            Assert.AreEqual(2, inventory.CountOf(_stackedReward));
            Assert.AreEqual(1, inventory.CountOf(_newReward));
        }

        [Test]
        public void QUEST_SESSION_004_NoRoomForOneRewardChangesNothing()
        {
            var inventory = new Inventory();
            inventory.Add(_stackedReward, 1);
            for (int i = 1; i < Inventory.MaxSlots; i++) inventory.Add(Item("item.session.filler." + i, 1), 1);
            Assert.IsFalse(inventory.CanAdd(_newReward, 1), "Fixture: every slot is taken.");
            Assert.IsTrue(inventory.CanAdd(_stackedReward, 2), "Fixture: the stacked reward alone would fit.");
            var session = CompletedSession();
            string savedBefore = File.ReadAllText(SavedPath());
            var context = session.RewardContext(inventory);

            Assert.IsFalse(session.QuestLog.CanClaimReward(_quest, in context), "Room is checked for all rewards together.");
            Assert.IsFalse(session.TryClaimReward(_quest, in context));
            Assert.AreEqual(1, inventory.CountOf(_stackedReward), "The reward that would fit alone must not be granted either.");
            Assert.AreEqual(0, inventory.CountOf(_newReward));
            Assert.AreEqual(QuestState.Completed, session.QuestLog.GetState(_quest));
            Assert.AreEqual(savedBefore, File.ReadAllText(SavedPath()), "A failed claim must not change the save.");
        }

        [Test]
        public void QUEST_SESSION_005_QuestsAddedLaterRestoreSavedProgressAndUnlistedProgressIsNeverDropped()
        {
            var linked = Quest("quest.session.linked");
            WriteSaved(Saved(_quest, QuestState.NotStarted), Saved(linked, QuestState.Active));
            var session = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });

            Assert.IsTrue(session.QuestLog.Accept(_quest));
            StringAssert.Contains("quest.session.linked", File.ReadAllText(SavedPath()), "Saving must keep progress of quests not listed yet.");

            Assert.IsTrue(session.AddQuests(new[] { linked }));
            Assert.AreEqual(QuestState.Active, session.QuestLog.GetState(linked), "An added quest restores its saved progress.");
            Assert.IsFalse(session.AddQuests(new[] { linked }), "Adding the same quest again changes nothing.");
            Assert.AreEqual(2, session.Quests.Count);
        }

        [Test]
        public void QUEST_SESSION_006_UnreadableFileIsReportedAndNeverOverwritten()
        {
            Directory.CreateDirectory(Path.Combine(_root, Slot));
            File.WriteAllText(SavedPath(), "{ invalid quest json");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Quest log could not be read"));
            var session = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });

            Assert.IsTrue(session.LoadFailed);
            session.QuestLog.Accept(_quest);
            Assert.IsFalse(session.Save());
            Assert.AreEqual("{ invalid quest json", File.ReadAllText(SavedPath()));
        }

        [Test]
        public void QUEST_SESSION_007_WithoutASaveSlotNothingIsWritten()
        {
            var session = PlayerQuestRuntime.Get(string.Empty, Player, new[] { _quest });
            Assert.IsFalse(session.IsPersistent);
            Assert.IsTrue(session.QuestLog.Accept(_quest));
            Assert.IsFalse(session.Save());
            Assert.IsFalse(Directory.Exists(_root) && Directory.GetFiles(_root, "quest-log*", SearchOption.AllDirectories).Length > 0);
        }

        private PlayerQuestSession CompletedSession()
        {
            var session = PlayerQuestRuntime.Get(Slot, Player, new[] { _quest });
            Assert.IsTrue(session.QuestLog.Accept(_quest));
            session.QuestLog.RecordEvent(new QuestEvent(QuestEventKind.Gather, "session-complete"));
            Assert.AreEqual(QuestState.Completed, session.QuestLog.GetState(_quest));
            return session;
        }

        private string SavedPath() => Path.Combine(_root, Slot, PlayerQuestRuntime.LogFileName(Player));

        private QuestState SavedState()
        {
            var data = JsonUtility.FromJson<QuestLogSaveData>(File.ReadAllText(SavedPath()));
            foreach (var entry in data.Quests) if (entry.QuestId == _quest.Id) return entry.State;
            Assert.Fail("Quest missing from the saved file.");
            return QuestState.NotStarted;
        }

        private void WriteSaved(params QuestProgressSaveData[] entries)
        {
            new SaveService(Slot).WriteJson(PlayerQuestRuntime.LogFileName(Player), JsonUtility.ToJson(new QuestLogSaveData { Quests = entries }, true));
        }

        private static QuestProgressSaveData Saved(QuestDefinition quest, QuestState state)
        {
            var log = new QuestLog(new[] { quest });
            if (state != QuestState.NotStarted) log.Accept(quest);
            var data = log.ToSaveData().Quests[0];
            Assert.AreEqual(state, data.State, "Fixture supports NotStarted and Active.");
            return data;
        }

        private ItemDefinition Item(string id, int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_maxStack", maxStack);
            _objects.Add(item);
            return item;
        }

        private ItemQuestReward Reward(ItemDefinition item, int count)
        {
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", count);
            _objects.Add(reward);
            return reward;
        }

        private QuestDefinition Quest(string id, params QuestRewardBase[] rewards)
        {
            var objective = ScriptableObject.CreateInstance<GatherAnyObjective>();
            objective.ConfigureForRuntime("objective.session", 1);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureForRuntime(id, id, id, new QuestObjectiveBase[] { objective }, rewards, null);
            _objects.Add(objective);
            _objects.Add(quest);
            return quest;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }

        private sealed class GatherAnyObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => questEvent.Kind == QuestEventKind.Gather;
        }
    }
}
