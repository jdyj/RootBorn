using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class MainStoryOpeningAssetTests
    {
        private const string Root = "Assets/Data/StudentLife/MainStory/";
        private static readonly string[] StepNames = { "BackToSchool", "AfterSchoolTour", "FirstPartTimeJob", "DustySecrets", "MyOwnSpace" };
        private static GameDataRegistry Registry => Load<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

        [Test]
        public void OpeningChain_ContainsFiveNamedStepsInDesignOrder_AndFirstGuideStartsIt()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            Assert.That(chain.Id, Is.EqualTo("main-story.school-opening"));
            CollectionAssert.Contains(Registry.QuestChains, chain);
            Assert.That(chain.Steps.Length, Is.EqualTo(5));
            for (int i = 0; i < StepNames.Length; i++)
            {
                Assert.That(chain.Steps[i], Is.SameAs(Step(StepNames[i])));
                Assert.That(chain.Steps[i].Objectives, Is.Not.Empty, StepNames[i] + " must have real objectives.");
                Assert.That(chain.Steps[i].DisplayNameKey, Is.Not.Empty);
                Assert.That(chain.Steps[i].DescriptionKey, Is.Not.Empty);
                foreach (var objective in chain.Steps[i].Objectives) CollectionAssert.Contains(Registry.QuestObjectives, objective);
            }
            Assert.That(Property(chain, "StartNpc"), Is.SameAs(Load<NpcDefinition>("Assets/Data/NPCs/Npc_FirstGuide.asset")));
        }

        [Test]
        public void BackToSchool_RequiresTeacherTalkAndRealDailyPlanSelection()
        {
            var objectives = Step("BackToSchool").Objectives;
            var teacher = Load<NpcDefinition>("Assets/Data/NPCs/Npc_Teacher.asset");
            Assert.That(objectives.OfType<TalkQuestObjective>().Any(x => x.TargetNpc == teacher), Is.True);
            Assert.That(objectives.Any(x => x != null && x.GetType().Name == "DailyPlanSelectedQuestObjective"), Is.True);
        }

        [Test]
        public void AfterSchoolTour_RequiresFourDistinctCareerFacilities_WithRegisteredTownIdentities()
        {
            var objectives = Step("AfterSchoolTour").Objectives;
            Assert.That(objectives.Length, Is.EqualTo(4));
            var expected = new[] { "Restaurant", "MedicalClinic", "DesignStudio", "TrainingGround" }
                .Select(name => Load<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_" + name + ".asset")).ToArray();
            var actual = objectives.Select(x => Property(x, "TargetLocation")).ToArray();
            CollectionAssert.AreEquivalent(expected, actual);
            Assert.That(expected.Select(x => x.WorldPosition).Distinct().Count(), Is.EqualTo(4), "Four world targets must not overlap.");
            foreach (var location in expected)
            {
                CollectionAssert.Contains(Registry.Locations, location);
                var identity = Registry.LocationIdentities.SingleOrDefault(x => x != null && x.Location == location);
                Assert.That(identity, Is.Not.Null, "Runtime installer needs an identity for " + location.Id);
                Assert.That(identity.DescriptionKey, Is.Not.Empty);
                Assert.That(identity.AvailableActivities.Count, Is.GreaterThan(0), "Facility must expose an actual practice activity.");
            }
        }

        [Test]
        public void FirstPartTimeJob_AcceptsTwoActualWorkDefinitions_ThatPayTheSharedAllowanceItem()
        {
            var objective = Step("FirstPartTimeJob").Objectives.Single();
            Assert.That(objective.GetType().Name, Is.EqualTo("PartTimeWorkQuestObjective"));
            var works = Values<PartTimeWorkDefinition>(Property(objective, "AcceptedWorks"));
            Assert.That(works.Length, Is.GreaterThanOrEqualTo(2));
            CollectionAssert.Contains(works, Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_CornerStoreShift.asset"));
            var allowance = Load<ItemDefinition>("Assets/Data/Items/Item_Allowance.asset");
            Assert.That(allowance.Id, Is.EqualTo("item.allowance"));
            CollectionAssert.Contains(Registry.Items, allowance);
            foreach (var work in works)
            {
                CollectionAssert.Contains(Registry.PartTimeWorks, work);
                Assert.That(work.Rewards.Any(x => x.Item == allowance && x.Count > 0), Is.True, work.name + " must pay allowance.");
                Assert.That(work.Workplace, Is.Not.Null);
                CollectionAssert.Contains(Registry.Workplaces, work.Workplace);
                CollectionAssert.Contains(work.Workplace.AvailableWorks, work);
                Assert.That(Registry.Locations.Any(x => x != null && x.Id == work.Workplace.LocationId), Is.True);
            }
        }

        [Test]
        public void DustySecrets_UsesTwoPlayableAlternativeActivities_AppendedToTheirExistingLocations()
        {
            var objective = Step("DustySecrets").Objectives.Single();
            Assert.That(objective.GetType().Name, Is.EqualTo("AnyOfQuestObjective"));
            var alternatives = Values<QuestObjectiveBase>(Property(objective, "Alternatives"));
            Assert.That(alternatives.Length, Is.EqualTo(2));
            var expected = new[] { "InspectArchiveDoor", "ReadRepairLedger" }
                .Select(x => Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_" + x + ".asset")).ToArray();
            CollectionAssert.AreEquivalent(expected, alternatives.Select(x => Property(x, "TargetActivity")).ToArray());
            foreach (var activity in expected)
            {
                CollectionAssert.Contains(Registry.LocationActivities, activity);
                Assert.That(Registry.LocationIdentities.Any(x => x != null && x.Location == activity.Location && x.AvailableActivities.Contains(activity)), Is.True);
            }
        }

        [Test]
        public void MyOwnSpace_RequiresOneOfTwoLockedFurnitureDefinitionsOnHouseSurface()
        {
            var objective = Step("MyOwnSpace").Objectives.Single();
            Assert.That(objective.GetType().Name, Is.EqualTo("FurniturePlacedQuestObjective"));
            Assert.That(Property(objective, "RequiredSurfaceId"), Is.EqualTo("house"));
            var furniture = Values<FurnitureDefinition>(Property(objective, "AcceptedFurniture"));
            var expected = new[] { "PhotoBoard", "StudyDesk" }.Select(name => Load<FurnitureDefinition>(
                "Assets/Data/Interiors/Furniture/Story/Furniture_Story_" + name + ".asset")).ToArray();
            CollectionAssert.AreEquivalent(expected, furniture);
            foreach (var definition in furniture)
            {
                CollectionAssert.Contains(Registry.FurnitureDefinitions, definition);
                Assert.That(definition.UnlockToken, Is.Not.Empty, "This is an earn/purchase/place goal; free catalog objects do not qualify.");
                Assert.That(definition.TileParts.Count, Is.GreaterThan(0));
                CollectionAssert.Contains(definition.AllowedSurfaceIds, "house");
            }
            Assert.That(furniture.Select(x => x.UnlockToken).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void DefaultCampaign_UsesThreeAuthoredDays_WithAlternativeAndSchoolFreeGrowthRoutes()
        {
            var campaign = Load<CampaignDefinition>(Root + "Campaigns/Campaign_SchoolOpeningThreeDays.asset");
            Assert.That(Property(Registry, "DefaultStudentCampaign"), Is.SameAs(campaign));
            CollectionAssert.Contains(Registry.Campaigns, campaign);
            Assert.That(campaign.Days.Length, Is.EqualTo(3));
            for (int i = 1; i <= 3; i++)
            {
                var day = campaign.GetDayByNumber(i);
                Assert.That(day.DayNumber, Is.EqualTo(i));
                Assert.That(day.DirectionText, Is.Not.Empty);
                Assert.That(day.Routes.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(day.HasSchoolFreeCompletionRoute(), Is.True);
                foreach (var route in day.Routes)
                {
                    CollectionAssert.Contains(Registry.CampaignRoutes, route);
                    Assert.That(route.Objectives, Is.Not.Empty);
                }
            }
            CollectionAssert.Contains(Registry.Campaigns, Load<CampaignDefinition>("Assets/Data/StudentLife/Campaigns/Campaign_FirstThreeDays.asset"));
        }

        private static QuestStepDefinition Step(string name) => Load<QuestStepDefinition>(Root + "Steps/MainQuest_" + name + ".asset");
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Missing authored asset: " + path);
            return asset;
        }
        private static object Property(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, target.GetType().Name + "." + name + " is required.");
            return property.GetValue(target);
        }
        private static T[] Values<T>(object value) => ((IEnumerable)value).Cast<T>().ToArray();
    }
}
