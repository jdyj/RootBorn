using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Network.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeCareerPracticeNetworkTests
    {
        [Test]
        public void LIFE_CAREER_PRACTICE_NET_001_PracticeRequestIsConfirmedByServerAuthorityBroadcast()
        {
            var practice = CreatePractice("practice.chef", CreateTrait("trait.focus"), CreateSkill("skill.cooking"), CreateCareer("career.chef"));
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new CareerPracticeRunner();
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(1UL);
            broadcaster.RegisterClient(2UL);

            Assert.IsTrue(runner.TryPerform(practice, progress, "server-practice-request", out var result));
            Assert.IsTrue(broadcaster.Publish(result));

            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("player-1", broadcaster.LastPlayerId);
            Assert.AreEqual("practice.chef", broadcaster.LastActivityId);
            Assert.AreEqual(2, broadcaster.BroadcastCount);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_NET_002_PlayerOnePracticeDoesNotMutatePlayerTwoProgress()
        {
            var trait = CreateTrait("trait.focus");
            var skill = CreateSkill("skill.cooking");
            var career = CreateCareer("career.chef");
            var practice = CreatePractice("practice.chef", trait, skill, career);
            var playerOne = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var playerTwo = new StudentLifeProgress("slot-a", "player-2", energy: 10, focus: 10);
            var runner = new CareerPracticeRunner();

            Assert.IsTrue(runner.TryPerform(practice, playerOne, "p1-practice-request", out var result));

            Assert.AreEqual("player-1", result.PlayerId);
            Assert.Greater(playerOne.GetTraitValue(trait), 0);
            Assert.Greater(playerOne.GetSkillValue(skill), 0);
            Assert.IsTrue(playerOne.IsCareerHintUnlocked(career));
            Assert.AreEqual(0, playerTwo.GetTraitValue(trait));
            Assert.AreEqual(0, playerTwo.GetSkillValue(skill));
            Assert.IsFalse(playerTwo.IsCareerHintUnlocked(career));
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_NET_003_DuplicatePracticeRequestDoesNotRebroadcastOrGrowTwice()
        {
            var trait = CreateTrait("trait.focus");
            var skill = CreateSkill("skill.cooking");
            var practice = CreatePractice("practice.chef", trait, skill, CreateCareer("career.chef"));
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new CareerPracticeRunner();
            var broadcaster = new StudentLifeNetworkStateBroadcaster(isServerAuthority: true, saveSlot: "slot-a");
            broadcaster.RegisterClient(1UL);

            Assert.IsTrue(runner.TryPerform(practice, progress, "same-practice-request", out var first));
            Assert.IsTrue(broadcaster.Publish(first));
            int traitAfterFirst = progress.GetTraitValue(trait);
            int skillAfterFirst = progress.GetSkillValue(skill);
            Assert.IsFalse(runner.TryPerform(practice, progress, "same-practice-request", out var second));
            Assert.IsFalse(broadcaster.Publish(first));
            Assert.IsFalse(broadcaster.Publish(second));

            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, second.Kind);
            Assert.AreEqual(traitAfterFirst, progress.GetTraitValue(trait));
            Assert.AreEqual(skillAfterFirst, progress.GetSkillValue(skill));
            Assert.AreEqual(1, broadcaster.BroadcastCount);
            Assert.AreEqual(1, broadcaster.DuplicateSuppressedCount);
        }

        private static CareerPracticeDefinition CreatePractice(string id, TraitDefinition trait, SkillDefinition skill, CareerDefinition career)
        {
            var traitEffect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            traitEffect.ConfigureForTests(trait, 2);
            var skillEffect = ScriptableObject.CreateInstance<SkillProgressActivityEffect>();
            skillEffect.ConfigureForTests(skill, 3);
            var careerEffect = ScriptableObject.CreateInstance<CareerHintUnlockActivityEffect>();
            careerEffect.ConfigureForTests(career);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(
                id,
                id,
                LifeActivityCategory.School,
                timeCostMinutes: 45,
                energyCost: 1,
                focusCost: 1,
                stressDelta: 0,
                requirements: null,
                effects: new LifeActivityEffectBase[] { traitEffect, skillEffect, careerEffect });
            var step = ScriptableObject.CreateInstance<PracticeStepDefinition>();
            step.ConfigureForTests(id + ".step", id + ".step");
            var practice = ScriptableObject.CreateInstance<CareerPracticeDefinition>();
            practice.ConfigureForTests(id, id, activity, career, new[] { step });
            return practice;
        }

        private static TraitDefinition CreateTrait(string id)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, id);
            return trait;
        }

        private static SkillDefinition CreateSkill(string id)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.ConfigureForTests(id, id);
            return skill;
        }

        private static CareerDefinition CreateCareer(string id)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id, null);
            return career;
        }
    }
}
