using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class NetworkLocalPlayerViewSourceTests
    {
        [Test]
        public void MULTI_DIRECT_003A_LocalOwnerBindsCameraToOwnedPlayer()
        {
            const string sourcePath = "Assets/Scripts/Network/Player/NetworkLocalPlayerViewBinder.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Multiplayer clients must bind the camera to the local owned Player, not the first root Player.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("NetworkBehaviour", source);
            StringAssert.Contains("IsOwner", source);
            StringAssert.Contains("CameraFollow", source);
            StringAssert.Contains("SetTarget(transform)", source);
        }

        [Test]
        public void MULTI_DIRECT_003A_InputGateReappliesAuthorityWhenLateInputComponentsAppear()
        {
            const string sourcePath = "Assets/Scripts/Network/Player/NetworkLocalPlayerInputGate.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Network local player input gate must exist.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("private void Update()", source);
            StringAssert.Contains("bool hadRouter", source);
            StringAssert.Contains("ApplyInputAuthority();", source);
            StringAssert.Contains("_interactionRouter != null", source);
            StringAssert.Contains("NeedsAuthorityReapply", source);
            StringAssert.Contains("_interactionRouter.enabled != allowLocalInput", source);
        }

        [Test]
        public void MULTI_DIRECT_003A_InputGateEnsuresOwnedNetworkPlayersHaveInteractionComponents()
        {
            const string sourcePath = "Assets/Scripts/Network/Player/NetworkLocalPlayerInputGate.cs";

            Assert.IsTrue(File.Exists(sourcePath), "Network input gate must guarantee the actual input path on network-spawned players.");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("EnsureInputComponents();", source);
            StringAssert.Contains("gameObject.AddComponent<GatherInteractor>()", source);
            StringAssert.Contains("gameObject.AddComponent<PlayerInteractionRouter>()", source);
        }
    }
}
