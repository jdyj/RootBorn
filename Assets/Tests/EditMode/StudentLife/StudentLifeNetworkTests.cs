using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Network.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeNetworkTests
    {
        [Test]
        public void LIFE_STUDENT_NET_001_ServerAuthorityBroadcastsAppliedActivityToRegisteredClients()
        {
            var result = CreateAppliedResult("slot-a", "player-1", "activity.study", "request-1");
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(10UL);
            broadcaster.RegisterClient(20UL);

            Assert.IsTrue(broadcaster.Publish(result));

            Assert.AreEqual(2, broadcaster.BroadcastCount);
            Assert.AreEqual(2, broadcaster.RecipientDeliveryCount);
            Assert.AreEqual(1, broadcaster.StateChangeCount);
            Assert.AreEqual("slot-a", broadcaster.LastSaveSlot);
            Assert.AreEqual("player-1", broadcaster.LastPlayerId);
            Assert.AreEqual("activity.study", broadcaster.LastActivityId);
        }

        [Test]
        public void LIFE_STUDENT_NET_003_PlayerGrowthBroadcastKeepsPlayerIdentity()
        {
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(10UL);
            StudentLifeNetworkStateBroadcast received = default;
            broadcaster.OnBroadcast += message => received = message;

            Assert.IsTrue(broadcaster.Publish(CreateAppliedResult("slot-a", "player-1", "activity.study", "request-1")));

            Assert.AreEqual("player-1", received.PlayerId);
            Assert.AreEqual("slot-a", received.SaveSlot);
            Assert.AreEqual("activity.study", received.ActivityId);
        }

        [Test]
        public void LIFE_STUDENT_NET_005_DuplicateActivityResultDoesNotRebroadcast()
        {
            var result = CreateAppliedResult("slot-a", "player-1", "activity.study", "request-1");
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(10UL);
            broadcaster.RegisterClient(20UL);

            Assert.IsTrue(broadcaster.Publish(result));
            Assert.IsFalse(broadcaster.Publish(result));

            Assert.AreEqual(2, broadcaster.BroadcastCount);
            Assert.AreEqual(2, broadcaster.RecipientDeliveryCount);
            Assert.AreEqual(1, broadcaster.StateChangeCount);
            Assert.AreEqual(1, broadcaster.DuplicateSuppressedCount);
        }

        [Test]
        public void LIFE_STUDENT_NET_006_FailedActivityResultDoesNotBroadcastOrMutateCounters()
        {
            var failed = new LifeActivityResult(
                LifeActivityResultKind.InsufficientResources,
                "slot-a",
                "player-1",
                "activity.study",
                "request-1");
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(10UL);

            Assert.IsFalse(broadcaster.Publish(failed));

            Assert.AreEqual(0, broadcaster.BroadcastCount);
            Assert.AreEqual(0, broadcaster.RecipientDeliveryCount);
            Assert.AreEqual(0, broadcaster.StateChangeCount);
        }

        [Test]
        public void LIFE_STUDENT_NET_010_ClientAuthorityCannotBroadcastActivityState()
        {
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: false, saveSlot: "slot-a");
            broadcaster.RegisterClient(10UL);

            Assert.IsFalse(broadcaster.Publish(CreateAppliedResult("slot-a", "player-1", "activity.study", "request-1")));

            Assert.AreEqual(0, broadcaster.BroadcastCount);
            Assert.AreEqual(0, broadcaster.RecipientDeliveryCount);
            Assert.AreEqual(0, broadcaster.StateChangeCount);
        }

        [Test]
        public void LIFE_STUDENT_NET_011_BroadcastDeliveryScalesLinearlyWithRegisteredClients()
        {
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(10UL);
            broadcaster.RegisterClient(20UL);
            broadcaster.RegisterClient(30UL);
            broadcaster.RegisterClient(40UL);

            Assert.IsTrue(broadcaster.Publish(CreateAppliedResult("slot-a", "player-1", "activity.study", "request-1")));

            Assert.AreEqual(4, broadcaster.BroadcastCount);
            Assert.AreEqual(4, broadcaster.RecipientDeliveryCount);
            Assert.AreEqual(1, broadcaster.StateChangeCount);
        }

        private static LifeActivityResult CreateAppliedResult(string saveSlot, string playerId, string activityId, string requestId)
        {
            return new LifeActivityResult(LifeActivityResultKind.Applied, saveSlot, playerId, activityId, requestId);
        }
    }
}
