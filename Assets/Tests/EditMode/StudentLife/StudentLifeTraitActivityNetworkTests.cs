using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Network.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTraitActivityNetworkTests
    {
        [Test]
        public void LIFE_TRAIT_ACTIVITY_NET_001_ServerAuthorityConfirmsChoiceResult()
        {
            var trait = CreateTrait("trait.focus");
            var choice = CreateChoice("choice.focus-notes", Trait(trait, 1));
            var activity = CreateActivity("activity.class-time", choice);
            var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var runner = new LifeActivityRunner();
            var broadcaster = new StudentLifeNetworkStateBroadcaster(true, "slot-a");
            broadcaster.RegisterClient(1UL);

            Assert.IsTrue(runner.TryPerformChoice(activity, choice, progress, "request-1", out var result));
            Assert.IsTrue(broadcaster.Publish(result));

            Assert.AreEqual("player-1", broadcaster.LastPlayerId);
            Assert.AreEqual("activity.class-time", broadcaster.LastActivityId);
            Assert.AreEqual("choice.focus-notes", broadcaster.LastChoiceId);
            Assert.AreEqual(1, broadcaster.BroadcastCount);
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_NET_002_PlayerOneChoiceDoesNotMutatePlayerTwoProgress()
        {
            var trait = CreateTrait("trait.focus");
            var choice = CreateChoice("choice.focus-notes", Trait(trait, 1));
            var activity = CreateActivity("activity.class-time", choice);
            var playerOne = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var playerTwo = new StudentLifeProgress("slot-a", "player-2", 10, 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerformChoice(activity, choice, playerOne, "request-p1", out var result));

            Assert.AreEqual("player-1", result.PlayerId);
            Assert.AreEqual(1, playerOne.GetTraitValue(trait));
            Assert.AreEqual(0, playerTwo.GetTraitValue(trait));
        }

        private static TraitDefinition CreateTrait(string id)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, id);
            return trait;
        }

        private static TraitDeltaActivityEffect Trait(TraitDefinition trait, int delta)
        {
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(trait, delta);
            return effect;
        }

        private static LifeChoiceDefinition CreateChoice(string id, params LifeActivityEffectBase[] effects)
        {
            var choice = ScriptableObject.CreateInstance<LifeChoiceDefinition>();
            choice.ConfigureForTests(id, id, effects);
            return choice;
        }

        private static LifeActivityDefinition CreateActivity(string id, params LifeChoiceDefinition[] choices)
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(id, id, LifeActivityCategory.School, 10, 1, 1, 0, null, null, choices);
            return activity;
        }
    }
}
