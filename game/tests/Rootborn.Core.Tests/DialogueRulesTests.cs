using System;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Dialogue/DialogueQuestTests.cs
    // (NpcDialogueAuthority/Context/Recovery/InteractorSelection/PublishedMeeting 테스트는 NpcInteractor·씬·저장 서비스를 검사하므로 옮기지 않음)
    public sealed class DialogueRulesTests
    {
        private static QuestDefinition TalkQuest(string id, QuestReward[] rewards = null, QuestCondition[] providerConditions = null)
            => new QuestDefinition(id, id, id, new QuestObjective[] { new TalkQuestObjective("talk", 1, "npc.a") }, rewards, null, providerConditions);

        [Test]
        public void QUEST_001_DialogueSession_OpenAndClose()
        {
            var dialogue = new DialogueDefinition("dialogue.a", null, null, null, null);
            var session = new DialogueSession();

            session.Open(dialogue);
            Assert.IsTrue(session.IsOpen);
            Assert.AreSame(dialogue, session.Current);

            session.Close();
            Assert.IsFalse(session.IsOpen);
            Assert.IsNull(session.Current);
        }

        [Test]
        public void QUEST_002_DialogueChoice_AcceptsQuest()
        {
            var quest = TalkQuest("quest.a");
            var choice = new DialogueChoice("choice.accept", DialogueQuestAction.AcceptQuest, quest);
            var log = new QuestLog(new[] { quest });

            Assert.IsTrue(choice.TryExecute(new DialogueChoiceContext(log, default)));
            Assert.AreEqual(QuestStage.Active, log.GetState(quest));
            Assert.IsFalse(choice.IsAvailable(new DialogueChoiceContext(log, default)), "An accepted quest cannot be accepted again.");
            Assert.IsFalse(choice.TryExecute(new DialogueChoiceContext(log, default)));
        }

        [Test]
        public void QUEST_005_DialogueChoice_ClaimsRewardThroughQuestLogPreflight()
        {
            var item = new ItemDefinition("item.a", 99);
            var quest = TalkQuest("quest.a", new QuestReward[] { new ItemQuestReward(item, 1) });
            var log = new QuestLog(new[] { quest });
            var inventory = new ItemInventory();
            var context = new DialogueChoiceContext(log, new RewardRuntimeContext(log, inventory, null, null));
            var choice = new DialogueChoice("choice.claim", DialogueQuestAction.ClaimReward, quest);

            Assert.IsFalse(choice.TryExecute(in context), "Not completed yet.");
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "t1", "npc.a"));

            Assert.IsTrue(choice.TryExecute(in context));
            Assert.AreEqual(1, inventory.CountOf(item));
            Assert.AreEqual(QuestStage.RewardClaimed, log.GetState(quest));
            Assert.IsFalse(choice.TryExecute(in context));
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        [Test]
        public void QUEST_013_NpcDialogueResolvesFromTutorialStageDataWithoutStageBranching()
        {
            var defaultDialogue = new DialogueDefinition("dialogue.guide.day1", new[] { "dialogue.guide.day1" }, null, null, null);
            var day2Dialogue = new DialogueDefinition("dialogue.guide.day2", new[] { "dialogue.guide.day2" }, null, "tutorial.day2", null);
            var npc = new NpcDefinition("npc.guide", null, null, null, null, defaultDialogue, null, new[] { day2Dialogue }, null);
            var progress = new StudentLifeProgress("slot-dialogue", "player-dialogue", 10, 10);

            Assert.AreSame(defaultDialogue, npc.ResolveDialogue(progress));
            progress.SetTutorialStage(new TutorialStage("tutorial.day2", "Day 2 guide text"), "goal.talk-to-guide-day2");
            Assert.AreSame(day2Dialogue, npc.ResolveDialogue(progress));
        }

        [Test]
        public void DIALOGUE_CORE_ConditionsCheckStageRelationshipAndStatus()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            progress.AddRelationship("rel.guide", 3);
            progress.AddStatus("status.fatigue", 5);
            progress.SetTutorialStageForTests("tutorial.day1");

            Assert.IsTrue(new DialogueCondition("tutorial.day1", "rel.guide", 3, "status.fatigue", 5).IsSatisfied(progress));
            Assert.IsFalse(new DialogueCondition("tutorial.day2", null, 0, null, int.MaxValue).IsSatisfied(progress));
            Assert.IsFalse(new DialogueCondition(null, "rel.guide", 4, null, int.MaxValue).IsSatisfied(progress));
            Assert.IsFalse(new DialogueCondition(null, null, 0, "status.fatigue", 4).IsSatisfied(progress));
            Assert.IsTrue(new DialogueCondition(null, null, 0, null, 0).IsSatisfied(progress), "A status limit without a status is ignored.");
            Assert.IsTrue(new DialogueCondition(null, null, 0, null, 0).IsSatisfied(null), "No requirements pass without progress.");
            Assert.IsFalse(new DialogueCondition(null, "rel.guide", 0, null, int.MaxValue).IsSatisfied(null), "Any requirement fails without progress.");

            var gated = new DialogueDefinition("dialogue.gated", null, null, null, new[] { new DialogueCondition(null, "rel.guide", 4, null, int.MaxValue), null });
            Assert.IsFalse(gated.IsAvailable(progress));
            progress.AddRelationship("rel.guide", 1);
            Assert.IsTrue(gated.IsAvailable(progress));
            Assert.IsFalse(new DialogueDefinition("d", null, null, "tutorial.day1", null).IsAvailable(null), "A required stage needs progress.");
        }

        [Test]
        public void DIALOGUE_CORE_NpcDefaultDialogueConditionsAndStageOrder()
        {
            var fallback = new DialogueDefinition("dialogue.default", null, null, null, null);
            var first = new DialogueDefinition("dialogue.first", null, null, "tutorial.a", null);
            var second = new DialogueDefinition("dialogue.second", null, null, "tutorial.a", null);
            var locked = new NpcDefinition("npc.locked", null, null, null, null, fallback,
                new INpcDialogueCondition[] { new DialogueCondition(null, "rel.x", 1, null, int.MaxValue), null }, new[] { null, first, second }, null);
            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);

            Assert.IsNull(locked.ResolveDialogue(progress), "Default dialogue is hidden while its conditions fail.");
            progress.AddRelationship("rel.x", 1);
            Assert.AreSame(fallback, locked.ResolveDialogue(progress));
            progress.SetTutorialStageForTests("tutorial.a");
            Assert.AreSame(first, locked.ResolveDialogue(progress), "The first matching stage dialogue wins.");
            Assert.AreEqual("npc.locked", locked.Id);
            Assert.AreEqual(string.Empty, locked.IntroductionKey, "Introduction falls back to the (empty) display name key.");
        }

        [Test]
        public void DIALOGUE_CORE_ChoiceRejectsMissingQuestOrLogAndUsesQuestConditionsOnAccept()
        {
            var knowledge = new FakeKnowledge();
            var gatedQuest = TalkQuest("quest.gated", providerConditions: new QuestCondition[] { new KnowledgeUnlockedCondition("knowledge.open") });
            var log = new QuestLog(new[] { gatedQuest });
            var accept = new DialogueChoice("choice.accept", DialogueQuestAction.AcceptQuest, gatedQuest);
            var context = new DialogueChoiceContext(log, new RewardRuntimeContext(log, null, knowledge, null));

            Assert.IsTrue(accept.IsAvailable(in context), "The choice shows while the quest is not started.");
            Assert.IsFalse(accept.TryExecute(in context), "Quest conditions still decide the accept.");
            Assert.AreEqual(QuestStage.NotStarted, log.GetState(gatedQuest));
            knowledge.Seed("knowledge.open");
            Assert.IsTrue(accept.TryExecute(in context));

            Assert.IsFalse(new DialogueChoice(null, DialogueQuestAction.AcceptQuest, null).TryExecute(in context));
            Assert.IsFalse(new DialogueChoice(null, DialogueQuestAction.ClaimReward, gatedQuest).IsAvailable(new DialogueChoiceContext(null, default)));
            Assert.IsTrue(new DialogueChoice("choice.close", DialogueQuestAction.Close, null).TryExecute(new DialogueChoiceContext(null, default)));
            Assert.IsTrue(new DialogueChoice("choice.none", DialogueQuestAction.None, null).TryExecute(new DialogueChoiceContext(null, default)));
        }

        [Test]
        public void DIALOGUE_CORE_EvaluateGivesWhetherChoosableAndWhy()
        {
            var knowledge = new FakeKnowledge();
            var gatedQuest = TalkQuest("quest.gated", providerConditions: new QuestCondition[] { new KnowledgeUnlockedCondition("knowledge.open") });
            var log = new QuestLog(new[] { gatedQuest });
            var context = new DialogueChoiceContext(log, new RewardRuntimeContext(log, new ItemInventory(), knowledge, null));
            var accept = new DialogueChoice("choice.accept", DialogueQuestAction.AcceptQuest, gatedQuest);

            var blocked = accept.Evaluate(in context);
            Assert.IsTrue(blocked.Visible, "Unity 판처럼 시작 전 퀘스트의 받기는 보인다.");
            Assert.IsFalse(blocked.CanChoose);
            Assert.AreEqual(DialogueChoiceBlock.QuestConditionsNotMet, blocked.Block);

            knowledge.Seed("knowledge.open");
            var open = accept.Evaluate(in context);
            Assert.IsTrue(open.Visible && open.CanChoose);
            Assert.AreEqual(DialogueChoiceBlock.None, open.Block);

            Assert.IsTrue(accept.TryExecute(in context));
            var taken = accept.Evaluate(in context);
            Assert.IsFalse(taken.Visible || taken.CanChoose);
            Assert.AreEqual(DialogueChoiceBlock.QuestAlreadyStarted, taken.Block);

            var noQuest = new DialogueChoice(null, DialogueQuestAction.AcceptQuest, null).Evaluate(in context);
            Assert.AreEqual(DialogueChoiceBlock.MissingQuest, noQuest.Block);
            Assert.IsFalse(noQuest.Visible);

            var close = new DialogueChoice("choice.close", DialogueQuestAction.Close, null).Evaluate(new DialogueChoiceContext(null, default));
            Assert.IsTrue(close.Visible && close.CanChoose);
        }

        [Test]
        public void DIALOGUE_CORE_EvaluateClaimReasons()
        {
            var item = new ItemDefinition("item.a", 1);
            var quest = TalkQuest("quest.a", new QuestReward[] { new ItemQuestReward(item, 1) });
            var log = new QuestLog(new[] { quest });
            var inventory = new ItemInventory();
            var context = new DialogueChoiceContext(log, new RewardRuntimeContext(log, inventory, null, null));
            var claim = new DialogueChoice("choice.claim", DialogueQuestAction.ClaimReward, quest);

            Assert.AreEqual(DialogueChoiceBlock.QuestNotCompleted, claim.Evaluate(in context).Block);
            log.Accept(quest);
            Assert.AreEqual(DialogueChoiceBlock.QuestNotCompleted, claim.Evaluate(in context).Block);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "t1", "npc.a"));

            for (int i = 0; i < ItemInventory.MaxSlots; i++)
                Assert.IsTrue(inventory.TryAddAll(new[] { new InventoryGrant(new ItemDefinition("filler." + i, 1), 1) }));
            var full = claim.Evaluate(in context);
            Assert.IsFalse(full.Visible || full.CanChoose, "Unity 판처럼 받을 수 없는 보상 받기는 숨긴다.");
            Assert.AreEqual(DialogueChoiceBlock.RewardDoesNotFit, full.Block);

            inventory.LoadFromSaveData(new ItemInventory().ToSaveData(string.Empty), id => null);
            var ready = claim.Evaluate(in context);
            Assert.IsTrue(ready.Visible && ready.CanChoose);
            Assert.IsTrue(claim.TryExecute(in context));
            Assert.AreEqual(DialogueChoiceBlock.RewardAlreadyClaimed, claim.Evaluate(in context).Block);
        }

        private sealed class FakeKnowledge : IQuestKnowledge
        {
            private readonly System.Collections.Generic.HashSet<string> _set = new System.Collections.Generic.HashSet<string>();
            public bool IsUnlocked(string knowledgeId) => knowledgeId != null && _set.Contains(knowledgeId);
            public void Seed(string knowledgeId) => _set.Add(knowledgeId);
        }
    }
}
