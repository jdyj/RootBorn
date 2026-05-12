using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
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
}
