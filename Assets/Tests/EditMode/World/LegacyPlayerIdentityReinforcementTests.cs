using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class LegacyPlayerIdentityReinforcementTests
    {
        [Test] public void WORLD_LEGACY_ACTOR_001_ExistingHouseControllerReceivesIdentityOnTheSameActor()
        {
            var actor = new GameObject("ExistingHousePlayer");
            try
            {
                var controller = actor.AddComponent<PlayerController>(); var originalTransform = actor.transform;
                Reinforce(actor);
                var identity = actor.GetComponent<PlayerIdentity>(); Assert.That(identity, Is.Not.Null, "Saved House player has a controller but no identity; the entry adapter cannot accept it.");
                Assert.That(identity.PlayerId, Is.EqualTo(PlayerIdentity.DefaultPlayerId)); Assert.That(actor.GetComponent<PlayerController>(), Is.SameAs(controller)); Assert.That(actor.transform, Is.SameAs(originalTransform));
            }
            finally { Object.DestroyImmediate(actor); }
        }
        [Test] public void WORLD_LEGACY_ACTOR_002_ExistingIdentityAndControllerArePreserved()
        {
            var actor = new GameObject("ExistingIdentifiedPlayer");
            try
            {
                var controller = actor.AddComponent<PlayerController>(); var identity = actor.AddComponent<PlayerIdentity>(); identity.Configure("existing-player");
                Reinforce(actor); Assert.That(actor.GetComponent<PlayerIdentity>(), Is.SameAs(identity)); Assert.That(identity.PlayerId, Is.EqualTo("existing-player")); Assert.That(actor.GetComponent<PlayerController>(), Is.SameAs(controller));
            }
            finally { Object.DestroyImmediate(actor); }
        }
        private static void Reinforce(GameObject actor)
        {
            var method = typeof(TownPlayableBaselineRuntimeInstaller).GetMethod("ReinforcePlayer", BindingFlags.Static | BindingFlags.NonPublic); Assert.That(method, Is.Not.Null); method.Invoke(null, new object[] { actor });
        }
    }
}
