using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.UI.Quests;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class QuestChainOwnedPanelCommandTests
    {
        [TestCase("AcceptSelected", QuestChainState.Available)]
        [TestCase("TrackSelected", QuestChainState.Active)]
        [TestCase("ClaimSelectedReward", QuestChainState.Completed)]
        public void PanelCommand_UsesAggregateExecutor_AndRejectedCommitDoesNotMutateLiveLog(string commandName, QuestChainState initial)
        {
            var bind = typeof(QuestChainLogPanel).GetMethod("BindCommandExecutor", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(bind, Is.Not.Null, "Player-facing commands must run on a detached aggregate before publishing live state.");
            var canvas = new GameObject("OwnedPanel", typeof(RectTransform), typeof(Canvas));
            var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
            var step = ScriptableObject.CreateInstance<QuestStepDefinition>();
            try
            {
                step.ConfigureForTests("owned.step", "step", "step", null, null, null, null, null);
                chain.ConfigureForTests("owned.chain", "chain", "chain", null, new ScriptableObject[] { step }, null, null, null, null, 0);
                var log = new QuestChainLog(new[] { chain });
                if (initial != QuestChainState.Available) log.Accept(chain);
                if (initial == QuestChainState.Completed)
                {
                    var saved = log.ToSaveData();
                    saved.Chains[0].ChainState = QuestChainState.Completed.ToString();
                    saved.Chains[0].CompletedStepIds = new[] { step.Id };
                    log.LoadFromSaveData(saved);
                }
                Assert.That(log.GetState(chain), Is.EqualTo(initial));
                var panel = QuestChainLogPanel.EnsureInScene(canvas.GetComponent<Canvas>());
                panel.Bind(new[] { chain }, log, null, default);
                int executed = 0;
                Func<Func<QuestChainLog, RewardRuntimeContext, bool>, bool> execute = operation => { executed++; return false; };
                bind.Invoke(panel, new object[] { execute });
                typeof(QuestChainLogPanel).GetMethod(commandName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);
                Assert.That(executed, Is.EqualTo(1));
                Assert.That(log.GetState(chain), Is.EqualTo(initial));
                Assert.That(log.IsTracked(chain), Is.False);
                Assert.That(log.IsRewardClaimed(chain, "completion"), Is.False);
            }
            finally { Object.DestroyImmediate(canvas); Object.DestroyImmediate(chain); Object.DestroyImmediate(step); }
        }
    }
}
