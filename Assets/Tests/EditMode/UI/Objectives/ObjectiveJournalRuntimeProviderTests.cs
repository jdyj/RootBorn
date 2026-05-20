using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalRuntimeProviderTests
    {
        [Test]
        public void ModernUiPanelAutoInstaller_InstallsRuntimeObjectiveProviderOnCanvasHost()
        {
            var canvasGo = new GameObject("ObjectiveJournalRuntimeProviderFixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvasGo.GetComponent<Canvas>(), null);

                var provider = canvasGo.GetComponent<ObjectiveJournalRuntimeProvider>();
                var journal = canvasGo.GetComponentInChildren<ObjectiveJournalPanel>(true);
                var hud = canvasGo.GetComponentInChildren<TrackedObjectiveHud>(true);

                Assert.IsNotNull(provider, "Objective Journal must have one runtime provider installed by the common UI installer.");
                Assert.IsNotNull(journal, "Objective Journal panel must still be installed by the common UI installer.");
                Assert.IsNotNull(hud, "TrackedObjectiveHud must still be the single always-visible objective HUD owner.");
                Assert.AreSame(journal, provider.Panel);
                Assert.AreSame(hud, provider.TrackedHud);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void QuestLogObjectiveSource_DisplaysRewardClaimedQuestsAndAllObjectiveProgress()
        {
            var firstObjective = ScriptableObject.CreateInstance<KeyedQuestObjective>();
            var secondObjective = ScriptableObject.CreateInstance<KeyedQuestObjective>();
            firstObjective.Configure("Gather herbs", 2, "herbs");
            secondObjective.Configure("Talk to guide", 1, "guide");

            var claimedQuest = ScriptableObject.CreateInstance<QuestDefinition>();
            claimedQuest.ConfigureForRuntime(
                "quest.claimed",
                "Claimed Quest",
                "Completed quest detail",
                new QuestObjectiveBase[] { firstObjective, secondObjective },
                Array.Empty<QuestRewardBase>(),
                Array.Empty<QuestCompletionEffectBase>());

            var log = new QuestLog(new[] { claimedQuest });
            log.Accept(claimedQuest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "herbs-1"));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "herbs-2"));
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "guide"));
            Assert.AreEqual(QuestState.Completed, log.GetState(claimedQuest));
            Assert.IsTrue(log.ClaimReward(claimedQuest, default));

            var panelGo = new GameObject("QuestLogPanel", typeof(RectTransform));
            try
            {
                panelGo.AddComponent<QuestLogPanel>().Bind(log, new[] { claimedQuest }, default);

                var items = BuildItemsFromSource("QuestLogObjectiveSource", null, null);

                Assert.AreEqual(1, items.Length, "RewardClaimed quests should remain visible in Objective Journal history.");
                Assert.AreEqual("RewardClaimed", items[0].State);
                StringAssert.Contains("Gather herbs 2 / 2", items[0].ProgressText);
                StringAssert.Contains("Talk to guide 1 / 1", items[0].ProgressText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelGo);
                UnityEngine.Object.DestroyImmediate(firstObjective);
                UnityEngine.Object.DestroyImmediate(secondObjective);
                UnityEngine.Object.DestroyImmediate(claimedQuest);
            }
        }

        [Test]
        public void QuestChainObjectiveSource_DisplaysTrackedChainProgressAndNextAction()
        {
            var objective = ScriptableObject.CreateInstance<KeyedQuestObjective>();
            objective.Configure("questchain.objective.help", 2, "help");
            var step = ScriptableObject.CreateInstance<QuestStepDefinition>();
            step.ConfigureForTests("questchain.step.help", "questchain.step.help", "questchain.step.help.desc", new QuestObjectiveBase[] { objective }, null, null, null, null);
            var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
            chain.ConfigureForTests(
                "questchain.source.help",
                "questchain.source.help",
                "questchain.source.help.desc",
                "career.helper",
                new ScriptableObject[] { step },
                null,
                null,
                null,
                Array.Empty<QuestRewardBase>(),
                7);

            var canvasGo = new GameObject("QuestChainSourceCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var log = new QuestChainLog(new ScriptableObject[] { chain });
                Assert.IsTrue(log.Accept(chain));
                Assert.IsTrue(log.Track(chain));
                log.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "help-1"));

                var panel = QuestChainLogPanel.EnsureInScene(canvasGo.GetComponent<Canvas>());
                panel.Bind(new[] { chain }, log, null, default);

                var items = BuildItemsFromSource("QuestChainObjectiveSource", null, null);

                Assert.AreEqual(1, items.Length);
                Assert.AreEqual("Quests", items[0].CategoryId);
                Assert.AreEqual("Tracked", items[0].State);
                Assert.IsTrue(items[0].IsTracked);
                StringAssert.Contains("questchain.objective.help 1 / 2", items[0].ProgressText);
                StringAssert.Contains("questchain.objective.help", items[0].NextHintText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(objective);
                UnityEngine.Object.DestroyImmediate(step);
                UnityEngine.Object.DestroyImmediate(chain);
            }
        }

        [Test]
        public void CampaignObjectiveSource_DisplaysCurrentDaySelectedRouteCompletedObjectiveAndNextGuide()
        {
            var visitObjective = ScriptableObject.CreateInstance<CampaignActivityLogObjective>();
            visitObjective.ConfigureForTests("campaign.objective.visit-library", "activity.library", 1, false);
            var talkObjective = ScriptableObject.CreateInstance<CampaignActivityLogObjective>();
            talkObjective.ConfigureForTests("campaign.objective.talk-guide", "npc.guide", 1, false);

            var route = ScriptableObject.CreateInstance<CampaignRouteDefinition>();
            route.ConfigureForTests("campaign.route.library", "Library route", "Use the library route", new CampaignObjectiveBase[] { visitObjective, talkObjective });
            var day = ScriptableObject.CreateInstance<CampaignDayDefinition>();
            day.ConfigureForTests("campaign.day.two", 2, "Day Two Theme", "Choose a route", "Ask the guide after library study", new[] { route });
            var campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
            campaign.ConfigureForTests("campaign.test", "Test Campaign", "Campaign description", new[] { day }, Array.Empty<CampaignRewardBase>());

            var player = new GameObject("CampaignPlayer");
            try
            {
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("campaign-slot", "player", 10, 10, 0, 8 * 60);
                student.Progress.TryStartNextDay();
                var component = player.AddComponent<CampaignProgressComponent>();
                var progress = component.EnsureProgress(campaign, student.Progress);
                progress.SelectRoute(day, route);
                progress.CompleteObjective(visitObjective.Id);

                var items = BuildItemsFromSource("CampaignObjectiveSource", player, null);

                Assert.AreEqual(1, items.Length);
                Assert.AreEqual("Campaign", items[0].CategoryId);
                Assert.AreEqual("Active", items[0].State);
                StringAssert.Contains("Campaign day 2", items[0].ProgressText);
                StringAssert.Contains("Library route", items[0].ProgressText);
                StringAssert.Contains("campaign.objective.visit-library", items[0].ProgressText);
                StringAssert.Contains("Ask the guide after library study", items[0].NextHintText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(visitObjective);
                UnityEngine.Object.DestroyImmediate(talkObjective);
                UnityEngine.Object.DestroyImmediate(route);
                UnityEngine.Object.DestroyImmediate(day);
                UnityEngine.Object.DestroyImmediate(campaign);
            }
        }

        [Test]
        public void CampaignObjectiveSource_DisplaysAvailableRoutesBeforeRouteSelection()
        {
            var visitObjective = ScriptableObject.CreateInstance<CampaignActivityLogObjective>();
            visitObjective.ConfigureForTests("campaign.objective.visit-town", "location.town-square", 1, false);
            var townRoute = ScriptableObject.CreateInstance<CampaignRouteDefinition>();
            townRoute.ConfigureForTests("campaign.route.town", "Town square path", "Visit town square", new CampaignObjectiveBase[] { visitObjective });
            var libraryRoute = ScriptableObject.CreateInstance<CampaignRouteDefinition>();
            libraryRoute.ConfigureForTests("campaign.route.library", "Library path", "Use the library", new CampaignObjectiveBase[] { visitObjective });
            var day = ScriptableObject.CreateInstance<CampaignDayDefinition>();
            day.ConfigureForTests("campaign.day.one", 1, "Town adaptation", "Choose a route", "Find growth", new[] { townRoute, libraryRoute });
            var campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
            campaign.ConfigureForTests("campaign.test", "Test Campaign", "Campaign description", new[] { day }, Array.Empty<CampaignRewardBase>());
            var player = new GameObject("CampaignPlayer");

            try
            {
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("campaign-slot", "player", 10, 10, 0, 8 * 60);
                player.AddComponent<CampaignProgressComponent>().EnsureProgress(campaign, student.Progress);

                var items = BuildItemsFromSource("CampaignObjectiveSource", player, null);

                Assert.AreEqual(1, items.Length);
                StringAssert.Contains("Town square path", items[0].ProgressText);
                StringAssert.Contains("Library path", items[0].ProgressText);
                StringAssert.Contains("Find growth", items[0].NextHintText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(visitObjective);
                UnityEngine.Object.DestroyImmediate(townRoute);
                UnityEngine.Object.DestroyImmediate(libraryRoute);
                UnityEngine.Object.DestroyImmediate(day);
                UnityEngine.Object.DestroyImmediate(campaign);
            }
        }

        [Test]
        public void MilestoneObjectiveSource_DisplaysActiveStateRoutesAndHintsCategory()
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "rootborn-objective-milestone-active-source-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);

            var objective = ScriptableObject.CreateInstance<OutsideSchoolLogMilestoneObjective>();
            objective.ConfigureForTests("milestone.objective.outside", "outside-school", 2);
            var route = ScriptableObject.CreateInstance<MilestoneRouteDefinition>();
            route.ConfigureForTests("milestone.route.outside", objective, "outside.help-neighbor");
            var milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
            milestone.ConfigureForTests("milestone.active", "Active Milestone", new MilestoneObjectiveBase[] { objective }, Array.Empty<MilestoneRewardBase>(), new[] { route }, 1);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var player = new GameObject("MilestonePlayer");

            try
            {
                SetPrivateField(registry, "_milestones", new[] { milestone });
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("milestone-slot", "player", 10, 10, 0, 8 * 60);
                MilestoneProgressPersistence.Save(new MilestoneProgress("milestone-slot", "player", new[] { milestone }));

                var items = BuildItemsFromSource("MilestoneObjectiveSource", player, registry);

                Assert.AreEqual(1, items.Length);
                Assert.AreEqual("Hints", items[0].CategoryId);
                Assert.AreEqual("Active", items[0].State);
                StringAssert.Contains("milestone.objective.outside 0 / 2", items[0].ProgressText);
                StringAssert.Contains("outside.help-neighbor", items[0].NextHintText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(registry);
                UnityEngine.Object.DestroyImmediate(objective);
                UnityEngine.Object.DestroyImmediate(route);
                UnityEngine.Object.DestroyImmediate(milestone);
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        [Test]
        public void MilestoneObjectiveSource_DisplaysRewardClaimedStateAndPerObjectiveProgress()
        {
            string rootDirectory = Path.Combine(Path.GetTempPath(), "rootborn-objective-milestone-source-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);

            var firstObjective = ScriptableObject.CreateInstance<OutsideSchoolLogMilestoneObjective>();
            firstObjective.ConfigureForTests("milestone.objective.study", "skill-study", 3);
            var secondObjective = ScriptableObject.CreateInstance<OutsideSchoolLogMilestoneObjective>();
            secondObjective.ConfigureForTests("milestone.objective.help", "trait-help", 1);
            var milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
            milestone.ConfigureForTests("milestone.claimed", "Claimed Milestone", new MilestoneObjectiveBase[] { firstObjective, secondObjective }, Array.Empty<MilestoneRewardBase>(), Array.Empty<MilestoneRouteDefinition>(), 2);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var player = new GameObject("MilestonePlayer");

            try
            {
                SetPrivateField(registry, "_milestones", new[] { milestone });
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("milestone-slot", "player", 10, 10, 0, 8 * 60);
                var progress = new MilestoneProgress("milestone-slot", "player", new[] { milestone });
                progress.SetObjectiveProgressForTests(milestone, firstObjective, 2);
                progress.SetObjectiveProgressForTests(milestone, secondObjective, 1);
                progress.MarkCompletedForTests(milestone);
                progress.MarkRewardClaimedForTests(milestone);
                MilestoneProgressPersistence.Save(progress);

                var items = BuildItemsFromSource("MilestoneObjectiveSource", player, registry);

                Assert.AreEqual(1, items.Length, "RewardClaimed milestones should remain visible in Objective Journal history.");
                Assert.AreEqual("RewardClaimed", items[0].State);
                StringAssert.Contains("milestone.objective.study 2 / 3", items[0].ProgressText);
                StringAssert.Contains("milestone.objective.help 1 / 1", items[0].ProgressText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(registry);
                UnityEngine.Object.DestroyImmediate(firstObjective);
                UnityEngine.Object.DestroyImmediate(secondObjective);
                UnityEngine.Object.DestroyImmediate(milestone);
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        private static ObjectiveJournalItem[] BuildItemsFromSource(string nestedSourceTypeName, GameObject player, GameDataRegistry registry)
        {
            var sourceType = typeof(ObjectiveJournalRuntimeProvider).GetNestedType(nestedSourceTypeName, BindingFlags.NonPublic);
            Assert.IsNotNull(sourceType, nestedSourceTypeName + " must remain available to build runtime objective items.");
            var source = Activator.CreateInstance(sourceType, true);
            var method = sourceType.GetMethod("BuildItems", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, nestedSourceTypeName + " must expose BuildItems through IObjectiveJournalSource.");
            return (ObjectiveJournalItem[])method.Invoke(source, new object[] { new ObjectiveJournalContext(registry, player, null) });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private sealed class KeyedQuestObjective : QuestObjectiveBase
        {
            private string _requiredKey;

            public void Configure(string displayKey, int requiredCount, string requiredKey)
            {
                ConfigureForRuntime(displayKey, requiredCount);
                _requiredKey = requiredKey;
            }

            public override bool Matches(in QuestEvent questEvent)
            {
                return !string.IsNullOrEmpty(questEvent.EventKey) && questEvent.EventKey.StartsWith(_requiredKey, StringComparison.Ordinal);
            }
        }
    }
}
