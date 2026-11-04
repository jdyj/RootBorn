using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Conditions;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainBranchingTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void FirstStepCompletion_AdvancesToSecondStepWithoutCompletingChain()
        {
            var first = Step("first");
            var second = Step("second");
            var chain = Chain(first, second);
            var log = Log(chain);
            log.RecordEvent(Event("first-event"));
            var saved = Saved(log);
            Assert.AreEqual(QuestChainState.Active, log.GetState(chain));
            Assert.AreEqual("second", saved.CurrentStepId);
            CollectionAssert.AreEqual(new[] { "first" }, saved.CompletedStepIds);
            Assert.AreEqual(0, log.GetObjectiveProgress(chain, 0));
            log.RecordEvent(Event("second-event"));
            Assert.AreEqual(QuestChainState.Completed, log.GetState(chain));
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, Saved(log).CompletedStepIds);
        }

        [Test]
        public void ExistingSingleStepChain_CompletesWithOriginalApi()
        {
            var chain = Chain(Step("only"));
            var log = Log(chain);
            log.RecordEvent(Event("complete"));
            Assert.AreEqual(QuestChainState.Completed, log.GetState(chain));
            Assert.AreEqual("only", Saved(log).CurrentStepId);
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
            Assert.AreEqual("second", Saved(restored).CurrentStepId);
            Assert.AreEqual(QuestChainState.Active, restored.GetState(chain));
            Assert.AreEqual(0, restored.GetObjectiveProgress(chain, 0));
            Assert.AreEqual(1, Saved(restored).TransitionHistory.Length);
        }

        [Test]
        public void ConditionalBranches_ChooseFirstSatisfiedTargetFromScriptableObjectData()
        {
            var flag = Create<StoryFlagDefinition>();
            var condition = FlagCondition(flag);
            var start = Step("start");
            var firstTarget = Step("preferred");
            var fallback = Step("fallback");
            Set(start, "_transitions", new QuestChainTransitionBase[]
            {
                Transition(firstTarget, false, condition), Transition(fallback, false)
            });
            var chain = Chain(start, firstTarget, fallback);
            var flags = new StoryFlagSet();
            flags.Set(flag);
            var log = Log(chain);
            Record(log, Event("choose"), Context(flags));
            Assert.AreEqual("preferred", Saved(log).CurrentStepId);
            Assert.AreEqual(QuestChainState.Active, log.GetState(chain));
        }

        [Test]
        public void AuthoredBranchesWithoutMatch_WaitAndReevaluateWithoutRepeatingObjective()
        {
            var flag = Create<StoryFlagDefinition>();
            var start = Step("start");
            var next = Step("next");
            Set(start, "_transitions", new QuestChainTransitionBase[] { Transition(next, false, FlagCondition(flag)) });
            var chain = Chain(start, next);
            var flags = new StoryFlagSet();
            var log = Log(chain);
            Record(log, Event("wait"), Context(flags));
            Assert.AreEqual("start", Saved(log).CurrentStepId);
            Assert.IsEmpty(Saved(log).CompletedStepIds);
            flags.Set(flag);
            Assert.IsTrue(Evaluate(log, chain, Context(flags)));
            Assert.AreEqual("next", Saved(log).CurrentStepId);
        }

        [Test]
        public void ExplicitTerminalBranch_CompletesWithoutVisitingUnselectedNodes()
        {
            var start = Step("start");
            Set(start, "_transitions", new QuestChainTransitionBase[] { Transition(null, true) });
            var chain = Chain(start, Step("unselected"));
            var log = Log(chain);
            Record(log, Event("finish"));
            Assert.AreEqual(QuestChainState.Completed, log.GetState(chain));
            CollectionAssert.AreEqual(new[] { "start" }, Saved(log).CompletedStepIds);
        }

        [Test]
        public void StartCondition_PreventsAcceptanceUntilSatisfied()
        {
            var flag = Create<StoryFlagDefinition>();
            var chain = Chain(Step("start"));
            Set(chain, "_startConditions", new QuestConditionBase[] { FlagCondition(flag) });
            var log = new QuestChainLog(new[] { chain });
            var flags = new StoryFlagSet();
            Assert.IsFalse(log.EvaluateAvailability(chain, Context(flags)));
            Assert.IsFalse((bool)Invoke(log, "AcceptWithContext", chain, Context(flags)));
            Assert.AreEqual(QuestChainState.Available, log.GetState(chain));
            flags.Set(flag);
            Assert.IsTrue((bool)Invoke(log, "AcceptWithContext", chain, Context(flags)));
        }

        [Test]
        public void StepCompletionCondition_KeepsObjectiveCreditUntilConditionBecomesTrue()
        {
            var flag = Create<StoryFlagDefinition>();
            var first = Step("first");
            Set(first, "_completionConditions", new QuestConditionBase[] { FlagCondition(flag) });
            var chain = Chain(first, Step("second"));
            var flags = new StoryFlagSet();
            var log = Log(chain);
            Record(log, Event("credit"), Context(flags));
            Assert.AreEqual(1, log.GetObjectiveProgress(chain, 0));
            Assert.AreEqual("first", Saved(log).CurrentStepId);
            flags.Set(flag);
            Assert.IsTrue(Evaluate(log, chain, Context(flags)));
            Assert.AreEqual("second", Saved(log).CurrentStepId);
        }

        [Test]
        public void StepBlockCondition_PreventsCreditAndUnblocksWhenSatisfied()
        {
            var flag = Create<StoryFlagDefinition>();
            var first = Step("first");
            Set(first, "_blockConditions", new QuestConditionBase[] { FlagCondition(flag) });
            var chain = Chain(first, Step("second"));
            var flags = new StoryFlagSet();
            var log = Log(chain);
            Record(log, Event("blocked"), Context(flags));
            Assert.AreEqual(QuestChainState.Blocked, log.GetState(chain));
            Assert.AreEqual(0, log.GetObjectiveProgress(chain, 0));
            flags.Set(flag);
            Record(log, Event("unblocked"), Context(flags));
            Assert.AreEqual(QuestChainState.Active, log.GetState(chain));
            Assert.AreEqual("second", Saved(log).CurrentStepId);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidOrSelfTransition_DoesNotGrantRewardOrAdvance(bool self)
        {
            var first = Step("first");
            var invalid = self ? first : Step("outside-chain");
            Set(first, "_transitions", new QuestChainTransitionBase[] { Transition(invalid, false) });
            var item = Item();
            Set(first, "_rewards", new QuestRewardBase[] { Reward(item, 1) });
            var chain = Chain(first, Step("second"));
            var inventory = new Inventory();
            var log = Log(chain);
            Record(log, Event("invalid"), default, Rewards(inventory));
            Assert.AreEqual("first", Saved(log).CurrentStepId);
            Assert.IsEmpty(Saved(log).CompletedStepIds);
            Assert.AreEqual(0, inventory.CountOf(item));
        }

        [Test]
        public void AggregateStepRewards_WhenOnlyOneSlotFree_LeaveInventoryAndStepUncommitted()
        {
            var first = Step("first");
            var itemA = Item();
            var itemB = Item();
            Set(first, "_rewards", new QuestRewardBase[] { Reward(itemA, 1), Reward(itemB, 1) });
            var chain = Chain(first, Step("second"));
            var inventory = new Inventory();
            var filler = Item();
            inventory.Add(filler, Inventory.MaxSlots - 1);
            var log = Log(chain);
            Record(log, Event("capacity"), default, Rewards(inventory));
            Assert.AreEqual(0, inventory.CountOf(itemA));
            Assert.AreEqual(0, inventory.CountOf(itemB));
            Assert.AreEqual("first", Saved(log).CurrentStepId);
            Assert.IsEmpty(Saved(log).CompletedStepIds);
            Assert.IsFalse(log.IsRewardClaimed(chain, "step:first"));
            inventory.Remove(filler, 1);
            Assert.IsTrue(Evaluate(log, chain, default, Rewards(inventory)));
            Assert.AreEqual(1, inventory.CountOf(itemA));
            Assert.AreEqual(1, inventory.CountOf(itemB));
            Assert.AreEqual("second", Saved(log).CurrentStepId);
            Assert.IsTrue(log.IsRewardClaimed(chain, "step:first"));
        }

        [Test]
        public void StepReward_SaveReloadAndInventoryNotificationReentryCannotDuplicate()
        {
            var first = Step("first");
            var item = Item(99);
            Set(first, "_rewards", new QuestRewardBase[] { Reward(item, 2) });
            var chain = Chain(first, Step("second"));
            var inventory = new Inventory();
            var log = Log(chain);
            inventory.OnChanged += () => Evaluate(log, chain, default, Rewards(inventory));
            Record(log, Event("reward"), default, Rewards(inventory));
            Assert.AreEqual(2, inventory.CountOf(item));
            var restored = new QuestChainLog(new[] { chain });
            restored.LoadFromSaveData(log.ToSaveData());
            Record(restored, Event("reward"), default, Rewards(inventory));
            Assert.AreEqual(2, inventory.CountOf(item));
            Assert.AreEqual("second", Saved(restored).CurrentStepId);
            Assert.AreEqual(0, restored.GetObjectiveProgress(chain, 0));
        }

        [Test]
        public void AggregateCompletionRewards_CannotPartiallyGrantIntoOneFreeSlot()
        {
            var itemA = Item();
            var itemB = Item();
            var chain = Chain(Step("only"));
            Set(chain, "_completionRewards", new QuestRewardBase[] { Reward(itemA, 1), Reward(itemB, 1) });
            var log = Log(chain);
            log.RecordEvent(Event("complete"));
            var inventory = new Inventory();
            inventory.Add(Item(), Inventory.MaxSlots - 1);
            var rewards = Rewards(inventory);
            Assert.IsFalse(log.CanClaimCompletionRewards(chain, in rewards));
            Assert.IsFalse(log.ClaimCompletionRewards(chain, in rewards));
            Assert.AreEqual(0, inventory.CountOf(itemA));
            Assert.AreEqual(0, inventory.CountOf(itemB));
            Assert.IsFalse(log.IsRewardClaimed(chain, "completion"));
        }

        [Test]
        public void CurrentStepAccessor_ReturnsAdvancedStepForJournalConsumers()
        {
            var first = Step("first");
            var second = Step("second");
            var chain = Chain(first, second);
            var log = Log(chain);
            log.RecordEvent(Event("advance"));
            Assert.AreSame(second, Invoke(log, "GetCurrentStep", chain));
        }

        private T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        private QuestStepDefinition Step(string id)
        {
            var objective = Create<AnyEventObjective>();
            objective.ConfigureForRuntime(id + ".objective", 1);
            var step = Create<QuestStepDefinition>();
            step.ConfigureForTests(id, id, id + ".description", new QuestObjectiveBase[] { objective }, null, null, null, null);
            return step;
        }

        private QuestChainDefinition Chain(params QuestStepDefinition[] steps)
        {
            var chain = Create<QuestChainDefinition>();
            chain.ConfigureForTests("branching-test", "branching-test", "branching-test.description", "", steps, null, null, null, null, 0);
            return chain;
        }

        private QuestChainTransitionBase Transition(QuestStepDefinition target, bool terminal, params QuestConditionBase[] conditions)
        {
            var type = Type.GetType("Rootborn.Game.Quests.ConditionalQuestChainTransition, Rootborn.Game");
            Assert.IsNotNull(type, "Conditional transition SO implementation is missing.");
            var value = (QuestChainTransitionBase)ScriptableObject.CreateInstance(type);
            _created.Add(value);
            value.ConfigureForTests("transition." + (target != null ? target.Id : "terminal"));
            Set(value, "_targetStep", target);
            Set(value, "_completeChain", terminal);
            Set(value, "_conditions", conditions);
            return value;
        }

        private StoryFlagCondition FlagCondition(StoryFlagDefinition flag)
        {
            var condition = Create<StoryFlagCondition>();
            Set(condition, "_requiredFlag", flag);
            return condition;
        }

        private ItemDefinition Item(int maxStack = 1)
        {
            var item = Create<ItemDefinition>();
            Set(item, "_maxStack", maxStack);
            return item;
        }

        private ItemQuestReward Reward(ItemDefinition item, int count)
        {
            var reward = Create<ItemQuestReward>();
            Set(reward, "_item", item);
            Set(reward, "_count", count);
            return reward;
        }

        private static QuestChainLog Log(QuestChainDefinition chain)
        {
            var log = new QuestChainLog(new[] { chain });
            Assert.IsTrue(log.Accept(chain));
            return log;
        }

        private static QuestChainProgressSaveData Saved(QuestChainLog log) => log.ToSaveData().Chains[0];
        private static QuestEvent Event(string key) => new QuestEvent(QuestEventKind.Talk, key);
        private static QuestRuntimeContext Context(StoryFlagSet flags) => new QuestRuntimeContext(null, null, null, flags);
        private static RewardRuntimeContext Rewards(Inventory inventory) => new RewardRuntimeContext(null, inventory, null, null);
        private static void Record(QuestChainLog log, QuestEvent value, QuestRuntimeContext context = default, RewardRuntimeContext rewards = default) => Invoke(log, "RecordEventWithContext", value, context, rewards);
        private static bool Evaluate(QuestChainLog log, QuestChainDefinition chain, QuestRuntimeContext context = default, RewardRuntimeContext rewards = default) => (bool)Invoke(log, "EvaluateStepProgress", chain, context, rewards);

        private static object Invoke(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "Missing behavior API: " + name);
            return method.Invoke(target, args);
        }

        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Assert.Fail("Missing serialized field: " + name);
        }

        private sealed class AnyEventObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }
    }
}
