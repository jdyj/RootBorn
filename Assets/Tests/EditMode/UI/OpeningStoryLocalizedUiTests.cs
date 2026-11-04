using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Localization;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class OpeningStoryLocalizedUiTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _language;
        [SetUp] public void SetUp()
        {
            _language = GameText.Language;
            var registry = Own(ScriptableObject.CreateInstance<GameDataRegistry>());
            var catalog = Own(ScriptableObject.CreateInstance<GameTextCatalog>());
            catalog.ConfigureJson("{\"entries\":[{\"key\":\"opening.test.step\",\"ko\":\"두근두근 첫 등교\",\"en\":\"Back to School\"},{\"key\":\"opening.test.detail\",\"ko\":\"선생님을 만나 보자\",\"en\":\"Meet your teacher\"},{\"key\":\"opening.test.objective\",\"ko\":\"하루 계획 선택\",\"en\":\"Choose a daily plan\"},{\"key\":\"opening.test.dialogue\",\"ko\":\"오늘은 무엇을 하고 싶니?\",\"en\":\"What would you like to do today?\"}]}");
            typeof(GameDataRegistry).GetField("_textCatalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, catalog);
            GameDataRegistryProvider.SetOverrideForTests(registry);
        }
        [TearDown] public void TearDown()
        {
            GameDataRegistryProvider.ClearOverrideForTests(); GameDataRegistryProvider.ResetCacheForTests(); GameText.Language = _language;
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [TestCase("ko", "두근두근 첫 등교", "하루 계획 선택")]
        [TestCase("en", "Back to School", "Choose a daily plan")]
        public void ObjectiveJournal_UsesCurrentNamedStepAndLocalizedObjective(string language, string title, string objectiveText)
        {
            GameText.Language = language;
            var objective = Own(ScriptableObject.CreateInstance<LocationActivityQuestObjective>());
            objective.ConfigureForRuntime("opening.test.objective", 1);
            var step = Own(ScriptableObject.CreateInstance<QuestStepDefinition>());
            step.ConfigureForTests("step", "opening.test.step", "opening.test.detail", new QuestObjectiveBase[] { objective }, null, null, null, null);
            var chain = Own(ScriptableObject.CreateInstance<QuestChainDefinition>());
            chain.ConfigureForTests("chain", "Whole childhood", "Whole childhood", null, new[] { step }, null, null, null, null, 0);
            var log = new QuestChainLog(new[] { chain }); log.Accept(chain);
            var canvas = Own(new GameObject("OpeningJournalCanvas", typeof(RectTransform), typeof(Canvas)));
            var panel = QuestChainLogPanel.EnsureInScene(canvas.GetComponent<Canvas>());
            panel.Bind(new[] { chain }, log, null, default);
            var items = new QuestChainObjectiveSource().BuildItems(default);
            Assert.That(items.Length, Is.EqualTo(1));
            Assert.That(items[0].Title, Is.EqualTo(title));
            StringAssert.Contains(objectiveText, items[0].ProgressText);
            StringAssert.Contains(objectiveText, items[0].NextHintText);
        }

        [TestCase("ko", "오늘은 무엇을 하고 싶니?")]
        [TestCase("en", "What would you like to do today?")]
        public void DialoguePanel_RendersAuthoredLocalizedProse_AndPreservesLiteralFallback(string language, string expected)
        {
            GameText.Language = language;
            var dialogue = Own(ScriptableObject.CreateInstance<DialogueDefinition>());
            dialogue.ConfigureForTests(new[] { "opening.test.dialogue", "Literal fallback" }, null, null);
            var canvas = Own(new GameObject("OpeningDialogueCanvas", typeof(RectTransform), typeof(Canvas)));
            var go = new GameObject("OpeningDialogue", typeof(RectTransform)); go.transform.SetParent(canvas.transform, false);
            var panel = go.AddComponent<DialoguePanel>(); panel.Open(dialogue, default);
            var line = (Text)typeof(DialoguePanel).GetField("_lineText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            StringAssert.Contains(expected, line.text);
            StringAssert.Contains("Literal fallback", line.text);
            StringAssert.DoesNotContain("opening.test.dialogue", line.text);
        }
        private T Own<T>(T value) where T : Object { _objects.Add(value); return value; }
    }
}
