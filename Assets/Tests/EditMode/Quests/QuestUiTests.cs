using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestUiTests
    {
        [Test]
        public void QuestLogPanel_BindWithoutQuestLog_DoesNotThrow()
        {
            var go = new GameObject("QuestLogPanel");
            try
            {
                var panel = go.AddComponent<QuestLogPanel>();

                Assert.DoesNotThrow(() => panel.Bind(null));
                Assert.IsNull(panel.QuestLog);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestLogPanel_BindBuildsModernUiQuestStructure()
        {
            var go = new GameObject("QuestLogPanel", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<QuestLogPanel>();
                panel.Bind(new QuestLog(null));

                AssertChildHasTiles(go.transform, "QuestTitleTab");
                AssertChildHasTiles(go.transform, "QuestList");
                AssertChildHasTiles(go.transform, "QuestDetail");
                AssertChildHasTiles(go.transform, "ObjectiveProgress");
                AssertChildHasTiles(go.transform, "RewardRow");
                AssertChildHasTiles(go.transform, "QuestScrollbar");

                var claim = go.transform.Find("ClaimButton");
                Assert.IsNotNull(claim, "Missing ClaimButton child.");
                Assert.IsNotNull(claim.GetComponent<ModernUiTileImage>(), "ClaimButton must use a tiled Modern UI background.");
                Assert.IsNotNull(claim.GetComponent<Button>(), "ClaimButton must expose a Unity Button.");
                Assert.IsNotNull(claim.GetComponent<QuestRewardButton>(), "ClaimButton must preserve QuestRewardButton reward preflight flow.");

                Assert.GreaterOrEqual(go.GetComponentsInChildren<ModernUiTileImage>(true).Sum(tile => tile.TileCount), 80);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestLogPanel_TemporaryQuestTextMode_DisablesGeneratedTileSpriteImages()
        {
            var go = new GameObject("QuestLogPanel", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<QuestLogPanel>();
                panel.Bind(new QuestLog(null));

                var generatedTiles = go.GetComponentsInChildren<Image>(true)
                    .Where(image => image.name.StartsWith("Tile_", StringComparison.Ordinal))
                    .ToArray();

                Assert.Greater(generatedTiles.Length, 0, "QuestLogPanel should still build tiled sprites so this temporary visibility toggle is explicit.");
                Assert.IsTrue(generatedTiles.All(image => !image.enabled), "QuestLogPanel generated tile sprite Images should be disabled while quest text readability is being checked.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestLogPanel_BindWithQuestsBuildsRowsDetailRewardAndClaimBinding()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_id", "GatherWood");
            SetField(quest, "_displayNameKey", "Gather Wood");
            SetField(quest, "_descriptionKey", "Bring wood to the guide.");

            var go = new GameObject("QuestLogPanel", typeof(RectTransform));
            try
            {
                var log = new QuestLog(new[] { quest });
                var panel = go.AddComponent<QuestLogPanel>();
                panel.Bind(log, new[] { quest }, default);

                var row = go.transform.Find("QuestList/QuestRow_GatherWood");
                Assert.IsNotNull(row, "Quest list should contain a row for the quest.");
                Assert.IsNotNull(row.GetComponent<ModernUiTileImage>());
                StringAssert.Contains("Gather Wood", row.GetComponentInChildren<Text>().text);

                var detailText = go.transform.Find("QuestDetail/DetailText")?.GetComponent<Text>();
                Assert.IsNotNull(detailText, "Quest detail should render selected quest text.");
                StringAssert.Contains("Bring wood to the guide.", detailText.text);

                var objectiveText = go.transform.Find("ObjectiveProgress/ObjectiveText")?.GetComponent<Text>();
                Assert.IsNotNull(objectiveText, "Objective progress text is required.");
                StringAssert.Contains("NotStarted", objectiveText.text);

                var rewardText = go.transform.Find("RewardRow/RewardText")?.GetComponent<Text>();
                Assert.IsNotNull(rewardText, "Reward row text is required.");

                var rewardButton = go.transform.Find("ClaimButton")?.GetComponent<QuestRewardButton>();
                Assert.IsNotNull(rewardButton, "Claim button should keep reward transaction preflight binding.");
                Assert.IsFalse(go.transform.Find("ClaimButton").GetComponent<Button>().interactable);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(quest);
            }
        }

        [Test]
        public void DialoguePanel_OpenClose_TracksVisibleState()
        {
            var go = new GameObject("DialoguePanel");
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

        [Test]
        public void DialoguePanel_QuestHudEventSystemSource_DoesNotReturnBeforeRepairingInputModule()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs").Replace("\r\n", "\n");

            StringAssert.DoesNotContain(
                "if (FindComponentInScene<EventSystem>(scene) != null)\n                {\n                    return;\n                }",
                source,
                "Quest HUD must not skip EventSystem repair when another installer created EventSystem without a BaseInputModule; dialogue choice buttons need a real UI input module.");
        }

        [Test]
        public void QuestRewardButton_BindWithoutClaimableQuest_DisablesButton()
        {
            var go = new GameObject("QuestRewardButton");
            try
            {
                var unityButton = go.AddComponent<Button>();
                var rewardButton = go.AddComponent<QuestRewardButton>();
                rewardButton.Bind(null, null, default);

                Assert.IsFalse(unityButton.interactable);
                Assert.IsFalse(rewardButton.Click());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void AssertChildHasTiles(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + " child.");
            var tileImage = child.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tileImage, name + " must use ModernUiTileImage.");
            Assert.Greater(tileImage.TileCount, 0, name + " must build deterministic 16x16 tiles.");
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + " on " + target.GetType().Name);
            field.SetValue(target, value);
        }
    }
}
