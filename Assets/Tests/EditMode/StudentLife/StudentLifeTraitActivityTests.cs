using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTraitActivityTests
    {
        [Test]
        public void LIFE_TRAIT_ACTIVITY_001_TenTargetTraitsExistInDataDrivenLifestyleSet()
        {
            var set = CreateLifestyleSet();

            Assert.AreEqual(10, set.RequiredTraits.Length);
            AssertTraitExists(set, "trait.focus");
            AssertTraitExists(set, "trait.service-sense");
            AssertTraitExists(set, "trait.creativity");
            AssertTraitExists(set, "trait.planning");
            AssertTraitExists(set, "trait.fitness");
            AssertTraitExists(set, "trait.discipline");
            AssertTraitExists(set, "trait.responsibility");
            AssertTraitExists(set, "trait.calm");
            AssertTraitExists(set, "trait.empathy");
            AssertTraitExists(set, "trait.observation");
            Assert.GreaterOrEqual(set.Activities.Length, 8);
            Assert.GreaterOrEqual(set.Activities[1].Choices.Count, 3);
            Assert.AreEqual("choice.focus-notes", set.Activities[1].GetChoice(0).Id);
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_002_EachTraitCanGrowFromAtLeastTwoChoices()
        {
            var set = CreateLifestyleSet();

            for (int i = 0; i < set.RequiredTraits.Length; i++)
            {
                int choices = CountChoicesIncreasingTrait(set.Activities, set.RequiredTraits[i].Id);
                Assert.GreaterOrEqual(choices, 2, set.RequiredTraits[i].Id);
            }
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_003_SchoolActivitiesGrowFocusPlanningObservation()
        {
            var set = CreateLifestyleSet();

            AssertTraitAvailableInCategory(set.Activities, LifeActivityCategory.School, "trait.focus");
            AssertTraitAvailableInCategory(set.Activities, LifeActivityCategory.School, "trait.planning");
            AssertTraitAvailableInCategory(set.Activities, LifeActivityCategory.School, "trait.observation");
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_004_HouseChoresGrowResponsibilityDisciplineEmpathy()
        {
            var set = CreateLifestyleSet();
            var house = FindActivity(set.Activities, "activity.house-chores");

            AssertTraitAvailable(house, "trait.responsibility");
            AssertTraitAvailable(house, "trait.discipline");
            AssertTraitAvailable(house, "trait.empathy");
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_005_NeighborhoodActivitiesGrowServiceObservationEmpathy()
        {
            var set = CreateLifestyleSet();
            var neighborhood = FindActivity(set.Activities, "activity.neighborhood-help");

            AssertTraitAvailable(neighborhood, "trait.service-sense");
            AssertTraitAvailable(neighborhood, "trait.observation");
            AssertTraitAvailable(neighborhood, "trait.empathy");
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_006_TrainingActivitiesGrowFitnessDisciplineCalm()
        {
            var set = CreateLifestyleSet();
            var training = FindActivity(set.Activities, "activity.training-practice");

            AssertTraitAvailable(training, "trait.fitness");
            AssertTraitAvailable(training, "trait.discipline");
            AssertTraitAvailable(training, "trait.calm");
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_007_SameRequestIdAndChoiceDoesNotApplyTwice()
        {
            var focus = CreateTrait("trait.focus");
            var choice = CreateChoice("choice.focus-notes", Trait(focus, 2));
            var activity = CreateActivity("activity.class-time", LifeActivityCategory.School, choice);
            var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerformChoice(activity, choice, progress, "same-choice-request", out var first));
            int afterFirst = progress.GetTraitValue(focus);
            Assert.IsFalse(runner.TryPerformChoice(activity, choice, progress, "same-choice-request", out var second));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual("choice.focus-notes", first.ChoiceId);
            CollectionAssert.Contains(first.ChangedTraitIds, "trait.focus");
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, second.Kind);
            Assert.AreEqual(afterFirst, progress.GetTraitValue(focus));
        }

        private sealed class LifestyleSet
        {
            public TraitDefinition[] RequiredTraits;
            public LifeActivityDefinition[] Activities;
        }

        private static LifestyleSet CreateLifestyleSet()
        {
            var focus = CreateTrait("trait.focus");
            var service = CreateTrait("trait.service-sense");
            var creativity = CreateTrait("trait.creativity");
            var planning = CreateTrait("trait.planning");
            var fitness = CreateTrait("trait.fitness");
            var discipline = CreateTrait("trait.discipline");
            var responsibility = CreateTrait("trait.responsibility");
            var calm = CreateTrait("trait.calm");
            var empathy = CreateTrait("trait.empathy");
            var observation = CreateTrait("trait.observation");

            return new LifestyleSet
            {
                RequiredTraits = new[] { focus, service, creativity, planning, fitness, discipline, responsibility, calm, empathy, observation },
                Activities = new[]
                {
                    CreateActivity("activity.morning-routine", LifeActivityCategory.SelfStudy,
                        CreateChoice("choice.pack-timetable", Trait(planning, 1), Trait(responsibility, 1)),
                        CreateChoice("choice.leave-on-time", Trait(discipline, 1), Trait(responsibility, 1)),
                        CreateChoice("choice.help-family-breakfast", Trait(empathy, 1), Trait(service, 1))),
                    CreateActivity("activity.class-time", LifeActivityCategory.School,
                        CreateChoice("choice.focus-notes", Trait(focus, 2), Trait(observation, 1)),
                        CreateChoice("choice.prepare-presentation", Trait(planning, 2), Trait(calm, 1), Trait(creativity, 1)),
                        CreateChoice("choice.help-friend-question", Trait(empathy, 1), Trait(service, 1))),
                    CreateActivity("activity.after-school-club", LifeActivityCategory.Hobby,
                        CreateChoice("choice.art-craft", Trait(creativity, 2), Trait(observation, 1)),
                        CreateChoice("choice.sports-club", Trait(fitness, 2), Trait(discipline, 1)),
                        CreateChoice("choice.volunteer-club", Trait(empathy, 1), Trait(responsibility, 1), Trait(service, 1))),
                    CreateActivity("activity.house-chores", LifeActivityCategory.Errand,
                        CreateChoice("choice.clean-room", Trait(planning, 1), Trait(discipline, 1)),
                        CreateChoice("choice.help-meal-prep", Trait(service, 1), Trait(responsibility, 1)),
                        CreateChoice("choice.check-family-mood", Trait(observation, 1), Trait(empathy, 1))),
                    CreateActivity("activity.neighborhood-help", LifeActivityCategory.Errand,
                        CreateChoice("choice.guide-lost-neighbor", Trait(observation, 1), Trait(empathy, 1)),
                        CreateChoice("choice.store-errand", Trait(responsibility, 1), Trait(service, 1)),
                        CreateChoice("choice.park-exercise", Trait(fitness, 1), Trait(discipline, 1))),
                    CreateActivity("activity.training-practice", LifeActivityCategory.Hobby,
                        CreateChoice("choice.pace-run", Trait(fitness, 1), Trait(calm, 1)),
                        CreateChoice("choice.follow-routine", Trait(discipline, 1), Trait(planning, 1)),
                        CreateChoice("choice.recover-after-mistake", Trait(calm, 1), Trait(responsibility, 1))),
                    CreateActivity("activity.sudden-trouble", LifeActivityCategory.Social,
                        CreateChoice("choice.calm-first-aid", Trait(calm, 1), Trait(empathy, 1), Trait(observation, 1)),
                        CreateChoice("choice.prioritize-schedule", Trait(planning, 1), Trait(calm, 1)),
                        CreateChoice("choice.choose-conflict-words", Trait(empathy, 1), Trait(calm, 1))),
                    CreateActivity("activity.part-time-shift", LifeActivityCategory.Work,
                        CreateChoice("choice.greet-customers", Trait(service, 1), Trait(observation, 1)),
                        CreateChoice("choice.track-orders", Trait(focus, 1), Trait(responsibility, 1)),
                        CreateChoice("choice.solve-queue-pressure", Trait(calm, 1), Trait(service, 1))),
                }
            };
        }

        private static void AssertTraitExists(LifestyleSet set, string traitId)
        {
            for (int i = 0; i < set.RequiredTraits.Length; i++)
            {
                if (set.RequiredTraits[i].Id == traitId)
                {
                    return;
                }
            }

            Assert.Fail("Missing trait: " + traitId);
        }

        private static int CountChoicesIncreasingTrait(LifeActivityDefinition[] activities, string traitId)
        {
            int count = 0;
            for (int i = 0; i < activities.Length; i++)
            {
                for (int j = 0; j < activities[i].Choices.Count; j++)
                {
                    var progress = new StudentLifeProgress("slot", "player", 99, 99);
                    var runner = new LifeActivityRunner();
                    var choice = activities[i].GetChoice(j);
                    runner.TryPerformChoice(activities[i], choice, progress, activities[i].Id + ":" + choice.Id, out _);
                    if (progress.GetTraitValueById(traitId) > 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void AssertTraitAvailableInCategory(LifeActivityDefinition[] activities, LifeActivityCategory category, string traitId)
        {
            for (int i = 0; i < activities.Length; i++)
            {
                if (activities[i].Category == category && ActivityCanIncreaseTrait(activities[i], traitId))
                {
                    return;
                }
            }

            Assert.Fail("Trait not available in category: " + traitId + " / " + category);
        }

        private static void AssertTraitAvailable(LifeActivityDefinition activity, string traitId)
        {
            Assert.IsNotNull(activity);
            Assert.IsTrue(ActivityCanIncreaseTrait(activity, traitId), traitId);
        }

        private static bool ActivityCanIncreaseTrait(LifeActivityDefinition activity, string traitId)
        {
            for (int i = 0; i < activity.Choices.Count; i++)
            {
                var progress = new StudentLifeProgress("slot", "player", 99, 99);
                var runner = new LifeActivityRunner();
                var choice = activity.GetChoice(i);
                runner.TryPerformChoice(activity, choice, progress, activity.Id + ":" + choice.Id, out _);
                if (progress.GetTraitValueById(traitId) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static LifeActivityDefinition FindActivity(LifeActivityDefinition[] activities, string id)
        {
            for (int i = 0; i < activities.Length; i++)
            {
                if (activities[i].Id == id)
                {
                    return activities[i];
                }
            }

            return null;
        }

        private static LifeActivityDefinition CreateActivity(string id, LifeActivityCategory category, params LifeChoiceDefinition[] choices)
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(id, id, category, 20, 1, 1, 0, null, null, choices);
            return activity;
        }

        private static LifeChoiceDefinition CreateChoice(string id, params LifeActivityEffectBase[] effects)
        {
            var choice = ScriptableObject.CreateInstance<LifeChoiceDefinition>();
            choice.ConfigureForTests(id, id, effects);
            return choice;
        }

        private static TraitDeltaActivityEffect Trait(TraitDefinition trait, int delta)
        {
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(trait, delta);
            return effect;
        }

        private static TraitDefinition CreateTrait(string id)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, id);
            return trait;
        }
    }
}
