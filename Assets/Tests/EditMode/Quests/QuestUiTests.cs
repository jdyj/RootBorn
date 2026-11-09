using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestUiTests
    {
        [Test]
        public void DialoguePanel_OpenClose_TracksVisibleState()
        {
            // The dialogue panel is a UI object; it always has a RectTransform in the game.
                var go = new GameObject("DialoguePanel", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<DialoguePanel>();

                panel.Open(null, default);
                Assert.IsTrue(panel.IsOpen);
                Assert.IsTrue(go.activeSelf);
                panel.Close();
                Assert.IsFalse(panel.IsOpen);
                Assert.IsFalse(go.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DialoguePanel_OpenBuildsCloseAndChoiceButtons()
        {
            var go = new GameObject("DialoguePanel", typeof(RectTransform));
            var dialogue = ScriptableObject.CreateInstance<Rootborn.Game.Dialogue.DialogueDefinition>();
            var choice = ScriptableObject.CreateInstance<Rootborn.Game.Dialogue.DialogueChoiceDefinition>();
            try
            {
                SetField(choice, "_labelKey", "dialogue.choice.close");
                SetField(choice, "_questAction", Rootborn.Game.Dialogue.DialogueQuestAction.Close);
                SetField(dialogue, "_choices", new[] { choice });

                var panel = go.AddComponent<DialoguePanel>();
                panel.Open(dialogue, default);

                Assert.IsNotNull(go.transform.Find("CloseButton")?.GetComponent<Button>(), "DialoguePanel should expose a clickable CloseButton.");
                Assert.IsNotNull(go.transform.Find("ChoiceButtons/ChoiceButton_0")?.GetComponent<Button>(), "DialoguePanel should expose real clickable choice buttons.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(dialogue);
                UnityEngine.Object.DestroyImmediate(choice);
            }
        }

        [Test]
        public void DialoguePanel_OpenRendersDialogueLineKeys()
        {
            var go = new GameObject("DialoguePanel", typeof(RectTransform));
            var dialogue = ScriptableObject.CreateInstance<Rootborn.Game.Dialogue.DialogueDefinition>();
            try
            {
                SetField(dialogue, "_lineKeys", new[] { "dialogue.guide.day2" });

                var panel = go.AddComponent<DialoguePanel>();
                panel.Open(dialogue, default);

                var texts = go.GetComponentsInChildren<Text>(true).Select(text => text.text).ToArray();
                Assert.IsTrue(texts.Any(text => text.Contains("dialogue.guide.day2")), "DialoguePanel must show stage-selected NPC dialogue text.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(dialogue);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + " on " + target.GetType().Name);
            field.SetValue(target, value);
        }
    }
}
