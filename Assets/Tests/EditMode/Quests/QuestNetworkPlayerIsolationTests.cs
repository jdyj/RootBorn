using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestNetworkPlayerIsolationTests
    {
        [Test]
        public void NPC_QUEST_NET_013_ServerBroadcastIncludesPlayerIdForEachRegisteredClient()
        {
            var quest = CreateQuest("quest.network.player.identity");
            var log = new QuestLog(new[] { quest });
            var broadcaster = CreateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            Invoke(broadcaster, "RegisterClient", 10UL);
            Invoke(broadcaster, "RegisterClient", 20UL);
            AttachForPlayer(broadcaster, log, "player-1");
            var receivedPlayerIds = new List<string>();
            SubscribePlayerIds(broadcaster, receivedPlayerIds);

            Assert.IsTrue(log.Accept(quest));

            CollectionAssert.AreEqual(new[] { "player-1", "player-1" }, receivedPlayerIds);
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

        private static void AttachForPlayer(object target, QuestLog questLog, string playerId)
        {
            var method = target.GetType().GetMethod("AttachForPlayer", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(QuestLog), typeof(string) }, null);
            Assert.IsNotNull(method, "QuestNetworkStateBroadcaster.AttachForPlayer(QuestLog, playerId) must exist so multiplayer quest broadcasts keep player identity without overloading Attach.");
            method.Invoke(target, new object[] { questLog, playerId });
        }

        private static void SubscribePlayerIds(object broadcaster, List<string> receivedPlayerIds)
        {
            var eventInfo = broadcaster.GetType().GetEvent("OnBroadcast", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(eventInfo);
            var eventType = eventInfo.EventHandlerType;
            var method = typeof(QuestNetworkPlayerIsolationTests).GetMethod(nameof(CaptureBroadcast), BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var closed = method.MakeGenericMethod(eventType.GetGenericArguments()[0]);
            var handler = Delegate.CreateDelegate(eventType, receivedPlayerIds, closed);
            eventInfo.AddEventHandler(broadcaster, handler);
        }

        private static void CaptureBroadcast<T>(List<string> receivedPlayerIds, T message)
        {
            var field = typeof(T).GetField("PlayerId", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, "QuestNetworkStateBroadcast must expose PlayerId so clients can isolate quest state per player.");
            receivedPlayerIds.Add((string)field.GetValue(message));
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, args);
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
