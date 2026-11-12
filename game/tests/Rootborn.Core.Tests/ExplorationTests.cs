using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/ExplorationInteractionChoiceTests.cs, ExplorationDismissChoiceTests.cs
    // (등록부·직렬화 필드·소스 문자열 검사는 옮기지 않음. 단서·도감 진행은 아직 Core에 없어 테스트용 조회 객체로 바꿨다)
    public sealed class ExplorationTests
    {
        private static ExplorationChoice Choice(string id, IExplorationCondition[] conditions = null, IExplorationOutcome[] outcomes = null, ExplorationRiskPolicy risk = null,
            bool repeatable = false, bool dismisses = false)
            => new ExplorationChoice(id, id, id + ".desc", conditions, outcomes, risk, repeatable, id + ".preview", dismisses);

        private static ExplorationInteraction Interaction(string id, ExplorationRiskPolicy risk = null, bool repeatable = false, int cooldownDays = 0, params ExplorationChoice[] choices)
            => new ExplorationInteraction(id, id, id + ".desc", repeatable, cooldownDays, risk, choices);

        private static ExplorationRiskPolicy Risk(int stress, int energy, int focus) => new ExplorationRiskPolicy(stress, energy, focus, null, 0);

        private sealed class Clues : IExplorationClues
        {
            public readonly List<string> Seen = new List<string>();
            public bool HasSeen(string clueId) => Seen.Contains(clueId);
            public void MarkSourceSeen(string clueId, string sourceId, int day) { if (!Seen.Contains(clueId)) Seen.Add(clueId); }
        }

        private sealed class Encyclopedia : IExplorationEncyclopedia
        {
            public readonly List<string> Unlocked = new List<string>();
            public bool TryUnlock(string entryId, int revealStage)
            {
                if (Unlocked.Contains(entryId + "@" + revealStage)) return false;
                Unlocked.Add(entryId + "@" + revealStage);
                return true;
            }
        }

        private static ExplorationInteractionContext Context(int energy, int focus, int day = 1, ItemInventory inventory = null)
            => new ExplorationInteractionContext(new ExplorationProgress("fixture-slot", "fixture-player"), new StudentLifeProgress("fixture-slot", "fixture-player", energy, focus, 7, 480),
                inventory ?? new ItemInventory(), new QuestLog(Array.Empty<QuestDefinition>()), new Clues(), new Encyclopedia(), day, 480);

        private static string Snapshot(in ExplorationInteractionContext context)
        {
            var s = context.StudentProgress;
            var records = context.ExplorationProgress.ToSaveData().Records;
            return string.Join("|", s.Energy, s.Focus, s.Stress, s.TimeMinutes, string.Join(",", s.GetTraitIds().Select(id => id + "=" + s.GetTraitValue(id))),
                string.Join(",", s.GetActivityLogIds()), string.Join(",", s.GetTodayResultLogIds()),
                string.Join(",", records.Select(r => r.InteractionId + ":" + r.RepeatCount + ":" + string.Join("+", r.SelectedChoiceIds))),
                string.Join(",", context.ExplorationProgress.TodayExplorationSummary),
                string.Join(",", context.Inventory.Slots.Select(slot => (slot.Item != null ? slot.Item.Id : "<empty>") + ":" + slot.Count)));
        }

        private static void AssertNoOutcomes(ExplorationChoiceResult result)
        {
            Assert.That(new[] { result.OutcomeIds, result.ItemGrantIds, result.ItemSpendIds, result.ClueIds, result.EncyclopediaEntryIds, result.CareerHintIds, result.FollowUpQuestIds, result.FatigueStatusIds },
                Is.All.Empty);
        }

        private static void AssertDismissed(ExplorationChoiceResult result, string interactionId, string choiceId)
        {
            Assert.That(result.Kind, Is.EqualTo(ExplorationChoiceResultKind.Dismissed));
            Assert.That(result.InteractionId, Is.EqualTo(interactionId));
            Assert.That(result.ChoiceId, Is.EqualTo(choiceId));
            Assert.That(result.LockedReasonKey, Is.Empty);
            AssertNoOutcomes(result);
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_002_003_005_ConditionsAndOutcomesApplyWithoutEntityBranches()
        {
            var condition = new ExplorationTraitThresholdCondition("trait.curiosity", 2, "need.curiosity");
            var choice = Choice("choice.inspect", new IExplorationCondition[] { condition },
                new IExplorationOutcome[] { new ExplorationTraitDeltaOutcome("trait.curiosity", 1), new ExplorationCareerHintOutcome("career.investigator") });
            var interaction = Interaction("exploration.alley-crate", null, false, 0, choice);
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var context = new ExplorationInteractionContext(new ExplorationProgress("slot-a", "player-a"), student, new ItemInventory(), null, null, null, 1, 480);
            var runner = new ExplorationInteractionRunner();

            Assert.IsFalse(runner.TryChoose(interaction, choice, context, out var locked));
            Assert.AreEqual(ExplorationChoiceResultKind.ConditionFailed, locked.Kind);
            Assert.AreEqual("need.curiosity", locked.LockedReasonKey);

            student.AddTrait("trait.curiosity", 2);
            Assert.IsTrue(runner.TryChoose(interaction, choice, context, out var result));
            Assert.AreEqual(ExplorationChoiceResultKind.Applied, result.Kind);
            Assert.AreEqual(3, student.GetTraitValue("trait.curiosity"));
            Assert.IsTrue(student.IsCareerHintUnlocked("career.investigator"));
            CollectionAssert.Contains(result.CareerHintIds, "career.investigator");
            CollectionAssert.Contains(result.OutcomeIds, "trait:trait.curiosity");
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_004_006_007_008_ProgressPersistsRepeatPolicyAndDedupe()
        {
            var item = new ItemDefinition("item.old-coin", 99);
            var choice = Choice("choice.open", null, new IExplorationOutcome[] { new ExplorationItemGrantOutcome(item, 1) }, new ExplorationRiskPolicy(2, 1, 0, null, 0));
            var interaction = Interaction("exploration.alley-crate", null, false, 0, choice, choice, choice);
            var inventory = new ItemInventory();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var progress = new ExplorationProgress("slot-a", "player-a");
            var context = new ExplorationInteractionContext(progress, student, inventory, null, null, null, 3, 615);
            var runner = new ExplorationInteractionRunner();

            Assert.AreEqual(3, interaction.Choices.Count);
            Assert.IsTrue(runner.TryChoose(interaction, choice, context, out var first));
            Assert.IsFalse(runner.TryChoose(interaction, choice, context, out var duplicate));
            var record = ExplorationProgress.FromSaveData(progress.ToSaveData()).GetRecord(interaction.Id);

            Assert.AreEqual(ExplorationChoiceResultKind.Applied, first.Kind);
            Assert.AreEqual(ExplorationChoiceResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(1, inventory.CountOf(item));
            Assert.IsTrue(record.Completed);
            CollectionAssert.Contains(record.SelectedChoiceIds, choice.Id);
            Assert.IsTrue(record.IsRewardClaimed(choice.Id));
            Assert.AreEqual(1, record.RepeatCount);
            Assert.AreEqual(3, record.FirstCompletedDay);
            Assert.AreEqual(615, record.LastCompletedTimeMinutes);
            Assert.AreEqual(2, student.Stress);
            Assert.AreEqual(9, student.Energy);
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_009_SummaryBuilderUsesLookupCacheAndDoesNotScanRegistryEveryCall()
        {
            var choice = Choice("choice.observe");
            var interaction = Interaction("exploration.test", null, true, 2, choice);
            var cache = new ExplorationLookupCache(new[] { interaction });

            var summary = ExplorationSummaryBuilder.Build("exploration.test", cache, new ExplorationProgress("slot-a", "player-a"), null, null, null, null, 1, 0);

            Assert.AreEqual("exploration.test", summary.InteractionId);
            Assert.AreEqual(1, cache.LookupCount);
            Assert.AreEqual(1, summary.Choices.Length);
            Assert.IsTrue(summary.Choices[0].Available);
            Assert.AreEqual("choice.observe.preview", summary.Choices[0].PreviewHintKey);
        }

        [Test]
        public void ResultKindOrdinalsStayStable()
        {
            Assert.That((int)ExplorationChoiceResultKind.Applied, Is.EqualTo(0));
            Assert.That((int)ExplorationChoiceResultKind.InvalidRequest, Is.EqualTo(1));
            Assert.That((int)ExplorationChoiceResultKind.ConditionFailed, Is.EqualTo(2));
            Assert.That((int)ExplorationChoiceResultKind.DuplicateRequest, Is.EqualTo(3));
            Assert.That((int)ExplorationChoiceResultKind.CooldownActive, Is.EqualTo(4));
            Assert.That((int)ExplorationChoiceResultKind.InsufficientResources, Is.EqualTo(5));
            Assert.That((int)ExplorationChoiceResultKind.Dismissed, Is.EqualTo(6));
        }

        [Test]
        public void DismissAtZeroEnergyBypassesDefaultRiskAndCreatesNoRecordActivityOrOutcome()
        {
            var dismiss = Choice("fixture.any-dismiss-name", dismisses: true);
            var interaction = Interaction("fixture.generic-site", Risk(3, 4, 2), false, 0, dismiss);
            var context = Context(0, 0);
            string before = Snapshot(context);
            int vitals = 0;
            context.StudentProgress.VitalsChanged += () => vitals++;

            Assert.That(new ExplorationInteractionRunner().TryChoose(interaction, dismiss, context, out var result), Is.True);
            AssertDismissed(result, interaction.Id, dismiss.Id);
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);
            Assert.That(vitals, Is.Zero);
        }

        [Test]
        public void RepeatedDismissalStillWorksAfterCompletionAndDuringCooldownWithoutChangingPriorRecords()
        {
            foreach (bool cooldown in new[] { false, true })
            {
                var dismiss = Choice(cooldown ? "fixture.dismiss.cooldown" : "fixture.dismiss.completed", dismisses: true);
                var interaction = Interaction(cooldown ? "fixture.cooldown" : "fixture.once", Risk(1, 1, 1), cooldown, cooldown ? 5 : 0, dismiss);
                var context = Context(0, 0, day: 3);
                context.ExplorationProgress.MarkCompleted(interaction.Id, "fixture.previously-earned", 3, 450, new ExplorationOutcomeCollector());
                string before = Snapshot(context);
                var runner = new ExplorationInteractionRunner();
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Assert.That(runner.TryChoose(interaction, dismiss, context, out var result), Is.True);
                    AssertDismissed(result, interaction.Id, dismiss.Id);
                    Assert.That(Snapshot(context), Is.EqualTo(before));
                }
                var record = context.ExplorationProgress.ToSaveData().Records.Single();
                Assert.That(record.RepeatCount, Is.EqualTo(1));
                Assert.That(record.SelectedChoiceIds, Is.EqualTo(new[] { "fixture.previously-earned" }));
            }
        }

        [Test]
        public void DismissalStillHonorsAnAuthoredTraitConditionWithoutMutatingRejectedOrAcceptedState()
        {
            var dismiss = Choice("fixture.conditioned", new IExplorationCondition[] { new ExplorationTraitThresholdCondition("fixture.trait", 2, "fixture.requires-trait") }, dismisses: true);
            var interaction = Interaction("fixture.conditioned-site", Risk(1, 1, 0), false, 0, dismiss);
            var context = Context(0, 0);
            var runner = new ExplorationInteractionRunner();
            string beforeRejected = Snapshot(context);

            Assert.That(runner.TryChoose(interaction, dismiss, context, out var rejected), Is.False);
            Assert.That(rejected.Kind, Is.EqualTo(ExplorationChoiceResultKind.ConditionFailed));
            Assert.That(rejected.LockedReasonKey, Is.EqualTo("fixture.requires-trait"));
            AssertNoOutcomes(rejected);
            Assert.That(Snapshot(context), Is.EqualTo(beforeRejected));

            context.StudentProgress.AddTrait("fixture.trait", 2);
            string beforeAccepted = Snapshot(context);
            Assert.That(runner.TryChoose(interaction, dismiss, context, out var accepted), Is.True);
            AssertDismissed(accepted, interaction.Id, dismiss.Id);
            Assert.That(Snapshot(context), Is.EqualTo(beforeAccepted));
        }

        [Test]
        public void MissingPreviousChoicePrerequisiteDoesNotCreateARecordAndEarnedPrerequisiteStillAllowsDismissal()
        {
            var condition = new ExplorationPreviousChoiceCondition("fixture.prerequisite-site", "fixture.prerequisite-choice", "fixture.requires-previous");
            var dismiss = Choice("fixture.previous-conditioned", new IExplorationCondition[] { condition }, dismisses: true);
            var interaction = Interaction("fixture.current-site", null, false, 0, dismiss);
            var context = Context(0, 0);
            var runner = new ExplorationInteractionRunner();
            string missing = Snapshot(context);

            Assert.That(runner.TryChoose(interaction, dismiss, context, out var rejected), Is.False);
            Assert.That(rejected.Kind, Is.EqualTo(ExplorationChoiceResultKind.ConditionFailed));
            Assert.That(rejected.LockedReasonKey, Is.EqualTo("fixture.requires-previous"));
            Assert.That(Snapshot(context), Is.EqualTo(missing));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);

            context.ExplorationProgress.MarkCompleted("fixture.prerequisite-site", "fixture.prerequisite-choice", 1, 420, new ExplorationOutcomeCollector());
            string earned = Snapshot(context);
            Assert.That(runner.TryChoose(interaction, dismiss, context, out var result), Is.True);
            AssertDismissed(result, interaction.Id, dismiss.Id);
            Assert.That(Snapshot(context), Is.EqualTo(earned));
        }

        [Test]
        public void SummaryQueryDoesNotCreateRecordsAndStillReportsExistingCompletion()
        {
            var choice = Choice("fixture.summary-choice");
            var interaction = Interaction("fixture.summary-site", Risk(1, 5, 5), false, 0, choice);
            var context = Context(0, 0);
            var cache = new ExplorationLookupCache(new[] { interaction });
            string before = Snapshot(context);

            var summary = ExplorationSummaryBuilder.Build(interaction.Id, cache, context.ExplorationProgress, context.StudentProgress, context.Inventory, context.QuestLog, context.Clues, 1, 480);
            Assert.That(summary.Completed, Is.False);
            Assert.That(summary.Choices.Single().Available, Is.True);
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);

            context.ExplorationProgress.MarkCompleted(interaction.Id, choice.Id, 1, 480, new ExplorationOutcomeCollector());
            var after = ExplorationSummaryBuilder.Build(interaction.Id, cache, context.ExplorationProgress, context.StudentProgress, context.Inventory, context.QuestLog, context.Clues, 1, 480);
            Assert.That(after.Completed, Is.True);
            Assert.That(cache.LookupCount, Is.EqualTo(2));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MisconfiguredDismissalWithItsOwnRiskOrOutcomeIsRejectedWithoutDiscardingAuthoredEffects(bool ownRisk)
        {
            var dismiss = Choice("fixture.misconfigured", outcomes: ownRisk ? null : new IExplorationOutcome[] { new ExplorationTraitDeltaOutcome("fixture.reward-trait", 3) },
                risk: ownRisk ? Risk(2, 1, 1) : null, dismisses: true);
            var interaction = Interaction("fixture.invalid-dismiss", null, false, 0, dismiss);
            var context = Context(10, 10);
            string before = Snapshot(context);

            Assert.That(new ExplorationInteractionRunner().TryChoose(interaction, dismiss, context, out var result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(ExplorationChoiceResultKind.InvalidRequest));
            AssertNoOutcomes(result);
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);
        }

        [Test]
        public void OrdinaryChoiceStillPaysFallbackRiskAppliesOutcomeRecordsActivityAndDeduplicates()
        {
            var outcome = new ExplorationTraitDeltaOutcome("fixture.normal-trait", 3);
            var choice = Choice("fixture.normal-choice", outcomes: new IExplorationOutcome[] { outcome });
            var interaction = Interaction("fixture.normal-site", Risk(2, 1, 1), false, 0, choice);
            var context = Context(10, 10);
            var runner = new ExplorationInteractionRunner();

            Assert.That(runner.TryChoose(interaction, choice, context, out var first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(ExplorationChoiceResultKind.Applied));
            Assert.That(context.StudentProgress.Energy, Is.EqualTo(9));
            Assert.That(context.StudentProgress.Focus, Is.EqualTo(9));
            Assert.That(context.StudentProgress.Stress, Is.EqualTo(9));
            Assert.That(context.StudentProgress.GetTraitValue("fixture.normal-trait"), Is.EqualTo(3));
            Assert.That(context.StudentProgress.GetTodayActivityIds(), Is.EqualTo(new[] { interaction.Id }));
            Assert.That(first.OutcomeIds, Does.Contain(outcome.OutcomeId));
            Assert.That(first.FatigueStatusIds, Is.EqualTo(new[] { "stress:2", "energy:-1", "focus:-1" }));
            var saved = context.ExplorationProgress.ToSaveData().Records.Single();
            Assert.That(saved.Completed, Is.True);
            Assert.That(saved.RepeatCount, Is.EqualTo(1));
            Assert.That(saved.RewardClaimedChoiceIds, Is.EqualTo(new[] { choice.Id }));
            string applied = Snapshot(context);

            Assert.That(runner.TryChoose(interaction, choice, context, out var duplicate), Is.False);
            Assert.That(duplicate.Kind, Is.EqualTo(ExplorationChoiceResultKind.DuplicateRequest));
            Assert.That(Snapshot(context), Is.EqualTo(applied));
        }

        // 더한 경계

        [Test]
        public void SeveralItemGrantsThatDoNotFitTogetherAreRejectedWithoutPartialGrant()
        {
            // Unity 판과 달라진 동작: 원본은 아이템 결과를 하나씩만 확인해 함께 넣으면 넘치는 경우 일부만 지급했다.
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots - 1; i++) inventory.Add(new ItemDefinition("item.filler." + i, 1), 1);
            var a = new ItemDefinition("item.a", 1);
            var b = new ItemDefinition("item.b", 1);
            var choice = Choice("choice.loot", outcomes: new IExplorationOutcome[] { new ExplorationItemGrantOutcome(a, 1), new ExplorationItemGrantOutcome(b, 1), new ExplorationTraitDeltaOutcome("t", 1) });
            var interaction = Interaction("site", Risk(1, 1, 0), false, 0, choice);
            var context = Context(5, 5, inventory: inventory);
            string before = Snapshot(context);

            Assert.That(new ExplorationInteractionRunner().TryChoose(interaction, choice, context, out var result), Is.False);
            Assert.That(result.Kind, Is.EqualTo(ExplorationChoiceResultKind.InsufficientResources));
            Assert.That(result.LockedReasonKey, Is.EqualTo("exploration.outcome.blocked"));
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(inventory.CountOf(a) + inventory.CountOf(b), Is.Zero);
            // Unity 판과 달라진 동작: 거절해도 빈 탐색 기록을 남기지 않는다.
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);
        }

        [Test]
        public void SpendAndGrantInTheSameChoiceApplyTogether()
        {
            var inventory = new ItemInventory();
            var key = new ItemDefinition("item.key", 1);
            inventory.Add(key, 1);
            var prize = new ItemDefinition("item.prize", 1);
            var choice = Choice("choice.trade", new IExplorationCondition[] { new ExplorationItemHeldCondition(key, 1, "need.key") },
                new IExplorationOutcome[] { new ExplorationItemSpendOutcome(key, 1), new ExplorationItemGrantOutcome(prize, 1) });
            var context = Context(5, 5, inventory: inventory);

            Assert.That(new ExplorationInteractionRunner().TryChoose(Interaction("site", null, false, 0, choice), choice, context, out var result), Is.True);
            Assert.That(inventory.CountOf(key), Is.Zero);
            Assert.That(inventory.CountOf(prize), Is.EqualTo(1));
            Assert.That(result.ItemSpendIds, Is.EqualTo(new[] { "item.key:1" }));
            Assert.That(result.ItemGrantIds, Is.EqualTo(new[] { "item.prize:1" }));
        }

        /// <summary>
        /// 지갑이 "될 수 있다"고 답한 뒤 실제 바꾸기에서 거절하는 경우(같이 하는 방의 방 지갑: 허락 전에는 잡기만 적고 거절한다).
        /// </summary>
        private sealed class RefusingWallet : IWallet
        {
            public int Balance { get; set; } = 50;
            public int Attempts { get; private set; }
            public bool CanExchange(int cost, int reward) => cost >= 0 && reward >= 0 && cost <= Balance;
            public bool TryExchange(int cost, int reward, Action commit)
            {
                Attempts++;
                return false;
            }
            public event Action Changed { add { } remove { } }
        }

        [Test]
        public void SpendingMoneyThatTheWalletRefusesAtTheLastStep_ChangesNothing_NotEvenTheOtherOutcomes()
        {
            var money = new ItemDefinition("item.money", 9999);
            var wallet = new RefusingWallet();
            var inventory = new ItemInventory(wallet, money);
            var prize = new ItemDefinition("item.prize", 1);
            var choice = Choice("choice.pay", outcomes: new IExplorationOutcome[]
            {
                new ExplorationItemSpendOutcome(money, 10), new ExplorationItemGrantOutcome(prize, 1), new ExplorationTraitDeltaOutcome("trait.curiosity", 1),
            }, risk: Risk(1, 1, 0));
            var context = Context(5, 5, inventory: inventory);
            string before = Snapshot(context);

            Assert.That(new ExplorationInteractionRunner().TryChoose(Interaction("site", null, false, 0, choice), choice, context, out var result), Is.False);
            Assert.That(result.Kind, Is.Not.EqualTo(ExplorationChoiceResultKind.Applied));
            Assert.That(wallet.Attempts, Is.EqualTo(1), "돈은 다른 바뀜과 한 번에 바꾸려 했다");
            Assert.That(inventory.CountOf(prize), Is.Zero, "물건도 받지 않았다(전부 아니면 전무)");
            Assert.That(Snapshot(context), Is.EqualTo(before), "위험 비용·성향·탐색 기록도 그대로");
        }

        [Test]
        public void SpendingMoneyThroughTheWallet_WithTheOtherOutcomes_HappensInOneExchange()
        {
            var money = new ItemDefinition("item.money", 9999);
            var wallet = new PersonalWallet();
            var inventory = new ItemInventory(wallet, money);
            inventory.Add(money, 30);
            var prize = new ItemDefinition("item.prize", 1);
            var choice = Choice("choice.pay", outcomes: new IExplorationOutcome[] { new ExplorationItemSpendOutcome(money, 10), new ExplorationItemGrantOutcome(prize, 1) });
            var context = Context(5, 5, inventory: inventory);
            Assert.That(new ExplorationInteractionRunner().TryChoose(Interaction("site", null, false, 0, choice), choice, context, out var result), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(20));
            Assert.That(inventory.CountOf(prize), Is.EqualTo(1));
            Assert.That(result.ItemSpendIds, Is.EqualTo(new[] { "item.money:10" }));
            Assert.That(result.ItemGrantIds, Is.EqualTo(new[] { "item.prize:1" }));
            Assert.That(result.OutcomeIds, Is.EqualTo(new[] { "item-spend:item.money", "item-grant:item.prize" }));
        }

        [Test]
        public void CooldownAndInsufficientRiskRejectWithoutChanges()
        {
            var choice = Choice("c", outcomes: new IExplorationOutcome[] { new ExplorationSkillDeltaOutcome("skill.s", 1) }, repeatable: true);
            var interaction = Interaction("site", Risk(0, 3, 0), true, 2, choice);
            var context = Context(5, 5, day: 4);
            var runner = new ExplorationInteractionRunner();

            Assert.That(runner.TryChoose(interaction, choice, context, out _), Is.True);
            var nextDay = new ExplorationInteractionContext(context.ExplorationProgress, context.StudentProgress, context.Inventory, context.QuestLog, context.Clues, context.Encyclopedia, 5, 480);
            string before = Snapshot(nextDay);
            Assert.That(runner.TryChoose(interaction, choice, nextDay, out var cooling), Is.False);
            Assert.That(cooling.Kind, Is.EqualTo(ExplorationChoiceResultKind.CooldownActive));
            Assert.That(cooling.LockedReasonKey, Is.EqualTo("exploration.cooldown"));

            var later = new ExplorationInteractionContext(context.ExplorationProgress, context.StudentProgress, context.Inventory, context.QuestLog, context.Clues, context.Encyclopedia, 6, 480);
            Assert.That(runner.TryChoose(interaction, choice, later, out var tired), Is.False);
            Assert.That(tired.Kind, Is.EqualTo(ExplorationChoiceResultKind.InsufficientResources));
            Assert.That(Snapshot(later), Is.EqualTo(before));
            Assert.That(runner.TryChoose(null, choice, later, out var invalid), Is.False);
            Assert.That(invalid.LockedReasonKey, Is.EqualTo("exploration.invalid"));
        }

        [Test]
        public void ClueEncyclopediaAndFollowUpQuestOutcomesGoThroughTheirOwnProgress()
        {
            var quest = new QuestDefinition("quest.follow", "q", "q", Array.Empty<QuestObjective>());
            var choice = Choice("c", outcomes: new IExplorationOutcome[]
            {
                new ExplorationClueOutcome("clue.ledger", "source.crate"), new ExplorationEncyclopediaOutcome("entry.ledger", 0), new ExplorationFollowUpQuestOutcome(quest),
                new ExplorationStatusDeltaOutcome("status.dusty", 1),
            }, repeatable: true);
            var interaction = Interaction("site", null, true, 0, choice);
            var context = Context(5, 5);
            var runner = new ExplorationInteractionRunner();

            Assert.That(runner.TryChoose(interaction, choice, context, out var first), Is.True);
            Assert.That(first.OutcomeIds, Is.EqualTo(new[] { "clue:clue.ledger", "encyclopedia:entry.ledger", "quest:quest.follow", "status:status.dusty" }));
            Assert.That(context.QuestLog.GetState("quest.follow"), Is.EqualTo(QuestStage.Active));
            Assert.That(((Encyclopedia)context.Encyclopedia).Unlocked, Is.EqualTo(new[] { "entry.ledger@1" }));
            Assert.That(first.FatigueStatusIds, Is.EqualTo(new[] { "status.dusty" }));

            Assert.That(runner.TryChoose(interaction, choice, context, out var second), Is.True);
            Assert.That(second.OutcomeIds, Is.EqualTo(new[] { "status:status.dusty" }), "이미 본 단서, 열린 도감, 받은 퀘스트는 새 결과로 세지 않는다.");
            Assert.That(second.ClueIds, Is.EqualTo(new[] { "clue.ledger" }));
            var record = ExplorationProgress.FromSaveData(context.ExplorationProgress.ToSaveData()).GetRecord("site");
            Assert.That(record.RepeatCount, Is.EqualTo(2));
            Assert.That(record.DiscoveredClueIds, Is.EqualTo(new[] { "clue.ledger" }));
            Assert.That(record.FollowUpQuestIds, Is.EqualTo(new[] { "quest.follow" }));

            // Unity 판과 달라진 동작: 이미 시작한(끝낸) 퀘스트를 다시 더해 진행을 지우지 않는다.
            context.QuestLog.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { new QuestProgressSaveData { QuestId = "quest.follow", State = QuestStage.RewardClaimed, StateName = "RewardClaimed" } } });
            Assert.That(runner.TryChoose(interaction, choice, context, out _), Is.True);
            Assert.That(context.QuestLog.GetState("quest.follow"), Is.EqualTo(QuestStage.RewardClaimed));
        }

        [Test]
        public void ProgressSaveRoundTripKeepsHistoryAndDefaultsMissingSave()
        {
            var collector = new ExplorationOutcomeCollector();
            collector.AddItemGrant("item.a:1");
            collector.AddCareerHint("career.c");
            collector.AddFatigueStatus("stress:1");
            collector.AddFatigueStatus("stress:1");
            var progress = new ExplorationProgress(null, "");
            progress.MarkCompleted("site", "choice", 0, -5, collector);
            progress.MarkCompleted("", "choice", 1, 0, null);

            var saved = progress.ToSaveData();
            Assert.That(saved.SaveSlot, Is.EqualTo("default"));
            Assert.That(saved.Records.Length, Is.EqualTo(1));
            Assert.That(saved.TodayExplorationSummary, Is.EqualTo(new[] { "site:choice" }));
            var record = ExplorationProgress.FromSaveData(saved).GetRecord("site");
            Assert.That(record.FirstCompletedDay, Is.EqualTo(1));
            Assert.That(record.FirstCompletedTimeMinutes, Is.EqualTo(0));
            Assert.That(record.ItemGrantHistory, Is.EqualTo(new[] { "item.a:1" }));
            Assert.That(record.CareerHintGrantHistory, Is.EqualTo(new[] { "career.c" }));
            Assert.That(record.FatigueStatusOutcomeHistory, Is.EqualTo(new[] { "stress:1" }));
            Assert.That(ExplorationProgress.FromSaveData(null).PlayerId, Is.EqualTo("player"));
            Assert.That(new ExplorationProgress("s", "p").TryGetRecord("", out _), Is.False);
        }
    }
}
