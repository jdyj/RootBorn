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
