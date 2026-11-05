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
        private static readonly string[] StepNames = { "BackToSchool", "AfterSchoolTour", "FirstPartTimeJob", "MyDream", "MyOwnSpace" };
        private static readonly string[] DreamCareers = { "Chef", "EmergencyCare", "Interior", "Soldier" };
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
        public void FirstPartTimeJob_AcceptsSixActualWorkDefinitions_ThatPayTheSharedAllowanceItem()
        {
            var objective = Step("FirstPartTimeJob").Objectives.Single();
            Assert.That(objective.GetType().Name, Is.EqualTo("PartTimeWorkQuestObjective"));
            var works = Values<PartTimeWorkDefinition>(Property(objective, "AcceptedWorks"));
            Assert.That(works.Length, Is.GreaterThanOrEqualTo(9), "Store, workshop and the seven career jobs.");
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
        public void MyDream_UsesTwoPlayableAlternativeActivities_AppendedToTheirExistingLocations()
        {
            var objective = Step("MyDream").Objectives.Single();
            Assert.That(objective.GetType().Name, Is.EqualTo("AnyOfQuestObjective"));
            var alternatives = Values<QuestObjectiveBase>(Property(objective, "Alternatives"));
            Assert.That(alternatives.Length, Is.EqualTo(3), "Choose a dream, or read about jobs, or watch people work.");
            Assert.That(alternatives.OfType<CareerInterestSelectedQuestObjective>().Count(), Is.EqualTo(1));
            CollectionAssert.Contains(Registry.QuestObjectives, alternatives.OfType<CareerInterestSelectedQuestObjective>().Single());
            var expected = new[] { "ReadJobBooks", "WatchWorkshopWork" }
                .Select(x => Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_" + x + ".asset")).ToArray();
            CollectionAssert.AreEquivalent(expected, alternatives.OfType<LocationActivityQuestObjective>().Select(x => x.TargetActivity).ToArray());
            foreach (var activity in expected)
            {
                CollectionAssert.Contains(Registry.LocationActivities, activity);
                Assert.That(Registry.LocationIdentities.Any(x => x != null && x.Location == activity.Location && x.AvailableActivities.Contains(activity)), Is.True);
            }
        }

        [Test]
        public void MyOwnSpace_AcceptsLockedStoryAndFreeCareerFurnitureOnHouseSurface()
        {
            var objective = Step("MyOwnSpace").Objectives.Single();
            Assert.That(objective.GetType().Name, Is.EqualTo("FurniturePlacedQuestObjective"));
            Assert.That(Property(objective, "RequiredSurfaceId"), Is.EqualTo("house"));
            var furniture = Values<FurnitureDefinition>(Property(objective, "AcceptedFurniture"));
            var story = new[] { "PhotoBoard", "StudyDesk" }.Select(name => Load<FurnitureDefinition>(
                "Assets/Data/Interiors/Furniture/Story/Furniture_Story_" + name + ".asset")).ToArray();
            var career = new[]
            {
                "StudentLifePixels/Furniture48/Furniture_StudentLife_toy_kitchen", "Growth31/Furniture_Growth31_first_aid_play_set",
                "Growth31/Furniture_Growth31_assembly_desk", "Activity31/Furniture_Activity31_training_tire"
            }.Select(path => Load<FurnitureDefinition>("Assets/Data/Interiors/Furniture/" + path + ".asset")).ToArray();
            CollectionAssert.AreEquivalent(story.Concat(career), furniture);
            foreach (var definition in furniture)
            {
                CollectionAssert.Contains(Registry.FurnitureDefinitions, definition);
                Assert.That(definition.TileParts.Count, Is.GreaterThan(0));
                CollectionAssert.Contains(definition.AllowedSurfaceIds, "house");
            }
            Assert.That(story.Select(x => x.UnlockToken).Where(x => !string.IsNullOrEmpty(x)).Distinct().Count(), Is.EqualTo(2));
            foreach (var definition in career) Assert.That(definition.UnlockToken, Is.Empty, "The chain must stay finishable by placing free career furniture.");
            Assert.That(Step("MyDream").Rewards, Is.Empty, "Story furniture is bought with allowance, not handed out.");
            var store = Load<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_CornerStore.asset");
            var allowance = Load<ItemDefinition>("Assets/Data/Items/Item_Allowance.asset");
            foreach (var definition in story)
            {
                Assert.That(definition.IsForSale, Is.True, definition.name);
                Assert.That(Registry.TextCatalog.Get(definition.DisplayNameKey, "en"), Is.Not.EqualTo(definition.DisplayNameKey), definition.name);
                Assert.That(definition.PriceItem, Is.SameAs(allowance));
                Assert.That(definition.Price, Is.EqualTo(10), "Two work shifts.");
                Assert.That(definition.PurchaseLocationId, Is.EqualTo(store.Id));
            }
            foreach (var definition in career) Assert.That(definition.IsForSale, Is.False, definition.name);
            Assert.That(Registry.LocationIdentities.Any(x => x != null && x.Location == store), Is.True, "The store must be a place the player can open.");
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

        [Test]
        public void OldSaveStoppedOnDustySecrets_ResumesOnMyDream_AndCanFinishTheStep()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var step = Step("MyDream");
            CollectionAssert.Contains(step.LegacyStepIds, "mainquest.DustySecrets");
            var progress = QuestChainProgress.FromSaveData(new QuestChainProgressSaveData
            {
                SaveSlot = "old-save", PlayerId = "player", ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(),
                CurrentStepId = "mainquest.DustySecrets", ObjectiveProgressValues = new[] { 0 },
                CompletedStepIds = new[] { "mainquest.BackToSchool", "mainquest.AfterSchoolTour", "mainquest.FirstPartTimeJob" }
            }, chain);
            Assert.That(progress.GetCurrentStep(chain), Is.SameAs(step));
            var log = new QuestChainLog(new ScriptableObject[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { progress.ToSaveData() } });
            var unlocks = new FurnitureUnlockProgress("old-save", "player");
            var rewards = new RewardRuntimeContext(null, null, null, null, null, null, unlocks);
            var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_ReadJobBooks.asset");
            log.RecordEventWithContext(new QuestEvent(QuestEventKind.LocationActivity, "old-save-read", activity: activity), default, rewards);
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.MyOwnSpace"));
            Assert.That(unlocks.UnlockedCount, Is.Zero, "Finishing the step does not hand out furniture.");
        }

        [Test]
        public void OldSaveThatFinishedDustySecrets_KeepsStepCompletionAndRewardClaim()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var progress = QuestChainProgress.FromSaveData(new QuestChainProgressSaveData
            {
                SaveSlot = "old-save", PlayerId = "player", ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(),
                CurrentStepId = "mainquest.MyOwnSpace", ObjectiveProgressValues = new[] { 0 },
                CompletedStepIds = new[] { "mainquest.DustySecrets" }, RewardClaimedIds = new[] { "step:mainquest.DustySecrets", "completion" }
            }, chain);
            var saved = progress.ToSaveData();
            CollectionAssert.Contains(saved.CompletedStepIds, "mainquest.MyDream");
            CollectionAssert.DoesNotContain(saved.CompletedStepIds, "mainquest.DustySecrets");
            Assert.That(progress.IsRewardClaimed("step:mainquest.MyDream"), Is.True, "A renamed step must not pay out twice.");
            Assert.That(progress.IsRewardClaimed("completion"), Is.True);
        }

        [Test]
        public void SavedStepWithNoKnownIdStaysVisibleAsInvalid_InsteadOfJumpingToAnotherStep()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var progress = QuestChainProgress.FromSaveData(new QuestChainProgressSaveData
            {
                SaveSlot = "old-save", PlayerId = "player", ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(), CurrentStepId = "mainquest.Unknown"
            }, chain);
            Assert.That(progress.CurrentStepId, Is.EqualTo("mainquest.Unknown"));
            Assert.That(progress.GetCurrentStep(chain), Is.Null);
        }

        [Test]
        public void FourDreams_ReplaceOldInterests_AndLinkToFacilityCandidates()
        {
            var interests = Registry.CareerInterests;
            Assert.That(interests.Length, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(new[] { "interest.chef", "interest.emergency-care", "interest.interior", "interest.soldier" }, interests.Select(x => x.Id));
            foreach (string career in DreamCareers)
            {
                var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_" + career + ".asset");
                CollectionAssert.Contains(interests, interest);
                Assert.That(interest.Candidate, Is.SameAs(Load<CareerCandidateDefinition>("Assets/Data/Careers/Facilities/" + career + "/Candidate_" + career + ".asset")));
                CollectionAssert.Contains(Registry.CareerCandidates, interest.Candidate);
                foreach (var strategies in new IEnumerable[] { interest.UnlockRequirements, interest.SelectionRules, interest.Recommendations, interest.Rewards })
                {
                    var values = strategies.Cast<UnityEngine.Object>().ToArray();
                    Assert.That(values, Is.Not.Empty, interest.Id);
                    foreach (var value in values) Assert.That(value != null, Is.True, interest.Id + " has a broken strategy reference.");
                }
            }
        }

        [Test]
        public void IntroducingAFacility_RevealsItsCandidate_SoTheDreamCanBeChosen_OnceAndForFree()
        {
            string[] introductions = { "Chef", "EmergencyCare", "Interior", "Soldier" };
            foreach (string career in introductions)
            {
                var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset");
                var outcome = Load<LocationActivityProgressDeltaOutcome>(Root + "Activities/Outcomes/CareerIntroduction_" + career + ".asset");
                Assert.That(outcome.Target, Is.EqualTo(LocationActivityProgressTarget.CareerHint));
                CollectionAssert.Contains(activity.Outcomes, outcome);
                var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_" + career + ".asset");
                var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
                var interests = new CareerInterestProgress("slot", "player");
                var candidates = new CareerCandidateProgress("slot", "player");
                Assert.That(interest.Evaluate(interests, candidates, student, 2).Allowed, Is.False, career + " must wait for its introduction.");

                Assert.That(new LocationActivityRunner().TryPerform(activity, student, "intro-1", out var first), Is.True, career);
                Assert.That(first.OutcomeLogIds, Is.Not.Empty, career + " introduction must leave a result the hint can read.");
                Assert.That(candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, first.RequestId, first.OutcomeLogIds).Applied, Is.True, career);
                Assert.That(interest.Candidate.GetState(candidates), Is.GreaterThanOrEqualTo(CareerCandidateState.Revealed), career);
                int score = candidates.GetUnderstandingScore(interest.Candidate);
                Assert.That(interest.Evaluate(interests, candidates, student, 2).Allowed, Is.True, career);
                Assert.That(interests.TrySelect(interest, candidates, student, 2, "select").Applied, Is.True, career);
                Assert.That(interests.CurrentInterestId, Is.EqualTo(interest.Id));

                Assert.That(new LocationActivityRunner().TryPerform(activity, student, "intro-2", out var again), Is.True, career);
                Assert.That(candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, again.RequestId, again.OutcomeLogIds).Applied, Is.False, "Listening again gives no new hint.");
                Assert.That(candidates.GetUnderstandingScore(interest.Candidate), Is.EqualTo(score));
            }
        }

        [TestCase("Chef")]
        [TestCase("EmergencyCare")]
        [TestCase("Interior")]
        [TestCase("Soldier")]
        public void CareerAlreadyUnlockedElsewhere_IntroductionStillRevealsCandidate(string career)
        {
            var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset");
            var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_" + career + ".asset");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            // An exploration choice or a save interrupted between the two saves leaves this state.
            student.UnlockCareerHint(Load<CareerDefinition>("Assets/Data/StudentLife/CareerPractices/Careers/Career_" + career + ".asset"));
            var candidates = new CareerCandidateProgress("slot", "player");
            Assert.That(new LocationActivityRunner().TryPerform(activity, student, "intro", out var result), Is.True);
            Assert.That(candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds).Applied, Is.True);
            Assert.That(interest.Evaluate(new CareerInterestProgress("slot", "player"), candidates, student, 2).Allowed, Is.True);
        }

        [Test]
        public void Dream_CanChangeAgainOnTheSameDay_AndRepickingIsANoOp()
        {
            var chef = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Chef.asset");
            var soldier = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Soldier.asset");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var candidates = new CareerCandidateProgress("slot", "player");
            foreach (string career in new[] { "Chef", "Soldier" })
            {
                var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset");
                Assert.That(new LocationActivityRunner().TryPerform(activity, student, "intro-" + career, out var result), Is.True);
                candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds);
            }
            var interests = new CareerInterestProgress("slot", "player");
            Assert.That(interests.TrySelect(chef, candidates, student, 2, "a").Applied, Is.True);
            Assert.That(interests.TrySelect(soldier, candidates, student, 2, "b").Applied, Is.True);
            Assert.That(interests.TrySelect(chef, candidates, student, 2, "c").Applied, Is.True);
            Assert.That(interests.TrySelect(chef, candidates, student, 2, "d").Applied, Is.False);
            Assert.That(interests.CurrentInterestId, Is.EqualTo(chef.Id));
        }

        [Test]
        public void OldInterestIdInASave_DoesNotBlockChoosingANewDream()
        {
            var interests = CareerInterestProgress.FromSaveData(new CareerInterestProgressSaveData { SaveSlot = "slot", PlayerId = "player", CurrentInterestId = "interest.technical", LastChangedDay = 1, ChangeCountForCurrentDay = 1 });
            var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Chef.asset");
            var candidates = new CareerCandidateProgress("slot", "player");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_IntroduceChef.asset");
            Assert.That(new LocationActivityRunner().TryPerform(activity, student, "intro", out var result), Is.True);
            candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds);
            Assert.That(interests.TrySelect(interest, candidates, student, 2, "select").Applied, Is.True);
            Assert.That(interests.CurrentInterestId, Is.EqualTo("interest.chef"));
            Assert.That(interests.PreviousInterestId, Is.EqualTo("interest.technical"));
        }

        [TestCase("RestaurantHelper", "Restaurant", "Game_Chef_Practice", "trait.taste")]
        [TestCase("ClinicHelper", "MedicalClinic", "Game_EmergencyCare_Practice", "trait.relief")]
        [TestCase("StudioHelper", "DesignStudio", "Game_Interior_Practice", "trait.aesthetic")]
        [TestCase("TrainingHelper", "TrainingGround", "Game_Soldier_Practice", "trait.discipline")]
        [TestCase("ErrandHelper", "Restaurant", "Game_Chef_Practice", "trait.delicacy")]
        [TestCase("SupplyHelper", "MedicalClinic", "Game_EmergencyCare_Practice", "trait.calm")]
        [TestCase("FitnessHelper", "TrainingGround", "Game_Soldier_Practice", "trait.fitness")]
        public void CareerJobs_AreRegistered_PayLikeTheWorkshop_AndRaiseTheirOwnTrait(string name, string location, string game, string traitId)
        {
            var work = Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_" + name + ".asset");
            var workshop = Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_WorkshopHelper.asset");
            CollectionAssert.Contains(Registry.PartTimeWorks, work);
            CollectionAssert.Contains(Registry.Workplaces, work.Workplace);
            CollectionAssert.Contains(work.Workplace.AvailableWorks, work);
            Assert.That(work.Workplace.LocationId, Is.EqualTo(Load<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_" + location + ".asset").Id));
            Assert.That(work.Minigame, Is.SameAs(Load<Rootborn.Game.Minigames.MinigameDefinition>("Assets/Data/StudentLife/Minigames/" + game + ".asset")));
            Assert.That(work.TimePolicy, Is.SameAs(workshop.TimePolicy));
            Assert.That((work.TimeCostMinutes, work.EnergyCost, work.FocusCost, work.StressDelta), Is.EqualTo((workshop.TimeCostMinutes, workshop.EnergyCost, workshop.FocusCost, workshop.StressDelta)));
            Assert.That(work.Rewards.Single().Count, Is.EqualTo(workshop.Rewards.Single().Count));
            Assert.That(work.Rewards.Single().Item, Is.SameAs(workshop.Rewards.Single().Item));
            var growth = work.Outcomes.OfType<WorkTraitDeltaOutcome>().Single();
            Assert.That(growth.Trait.Id, Is.EqualTo(traitId));
            Assert.That(growth.Delta, Is.GreaterThan(0));
            CollectionAssert.Contains(Registry.StudentLifeTraits, growth.Trait);
        }

        [Test]
        public void CareerJobs_RaiseSevenDifferentTraits_AndDayTwoHasARouteForEach()
        {
            string[] names = { "RestaurantHelper", "ClinicHelper", "StudioHelper", "TrainingHelper", "ErrandHelper", "SupplyHelper", "FitnessHelper" };
            var works = names.Select(x => Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_" + x + ".asset")).ToArray();
            Assert.That(works.Select(x => x.Outcomes.OfType<WorkTraitDeltaOutcome>().Single().Trait).Distinct().Count(), Is.EqualTo(7));
            Assert.That(works.Select(x => x.Id).Concat(Registry.PartTimeWorks.Select(x => x.Id)).Distinct().Count(), Is.EqualTo(Registry.PartTimeWorks.Length));
            var day = Load<CampaignDefinition>(Root + "Campaigns/Campaign_SchoolOpeningThreeDays.asset").GetDayByNumber(2);
            foreach (var work in works)
            {
                var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
                student.RecordActivityCompleted(work.Id);
                var context = new CampaignEvaluationContext(student, null, null, null);
                Assert.That(day.Routes.Any(route => route.Objectives.All(objective => objective.Evaluate(in context).IsComplete)), Is.True, work.Id + " must complete a Day 2 route.");
            }
        }

        [TestCase("ko")]
        [TestCase("en")]
        public void PlayerFacingOpeningText_HasNoHiddenPastStory(string language)
        {
            var catalog = Registry.TextCatalog;
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var keys = chain.Steps.SelectMany(step => new[] { step.DisplayNameKey, step.DescriptionKey }.Concat(step.Objectives.Select(x => x.DisplayKey))).ToList();
            keys.AddRange(new[] { chain.DisplayNameKey, chain.DescriptionKey });
            var campaign = Load<CampaignDefinition>(Root + "Campaigns/Campaign_SchoolOpeningThreeDays.asset");
            foreach (var day in campaign.Days)
            {
                keys.AddRange(new[] { day.DisplayNameKey, day.DirectionText, day.NextGuideText });
                foreach (var route in day.Routes) keys.AddRange(new[] { route.DisplayNameKey, route.DescriptionKey });
            }
            foreach (var name in new[] { "ReadJobBooks", "WatchWorkshopWork" })
            {
                var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_" + name + ".asset");
                keys.AddRange(new[] { activity.DisplayNameKey, activity.ResultTextKey });
            }
            foreach (var path in new[] { "Assets/Data/Dialogue/Dialogue_LibrarianIntro.asset", "Assets/Data/Dialogue/Dialogue_WorkManagerIntro.asset", Root + "Dialogue/Npc_Librarian_Schedule_0.asset", Root + "Dialogue/Npc_WorkManager_Schedule_0.asset" })
                keys.AddRange(Load<DialogueDefinition>(path).LineKeys);
            var entry = Load<Rootborn.Game.Encyclopedia.EncyclopediaEntryDefinition>("Assets/Data/Encyclopedia/Entries/EncyclopediaEntry_LockedArchive.asset");
            string[] banned = language == "ko"
                ? new[] { "비밀", "단서", "과거", "Archive", "잠긴", "장부", "수수께끼" }
                : new[] { "secret", "clue", "past", "archive", "locked", "ledger", "mystery" };
            foreach (string key in keys.Where(x => !string.IsNullOrEmpty(x)).Distinct())
            {
                string text = catalog.Get(key, language);
                Assert.That(text, Is.Not.EqualTo(key), "Missing text for " + key);
                foreach (string word in banned) Assert.That(text, Does.Not.Contain(word).IgnoreCase, key);
            }
            string encyclopedia = new SerializedObject(entry).FindProperty("_description").stringValue;
            Assert.That(encyclopedia, Does.StartWith("dream."));
        }

        [TestCase("Chef", "RestaurantHelper", "ErrandHelper")]
        [TestCase("EmergencyCare", "ClinicHelper", "SupplyHelper")]
        [TestCase("Interior", "StudioHelper", "WorkshopHelper")]
        [TestCase("Soldier", "TrainingHelper", "FitnessHelper")]
        public void IntroductionAndTwoJobs_ReachCareerChoiceWithoutPractice_AndRepeatingAJobAddsNothing(string career, string firstJob, string secondJob)
        {
            var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_" + career + ".asset");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var candidates = new CareerCandidateProgress("slot", "player");
            var intro = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset");
            Assert.That(new LocationActivityRunner().TryPerform(intro, student, "intro", out var result), Is.True);
            candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds);
            string Work(string name) => Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_" + name + ".asset").Id;
            Assert.That(candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, "job-1", new[] { Work(firstJob) }).Applied, Is.True);
            Assert.That(candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, "job-1-again", new[] { Work(firstJob) }).Applied, Is.False);
            Assert.That(interest.Candidate.GetState(candidates), Is.LessThan(CareerCandidateState.ReadyForChoice));
            Assert.That(candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, "job-2", new[] { Work(secondJob) }).Applied, Is.True);
            Assert.That(interest.Candidate.GetState(candidates), Is.EqualTo(CareerCandidateState.ReadyForChoice));
        }

        [TestCase("Chef")]
        [TestCase("EmergencyCare")]
        [TestCase("Interior")]
        [TestCase("Soldier")]
        public void EachDream_NamesTeacherFollowUp_AndIntroductionHasSpokenResult(string career)
        {
            var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_" + career + ".asset");
            Assert.That(interest.FollowUpNpc, Is.SameAs(Load<NpcDefinition>("Assets/Data/NPCs/Npc_Teacher.asset")));
            foreach (string language in new[] { "ko", "en" })
                foreach (string key in new[] { interest.FollowUpLineKey, interest.ChangedLineKey, Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset").ResultTextKey })
                {
                    Assert.That(key, Is.Not.Empty);
                    Assert.That(Registry.TextCatalog.Get(key, language), Is.Not.EqualTo(key), key);
                }
        }

        private static StudentLifeProgress OnDay(int day)
        {
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            typeof(StudentLifeProgress).GetProperty("CurrentDay").SetValue(student, day);
            return student;
        }

        private static QuestChainLog LogAt(QuestChainDefinition chain, string step, params int[] progress)
        {
            var log = new QuestChainLog(new ScriptableObject[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData
            {
                SaveSlot = "slot", PlayerId = "player", ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(),
                CurrentStepId = step, ObjectiveProgressValues = progress
            } } });
            return log;
        }

        [Test]
        public void StoryPacing_DayConditionsSitOnTheStepsThatEndADay()
        {
            var expected = new System.Collections.Generic.Dictionary<string, int> { { "BackToSchool", 2 }, { "FirstPartTimeJob", 3 } };
            foreach (string name in StepNames)
            {
                var conditions = Step(name).CompletionConditions;
                if (!expected.TryGetValue(name, out int day)) { Assert.That(conditions, Is.Empty, name); continue; }
                Assert.That(conditions.Length, Is.EqualTo(1), name);
                var condition = conditions[0] as Rootborn.Game.Quests.Conditions.DayAtLeastCondition;
                Assert.That(condition, Is.Not.Null, name);
                Assert.That(condition.MinimumDay, Is.EqualTo(day), name);
                CollectionAssert.Contains(Registry.QuestConditions, condition);
                Assert.That(Registry.TextCatalog.Get(condition.BlockedSummary, "ko"), Is.Not.EqualTo(condition.BlockedSummary));
                Assert.That(Registry.TextCatalog.Get(condition.BlockedSummary, "en"), Is.Not.EqualTo(condition.BlockedSummary));
            }
        }

        [Test]
        public void StoryPacing_FinishedStepWaitsForItsDay_ThenAdvancesWithoutANewAction()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var rewards = new RewardRuntimeContext(null, null, null, null, null, null, new FurnitureUnlockProgress("slot", "player"));
            var log = LogAt(chain, "mainquest.BackToSchool", 1, 1);
            Assert.That(log.EvaluateStepProgress(chain, new QuestRuntimeContext(null, null, null, null, OnDay(1)), rewards), Is.False);
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.BackToSchool"));
            Assert.That(log.EvaluateStepProgress(chain, new QuestRuntimeContext(null, null, null, null, OnDay(2)), rewards), Is.True);
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.AfterSchoolTour"));

            log = LogAt(chain, "mainquest.FirstPartTimeJob", 1);
            Assert.That(log.EvaluateStepProgress(chain, new QuestRuntimeContext(null, null, null, null, OnDay(2)), rewards), Is.False);
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.FirstPartTimeJob"));
            Assert.That(log.EvaluateStepProgress(chain, new QuestRuntimeContext(null, null, null, null, OnDay(3)), rewards), Is.True);
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.MyDream"));
        }

        [Test]
        public void StoryPacing_UnknownDayNeverPassesAStep()
        {
            var condition = ScriptableObject.CreateInstance<Rootborn.Game.Quests.Conditions.DayAtLeastCondition>();
            try
            {
                condition.ConfigureForTests(2, "key");
                Assert.That(condition.IsSatisfied(default), Is.False);
                Assert.That(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, null, OnDay(1))), Is.False);
                Assert.That(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, null, OnDay(2))), Is.True);
                Assert.That(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, null, OnDay(9))), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(condition); }
        }

        [Test]
        public void DayStart_AnnouncesTheDayFirst_ThenADreamChosenEarlier()
        {
            var interest = Registry.CareerInterests[0];
            var recorded = new System.Collections.Generic.List<QuestEvent>();
            Action<QuestEvent, StudentLifeProgress> listener = (questEvent, progress) => recorded.Add(questEvent);
            string saves = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-day-start-" + Guid.NewGuid().ToString("N"));
            Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(saves);
            var root = new GameObject("DayStartFixture");
            root.SetActive(false);
            var student = root.AddComponent<StudentLifeProgressComponent>();
            student.ConfigureForTests("slot-day-start", "player", 12, 10, 0, 480);
            var dreams = root.AddComponent<CareerInterestProgressComponent>();
            dreams.ConfigureForTests("slot-day-start", "player");
            dreams.Bind(Registry.CareerInterests);
            QuestGameplayEventBridge.OnRecorded += listener;
            try
            {
                Rootborn.UI.StudentLife.StudentDayResultPanel.PublishDayStartEvents(student);
                Assert.That(recorded.Count, Is.EqualTo(1), "No dream yet: only the day is announced.");
                Assert.That(recorded[0].Kind, Is.EqualTo(QuestEventKind.DayStarted));
                recorded.Clear();
                typeof(CareerInterestProgress).GetProperty("CurrentInterestId").SetValue(dreams.Progress, interest.Id);
                Rootborn.UI.StudentLife.StudentDayResultPanel.PublishDayStartEvents(student);
                Assert.That(recorded.Select(x => x.Kind), Is.EqualTo(new[] { QuestEventKind.DayStarted, QuestEventKind.CareerInterestSelected }));
                Assert.That(recorded[1].CareerInterest, Is.SameAs(interest));
                Assert.That(recorded[0].EventKey, Is.Not.EqualTo(recorded[1].EventKey));
            }
            finally
            {
                QuestGameplayEventBridge.OnRecorded -= listener;
                UnityEngine.Object.DestroyImmediate(root);
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                if (System.IO.Directory.Exists(saves)) System.IO.Directory.Delete(saves, true);
            }
        }

        [Test]
        public void ChoosingAnyDream_CompletesMyDream()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            foreach (var interest in Registry.CareerInterests)
            {
                var log = new QuestChainLog(new ScriptableObject[] { chain });
                log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData
                {
                    SaveSlot = "slot", PlayerId = "player", ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(),
                    CurrentStepId = "mainquest.MyDream", ObjectiveProgressValues = new[] { 0 }
                } } });
                var rewards = new RewardRuntimeContext(null, null, null, null, null, null, new FurnitureUnlockProgress("slot", "player"));
                log.RecordEventWithContext(new QuestEvent(QuestEventKind.CareerInterestSelected, "pick-" + interest.Id, careerInterest: interest), default, rewards);
                Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.MyOwnSpace"), interest.Id);
            }
        }

        [Test]
        public void AlreadyUnlockedFurniture_DoesNotBlockMyDream()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var log = new QuestChainLog(new ScriptableObject[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData
            {
                SaveSlot = "slot", PlayerId = "player", ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(),
                CurrentStepId = "mainquest.MyDream", ObjectiveProgressValues = new[] { 0 }
            } } });
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            unlocks.TryUnlock("unlock.furniture.story.photo-board");
            var rewards = new RewardRuntimeContext(null, null, null, null, null, null, unlocks);
            var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_ReadJobBooks.asset");
            log.RecordEventWithContext(new QuestEvent(QuestEventKind.LocationActivity, "read", activity: activity), default, rewards);
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.MyOwnSpace"));
            Assert.That(unlocks.UnlockedCount, Is.EqualTo(1), "Owned furniture stays owned and nothing extra is granted.");
        }

        [Test]
        public void TeacherReactsAgain_WhenThePlayerChangesAwayAndBackWithoutTalking()
        {
            var chef = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Chef.asset");
            var soldier = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Soldier.asset");
            var progress = new CareerInterestProgress("slot", "player");
            var candidates = new CareerCandidateProgress("slot", "player");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            foreach (string career in new[] { "Chef", "Soldier" })
            {
                var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset");
                new LocationActivityRunner().TryPerform(activity, student, "intro-" + career, out var result);
                candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds);
            }
            progress.TrySelect(chef, candidates, student, 2, "a");
            Assert.That(progress.TryBeginFollowUp(), Is.True);
            progress.TrySelect(soldier, candidates, student, 2, "b");
            progress.TrySelect(chef, candidates, student, 2, "c");
            Assert.That(progress.TryBeginFollowUp(), Is.True, "Coming back is still a new choice.");
        }

        [Test]
        public void TeacherReactsOncePerPickedDream_SurvivesSave_AndNamesAChange()
        {
            var progress = new CareerInterestProgress("slot", "player");
            Assert.That(progress.TryBeginFollowUp(), Is.False, "No dream yet.");
            var candidates = new CareerCandidateProgress("slot", "player");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            foreach (string career in new[] { "Chef", "Soldier" })
            {
                var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_Introduce" + career + ".asset");
                new LocationActivityRunner().TryPerform(activity, student, "intro-" + career, out var result);
                candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds);
            }
            progress.TrySelect(Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Chef.asset"), candidates, student, 2, "a");
            Assert.That(progress.TryBeginFollowUp(), Is.True);
            Assert.That(progress.TryBeginFollowUp(), Is.False);
            var restored = CareerInterestProgress.FromSaveData(progress.ToSaveData());
            Assert.That(restored.TryBeginFollowUp(), Is.False, "Reloading must not repeat the reaction.");
            restored.TrySelect(Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Soldier.asset"), candidates, student, 2, "b");
            Assert.That(restored.TryBeginFollowUp(), Is.True);
            Assert.That(restored.PreviousInterestId, Is.EqualTo("interest.chef"));
        }

        [Test]
        public void ChoosingADreamInThePanel_RecordsOneQuestEventCarryingThatDream()
        {
            var interest = Load<CareerInterestDefinition>("Assets/Data/Careers/Interests/Interest_Chef.asset");
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var candidates = new CareerCandidateProgress("slot", "player");
            var activity = Load<LocationActivityDefinition>(Root + "Activities/LocationActivity_IntroduceChef.asset");
            Assert.That(new LocationActivityRunner().TryPerform(activity, student, "intro", out var result), Is.True);
            candidates.ApplyFirstMatchingHint(Registry.CareerHints, student, result.RequestId, result.OutcomeLogIds);
            var recorded = new System.Collections.Generic.List<QuestEvent>();
            Action<QuestEvent, StudentLifeProgress> listener = (questEvent, progress) => recorded.Add(questEvent);
            var root = new GameObject("DreamPanelFixture");
            QuestGameplayEventBridge.OnRecorded += listener;
            try
            {
                var panel = root.AddComponent<Rootborn.UI.StudentLife.CareerInterestPanel>();
                panel.Show(new[] { interest }, new CareerInterestProgress("slot", "player"), candidates, student, 2);
                panel.GetInterestButtonForTests(0).onClick.Invoke();
                panel.GetInterestButtonForTests(0).onClick.Invoke();
                Assert.That(recorded.Count, Is.EqualTo(1), "Picking the same dream again is not a new choice.");
                Assert.That(recorded[0].Kind, Is.EqualTo(QuestEventKind.CareerInterestSelected));
                Assert.That(recorded[0].CareerInterest, Is.SameAs(interest));
            }
            finally
            {
                QuestGameplayEventBridge.OnRecorded -= listener;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("ko", "[E] 말 걸기", "[E] 오늘의 계획 세우기", "[E] 마을로 가기", "[E] 들어가기", "[E] 동네 가게 둘러보기")]
        [TestCase("en", "[E] Talk", "[E] Plan Day", "[E] Enter Town", "[E] Enter", "[E] Visit Corner store")]
        public void InteractionPrompts_FollowTheLanguage(string language, string talk, string plan, string portal, string unnamed, string visit)
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            var root = new GameObject("PromptFixture");
            GameDataRegistryProvider.SetOverrideForTests(Registry);
            try
            {
                Rootborn.Game.Localization.GameText.Language = language;
                Assert.That(root.AddComponent<NpcInteractor>().InteractionPrompt, Is.EqualTo(talk));
                Assert.That(root.AddComponent<Rootborn.UI.StudentLife.DailyPlanBoardInteractor>().InteractionPrompt, Is.EqualTo(plan));
                var named = root.AddComponent<Rootborn.Game.World.WorldPortal>();
                new SerializedObject(named).FindProperty("_displayName").stringValue = "Town";
                var so = new SerializedObject(named); so.FindProperty("_displayName").stringValue = "Town"; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(named.InteractionPrompt, Is.EqualTo(portal));
                var blank = new GameObject("Blank"); blank.transform.SetParent(root.transform);
                Assert.That(blank.AddComponent<Rootborn.Game.World.WorldPortal>().InteractionPrompt, Is.EqualTo(unnamed));
                var visitor = root.AddComponent<LocationVisitInteractor>();
                visitor.Bind(Load<LocationDefinition>("Assets/Data/StudentLife/Locations/Location_CornerStore.asset"));
                Assert.That(visitor.InteractionPrompt, Is.EqualTo(visit));
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SaveRootArgument_RedirectsSavesAndIsOptional()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-save-root-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.That(Rootborn.Game.Save.SaveService.ApplyRootArgument(new[] { "game.exe", "-mode", "client" }), Is.False);
                Assert.That(Rootborn.Game.Save.SaveService.ApplyRootArgument(new[] { "game.exe", "-saveRoot" }), Is.False, "A missing value changes nothing.");
                Assert.That(Rootborn.Game.Save.SaveService.ApplyRootArgument(new[] { "game.exe", "-saveRoot", root }), Is.True);
                Assert.That(new Rootborn.Game.Save.SaveService("slot-a").DirectoryPath.Replace('\\', '/'), Does.StartWith(root.Replace('\\', '/')));
            }
            finally
            {
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
            }
        }

        [Test]
        public void PlanChosenBeforeTheChainStarted_CountsOnceWhenItStarts()
        {
            var chain = Load<QuestChainDefinition>(Root + "QuestChain_SchoolOpening.asset");
            var registry = Registry;
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var rewards = new RewardRuntimeContext(null, null, null, null, null, null, new FurnitureUnlockProgress("slot", "player"));
            var context = new QuestRuntimeContext(null, null, null, null, student);
            var log = new QuestChainLog(new ScriptableObject[] { chain });
            Assert.That(log.Accept(chain), Is.True);
            Assert.That(Rootborn.UI.Quests.QuestChainRuntimeService.CreditPlanChosenBeforeStart(registry, log, student, context, rewards), Is.False, "No plan yet.");
            Assert.That(log.GetObjectiveProgress(chain, 1), Is.EqualTo(0));
            var route = registry.DailyPlanRouteSets[0].Routes[0];
            Assert.That(student.DailyPlan.SelectRoute(route), Is.True);
            Assert.That(Rootborn.UI.Quests.QuestChainRuntimeService.CreditPlanChosenBeforeStart(registry, log, student, context, rewards), Is.True);
            Assert.That(log.GetObjectiveProgress(chain, 1), Is.EqualTo(1));
            Rootborn.UI.Quests.QuestChainRuntimeService.CreditPlanChosenBeforeStart(registry, log, student, context, rewards);
            Assert.That(log.GetObjectiveProgress(chain, 1), Is.EqualTo(1));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void JobWithNoTimeLeft_ExplainsWhyNothingHappened(int spend, bool noTimeLeft)
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            var root = new GameObject("JobFailureFixture");
            root.SetActive(false);
            GameDataRegistryProvider.SetOverrideForTests(Registry);
            string saves = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-job-failure-" + Guid.NewGuid().ToString("N"));
            Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(saves);
            try
            {
                Rootborn.Game.Localization.GameText.Language = "ko";
                var student = root.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-job-failure", "player", 12, 10, 0, 480);
                var progress = student.EnsureProgress();
                if (noTimeLeft) typeof(StudentLifeProgress).GetProperty("TimePointsSpentToday").SetValue(progress, progress.DailyTimePointBudget);
                var job = root.AddComponent<PartTimeWorkInteractor>();
                job.Bind(Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_RestaurantHelper.asset"));
                string message = ((Rootborn.Game.Player.IPlayerInteractionFailureExplainer)job).ExplainFailure(root);
                if (noTimeLeft) { Assert.That(message, Does.Contain("내일")); }
                else { Assert.That(message, Is.Empty); }
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
                UnityEngine.Object.DestroyImmediate(root);
                if (System.IO.Directory.Exists(saves)) System.IO.Directory.Delete(saves, true);
            }
        }

        // Korean text calls the board by the name its prompt uses, not the English "Daily Plan".
        [TestCase("Assets/Data/StudentLife/MainStory/Campaigns/Day_1.asset")]
        [TestCase("Assets/Data/StudentLife/MainStory/Objectives/Objective_DailyPlan.asset")]
        [TestCase("Assets/Data/StudentLife/MainStory/Steps/MainQuest_BackToSchool.asset")]
        [TestCase("Assets/Data/Dialogue/Dialogue_TeacherIntro.asset")]
        public void KoreanOpeningText_DoesNotSayDailyPlanInEnglish(string path)
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            try
            {
                GameDataRegistryProvider.SetOverrideForTests(Registry);
                Rootborn.Game.Localization.GameText.Language = "ko";
                var keys = System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(path), @"(?:^|\s)((?:opening|dream)\.[\w.\-]+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline)
                    .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToArray();
                Assert.That(keys, Is.Not.Empty);
                foreach (var key in keys)
                {
                    string text = Rootborn.Game.Localization.GameText.Get(key);
                    Assert.That(text, Does.Not.Contain("Daily Plan"), key);
                }
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
            }
        }

        // Store work happens outside school but is not an outside-school activity, so "nothing outside school" would contradict the list above it.
        [Test]
        public void DayResult_SaysNothingAboutOutsideSchoolWhenThereIsNoEntry()
        {
            var root = new GameObject("DayResultOutsideFixture");
            try
            {
                var panel = root.AddComponent<Rootborn.UI.StudentLife.StudentDayResultPanel>();
                var format = typeof(Rootborn.UI.StudentLife.StudentDayResultPanel).GetMethod("FormatOutsideSchoolList", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(format.Invoke(panel, new object[] { new string[0] }), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DayResult_RepeatedWork_ShowsTheTotal()
        {
            string log = Rootborn.Game.StudentLife.PartTimeWorkLogCodec.EncodeDelta("work.store", "item.allowance", 5);
            Assert.That(Rootborn.UI.StudentLife.StudentDayResultPanel.GrowthLines(new[] { log, log }), Is.EqualTo(new[] { Rootborn.Game.StudentLife.PartTimeWorkLogCodec.EncodeDelta("work.store", "item.allowance", 10) }));
        }

        [Test]
        public void DayResult_CareerLine_ReadsAsWordsNotRawFields()
        {
            var root = new GameObject("DayResultCareerFixture");
            try
            {
                var panel = root.AddComponent<Rootborn.UI.StudentLife.StudentDayResultPanel>();
                var format = typeof(Rootborn.UI.StudentLife.StudentDayResultPanel).GetMethod("FormatCareerCandidateList", BindingFlags.Instance | BindingFlags.NonPublic);
                string text = (string)format.Invoke(panel, new object[] { new[] { "career.a:hint.b:Known" } });
                Assert.That(text, Does.Not.Contain(":"));
                Assert.That(text, Does.Contain(" · "));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Journal_CampaignCompletedLabels_AreNotRawIds()
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            try
            {
                GameDataRegistryProvider.SetOverrideForTests(Registry);
                Rootborn.Game.Localization.GameText.Language = "ko";
                var guids = AssetDatabase.FindAssets("Objective_ t:CampaignObjectiveBase", new[] { "Assets/Data/StudentLife/MainStory/Campaigns" });
                Assert.That(guids, Is.Not.Empty);
                foreach (var guid in guids)
                {
                    var objective = AssetDatabase.LoadAssetAtPath<CampaignObjectiveBase>(AssetDatabase.GUIDToAssetPath(guid));
                    Assert.That(Rootborn.UI.Objectives.CampaignObjectiveSource.CompletedLabel(objective.Id), Does.Not.Contain("opening."), objective.Id);
                }
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
            }
        }

        [Test]
        public void HouseEntry_OnlyAnnouncesFurnitureWhenACurrentStepWantsIt()
        {
            var chain = Load<QuestChainDefinition>("Assets/Data/StudentLife/MainStory/QuestChain_SchoolOpening.asset");
            var furnitureStep = Load<QuestStepDefinition>("Assets/Data/StudentLife/MainStory/Steps/MainQuest_MyOwnSpace.asset");
            var otherStep = Load<QuestStepDefinition>("Assets/Data/StudentLife/MainStory/Steps/MainQuest_BackToSchool.asset");
            Assert.That(Rootborn.UI.Interiors.AuthoredHousePlacementBinding.StepWantsFurniture(furnitureStep), Is.True);
            Assert.That(Rootborn.UI.Interiors.AuthoredHousePlacementBinding.StepWantsFurniture(otherStep), Is.False);
            Assert.That(Rootborn.UI.Interiors.AuthoredHousePlacementBinding.StepWantsFurniture(null), Is.False);
            Assert.That(chain, Is.Not.Null);
        }

        [Test]
        public void TimeHud_ShowsAllowanceOncePlayerHasInventory()
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            var root = new GameObject("TimeHudAllowanceFixture");
            GameDataRegistryProvider.SetOverrideForTests(Registry);
            try
            {
                Rootborn.Game.Localization.GameText.Language = "ko";
                Rootborn.Game.Player.PlayerGlobalState.ClearForTests();
                var inventory = root.AddComponent<Rootborn.Game.Player.PlayerInventory>();
                var desk = Load<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Story/Furniture_Story_StudyDesk.asset");
                Assert.That(Rootborn.UI.HUD.TimeHud.AllowanceText(null), Is.Empty);
                Assert.That(Rootborn.UI.HUD.TimeHud.AllowanceText(inventory), Is.EqualTo(" · 용돈 0"));
                inventory.Inventory.Add(desk.PriceItem, 7);
                Assert.That(Rootborn.UI.HUD.TimeHud.AllowanceText(inventory), Is.EqualTo(" · 용돈 7"));
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlacementPanel_HidesDeveloperControlsForPlayers()
        {
            var root = new GameObject("PlacementDevFixture");
            try
            {
                foreach (var name in new[] { "SeedLabel", "SeedInput", "RegenerateButton", "ApplyButton", "ValidationLabel", "InteriorPlacementStatusText", "InteriorPlacementSummaryText", "SaveFurnitureLayoutButton" })
                    new GameObject(name).transform.SetParent(root.transform, false);
                Rootborn.UI.Interiors.InteriorFurniturePlacementPanel.HideDeveloperControls(root.transform);
                Assert.That(root.transform.Find("SeedInput").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("RegenerateButton").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("InteriorPlacementStatusText").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("SaveFurnitureLayoutButton").gameObject.activeSelf, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(1920, 1080, 480, 270)]
        [TestCase(2560, 1440, 512, 288)]
        [TestCase(3840, 2160, 480, 270)]
        [TestCase(1600, 900, 533, 300)]
        public void TownCamera_FillsTheWindowAtTheNearestWholeZoom(int width, int height, int frameWidth, int frameHeight)
        {
            var frame = Rootborn.Game.World.AuthoredWorldScene.FitPixelFrame(width, height, 288);
            Assert.That(frame.x, Is.EqualTo(frameWidth));
            Assert.That(frame.y, Is.EqualTo(frameHeight));
        }

        [Test]
        public void CharacterPreview_ShowsTheCharacterTheGameActuallyUses()
        {
            var root = new GameObject("CharacterPreviewFixture");
            GameDataRegistryProvider.SetOverrideForTests(Registry);
            try
            {
                var method = typeof(Rootborn.UI.MainMenu.SaveSlotSelectPanel).GetMethod("BuildCharacterPreviewImage", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                method.Invoke(null, new object[] { root.transform, null });
                var growing = Rootborn.Game.Characters.Growing.GrowingCharacterRuntimeInstaller.FindDefault(Registry);
                Assert.That(growing, Is.Not.Null);
                var layer = root.transform.Find("CharacterPreviewImage/PreviewGrowing_Body");
                Assert.That(layer, Is.Not.Null);
                Assert.That(layer.GetComponent<UnityEngine.UI.Image>().sprite, Is.EqualTo(growing.Body.GetFrame(Rootborn.Game.Characters.Growing.GrowingCharacterVisualView.RowDown, Rootborn.Game.Characters.Growing.GrowingCharacterVisualView.StandingColumn)));
                Assert.That(root.transform.Find("CharacterPreviewImage/PreviewGrowing_Head"), Is.Not.Null);
                Assert.That(root.transform.Find("CharacterPreviewImage/PreviewGrowing_Outfit"), Is.Not.Null);
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StorePanel_ShowsHowMuchAllowanceThePlayerHas()
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            var root = new GameObject("StoreBalanceFixture");
            GameDataRegistryProvider.SetOverrideForTests(Registry);
            try
            {
                Rootborn.Game.Localization.GameText.Language = "ko";
                var desk = Load<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Story/Furniture_Story_StudyDesk.asset");
                var inventory = root.AddComponent<Rootborn.Game.Player.PlayerInventory>();
                var offers = new[] { desk };
                Assert.That(Rootborn.UI.StudentLife.LocationIdentityPanel.OfferBalanceText(new FurnitureDefinition[0], inventory), Is.Empty, "A place that sells nothing shows no balance.");
                Assert.That(Rootborn.UI.StudentLife.LocationIdentityPanel.OfferBalanceText(offers, inventory), Is.EqualTo(" · 용돈 0"));
                inventory.Inventory.Add(desk.PriceItem, 5);
                Assert.That(Rootborn.UI.StudentLife.LocationIdentityPanel.OfferBalanceText(offers, inventory), Is.EqualTo(" · 용돈 5"));

                // The number has to reach the screen on the first look, not only after buying something.
                var store = Load<LocationIdentityDefinition>("Assets/Data/StudentLife/LocationIdentities/LocationIdentity_CornerStore.asset");
                var panel = root.AddComponent<Rootborn.UI.StudentLife.LocationIdentityPanel>();
                panel.Show(store, root);
                var budget = panel.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(text => text.name == "TimeBudget");
                Assert.That(budget.text, Does.EndWith(" · 용돈 5"));
                panel.Hide();
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // The shipped game starts in the House.
        [Test]
        public void NewGame_StartsInTheHouse()
        {
            var shipped = Rootborn.Game.World.WorldEntryService.ResolveNewGame(Registry.WorldTravelCatalog);
            Assert.That(shipped.SceneName, Is.EqualTo("House"));
            Assert.That(shipped.SpawnId, Is.EqualTo("house-entry"));
        }

        // The shipped game starts in the House. Tests that only need a game running may start elsewhere for setup.
        [Test]
        public void NewGame_StartsInTheHouse_UnlessATestAsksForAnotherEntry()
        {
            var catalog = Registry.WorldTravelCatalog;
            try
            {
                var shipped = Rootborn.Game.World.WorldEntryService.ResolveNewGame(catalog);
                Assert.That(shipped.SceneName, Is.EqualTo("House"));
                Assert.That(shipped.SpawnId, Is.EqualTo("house-entry"));
                Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("Town", "town-home-entry");
                var forTest = Rootborn.Game.World.WorldEntryService.ResolveNewGame(catalog);
                Assert.That(forTest.SceneName, Is.EqualTo("Town"));
                Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests("NoSuchScene", "nowhere");
                Assert.That(Rootborn.Game.World.WorldEntryService.ResolveNewGame(catalog).SceneName, Is.EqualTo("House"), "An unknown entry falls back to the shipped start.");
            }
            finally { Rootborn.Game.World.WorldEntryService.SetNewGameEntryForTests(null, null); }
            Assert.That(Rootborn.Game.World.WorldEntryService.ResolveNewGame(catalog).SceneName, Is.EqualTo("House"));
        }

        [TestCase("ko", "[E] 식당 일 돕기")]
        [TestCase("en", "[E] Help at the restaurant")]
        public void JobPrompt_ShowsLocalizedWorkName(string language, string expected)
        {
            string before = Rootborn.Game.Localization.GameText.Language;
            var root = new GameObject("JobPromptFixture");
            GameDataRegistryProvider.SetOverrideForTests(Registry);
            try
            {
                Rootborn.Game.Localization.GameText.Language = language;
                var interactor = root.AddComponent<PartTimeWorkInteractor>();
                interactor.Bind(Load<PartTimeWorkDefinition>("Assets/Data/StudentLife/PartTimeWork/PartTimeWork_RestaurantHelper.asset"));
                Assert.That(interactor.InteractionPrompt, Is.EqualTo(expected));
            }
            finally
            {
                GameDataRegistryProvider.SetOverrideForTests(null);
                Rootborn.Game.Localization.GameText.Language = before;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArchiveRumorCluesAndUsage_NoLongerTellTheOldStory()
        {
            foreach (string path in new[]
            {
                "Assets/Data/DiscoveryClues/Definitions/DiscoveryClue_LibraryArchiveRumor.asset",
                "Assets/Data/DiscoveryClues/Definitions/DiscoveryClue_LocationState_LibraryArchiveRumor.asset",
                "Assets/Data/DiscoveryClues/Sources/Source_BoardArchiveRumor.asset",
                "Assets/Data/DiscoveryClues/Sources/Source_LibrarianArchiveRumor.asset",
                "Assets/Data/WorldState/Usage/Usage_LibraryArchiveTable.asset",
                "Assets/Data/DiscoveryClues/Interpretations/Interpretation_AskLibrarian.asset"
            })
            {
                var iterator = new SerializedObject(Load<ScriptableObject>(path)).GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.String || iterator.name == "_id") continue;
                    if (iterator.name == "m_Name" || iterator.name == "m_EditorClassIdentifier") continue;
                    foreach (string word in new[] { "rumor", "archive", "clue", "old record", "secret" })
                        Assert.That(iterator.stringValue, Does.Not.Contain(word).IgnoreCase, path + " " + iterator.name);
                    if (iterator.stringValue.Length > 0 && iterator.stringValue.Any(char.IsLetter))
                        Assert.That(Registry.TextCatalog.Get(iterator.stringValue, "ko").Any(c => c >= 0xAC00 && c <= 0xD7A3), Is.True, "Missing Korean text: " + iterator.stringValue);
                }
            }
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
