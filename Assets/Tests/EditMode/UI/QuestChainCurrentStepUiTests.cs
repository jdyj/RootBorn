using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class QuestChainCurrentStepUiTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void ChainPanel_LoadedSecondStepShowsCurrentObjective()
        {
            var panel = CreateSecondStepPanel();
            StringAssert.Contains("second.objective 1 / 2", panel.VisibleText);
            StringAssert.DoesNotContain("first.objective", panel.VisibleText);
        }

        [Test]
        public void JournalSource_LoadedSecondStepUsesCurrentProgressAndHint()
        {
            CreateSecondStepPanel();
            var items = new QuestChainObjectiveSource().BuildItems(default);
            Assert.AreEqual(1, items.Length);
            StringAssert.Contains("second.objective 1 / 2", items[0].ProgressText);
            StringAssert.Contains("second.objective", items[0].NextHintText);
            StringAssert.DoesNotContain("first.objective", items[0].ProgressText);
        }

        [Test]
        public void JournalSource_HidesDeveloperOnlyChain()
        {
            var panel = CreateSecondStepPanel();
            var chain = panel.Chains[0];
            var so = new UnityEditor.SerializedObject(chain);
            var flag = so.FindProperty("_developerOnly");
            Assert.That(flag, Is.Not.Null, "QuestChainDefinition needs a data flag for developer-only chains.");
            flag.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(0, new QuestChainObjectiveSource().BuildItems(default).Length);
        }

        [Test]
        public void RegistryChains_OnlyTestChainsAreDeveloperOnly()
        {
            var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            int main = 0, test = 0;
            foreach (var chain in registry.QuestChains)
            {
                var flag = new UnityEditor.SerializedObject(chain).FindProperty("_developerOnly");
                Assert.That(flag, Is.Not.Null);
                bool isTest = chain.Id.StartsWith("questchain.test.");
                Assert.AreEqual(isTest, flag.boolValue, chain.Id);
                if (isTest) test++; else main++;
            }
            Assert.That(test, Is.EqualTo(2));
            Assert.That(main, Is.GreaterThan(0));
        }

        [Test]
        public void JournalSource_FinishedStepWaitingForAConditionSaysSo()
        {
            var objective = ScriptableObject.CreateInstance<Rootborn.Game.Quests.Objectives.DailyPlanSelectedQuestObjective>();
            var condition = ScriptableObject.CreateInstance<Rootborn.Game.Quests.Conditions.DayAtLeastCondition>();
            condition.ConfigureForTests(2, "wait.summary");
            var step = Create<QuestStepDefinition>();
            step.ConfigureForTests("wait", "wait", "wait", new QuestObjectiveBase[] { objective }, new QuestConditionBase[] { condition }, null, null, null);
            var next = Step("after", 1);
            var chain = Create<QuestChainDefinition>();
            chain.ConfigureForTests("ui.wait", "ui.wait", "ui.wait.description", "", new ScriptableObject[] { step, next }, null, null, null, null, 0);
            var log = new QuestChainLog(new[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData
            {
                ChainId = chain.Id, ChainState = QuestChainState.Active.ToString(), CurrentStepId = "wait", ObjectiveProgressValues = new[] { 1 }
            } } });
            var go = new GameObject("WaitPanel"); _created.Add(go); _created.Add(objective); _created.Add(condition);
            var panel = go.AddComponent<QuestChainLogPanel>();
            panel.Bind(new[] { chain }, log, null);
            var items = new QuestChainObjectiveSource().BuildItems(default);
            Assert.AreEqual(1, items.Length);
            StringAssert.Contains("wait.summary", items[0].NextHintText);
        }

        [Test]
        public void JournalChainState_IsReadableText_NotAnEnumName()
        {
            var panel = CreateSecondStepPanel();
            var previous = Rootborn.Game.Localization.GameText.Language;
            Rootborn.Game.Common.GameDataRegistryProvider.SetOverrideForTests(UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset"));
            try
            {
                Rootborn.Game.Localization.GameText.Language = "ko";
                var item = new QuestChainObjectiveSource().BuildItems(default)[0];
                Assert.AreEqual("진행 중", item.State);
                StringAssert.StartsWith("다음: ", item.NextHintText);
                Rootborn.Game.Localization.GameText.Language = "en";
                Assert.AreEqual("In progress", new QuestChainObjectiveSource().BuildItems(default)[0].State);
            }
            finally
            {
                Rootborn.Game.Localization.GameText.Language = previous;
                Rootborn.Game.Common.GameDataRegistryProvider.ClearOverrideForTests();
                Rootborn.Game.Common.GameDataRegistryProvider.ResetCacheForTests();
            }
        }

        [Test]
        public void DayResultText_ComposedIdsReadAsNames()
        {
            var previous = Rootborn.Game.Localization.GameText.Language;
            var registry = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Rootborn.Game.Common.GameDataRegistryProvider.SetOverrideForTests(registry);
            var go = new GameObject("DayResult"); _created.Add(go);
            go.SetActive(false);
            var panel = go.AddComponent<Rootborn.UI.StudentLife.StudentDayResultPanel>();
            typeof(Rootborn.UI.StudentLife.StudentDayResultPanel).GetMethod("RefreshDisplayKeys", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, null);
            try
            {
                Rootborn.Game.Localization.GameText.Language = "ko";
                string name = registry.TextCatalog.Get("npc.teacher.name", "ko");
                Assert.AreNotEqual("npc.teacher.name", name);
                foreach (string raw in new[] { "npc.teacher", "npc:npc.teacher:met", "npc.teacher@location.school@time.morning", "item.allowance", "home-entry", "tutorial.day2", "milestone.first-guide-trust active", "npc.teacher.notice.outside-school", "town.help.first-guide-errand" })
                {
                    string shown = panel.ResolveText(raw);
                    StringAssert.DoesNotMatch(@"[a-z]+[.:@-][a-z]", shown, raw + " -> " + shown);
                }
                StringAssert.Contains(name, panel.ResolveText("npc:npc.teacher:met"));
                StringAssert.DoesNotContain("@", panel.ResolveText("npc.teacher@location.school@time.morning"));
            }
            finally
            {
                Rootborn.Game.Localization.GameText.Language = previous;
                Rootborn.Game.Common.GameDataRegistryProvider.ClearOverrideForTests();
                Rootborn.Game.Common.GameDataRegistryProvider.ResetCacheForTests();
            }
        }

        private QuestChainLogPanel CreateSecondStepPanel()
        {
            var first = Step("first", 1);
            var second = Step("second", 2);
            var chain = Create<QuestChainDefinition>();
            chain.ConfigureForTests("ui.multistep", "ui.multistep", "ui.multistep.description", "", new[] { first, second }, null, null, null, null, 0);
            var log = new QuestChainLog(new[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData
            {
                Chains = new[]
                {
                    new QuestChainProgressSaveData
                    {
                        ChainId = chain.Id,
                        ChainState = QuestChainState.Active.ToString(),
                        CurrentStepId = "second",
                        CompletedStepIds = new[] { "first" },
                        ObjectiveProgressValues = new[] { 1 },
                        ProcessedEventKeys = new[] { "first-event", "second-progress" }
                    }
                }
            });
            var canvas = new GameObject("CurrentStepTestCanvas", typeof(RectTransform), typeof(Canvas));
            _created.Add(canvas);
            var panel = QuestChainLogPanel.EnsureInScene(canvas.GetComponent<Canvas>());
            panel.Bind(new[] { chain }, log, null, default);
            return panel;
        }

        private QuestStepDefinition Step(string id, int required)
        {
            var objective = Create<AnyEventObjective>();
            objective.ConfigureForRuntime(id + ".objective", required);
            var step = Create<QuestStepDefinition>();
            step.ConfigureForTests(id, id, id, new QuestObjectiveBase[] { objective }, null, null, null, null);
            return step;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        private sealed class AnyEventObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }
    }
}
