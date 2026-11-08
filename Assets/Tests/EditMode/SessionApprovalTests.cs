using NUnit.Framework;
using Rootborn.Network.Session;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    /// <summary>NET: -maxPlayers only works when every session turns connection approval on (it is part of the config hash).</summary>
    public sealed class SessionApprovalTests
    {
        private GameObject _root;

        [TearDown]
        public void Cleanup()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private NetworkManager MakeManager()
        {
            _root = new GameObject("[ApprovalTestNetworkManager]", typeof(NetworkManager));
            var nm = _root.GetComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { ConnectionApproval = false };
            return nm;
        }

        [Test]
        public void NET_APPROVAL_001_ServerAndHostTurnApprovalOnWithALimitCallback()
        {
            var nm = MakeManager();
            SessionApproval.EnableLimit(nm, 2);
            Assert.That(nm.NetworkConfig.ConnectionApproval, Is.True);
            Assert.That(nm.ConnectionApprovalCallback, Is.Not.Null);
            SessionApproval.Disable(nm);
            Assert.That(nm.ConnectionApprovalCallback, Is.Null);
        }

        [Test]
        public void NET_APPROVAL_002_ClientMatchesTheServerConfigHash()
        {
            var nm = MakeManager();
            SessionApproval.EnableForClient(nm);
            Assert.That(nm.NetworkConfig.ConnectionApproval, Is.True);
            Assert.That(nm.ConnectionApprovalCallback, Is.Null, "Clients never decide approval.");
        }
    }
}
