using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainStateTests
    {
        [Test]
        public void QUEST_CHAIN_EDIT_001_TypesExposeDataDrivenChainStepStateAndRegistrySurfaces()
        {
            AssertType("Rootborn.Game.Quests.QuestChainDefinition, Rootborn.Game");
            AssertType("Rootborn.Game.Quests.QuestStepDefinition, Rootborn.Game");
            AssertType("Rootborn.Game.Quests.QuestChainTransitionBase, Rootborn.Game");
            AssertType("Rootborn.Game.Quests.QuestChainProgress, Rootborn.Game");
            AssertType("Rootborn.Game.Quests.QuestChainLog, Rootborn.Game");
            AssertType("Rootborn.Game.Quests.QuestChainState, Rootborn.Game");

            var registryType = AssertType("Rootborn.Game.Common.GameDataRegistry, Rootborn.Game");
            Assert.IsNotNull(registryType.GetProperty("QuestChains"), "QUEST-CHAIN-EDIT-001 failed: GameDataRegistry must expose QuestChainDefinition[] QuestChains.");
        }

        [Test]
        public void QUEST_CHAIN_EDIT_004_SameEventProgressesMultipleActiveChainsIndependently()
        {
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            try
            {
                activity.ConfigureForTests("activity.library.study", "activity.library.study", null, LocationGrowthRoute.SelfStudy, 30, 0, 0, 0, null, null);
                var first = CreateChain("chain.learning", "career.learning", CreateLocationActivityObjective(activity, 1));
                var second = CreateChain("chain.service", "career.service", CreateLocationActivityObjective(activity, 2));
                var log = CreateLog(first, second);

                AssertInvokeTrue(log, "EvaluateAvailability", first, null);
                AssertInvokeTrue(log, "EvaluateAvailability", second, null);
                AssertInvokeTrue(log, "Accept", first);
                AssertInvokeTrue(log, "Accept", second);

                Invoke(log, "RecordEvent", new object[] { new QuestEvent(QuestEventKind.LocationActivity, "shared-activity-1", 1, activity: activity) });

                Assert.AreEqual("Completed", Invoke(log, "GetState", first).ToString(), "QUEST-CHAIN-EDIT-004 failed: first chain should complete from the shared event.");
                Assert.AreEqual("Active", Invoke(log, "GetState", second).ToString(), "QUEST-CHAIN-EDIT-004 failed: second chain should remain active until its own required count is met.");
                Assert.AreEqual(1, Invoke(log, "GetObjectiveProgress", second, 0), "QUEST-CHAIN-EDIT-004 failed: second chain objective progress must be stored independently.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(activity);
            }
        }

        [Test]
        public void QUEST_CHAIN_EDIT_005_TrackingAnotherChainPausesPreviousWithoutDeletingProgress()
        {
            var first = CreateChain("chain.learning", "career.learning", CreateAnyEventObjective(2));
            var second = CreateChain("chain.service", "career.service", CreateAnyEventObjective(1));
            var log = CreateLog(first, second);

            AssertInvokeTrue(log, "EvaluateAvailability", first, null);
            AssertInvokeTrue(log, "EvaluateAvailability", second, null);
            AssertInvokeTrue(log, "Accept", first);
            AssertInvokeTrue(log, "Track", first);
            Invoke(log, "RecordEvent", new object[] { new QuestEvent(QuestEventKind.Talk, "first-progress", 1) });
            Assert.AreEqual(1, Invoke(log, "GetObjectiveProgress", first, 0));

            AssertInvokeTrue(log, "Accept", second);
            AssertInvokeTrue(log, "Track", second);

            Assert.AreEqual("Paused", Invoke(log, "GetState", first).ToString(), "QUEST-CHAIN-EDIT-005 failed: tracking another chain should pause the previous tracked chain.");
            Assert.AreEqual(1, Invoke(log, "GetObjectiveProgress", first, 0), "QUEST-CHAIN-EDIT-005 failed: paused chain progress should remain intact.");
            Assert.AreEqual("Tracked", Invoke(log, "GetState", second).ToString());
        }

        [Test]
        public void QUEST_CHAIN_EDIT_006_BlockedStateStoresReasonAndUnblockSummary()
        {
            var chain = CreateChain("chain.blocked", "career.learning", CreateAnyEventObjective(1));
            var log = CreateLog(chain);
            AssertInvokeTrue(log, "EvaluateAvailability", chain, null);
            AssertInvokeTrue(log, "Accept", chain);

            AssertInvokeTrue(log, "Block", chain, "missing.npc", "Talk to the librarian after opening the library.");

            Assert.AreEqual("Blocked", Invoke(log, "GetState", chain).ToString());
            Assert.AreEqual("missing.npc", Invoke(log, "GetBlockedReasonId", chain));
            Assert.AreEqual("Talk to the librarian after opening the library.", Invoke(log, "GetBlockedSummary", chain));
        }

        [Test]
        public void QUEST_CHAIN_EDIT_006_BlockConditionsPauseProgressUntilUnblockConditionsAreSatisfied()
        {
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var condition = ScriptableObject.CreateInstance<StoryFlagQuestChainCondition>();
            var flags = new StoryFlagSet();
            try
            {
                condition.ConfigureForTests(flag, "missing.library.access", "Visit the library desk to reopen this chain.");
                var chain = CreateChain("chain.blocked-by-condition", "career.learning", CreateAnyEventObjective(1), new[] { condition }, new[] { condition });
                var log = CreateLog(chain);
                AssertInvokeTrue(log, "EvaluateAvailability", chain, null);
                AssertInvokeTrue(log, "Accept", chain);

                AssertInvokeTrue(log, "EvaluateBlockState", chain, new QuestRuntimeContext(null, null, null, flags));

                Assert.AreEqual("Blocked", Invoke(log, "GetState", chain).ToString(), "QUEST-CHAIN-EDIT-006 failed: missing SO condition should block the active chain.");
                Assert.AreEqual("missing.library.access", Invoke(log, "GetBlockedReasonId", chain));
                Assert.AreEqual("Visit the library desk", Invoke(log, "GetBlockedSummary", chain).ToString().Substring(0, 22));
                Invoke(log, "RecordEvent", new object[] { new QuestEvent(QuestEventKind.Talk, "ignored-while-blocked", 1) });
                Assert.AreEqual(0, Invoke(log, "GetObjectiveProgress", chain, 0), "QUEST-CHAIN-EDIT-006 failed: blocked chain must not progress until reopened.");

                flags.Set(flag);
                AssertInvokeTrue(log, "EvaluateBlockState", chain, new QuestRuntimeContext(null, null, null, flags));

                Assert.AreEqual("Active", Invoke(log, "GetState", chain).ToString(), "QUEST-CHAIN-EDIT-006 failed: satisfying unblock SO conditions should resume the chain.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(flag);
            }
        }

        [Test]
        public void QUEST_CHAIN_EDIT_003_FailedAndExpiredConditionsAreDataDrivenTerminalStates()
        {
            var failedCondition = ScriptableObject.CreateInstance<SwitchQuestChainCondition>();
            var expiredCondition = ScriptableObject.CreateInstance<SwitchQuestChainCondition>();
            try
            {
                failedCondition.ConfigureForTests("failed.low-trust", "Trust dropped below the route threshold.");
                expiredCondition.ConfigureForTests("expired.deadline", "The deadline passed before the next step was completed.");
                var failedChain = CreateChain("chain.failed", "career.learning", CreateAnyEventObjective(1), Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), new[] { failedCondition }, Array.Empty<QuestConditionBase>());
                var expiredChain = CreateChain("chain.expired", "career.learning", CreateAnyEventObjective(1), Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), new[] { expiredCondition });
                var log = CreateLog(failedChain, expiredChain);
                AssertInvokeTrue(log, "EvaluateAvailability", failedChain, null);
                AssertInvokeTrue(log, "EvaluateAvailability", expiredChain, null);
                AssertInvokeTrue(log, "Accept", failedChain);
                AssertInvokeTrue(log, "Accept", expiredChain);

                failedCondition.IsOn = true;
                expiredCondition.IsOn = true;
                AssertInvokeTrue(log, "EvaluateTerminalState", failedChain, new QuestRuntimeContext(null, null, null, null));
                AssertInvokeTrue(log, "EvaluateTerminalState", expiredChain, new QuestRuntimeContext(null, null, null, null));

                Assert.AreEqual("Failed", Invoke(log, "GetState", failedChain).ToString());
                Assert.AreEqual("failed.low-trust", Invoke(log, "GetBlockedReasonId", failedChain));
                Assert.AreEqual("Expired", Invoke(log, "GetState", expiredChain).ToString());
                Assert.AreEqual("expired.deadline", Invoke(log, "GetBlockedReasonId", expiredChain));
                Invoke(log, "RecordEvent", new object[] { new QuestEvent(QuestEventKind.Talk, "ignored-terminal", 1) });
                Assert.AreEqual(0, Invoke(log, "GetObjectiveProgress", failedChain, 0));
                Assert.AreEqual(0, Invoke(log, "GetObjectiveProgress", expiredChain, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(failedCondition);
                UnityEngine.Object.DestroyImmediate(expiredCondition);
            }
        }

        [Test]
        public void QUEST_CHAIN_EDIT_007_SaveLoadRestoresStateStepProgressTrackingBlockAndRewardFlags()
        {
            var chain = CreateChain("chain.persist", "career.learning", CreateAnyEventObjective(2));
            var log = CreateLog(chain);
            AssertInvokeTrue(log, "EvaluateAvailability", chain, null);
            AssertInvokeTrue(log, "Accept", chain);
            AssertInvokeTrue(log, "Track", chain);
            Invoke(log, "RecordEvent", new object[] { new QuestEvent(QuestEventKind.Talk, "persist-progress", 1) });
            AssertInvokeTrue(log, "Block", chain, "time.mismatch", "Come back in the afternoon.");
            AssertInvokeTrue(log, "MarkRewardClaimed", chain, "reward.learning.badge");

            var saveData = Invoke(log, "ToSaveData");
            var restored = CreateLog(chain);
            Invoke(restored, "LoadFromSaveData", saveData);

            Assert.AreEqual("Blocked", Invoke(restored, "GetState", chain).ToString());
            Assert.AreEqual(1, Invoke(restored, "GetObjectiveProgress", chain, 0));
            Assert.AreEqual("time.mismatch", Invoke(restored, "GetBlockedReasonId", chain));
            Assert.AreEqual("Come back in the afternoon.", Invoke(restored, "GetBlockedSummary", chain));
            Assert.AreEqual(true, Invoke(restored, "IsTracked", chain));
            Assert.AreEqual(true, Invoke(restored, "IsRewardClaimed", chain, "reward.learning.badge"));
        }

        [Test]
        public void QUEST_CHAIN_EDIT_010_LogCachesProgressByChainIdForEquivalentAssetLookups()
        {
            var chain = CreateChain("chain.cached", "career.learning", CreateAnyEventObjective(1));
            var equivalent = CreateChain("chain.cached", "career.learning", CreateAnyEventObjective(1));
            var log = CreateLog(chain);
            AssertInvokeTrue(log, "EvaluateAvailability", chain, null);
            AssertInvokeTrue(log, "Accept", chain);

            var cacheField = log.GetType().GetField("_progressByChainId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(cacheField, "QUEST-CHAIN-EDIT-010 failed: QuestChainLog should keep an id-indexed cache instead of relying on repeated registry-wide scans.");
            var cache = cacheField.GetValue(log) as IDictionary;
            Assert.IsNotNull(cache, "QUEST-CHAIN-EDIT-010 failed: id-indexed cache should be dictionary-like.");
            Assert.IsTrue(cache.Contains("chain.cached"), "QUEST-CHAIN-EDIT-010 failed: id-indexed cache omitted registered chain id.");
            Assert.AreEqual("Active", Invoke(log, "GetState", equivalent).ToString(), "QUEST-CHAIN-EDIT-010 failed: equivalent chain assets should resolve through the id cache.");
        }

        private static Type AssertType(string qualifiedName)
        {
            var type = Type.GetType(qualifiedName);
            Assert.IsNotNull(type, "Missing type: " + qualifiedName);
            return type;
        }

        private static ScriptableObject CreateChain(string id, string careerInterestId, QuestObjectiveBase objective)
        {
            return CreateChain(id, careerInterestId, objective, Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>());
        }

        private static ScriptableObject CreateChain(string id, string careerInterestId, QuestObjectiveBase objective, QuestConditionBase[] blockConditions, QuestConditionBase[] unblockConditions)
        {
            return CreateChain(id, careerInterestId, objective, blockConditions, unblockConditions, Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>());
        }

        private static ScriptableObject CreateChain(string id, string careerInterestId, QuestObjectiveBase objective, QuestConditionBase[] blockConditions, QuestConditionBase[] unblockConditions, QuestConditionBase[] failConditions, QuestConditionBase[] expireConditions)
        {
            var stepType = AssertType("Rootborn.Game.Quests.QuestStepDefinition, Rootborn.Game");
            var chainType = AssertType("Rootborn.Game.Quests.QuestChainDefinition, Rootborn.Game");
            var step = ScriptableObject.CreateInstance(stepType);
            Invoke(step, "ConfigureForTests", "step." + id, "step." + id, "step." + id + ".desc", new[] { objective }, Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), Array.Empty<QuestRewardBase>(), Array.Empty<ScriptableObject>());
            var chain = ScriptableObject.CreateInstance(chainType);
            Invoke(chain, "ConfigureForTests", id, id, id + ".desc", careerInterestId, new[] { step }, Array.Empty<QuestConditionBase>(), unblockConditions, blockConditions, Array.Empty<QuestRewardBase>(), 0, failConditions, expireConditions);
            return chain;
        }

        private static QuestObjectiveBase CreateLocationActivityObjective(LocationActivityDefinition activity, int requiredCount)
        {
            var objective = ScriptableObject.CreateInstance<LocationActivityQuestObjective>();
            objective.ConfigureForRuntime("objective.location", requiredCount, activity);
            return objective;
        }

        private static QuestObjectiveBase CreateAnyEventObjective(int requiredCount)
        {
            var objective = ScriptableObject.CreateInstance<AnyQuestEventObjective>();
            objective.ConfigureForRuntime("objective.any", requiredCount);
            return objective;
        }

        private static object CreateLog(params ScriptableObject[] chains)
        {
            var logType = AssertType("Rootborn.Game.Quests.QuestChainLog, Rootborn.Game");
            return Activator.CreateInstance(logType, new object[] { chains });
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var methodInfo = target.GetType().GetMethod(method, flags);
            Assert.IsNotNull(methodInfo, "Missing method " + target.GetType().FullName + "." + method);
            return methodInfo.Invoke(target, args);
        }

        private static void AssertInvokeTrue(object target, string method, params object[] args)
        {
            Assert.AreEqual(true, Invoke(target, method, args));
        }

        private sealed class AnyQuestEventObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private sealed class StoryFlagQuestChainCondition : QuestConditionBase
        {
            private StoryFlagDefinition _requiredFlag;
            private string _reasonId;
            private string _summary;

            public override string BlockedReasonId => _reasonId;
            public override string BlockedSummary => _summary;

            public void ConfigureForTests(StoryFlagDefinition requiredFlag, string reasonId, string summary)
            {
                _requiredFlag = requiredFlag;
                _reasonId = reasonId;
                _summary = summary;
            }

            public override bool IsSatisfied(in QuestRuntimeContext context)
            {
                return context.StoryFlags != null && context.StoryFlags.IsSet(_requiredFlag);
            }
        }

        private sealed class SwitchQuestChainCondition : QuestConditionBase
        {
            private string _reasonId;
            private string _summary;

            public bool IsOn { get; set; }
            public override string BlockedReasonId => _reasonId;
            public override string BlockedSummary => _summary;

            public void ConfigureForTests(string reasonId, string summary)
            {
                _reasonId = reasonId;
                _summary = summary;
            }

            public override bool IsSatisfied(in QuestRuntimeContext context) => IsOn;
        }
    }
}
