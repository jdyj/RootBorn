using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeProgressIdentityTests
    {
        [Test]
        public void MULTI_DIRECT_010_ProgressRebindsDefaultIdentityToNetworkPlayerIdentity()
        {
            var player = new GameObject("Player");
            try
            {
                var identity = player.AddComponent<PlayerIdentity>();
                var progress = player.AddComponent<StudentLifeProgressComponent>();

                Assert.AreEqual(PlayerIdentity.DefaultPlayerId, progress.EnsureProgress().PlayerId);

                identity.Configure("client-1", 1UL);
                progress.SynchronizeWithPlayerIdentity();

                Assert.AreEqual("client-1", progress.EnsureProgress().PlayerId);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
