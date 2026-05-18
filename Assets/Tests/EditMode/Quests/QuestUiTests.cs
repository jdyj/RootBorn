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
        public void QuestLogPanel_BindBuildsSingleCommonPanelWindowWithPlainContentInside()
        {
            var go = new GameObject("QuestLogPanel", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<QuestLogPanel>();
                panel.Bind(new QuestLog(null));

                AssertChildHasTiles(go.transform, "QuestWindow");
                Assert.AreEqual(1, CountCommonPanel48(go), "Quest UI should use exactly one CommonPanel48: the outer QuestWindow.");
                Assert.IsNull(go.transform.Find("QuestList"), "QuestList must not be a loose top-level panel.");
                Assert.IsNull(go.transform.Find("QuestDetail"), "QuestDetail must not be a loose top-level panel.");

                var window = go.transform.Find("QuestWindow");
                AssertPlainChild(window, "QuestTitleTab");
                AssertPlainChild(window, "QuestList");
                AssertPlainChild(window, "QuestDetail");
                AssertPlainChild(window, "ObjectiveProgress");
                AssertPlainChild(window, "RewardRow");
                AssertPlainChild(window, "QuestScrollbar");

                var claim = window.Find("ClaimButton");
                Assert.IsNotNull(claim, "Missing ClaimButton child inside QuestWindow.");
                Assert.IsNull(claim.GetComponent<ModernUiTileImage>(), "ClaimButton must not reuse CommonPanel art.");
                Assert.IsNotNull(claim.GetComponent<Button>(), "ClaimButton must expose a Unity Button.");
                Assert.IsNotNull(claim.GetComponent<QuestRewardButton>(), "ClaimButton must preserve QuestRewardButton reward preflight flow.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestLogPanel_CommonPanelTilesRemainVisibleForOuterWindowOnly()
        {
            var go = new GameObject("QuestLogPanel", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<QuestLogPanel>();
                panel.Bind(new QuestLog(null));

                var generatedTiles = go.GetComponentsInChildren<Image>(true)
                    .Where(image => image.name.StartsWith("Tile_", StringComparison.Ordinal))
                    .ToArray();

                Assert.Greater(generatedTiles.Length, 0, "QuestLogPanel should build tiled panel sprite Images.");
                Assert.IsTrue(generatedTiles.All(image => image.enabled), "QuestLogPanel generated tile sprite Images must stay visible for CommonPanel art.");
                Assert.AreEqual(1, CountCommonPanel48(go), "Only QuestWindow should use the sliced 48px CommonPanel PNG recipe.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestLogPanel_BindWithQuestsBuildsRowsDetailRewardAndClaimBindingInsideWindow()
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

                var window = go.transform.Find("QuestWindow");
                Assert.IsNotNull(window, "Quest content must be nested under the reusable QuestWindow panel.");
                Assert.AreEqual(1, CountCommonPanel48(go), "Quest rows and inner sections must not reuse CommonPanel48.");

                var row = window.Find("QuestList/QuestRow_GatherWood");
                Assert.IsNotNull(row, "Quest list should contain a row for the quest.");
                Assert.IsNull(row.GetComponent<ModernUiTileImage>(), "Quest row should be plain content inside QuestWindow, not another CommonPanel.");
                StringAssert.Contains("Gather Wood", row.GetComponentInChildren<Text>().text);

                var detailText = window.Find("QuestDetail/DetailText")?.GetComponent<Text>();
                Assert.IsNotNull(detailText, "Quest detail should render selected quest text.");
                StringAssert.Contains("Bring wood to the guide.", detailText.text);

                var objectiveText = window.Find("ObjectiveProgress/ObjectiveText")?.GetComponent<Text>();
                Assert.IsNotNull(objectiveText, "Objective progress text is required.");
                StringAssert.Contains("NotStarted", objectiveText.text);

                var rewardText = window.Find("RewardRow/RewardText")?.GetComponent<Text>();
                Assert.IsNotNull(rewardText, "Reward row text is required.");

                var rewardButton = window.Find("ClaimButton")?.GetComponent<QuestRewardButton>();
                Assert.IsNotNull(rewardButton, "Claim button should keep reward transaction preflight binding.");
                Assert.IsFalse(window.Find("ClaimButton").GetComponent<Button>().interactable);
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

        private static int CountCommonPanel48(GameObject root)
        {
            return root.GetComponentsInChildren<ModernUiTileImage>(true)
                .Count(tile => tile.Recipe == ModernUiRecipes.CommonPanel48);
        }

        private static void AssertChildHasTiles(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + " child.");
            var tileImage = child.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tileImage, name + " must use ModernUiTileImage.");
            Assert.AreSame(ModernUiRecipes.CommonPanel48, tileImage.Recipe, name + " must use the sliced 48px CommonPanel PNG recipe.");
            Assert.Greater(tileImage.TileCount, 0, name + " must build deterministic CommonPanel tiles.");
            Assert.AreEqual(new Vector2(48f, 48f), tileImage.TileSize, name + " should use the 48px CommonPanel tile scale.");
        }

        private static void AssertPlainChild(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + " child.");
            Assert.IsNull(child.GetComponent<ModernUiTileImage>(), name + " must be plain content inside the outer CommonPanel, not another CommonPanel.");
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + " on " + target.GetType().Name);
            field.SetValue(target, value);
        }
    }
}
