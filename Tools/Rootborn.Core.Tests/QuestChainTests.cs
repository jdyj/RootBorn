using System;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/QuestChainStateTests.cs, Assets/Tests/EditMode/StudentLife/QuestChainBranchingTests.cs,
    // Assets/Tests/EditMode/Quests/QuestRewardTests.cs (QUEST_CHAIN_EDIT_008), Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs (QUEST_ID_009)
    public sealed class QuestChainTests
    {
        private static QuestStepDefinition Step(string id, QuestObjective objective = null, QuestCondition[] completion = null, QuestCondition[] block = null,
            QuestReward[] rewards = null, QuestChainTransition[] transitions = null, string[] legacyIds = null)
            => new QuestStepDefinition(id, id, id + ".description", new[] { objective ?? new AnyEventObjective(1, id + ".objective") }, completion, block, rewards, transitions, legacyIds);

        private static QuestChainDefinition Chain(params QuestStepDefinition[] steps)
            => new QuestChainDefinition("branching-test", "branching-test", "branching-test.description", "", steps);

        private static QuestChainDefinition SingleStepChain(string id, QuestObjective objective, QuestCondition[] block = null, QuestCondition[] unblock = null,
            QuestCondition[] fail = null, QuestCondition[] expire = null, QuestReward[] completionRewards = null)
            => new QuestChainDefinition(id, id, id + ".desc", "career.learning",
                new[] { new QuestStepDefinition("step." + id, "step." + id, "step." + id + ".desc", new[] { objective }) },
                null, unblock, block, completionRewards, 0, fail, expire);

        private static QuestChainTransition Transition(string targetStepId, bool terminal, params QuestCondition[] conditions)
            => new ConditionalQuestChainTransition("transition." + (targetStepId ?? "terminal"), targetStepId, terminal, conditions);

        private static QuestChainLog Log(QuestChainDefinition chain)
        {
            var log = new QuestChainLog(new[] { chain });
            Assert.That(log.Accept(chain), Is.True);
            return log;
        }

        private static QuestChainProgressSaveData Saved(QuestChainLog log) => log.ToSaveData().Chains[0];
        private static QuestEvent Event(string key) => new QuestEvent(QuestEventKind.Talk, key);
        private static QuestRuntimeContext Context(IQuestStoryFlags flags) => new QuestRuntimeContext(null, null, null, flags);
        private static RewardRuntimeContext Rewards(ItemInventory inventory) => new RewardRuntimeContext(null, inventory, null, null);
        private static ItemDefinition Item(int maxStack = 1) => new ItemDefinition("item." + Guid.NewGuid().ToString("N"), maxStack);

        // ---- QuestChainStateTests ----

        [Test]
        public void QUEST_CHAIN_EDIT_004_SameEventProgressesMultipleActiveChainsIndependently()
        {
            var first = SingleStepChain("chain.learning", new LocationActivityQuestObjective("objective.location", 1, "activity.library.study"));
            var second = SingleStepChain("chain.service", new LocationActivityQuestObjective("objective.location", 2, "activity.library.study"));
            var log = new QuestChainLog(new[] { first, second });
            Assert.That(log.EvaluateAvailability(first, default), Is.True);
            Assert.That(log.EvaluateAvailability(second, default), Is.True);
            Assert.That(log.Accept(first), Is.True);
            Assert.That(log.Accept(second), Is.True);

            log.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "shared-activity-1", "activity.library.study"));

            Assert.That(log.GetState(first), Is.EqualTo(QuestChainState.Completed), "first chain should complete from the shared event.");
            Assert.That(log.GetState(second), Is.EqualTo(QuestChainState.Active), "second chain should remain active until its own required count is met.");
            Assert.That(log.GetObjectiveProgress(second, 0), Is.EqualTo(1), "second chain objective progress must be stored independently.");
        }

        [Test]
        public void QUEST_CHAIN_EDIT_005_TrackingAnotherChainPausesPreviousWithoutDeletingProgress()
        {
            var first = SingleStepChain("chain.learning", new AnyEventObjective(2));
            var second = SingleStepChain("chain.service", new AnyEventObjective(1));
            var log = new QuestChainLog(new[] { first, second });
            Assert.That(log.Accept(first), Is.True);
            Assert.That(log.Track(first), Is.True);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "first-progress"));
            Assert.That(log.GetObjectiveProgress(first, 0), Is.EqualTo(1));

            Assert.That(log.Accept(second), Is.True);
            Assert.That(log.Track(second), Is.True);

            Assert.That(log.GetState(first), Is.EqualTo(QuestChainState.Paused), "tracking another chain should pause the previous tracked chain.");
            Assert.That(log.GetObjectiveProgress(first, 0), Is.EqualTo(1), "paused chain progress should remain intact.");
            Assert.That(log.GetState(second), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(log.IsTracked(first), Is.False);
        }

        [Test]
        public void QUEST_CHAIN_EDIT_006_BlockedStateStoresReasonAndUnblockSummary()
        {
            var chain = SingleStepChain("chain.blocked", new AnyEventObjective(1));
            var log = Log(chain);
            Assert.That(log.Block(chain, "missing.npc", "Talk to the librarian after opening the library."), Is.True);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Blocked));
            Assert.That(log.GetBlockedReasonId(chain), Is.EqualTo("missing.npc"));
            Assert.That(log.GetBlockedSummary(chain), Is.EqualTo("Talk to the librarian after opening the library."));
            Assert.That(log.Unblock(chain), Is.True);
            Assert.That(log.GetBlockedReasonId(chain), Is.Empty);
            Assert.That(log.Unblock(chain), Is.False, "only a blocked chain unblocks");
        }

        [Test]
        public void QUEST_CHAIN_EDIT_006_BlockConditionsPauseProgressUntilUnblockConditionsAreSatisfied()
        {
            var flags = new FakeStoryFlags();
            var condition = new BlockingFlagCondition("flag.library", "missing.library.access", "Visit the library desk to reopen this chain.");
            var chain = SingleStepChain("chain.blocked-by-condition", new AnyEventObjective(1), new QuestCondition[] { condition }, new QuestCondition[] { condition });
            var log = Log(chain);

            Assert.That(log.EvaluateBlockState(chain, Context(flags)), Is.True);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Blocked), "missing condition should block the active chain.");
            Assert.That(log.GetBlockedReasonId(chain), Is.EqualTo("missing.library.access"));
            Assert.That(log.GetBlockedSummary(chain), Does.StartWith("Visit the library desk"));
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "ignored-while-blocked"));
            Assert.That(log.GetObjectiveProgress(chain, 0), Is.Zero, "blocked chain must not progress until reopened.");

            flags.Set("flag.library");
            Assert.That(log.EvaluateBlockState(chain, Context(flags)), Is.True);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active), "satisfying unblock conditions should resume the chain.");
        }

        [Test]
        public void QUEST_CHAIN_EDIT_003_FailedAndExpiredConditionsAreDataDrivenTerminalStates()
        {
            var failedCondition = new SwitchCondition("failed.low-trust", "Trust dropped below the route threshold.");
            var expiredCondition = new SwitchCondition("expired.deadline", "The deadline passed before the next step was completed.");
            var failedChain = SingleStepChain("chain.failed", new AnyEventObjective(1), fail: new QuestCondition[] { failedCondition });
            var expiredChain = SingleStepChain("chain.expired", new AnyEventObjective(1), expire: new QuestCondition[] { expiredCondition });
            var log = new QuestChainLog(new[] { failedChain, expiredChain });
            Assert.That(log.Accept(failedChain), Is.True);
            Assert.That(log.Accept(expiredChain), Is.True);

            failedCondition.IsOn = true;
            expiredCondition.IsOn = true;
            var student = new StudentLifeProgress("slot", "player", 10, 10);
            Assert.That(log.EvaluateTerminalState(failedChain, new QuestRuntimeContext(null, null, null, null, student)), Is.True);
            Assert.That(log.EvaluateTerminalState(expiredChain, default), Is.True);

            Assert.That(log.GetState(failedChain), Is.EqualTo(QuestChainState.Failed));
            Assert.That(log.GetBlockedReasonId(failedChain), Is.EqualTo("failed.low-trust"));
            Assert.That(log.GetState(expiredChain), Is.EqualTo(QuestChainState.Expired));
            Assert.That(log.GetBlockedReasonId(expiredChain), Is.EqualTo("expired.deadline"));
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "ignored-terminal"));
            Assert.That(log.GetObjectiveProgress(failedChain, 0), Is.Zero);
            Assert.That(log.GetObjectiveProgress(expiredChain, 0), Is.Zero);
            var saved = log.ToSaveData();
            Assert.That(saved.Chains[0].FailedDayIndex, Is.EqualTo(1), "the student's current day");
            Assert.That(saved.Chains[1].ExpiredDayIndex, Is.EqualTo(-1), "no student, no day");
            Assert.That(log.EvaluateTerminalState(failedChain, default), Is.False, "terminal states stay");
        }

        [Test]
        public void QUEST_CHAIN_EDIT_007_SaveLoadRestoresStateStepProgressTrackingBlockAndRewardFlags()
        {
            var chain = SingleStepChain("chain.persist", new AnyEventObjective(2));
            var log = Log(chain);
            Assert.That(log.Track(chain), Is.True);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "persist-progress"));
            Assert.That(log.Block(chain, "time.mismatch", "Come back in the afternoon."), Is.True);
            Assert.That(log.MarkRewardClaimed(chain, "reward.learning.badge"), Is.True);
            Assert.That(log.MarkRewardClaimed(chain, "reward.learning.badge"), Is.False);

            var restored = new QuestChainLog(new[] { chain });
            restored.LoadFromSaveData(log.ToSaveData());

            Assert.That(restored.GetState(chain), Is.EqualTo(QuestChainState.Blocked));
            Assert.That(restored.GetObjectiveProgress(chain, 0), Is.EqualTo(1));
            Assert.That(restored.GetBlockedReasonId(chain), Is.EqualTo("time.mismatch"));
            Assert.That(restored.GetBlockedSummary(chain), Is.EqualTo("Come back in the afternoon."));
            Assert.That(restored.IsTracked(chain), Is.True);
            Assert.That(restored.IsRewardClaimed(chain, "reward.learning.badge"), Is.True);
        }

        [Test]
        public void QUEST_CHAIN_EDIT_010_EquivalentDefinitionWithTheSameIdFindsTheSameProgress()
        {
            var chain = SingleStepChain("chain.cached", new AnyEventObjective(1));
            var equivalent = SingleStepChain("chain.cached", new AnyEventObjective(1));
            var log = Log(chain);
            Assert.That(log.GetState(equivalent), Is.EqualTo(QuestChainState.Active), "equivalent chain definitions resolve through the id cache.");
            Assert.That(log.GetState(SingleStepChain("chain.other", new AnyEventObjective(1))), Is.EqualTo(QuestChainState.Available), "an unknown chain reads as available");
            Assert.That(log.GetState(null), Is.EqualTo(QuestChainState.Available));
        }

        // ---- QuestChainBranchingTests ----

        [Test]
        public void FirstStepCompletion_AdvancesToSecondStepWithoutCompletingChain()
        {
            var chain = Chain(Step("first"), Step("second"));
            var log = Log(chain);
            log.RecordEvent(Event("first-event"));
            var saved = Saved(log);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(saved.CurrentStepId, Is.EqualTo("second"));
            CollectionAssert.AreEqual(new[] { "first" }, saved.CompletedStepIds);
            Assert.That(log.GetObjectiveProgress(chain, 0), Is.Zero);
            log.RecordEvent(Event("second-event"));
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Completed));
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, Saved(log).CompletedStepIds);
            CollectionAssert.AreEqual(new[] { "first->second", "second->completed" }, Saved(log).TransitionHistory);
            Assert.That(Saved(log).TodayQuestChainSummary, Is.EqualTo("second"));
        }

        [Test]
        public void ExistingSingleStepChain_CompletesWithOriginalApi()
        {
            var chain = Chain(Step("only"));
            var log = Log(chain);
            log.RecordEvent(Event("complete"));
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Completed));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("only"));
        }

        [Test]
        public void SaveReloadAfterTransition_DuplicateEventCannotCreditNextStep()
        {
            var chain = Chain(Step("first"), Step("second"));
            var log = Log(chain);
            log.RecordEvent(Event("same-event"));
            var restored = new QuestChainLog(new[] { chain });
            restored.LoadFromSaveData(log.ToSaveData());
            restored.RecordEvent(Event("same-event"));
            Assert.That(Saved(restored).CurrentStepId, Is.EqualTo("second"));
            Assert.That(restored.GetState(chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(restored.GetObjectiveProgress(chain, 0), Is.Zero);
            Assert.That(Saved(restored).TransitionHistory.Length, Is.EqualTo(1));
        }

        [Test]
        public void ConditionalBranches_ChooseFirstSatisfiedTarget()
        {
            var start = Step("start", transitions: new[] { Transition("preferred", false, new StoryFlagCondition("flag.choice")), Transition("fallback", false) });
            var chain = Chain(start, Step("preferred"), Step("fallback"));
            var flags = new FakeStoryFlags();
            flags.Set("flag.choice");
            var log = Log(chain);
            log.RecordEventWithContext(Event("choose"), Context(flags), default);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("preferred"));
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active));
        }

        [Test]
        public void ConditionalBranches_FallBackWhenTheFirstConditionFails()
        {
            var start = Step("start", transitions: new[] { Transition("preferred", false, new StoryFlagCondition("flag.choice")), Transition("fallback", false) });
            var chain = Chain(start, Step("preferred"), Step("fallback"));
            var log = Log(chain);
            log.RecordEventWithContext(Event("choose"), Context(new FakeStoryFlags()), default);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("fallback"));
        }

        [Test]
        public void AuthoredBranchesWithoutMatch_WaitAndReevaluateWithoutRepeatingObjective()
        {
            var start = Step("start", transitions: new[] { Transition("next", false, new StoryFlagCondition("flag.go")) });
            var chain = Chain(start, Step("next"));
            var flags = new FakeStoryFlags();
            var log = Log(chain);
            log.RecordEventWithContext(Event("wait"), Context(flags), default);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("start"));
            Assert.That(Saved(log).CompletedStepIds, Is.Empty);
            flags.Set("flag.go");
            Assert.That(log.EvaluateStepProgress(chain, Context(flags), default), Is.True);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("next"));
        }

        [Test]
        public void ExplicitTerminalBranch_CompletesWithoutVisitingUnselectedNodes()
        {
            var chain = Chain(Step("start", transitions: new[] { Transition(null, true) }), Step("unselected"));
            var log = Log(chain);
            log.RecordEventWithContext(Event("finish"), default, default);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Completed));
            CollectionAssert.AreEqual(new[] { "start" }, Saved(log).CompletedStepIds);
        }

        [Test]
        public void StartCondition_PreventsAcceptanceUntilSatisfied()
        {
            var chain = new QuestChainDefinition("branching-test", "n", "d", "", new[] { Step("start") }, new QuestCondition[] { new StoryFlagCondition("flag.start") });
            var log = new QuestChainLog(new[] { chain });
            var flags = new FakeStoryFlags();
            Assert.That(log.EvaluateAvailability(chain, Context(flags)), Is.False);
            Assert.That(log.AcceptWithContext(chain, Context(flags)), Is.False);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Available));
            flags.Set("flag.start");
            Assert.That(log.EvaluateAvailability(chain, Context(flags)), Is.True);
            Assert.That(log.AcceptWithContext(chain, Context(flags)), Is.True);
            Assert.That(log.EvaluateAvailability(chain, Context(flags)), Is.False, "only an available chain is available");
        }

        [Test]
        public void StepCompletionCondition_KeepsObjectiveCreditUntilConditionBecomesTrue()
        {
            var chain = Chain(Step("first", completion: new QuestCondition[] { new StoryFlagCondition("flag.done") }), Step("second"));
            var flags = new FakeStoryFlags();
            var log = Log(chain);
            log.RecordEventWithContext(Event("credit"), Context(flags), default);
            Assert.That(log.GetObjectiveProgress(chain, 0), Is.EqualTo(1));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("first"));
            flags.Set("flag.done");
            Assert.That(log.EvaluateStepProgress(chain, Context(flags), default), Is.True);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("second"));
        }

        [Test]
        public void StepBlockCondition_PreventsCreditAndUnblocksWhenSatisfied()
        {
            var chain = Chain(Step("first", block: new QuestCondition[] { new StoryFlagCondition("flag.open") }), Step("second"));
            var flags = new FakeStoryFlags();
            var log = Log(chain);
            log.RecordEventWithContext(Event("blocked"), Context(flags), default);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Blocked));
            Assert.That(log.GetObjectiveProgress(chain, 0), Is.Zero);
            Assert.That(log.GetBlockedReasonId(chain), Is.EqualTo("StoryFlagCondition"));
            flags.Set("flag.open");
            log.RecordEventWithContext(Event("unblocked"), Context(flags), default);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("second"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidOrSelfTransition_DoesNotGrantRewardOrAdvance(bool self)
        {
            var item = Item();
            var first = Step("first", rewards: new QuestReward[] { new ItemQuestReward(item, 1) }, transitions: new[] { Transition(self ? "first" : "outside-chain", false) });
            var chain = Chain(first, Step("second"));
            var inventory = new ItemInventory();
            var log = Log(chain);
            log.RecordEventWithContext(Event("invalid"), default, Rewards(inventory));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("first"));
            Assert.That(Saved(log).CompletedStepIds, Is.Empty);
            Assert.That(inventory.CountOf(item), Is.Zero);
        }

        [Test]
        public void TransitionWithoutTargetAndNotTerminal_NeverResolves()
        {
            var chain = Chain(Step("first", transitions: new[] { Transition(null, false), Transition("", false) }), Step("second"));
            var log = Log(chain);
            log.RecordEventWithContext(Event("nowhere"), default, default);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("first"));
        }

        [Test]
        public void TransitionBackToACompletedStep_IsRefused()
        {
            var chain = Chain(Step("first"), Step("second", transitions: new[] { Transition("first", false) }));
            var log = Log(chain);
            log.RecordEvent(Event("e1"));
            log.RecordEvent(Event("e2"));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("second"));
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active));
        }

        [Test]
        public void AggregateStepRewards_WhenOnlyOneSlotFree_LeaveInventoryAndStepUncommitted()
        {
            var itemA = Item();
            var itemB = Item();
            var chain = Chain(Step("first", rewards: new QuestReward[] { new ItemQuestReward(itemA, 1), new ItemQuestReward(itemB, 1) }), Step("second"));
            var inventory = new ItemInventory();
            var filler = Item();
            inventory.Add(filler, ItemInventory.MaxSlots - 1);
            var log = Log(chain);
            log.RecordEventWithContext(Event("capacity"), default, Rewards(inventory));
            Assert.That(inventory.CountOf(itemA), Is.Zero);
            Assert.That(inventory.CountOf(itemB), Is.Zero);
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("first"));
            Assert.That(Saved(log).CompletedStepIds, Is.Empty);
            Assert.That(log.IsRewardClaimed(chain, "step:first"), Is.False);
            inventory.Remove(filler, 1);
            Assert.That(log.EvaluateStepProgress(chain, default, Rewards(inventory)), Is.True);
            Assert.That(inventory.CountOf(itemA), Is.EqualTo(1));
            Assert.That(inventory.CountOf(itemB), Is.EqualTo(1));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("second"));
            Assert.That(log.IsRewardClaimed(chain, "step:first"), Is.True);
        }

        [Test]
        public void StepReward_SaveReloadAndInventoryNotificationReentryCannotDuplicate()
        {
            var item = Item(99);
            var chain = Chain(Step("first", rewards: new QuestReward[] { new ItemQuestReward(item, 2) }), Step("second"));
            var inventory = new ItemInventory();
            var log = Log(chain);
            inventory.OnChanged += () => log.EvaluateStepProgress(chain, default, Rewards(inventory));
            log.RecordEventWithContext(Event("reward"), default, Rewards(inventory));
            Assert.That(inventory.CountOf(item), Is.EqualTo(2));
            var restored = new QuestChainLog(new[] { chain });
            restored.LoadFromSaveData(log.ToSaveData());
            restored.RecordEventWithContext(Event("reward"), default, Rewards(inventory));
            Assert.That(inventory.CountOf(item), Is.EqualTo(2));
            Assert.That(Saved(restored).CurrentStepId, Is.EqualTo("second"));
            Assert.That(restored.GetObjectiveProgress(chain, 0), Is.Zero);
        }

        [Test]
        public void StepRewardAlreadyClaimedInSave_AdvancesWithoutPayingAgain()
        {
            var item = Item(99);
            var chain = Chain(Step("first", rewards: new QuestReward[] { new ItemQuestReward(item, 2) }), Step("second"));
            var log = new QuestChainLog(new[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData
            {
                ChainId = chain.Id, ChainState = "Active", CurrentStepId = "first", ObjectiveProgressValues = new[] { 0 }, RewardClaimedIds = new[] { "step:first" },
            } } });
            var inventory = new ItemInventory();
            log.RecordEventWithContext(Event("credit"), default, Rewards(inventory));
            Assert.That(Saved(log).CurrentStepId, Is.EqualTo("second"));
            Assert.That(inventory.CountOf(item), Is.Zero);
        }

        [Test]
        public void AggregateCompletionRewards_CannotPartiallyGrantIntoOneFreeSlot()
        {
            var itemA = Item();
            var itemB = Item();
            var chain = new QuestChainDefinition("branching-test", "n", "d", "", new[] { Step("only") }, completionRewards: new QuestReward[] { new ItemQuestReward(itemA, 1), new ItemQuestReward(itemB, 1) });
            var log = Log(chain);
            log.RecordEvent(Event("complete"));
            var inventory = new ItemInventory();
            inventory.Add(Item(), ItemInventory.MaxSlots - 1);
            var rewards = Rewards(inventory);
            Assert.That(log.CanClaimCompletionRewards(chain, rewards), Is.False);
            Assert.That(log.ClaimCompletionRewards(chain, rewards), Is.False);
            Assert.That(inventory.CountOf(itemA) + inventory.CountOf(itemB), Is.Zero);
            Assert.That(log.IsRewardClaimed(chain, "completion"), Is.False);
        }

        [Test]
        public void CurrentStepAccessor_ReturnsAdvancedStepForJournalConsumers()
        {
            var second = Step("second");
            var chain = Chain(Step("first"), second);
            var log = Log(chain);
            log.RecordEvent(Event("advance"));
            Assert.That(log.GetCurrentStep(chain), Is.SameAs(second));
            Assert.That(log.AreCurrentObjectivesComplete(chain), Is.False);
        }

        // ---- QuestRewardTests (chains) ----

        [Test]
        public void QUEST_CHAIN_EDIT_008_CompletionRewardPreflightAndDuplicateClaimAreAtomic()
        {
            var item = Item(99);
            var chain = SingleStepChain("chain.reward", new AnyEventObjective(1, "objective.chain.reward"), completionRewards: new QuestReward[] { new ItemQuestReward(item, 2) });
            var log = Log(chain);
            var context = Rewards(new ItemInventory());
            Assert.That(log.CanClaimCompletionRewards(chain, context), Is.False, "not completed yet");
            log.RecordEvent(Event("chain-reward-progress"));

            Assert.That(log.CanClaimCompletionRewards(chain, context), Is.True);
            Assert.That(log.ClaimCompletionRewards(chain, context), Is.True);
            Assert.That(log.ClaimCompletionRewards(chain, context), Is.False, "duplicate reward claim should be rejected.");
            Assert.That(context.Inventory.CountOf(item), Is.EqualTo(2));
            Assert.That(log.IsRewardClaimed(chain, "completion"), Is.True);

            var restored = new QuestChainLog(new[] { chain });
            restored.LoadFromSaveData(log.ToSaveData());
            Assert.That(restored.ClaimCompletionRewards(chain, context), Is.False, "a reload cannot pay again");
            Assert.That(context.Inventory.CountOf(item), Is.EqualTo(2));
        }

        [Test]
        public void QUEST_CHAIN_EDIT_008_FullInventoryCompletionRewardMutatesNothing()
        {
            var rewardItem = Item();
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) inventory.Add(Item(), 1);
            var chain = SingleStepChain("chain.reward", new AnyEventObjective(1), completionRewards: new QuestReward[] { new ItemQuestReward(rewardItem, 1) });
            var log = Log(chain);
            log.RecordEvent(Event("chain-full-inventory-progress"));
            var context = Rewards(inventory);
            Assert.That(log.CanClaimCompletionRewards(chain, context), Is.False);
            Assert.That(log.ClaimCompletionRewards(chain, context), Is.False);
            Assert.That(inventory.CountOf(rewardItem), Is.Zero);
            Assert.That(log.IsRewardClaimed(chain, "completion"), Is.False);
        }

        [Test]
        public void CompletionRewardClaimStartedFromInsideAClaim_IsRefused()
        {
            var item = Item(99);
            var reentrant = new ReentrantReward();
            var chain = SingleStepChain("chain.reentry", new AnyEventObjective(1), completionRewards: new QuestReward[] { reentrant, new ItemQuestReward(item, 1) });
            var log = Log(chain);
            log.RecordEvent(Event("done"));
            var context = Rewards(new ItemInventory());
            reentrant.Claim = () => log.ClaimCompletionRewards(chain, context);
            Assert.That(log.ClaimCompletionRewards(chain, context), Is.True);
            Assert.That(reentrant.InnerResult, Is.False);
            Assert.That(context.Inventory.CountOf(item), Is.EqualTo(1));
        }

        [Test]
        public void QUEST_ID_009_QuestChainStartsWhenTalkingToItsStartNpc()
        {
            var chain = new QuestChainDefinition("chain.mom", "n", "d", "", new[] { Step("s") }, startNpcId: "npc.test-mom");
            Assert.That(chain.IsStartedByTalkingTo("npc.test-mom"), Is.True);
            Assert.That(chain.IsStartedByTalkingTo("npc.test-teacher"), Is.False);
            Assert.That(chain.IsStartedByTalkingTo(null), Is.False);
            Assert.That(new QuestChainDefinition("chain.none", "n", "d", "", new[] { Step("s") }).IsStartedByTalkingTo("npc.test-mom"), Is.False,
                "a chain without a start NPC is never started by talking");
        }

        // ---- 새 경계 ----

        [Test]
        public void InvalidDefinitions_CannotBeAcceptedOrMadeAvailable()
        {
            var noId = new QuestChainDefinition("", "n", "d", "", new[] { Step("a") });
            var noSteps = new QuestChainDefinition("chain.empty", "n", "d", "", null);
            var duplicateSteps = new QuestChainDefinition("chain.dup", "n", "d", "", new[] { Step("a"), Step("a") });
            var emptyStepId = new QuestChainDefinition("chain.blank", "n", "d", "", new[] { Step("") });
            var log = new QuestChainLog(new[] { noId, noSteps, duplicateSteps, emptyStepId });
            foreach (var chain in new[] { noId, noSteps, duplicateSteps, emptyStepId })
            {
                Assert.That(log.EvaluateAvailability(chain, default), Is.False);
                Assert.That(log.Accept(chain), Is.False);
            }

            Assert.That(log.ToSaveData().Chains.Length, Is.EqualTo(3), "a chain without an id is not saved");
            Assert.That(log.AddChain(noSteps), Is.False, "already added");
            Assert.That(log.AddChain(null), Is.False);
        }

        [Test]
        public void SavedStepThatNoLongerExists_BlocksWithAnInvalidStepReason()
        {
            var chain = Chain(Step("first"));
            var log = new QuestChainLog(new[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { new QuestChainProgressSaveData { ChainId = chain.Id, ChainState = "Active", CurrentStepId = "removed" } } });
            Assert.That(log.EvaluateBlockState(chain, default), Is.True);
            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Blocked));
            Assert.That(log.GetBlockedReasonId(chain), Is.EqualTo("questchain.invalid-step"));
        }

        [Test]
        public void RenamedStep_OldSaveResumesOnTheNewStepAndKeepsItsRewardClaim()
        {
            var item = Item(99);
            var renamed = Step("step.new", rewards: new QuestReward[] { new ItemQuestReward(item, 1) }, legacyIds: new[] { "step.old" });
            var chain = Chain(Step("step.before"), renamed, Step("step.after"));
            var progress = QuestChainProgress.FromSaveData(new QuestChainProgressSaveData
            {
                SaveSlot = "old-save", PlayerId = "player", ChainId = chain.Id, ChainState = "Active", CurrentStepId = "step.old",
                ObjectiveProgressValues = new[] { 0 }, CompletedStepIds = new[] { "step.before" }, RewardClaimedIds = new[] { "step:step.old" },
            }, chain);
            Assert.That(progress.GetCurrentStep(chain), Is.SameAs(renamed));
            Assert.That(progress.IsRewardClaimed("step:step.new"), Is.True, "a renamed step cannot pay out twice");

            var log = new QuestChainLog(new[] { chain });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { progress.ToSaveData() } });
            var inventory = new ItemInventory();
            log.RecordEventWithContext(Event("old-save-credit"), default, Rewards(inventory));
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("step.after"));
            Assert.That(inventory.CountOf(item), Is.Zero);
        }

        [Test]
        public void FromSaveData_ClampsProgressAndFillsDefaults()
        {
            var chain = new QuestChainDefinition("chain.c", "n", "d", "career.c", new[] { Step("a", new AnyEventObjective(2)) });
            var progress = QuestChainProgress.FromSaveData(new QuestChainProgressSaveData
            {
                ChainState = "Completed", IsTracked = true, ObjectiveProgressValues = new[] { 9, 9 }, AcceptedDayIndex = 3,
            }, chain);
            Assert.That(progress.SaveSlot, Is.EqualTo("default"));
            Assert.That(progress.PlayerId, Is.EqualTo("player"));
            Assert.That(progress.ChainId, Is.EqualTo("chain.c"));
            Assert.That(progress.RelatedCareerInterestId, Is.EqualTo("career.c"));
            Assert.That(progress.State, Is.EqualTo(QuestChainState.Completed));
            Assert.That(progress.IsTracked, Is.False, "a finished chain is not tracked");
            Assert.That(progress.CurrentStepId, Is.EqualTo("a"));
            Assert.That(progress.GetObjectiveProgress(0), Is.EqualTo(2), "clamped to the required count");
            Assert.That(progress.AcceptedDayIndex, Is.EqualTo(3));

            var unknownState = QuestChainProgress.FromSaveData(new QuestChainProgressSaveData { ChainState = "Nonsense" }, chain);
            Assert.That(unknownState.State, Is.EqualTo(QuestChainState.Available));
            Assert.That(QuestChainProgress.FromSaveData(null, chain).State, Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void SaveData_RoundTripsEveryField()
        {
            var chain = Chain(Step("first"), Step("second"));
            var log = Log(chain);
            var student = new StudentLifeProgress("slot", "player", 10, 10);
            Assert.That(log.AcceptWithContext(chain, default), Is.False, "already accepted");
            log.RecordEventWithContext(Event("e1"), new QuestRuntimeContext(null, null, null, null, student), default);
            var saved = log.ToSaveData();
            var restored = new QuestChainLog(new[] { chain });
            restored.LoadFromSaveData(saved);
            var a = saved.Chains[0];
            var b = restored.ToSaveData().Chains[0];
            Assert.That(b.ChainId, Is.EqualTo(a.ChainId));
            Assert.That(b.ChainState, Is.EqualTo(a.ChainState));
            Assert.That(b.CurrentStepId, Is.EqualTo(a.CurrentStepId));
            CollectionAssert.AreEqual(a.CompletedStepIds, b.CompletedStepIds);
            CollectionAssert.AreEqual(a.ProcessedEventKeys, b.ProcessedEventKeys);
            CollectionAssert.AreEqual(a.RewardClaimedIds, b.RewardClaimedIds);
            CollectionAssert.AreEqual(a.TransitionHistory, b.TransitionHistory);
            CollectionAssert.AreEqual(a.ObjectiveProgressValues, b.ObjectiveProgressValues);
            Assert.That(b.TodayQuestChainSummary, Is.EqualTo(a.TodayQuestChainSummary));
            Assert.That(b.AcceptedDayIndex, Is.EqualTo(-1), "plain Accept has no day");
            Assert.That(b.CompletedDayIndex, Is.EqualTo(-1));
            Assert.That(b.RelatedCareerInterestId, Is.EqualTo(a.RelatedCareerInterestId));
        }

        [Test]
        public void EmptyEventKey_CreditsEveryTime()
        {
            var chain = SingleStepChain("chain.keyless", new AnyEventObjective(3));
            var log = Log(chain);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, null));
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, ""));
            Assert.That(log.GetObjectiveProgress(chain, 0), Is.EqualTo(2));
            Assert.That(Saved(log).ProcessedEventKeys, Is.Empty);
        }

        [Test]
        public void PauseAndAcceptAgain_KeepsTheStepAndProgress()
        {
            var chain = Chain(Step("first", new AnyEventObjective(2)), Step("second"));
            var log = Log(chain);
            log.Track(chain);
            log.RecordEvent(Event("e1"));
            var other = SingleStepChain("chain.other", new AnyEventObjective(1));
            var both = new QuestChainLog(new[] { chain, other });
            both.LoadFromSaveData(log.ToSaveData());
            Assert.That(both.Accept(other), Is.True);
            Assert.That(both.Track(other), Is.True);
            Assert.That(both.GetState(chain), Is.EqualTo(QuestChainState.Paused));
            Assert.That(both.Accept(chain), Is.True, "a paused chain can be accepted again");
            Assert.That(both.GetState(chain), Is.EqualTo(QuestChainState.Active));
            Assert.That(both.GetObjectiveProgress(chain, 0), Is.EqualTo(1));
        }

        private sealed class BlockingFlagCondition : QuestCondition
        {
            private readonly string _flagId;
            private readonly string _reasonId;
            private readonly string _summary;
            public BlockingFlagCondition(string flagId, string reasonId, string summary) { _flagId = flagId; _reasonId = reasonId; _summary = summary; }
            public override string BlockedReasonId => _reasonId;
            public override string BlockedSummary => _summary;
            public override bool IsSatisfied(in QuestRuntimeContext context) => context.StoryFlags != null && context.StoryFlags.IsSet(_flagId);
        }

        private sealed class ReentrantReward : QuestReward
        {
            public Func<bool> Claim;
            public bool InnerResult = true;
            public override bool CanApply(in RewardRuntimeContext context) => true;
            public override void Apply(in RewardRuntimeContext context) { InnerResult = Claim(); }
        }
    }
}
