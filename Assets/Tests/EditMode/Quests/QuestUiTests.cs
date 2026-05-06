using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.UI.Quests;
using UnityEngine;
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
                Object.DestroyImmediate(go);
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
                Object.DestroyImmediate(go);
            }
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
                Object.DestroyImmediate(go);
            }
        }
    }
}
