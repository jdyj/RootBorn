using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class NpcQuestNetworkSyncTests
    {
        [Test]
        public void NPC_QUEST_008_ServerAuthorityBroadcastsQuestAcceptToRegisteredClients()
        {
            var quest = CreateQuest("quest-network-accept");
            var log = new QuestLog(new[] { quest });
            var broadcaster = CreateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            Invoke(broadcaster, "RegisterClient", 10UL);
            Invoke(broadcaster, "RegisterClient", 20UL);
            Invoke(broadcaster, "Attach", log);

            Assert.IsTrue(log.Accept(quest));

            Assert.AreEqual(2, GetInt(broadcaster, "BroadcastCount"));
            Assert.AreEqual(2, GetInt(broadcaster, "RecipientDeliveryCount"));
            Assert.AreEqual(1, GetInt(broadcaster, "StateChangeCount"));
            Assert.AreEqual("slot-a", GetString(broadcaster, "LastSaveSlot"));
            Assert.AreEqual(QuestState.Active.ToString(), GetString(broadcaster, "LastState"));
        }

        [Test]
        public void NPC_QUEST_009_ServerAuthorityBroadcastsProgressCompleteAndRewardClaimStates()
        {
            var quest = CreateQuest("quest-network-full-state");
            var log = new QuestLog(new[] { quest });
            var broadcaster = CreateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            Invoke(broadcaster, "RegisterClient", 10UL);
            Invoke(broadcaster, "RegisterClient", 20UL);
            Invoke(broadcaster, "Attach", log);

            Assert.IsTrue(log.Accept(quest));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "network-progress-event"));
            Assert.AreEqual(QuestState.Completed, log.GetState(quest));
            Assert.IsTrue(log.ClaimReward(quest, new RewardRuntimeContext(log, null, null, null)));

            Assert.AreEqual(8, GetInt(broadcaster, "BroadcastCount"));
            Assert.AreEqual(8, GetInt(broadcaster, "RecipientDeliveryCount"));
            Assert.AreEqual(4, GetInt(broadcaster, "StateChangeCount"));
            Assert.AreEqual(QuestState.RewardClaimed.ToString(), GetString(broadcaster, "LastState"));
        }

        [Test]
        public void NPC_QUEST_010_DuplicateEventsDoNotRebroadcastOrGrowPerfCounters()
        {
            var quest = CreateQuest("quest-network-duplicate");
            var log = new QuestLog(new[] { quest });
            var broadcaster = CreateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            Invoke(broadcaster, "RegisterClient", 10UL);
            Invoke(broadcaster, "RegisterClient", 20UL);
            Invoke(broadcaster, "Attach", log);

            Assert.IsTrue(log.Accept(quest));
            Assert.IsFalse(log.Accept(quest));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "network-duplicate-event"));
            int afterFirstProgress = GetInt(broadcaster, "BroadcastCount");
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "network-duplicate-event"));
            Assert.IsTrue(log.ClaimReward(quest, new RewardRuntimeContext(log, null, null, null)));
            int afterFirstClaim = GetInt(broadcaster, "BroadcastCount");
            Assert.IsFalse(log.ClaimReward(quest, new RewardRuntimeContext(log, null, null, null)));

            Assert.AreEqual(6, afterFirstProgress);
            Assert.AreEqual(8, afterFirstClaim);
            Assert.AreEqual(8, GetInt(broadcaster, "BroadcastCount"));
            Assert.AreEqual(8, GetInt(broadcaster, "RecipientDeliveryCount"));
            Assert.AreEqual(4, GetInt(broadcaster, "StateChangeCount"));
        }

        [Test]
        public void NPC_QUEST_011_BroadcastDeliveryScalesLinearlyWithRegisteredClients()
        {
            var quest = CreateQuest("quest-network-linear-scale");
            var log = new QuestLog(new[] { quest });
            var broadcaster = CreateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            Invoke(broadcaster, "RegisterClient", 10UL);
            Invoke(broadcaster, "RegisterClient", 20UL);
            Invoke(broadcaster, "RegisterClient", 30UL);
            Invoke(broadcaster, "RegisterClient", 40UL);
            Invoke(broadcaster, "Attach", log);

            Assert.IsTrue(log.Accept(quest));

            Assert.AreEqual(4, GetInt(broadcaster, "BroadcastCount"));
            Assert.AreEqual(4, GetInt(broadcaster, "RecipientDeliveryCount"));
            Assert.AreEqual(1, GetInt(broadcaster, "StateChangeCount"));
        }

        [Test]
        public void NPC_QUEST_012_ClientAuthorityCannotBroadcastQuestState()
        {
            var quest = CreateQuest("quest-network-client-authority");
            var log = new QuestLog(new[] { quest });
            var broadcaster = CreateBroadcaster(isServerAuthority: false, saveSlot: "slot-a");
            Invoke(broadcaster, "RegisterClient", 10UL);
            Invoke(broadcaster, "Attach", log);

            Assert.IsTrue(log.Accept(quest));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "client-authority-event"));
            Assert.IsTrue(log.ClaimReward(quest, new RewardRuntimeContext(log, null, null, null)));

            Assert.AreEqual(0, GetInt(broadcaster, "BroadcastCount"));
            Assert.AreEqual(0, GetInt(broadcaster, "RecipientDeliveryCount"));
            Assert.AreEqual(0, GetInt(broadcaster, "StateChangeCount"));
        }

        private static QuestDefinition CreateQuest(string id)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_id", id);
            SetField(quest, "_objectives", new QuestObjectiveBase[] { ScriptableObject.CreateInstance<AlwaysMatchObjective>() });
            return quest;
        }

        private static object CreateBroadcaster(bool isServerAuthority, string saveSlot)
        {
            var type = Type.GetType("Rootborn.Network.Quests.QuestNetworkStateBroadcaster, Rootborn.Network");
            Assert.IsNotNull(type, "Rootborn.Network.Quests.QuestNetworkStateBroadcaster must exist for NPC quest multiplayer sync tests.");
            return Activator.CreateInstance(type, isServerAuthority, saveSlot);
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, args);
        }

        private static int GetInt(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, propertyName);
            return (int)property.GetValue(target);
        }

        private static string GetString(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, propertyName);
            return (string)property.GetValue(target);
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
