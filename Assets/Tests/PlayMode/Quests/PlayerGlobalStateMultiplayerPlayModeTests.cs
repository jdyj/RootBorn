using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class PlayerGlobalStateMultiplayerPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerGlobalState.ClearForTests();
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-mp" });
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAYER_STATE_NET_PM_001_TwoRuntimePlayersKeepSeparateInventoriesAcrossObjectRecreation()
        {
            var wood = CreateItem("wood");
            var registry = CreateRegistry(wood);
            var playerOne = CreatePlayer("PlayerOne", "player-1", registry);
            var playerTwo = CreatePlayer("PlayerTwo", "player-2", registry);
            try
            {
                playerOne.Inventory.Add(wood, 5);
                playerTwo.Inventory.Add(wood, 2);
                yield return null;

                Object.Destroy(playerOne.gameObject);
                yield return null;
                var playerOneReloaded = CreatePlayer("PlayerOneReloaded", "player-1", registry);

                Assert.AreEqual(5, playerOneReloaded.Inventory.CountOf(wood));
                Assert.AreEqual(2, playerTwo.Inventory.CountOf(wood));
                Assert.AreNotSame(playerOneReloaded.Inventory, playerTwo.Inventory);

                Object.Destroy(playerOneReloaded.gameObject);
            }
            finally
            {
                if (playerOne != null)
                {
                    Object.Destroy(playerOne.gameObject);
                }
                if (playerTwo != null)
                {
                    Object.Destroy(playerTwo.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator PLAYER_STATE_NET_PM_002_TwoRuntimePlayersKeepSeparateQuestProgressInSameSaveSlot()
        {
            var quest = CreateQuest("quest.mp.gather");
            var playerOneLog = PlayerGlobalState.GetQuestLog("slot-mp", "player-1", new[] { quest });
            var playerTwoLog = PlayerGlobalState.GetQuestLog("slot-mp", "player-2", new[] { quest });

            Assert.IsTrue(playerOneLog.Accept(quest));
            playerOneLog.RecordEvent(new QuestEvent(QuestEventKind.Gather, "player-1-event"));
            yield return null;

            Assert.AreEqual(QuestState.Completed, playerOneLog.GetState(quest));
            Assert.AreEqual(1, playerOneLog.GetObjectiveCount(quest, 0));
            Assert.AreEqual(QuestState.NotStarted, playerTwoLog.GetState(quest));
            Assert.AreEqual(0, playerTwoLog.GetObjectiveCount(quest, 0));
        }

        private static PlayerInventory CreatePlayer(string name, string playerId, GameDataRegistry registry)
        {
            var go = new GameObject(name, typeof(PlayerIdentity), typeof(PlayerInventory));
            go.GetComponent<PlayerIdentity>().Configure(playerId);
            var inventory = go.GetComponent<PlayerInventory>();
            inventory.Bind(registry);
            return inventory;
        }

        private static GameDataRegistry CreateRegistry(ItemDefinition item)
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetField(registry, "_items", new[] { item });
            return registry;
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
