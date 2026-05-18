using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Save;
using Rootborn.Game.WorldState;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class WorldStateQuestRewardButtonTests
    {
        [Test]
        public void WORLD_STATE_EDIT_006_008_QuestRewardButtonPersistsWorldStateRewardOnce()
        {
            var root = Path.Combine(Application.temporaryCachePath, "rootborn-world-state-reward-button-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SaveService.SetRootDirectoryForTests(root);
            var buttonGo = new GameObject("QuestRewardButton", typeof(Button), typeof(QuestRewardButton));
            try
            {
                var metadata = new SaveService("slot-0", root).CreateMetadata("slot-0", new CharacterCustomization(), 1, 2);
                ActiveSaveContext.Set(metadata);

                var flag = MakeFlag("world.library.archive-open");
                var reward = ScriptableObject.CreateInstance<WorldStateChangeQuestReward>();
                reward.ConfigureForTests(new[] { flag }, "chain.library", "step.archive", "event.archive", 2);
                var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
                objective.ConfigureForRuntime("objective.any", 1);
                var quest = ScriptableObject.CreateInstance<QuestDefinition>();
                quest.ConfigureForRuntime("quest.world-state", "quest.world-state", "quest.world-state.desc", new[] { objective }, new QuestRewardBase[] { reward }, null);
                var questLog = new QuestLog(new[] { quest });
                questLog.Accept(quest);
                questLog.RecordEvent(new QuestEvent(QuestEventKind.Talk, "event.archive", 1));
                var worldState = new WorldStateProgress("slot-0", "player-a");
                var context = new RewardRuntimeContext(questLog, null, null, null, null, worldState);

                var rewardButton = buttonGo.GetComponent<QuestRewardButton>();
                rewardButton.Bind(questLog, quest, context);

                Assert.IsTrue(rewardButton.Click());
                Assert.IsFalse(rewardButton.Click(), "WORLD-STATE-EDIT-008 failed: duplicate reward click should be rejected.");
                var loaded = WorldStateProgressPersistence.LoadOrCreate("slot-0", "player-a");

                Assert.IsTrue(loaded.IsActive(flag), "WORLD-STATE-EDIT-006 failed: world-state reward claim must persist activated flag.");
                Assert.AreEqual(1, loaded.ActiveFlagCount, "WORLD-STATE-EDIT-008 failed: repeated click must not duplicate world-state changes.");
            }
            finally
            {
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                UnityEngine.Object.DestroyImmediate(buttonGo);
                Directory.Delete(root, true);
            }
        }

        private static WorldStateFlagDefinition MakeFlag(string id)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(
                id,
                id + ".name",
                id + ".desc",
                null,
                null,
                null,
                WorldStateScopeKind.Shared,
                WorldStateChangeKind.LocationUnlocked,
                id + ".next",
                new[] { WorldStateSummarySurface.DayResult, WorldStateSummarySurface.WorldLog },
                new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared },
                1,
                1);
            return flag;
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }
    }
}
