using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class QuestChainLogPanelTests
    {
        [Test]
        public void QUEST_CHAIN_EDIT_009_LogPanelFiltersByCareerAndStateAndShowsBlockedSummary()
        {
            var canvasGo = new GameObject("quest-chain-log-canvas", typeof(Canvas));
            try
            {
                var learning = CreateChain("chain.learning", "career.learning", CreateAnyEventObjective(1));
                var service = CreateChain("chain.service", "career.service", CreateAnyEventObjective(1));
                var log = new QuestChainLog(new ScriptableObject[] { learning, service });
                log.EvaluateAvailability(learning, default);
                log.EvaluateAvailability(service, default);
                log.Accept(learning);
                log.Accept(service);
                log.Block(learning, "missing.npc", "Talk to the librarian after opening the library.");

                var panel = QuestChainLogPanel.EnsureInScene(canvasGo.GetComponent<Canvas>());
                panel.Show(new[] { learning, service }, log);
                panel.SelectCareerFilter("career.learning");
                panel.SelectStateFilter(QuestChainState.Blocked);

                StringAssert.Contains("chain.learning", panel.VisibleText);
                StringAssert.Contains("Blocked", panel.VisibleText);
                StringAssert.Contains("missing.npc", panel.VisibleText);
                StringAssert.Contains("Talk to the librarian", panel.VisibleText);
                StringAssert.DoesNotContain("chain.service", panel.VisibleText);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void QUEST_CHAIN_EDIT_010_HudWidgetShowsOnlyOneTrackedChainCurrentObjective()
        {
            var canvasGo = new GameObject("quest-chain-hud-canvas", typeof(Canvas));
            try
            {
                var learning = CreateChain("chain.learning", "career.learning", CreateAnyEventObjective(2));
                var service = CreateChain("chain.service", "career.service", CreateAnyEventObjective(1));
                var log = new QuestChainLog(new ScriptableObject[] { learning, service });
                log.EvaluateAvailability(learning, default);
                log.EvaluateAvailability(service, default);
                log.Accept(learning);
                log.Track(learning);
                log.Accept(service);

                var hud = QuestChainHudWidget.EnsureInScene(canvasGo.GetComponent<Canvas>());
                hud.Refresh(new[] { learning, service }, log);

                StringAssert.Contains("chain.learning", hud.VisibleText);
                StringAssert.Contains("objective.any", hud.VisibleText);
                StringAssert.Contains("0 / 2", hud.VisibleText);
                StringAssert.DoesNotContain("chain.service", hud.VisibleText);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void QUEST_CHAIN_EDIT_011_LogPanelClaimsCompletedChainRewardThroughButtonOnce()
        {
            var canvasGo = new GameObject("quest-chain-reward-canvas", typeof(Canvas));
            var reward = ScriptableObject.CreateInstance<CountingQuestChainReward>();
            try
            {
                var chain = CreateChain("chain.reward", "career.learning", CreateAnyEventObjective(1), reward);
                var log = new QuestChainLog(new ScriptableObject[] { chain });
                log.EvaluateAvailability(chain, default);
                log.Accept(chain);
                log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "finish-chain", 1));

                var panel = QuestChainLogPanel.EnsureInScene(canvasGo.GetComponent<Canvas>());
                panel.Bind(new[] { chain }, log, null, default);

                var button = canvasGo.GetComponentInChildren<Button>(true, "QuestChainClaimRewardButton");
                Assert.IsNotNull(button, "QUEST-CHAIN-EDIT-011 failed: QuestChainLogPanel must expose a claim reward button for completed chain rewards.");
                Assert.IsTrue(button.interactable, "QUEST-CHAIN-EDIT-011 failed: completed unclaimed chain reward button should be interactable.");

                button.onClick.Invoke();
                button.onClick.Invoke();

                Assert.AreEqual(1, reward.ApplyCount, "QUEST-CHAIN-EDIT-011 failed: reward should be applied exactly once through the UI button.");
                Assert.IsTrue(log.IsRewardClaimed(chain, "completion"));
                Assert.IsFalse(button.interactable, "QUEST-CHAIN-EDIT-011 failed: claimed reward button should disable after refresh.");
            }
            finally
            {
                Object.DestroyImmediate(reward);
                Object.DestroyImmediate(canvasGo);
            }
        }

        private static QuestChainDefinition CreateChain(string id, string careerInterestId, QuestObjectiveBase objective, QuestRewardBase reward = null)
        {
            var step = ScriptableObject.CreateInstance<QuestStepDefinition>();
            step.ConfigureForTests("step." + id, "step." + id, "step." + id + ".desc", new[] { objective }, null, null, null, null);
            var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
            var rewards = reward != null ? new[] { reward } : null;
            chain.ConfigureForTests(id, id, id + ".desc", careerInterestId, new ScriptableObject[] { step }, null, null, null, rewards, 0);
            return chain;
        }

        private static QuestObjectiveBase CreateAnyEventObjective(int requiredCount)
        {
            var objective = ScriptableObject.CreateInstance<AnyQuestEventObjective>();
            objective.ConfigureForRuntime("objective.any", requiredCount);
            return objective;
        }

        private sealed class AnyQuestEventObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private sealed class CountingQuestChainReward : QuestRewardBase
        {
            public int ApplyCount { get; private set; }

            public override bool CanApply(in RewardRuntimeContext context) => true;

            public override void Apply(in RewardRuntimeContext context)
            {
                ApplyCount++;
            }
        }
    }

    internal static class QuestChainLogPanelTestExtensions
    {
        public static T GetComponentInChildren<T>(this GameObject gameObject, bool includeInactive, string objectName) where T : Component
        {
            var components = gameObject.GetComponentsInChildren<T>(includeInactive);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].name == objectName) return components[i];
            }

            return null;
        }
    }
}
