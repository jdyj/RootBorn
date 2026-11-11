using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;

namespace Rootborn.Core.StudentLife
{
    internal static class ExplorationReasons
    {
        public static string OrDefault(string key) => string.IsNullOrEmpty(key) ? "condition.missing" : key;
    }

    public sealed class ExplorationTraitThresholdCondition : IExplorationCondition
    {
        public ExplorationTraitThresholdCondition(string traitId, int minimum, string lockedReasonKey = null)
        {
            TraitId = traitId ?? string.Empty;
            Minimum = minimum;
            LockedReasonKey = ExplorationReasons.OrDefault(lockedReasonKey);
        }

        public string TraitId { get; }
        public int Minimum { get; }
        public string LockedReasonKey { get; }
        public bool Evaluate(in ExplorationInteractionContext context, ExplorationChoice choice) => context.StudentProgress != null && TraitId.Length > 0 && context.StudentProgress.GetTraitValue(TraitId) >= Minimum;
    }

    public sealed class ExplorationItemHeldCondition : IExplorationCondition
    {
        public ExplorationItemHeldCondition(ItemDefinition item, int count, string lockedReasonKey = null)
        {
            Item = item;
            Count = Math.Max(1, count);
            LockedReasonKey = ExplorationReasons.OrDefault(lockedReasonKey);
        }

        public ItemDefinition Item { get; }
        public int Count { get; }
        public string LockedReasonKey { get; }
        public bool Evaluate(in ExplorationInteractionContext context, ExplorationChoice choice) => context.Inventory != null && Item != null && context.Inventory.CountOf(Item) >= Count;
    }

    /// <summary>다른(또는 같은) 지점에서 그 선택지를 고른 적이 있을 때. 기록을 만들지 않고 본다.</summary>
    public sealed class ExplorationPreviousChoiceCondition : IExplorationCondition
    {
        public ExplorationPreviousChoiceCondition(string interactionId, string choiceId, string lockedReasonKey = null)
        {
            InteractionId = interactionId ?? string.Empty;
            ChoiceId = choiceId ?? string.Empty;
            LockedReasonKey = ExplorationReasons.OrDefault(lockedReasonKey);
        }

        public string InteractionId { get; }
        public string ChoiceId { get; }
        public string LockedReasonKey { get; }

        public bool Evaluate(in ExplorationInteractionContext context, ExplorationChoice choice)
        {
            if (context.ExplorationProgress == null || InteractionId.Length == 0 || ChoiceId.Length == 0) return false;
            return context.ExplorationProgress.TryGetRecord(InteractionId, out var record) && record.HasSelectedChoice(ChoiceId);
        }
    }

    public sealed class ExplorationTraitDeltaOutcome : IExplorationOutcome
    {
        public ExplorationTraitDeltaOutcome(string traitId, int delta) { TraitId = traitId ?? string.Empty; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }
        public string OutcomeId => TraitId.Length > 0 ? "trait:" + TraitId : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => true;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || TraitId.Length == 0 || Delta == 0) return false;
            context.StudentProgress.AddTrait(TraitId, Delta);
            return true;
        }
    }

    public sealed class ExplorationSkillDeltaOutcome : IExplorationOutcome
    {
        public ExplorationSkillDeltaOutcome(string skillId, int delta) { SkillId = skillId ?? string.Empty; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }
        public string OutcomeId => SkillId.Length > 0 ? "skill:" + SkillId : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => true;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || SkillId.Length == 0 || Delta == 0) return false;
            context.StudentProgress.AddSkill(SkillId, Delta);
            return true;
        }
    }

    public sealed class ExplorationStatusDeltaOutcome : IExplorationOutcome
    {
        public ExplorationStatusDeltaOutcome(string statusId, int delta) { StatusId = statusId ?? string.Empty; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }
        public string OutcomeId => StatusId.Length > 0 ? "status:" + StatusId : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => true;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || StatusId.Length == 0 || Delta == 0) return false;
            context.StudentProgress.AddStatus(StatusId, Delta);
            collector?.AddFatigueStatus(StatusId);
            return true;
        }
    }

    /// <summary>진로 힌트를 연다. 처음 열 때만 새 결과로 센다.</summary>
    public sealed class ExplorationCareerHintOutcome : IExplorationOutcome
    {
        public ExplorationCareerHintOutcome(string careerId) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }
        public string OutcomeId => CareerId.Length > 0 ? "career:" + CareerId : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => true;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (context.StudentProgress == null || CareerId.Length == 0) return false;
            bool already = context.StudentProgress.IsCareerHintUnlocked(CareerId);
            context.StudentProgress.UnlockCareerHint(CareerId);
            if (!already) collector?.AddCareerHint(CareerId);
            return !already;
        }
    }

    /// <summary>물건을 준다(수량 1 이상). 인벤토리가 없으면 확인은 통과하고 적용은 하지 않는다(원본과 같음).</summary>
    public sealed class ExplorationItemGrantOutcome : IExplorationOutcome, IExplorationInventoryChange
    {
        public ExplorationItemGrantOutcome(ItemDefinition item, int count) { Item = item; Count = Math.Max(1, count); }
        public ItemDefinition Item { get; }
        public int Count { get; }
        public string OutcomeId => Item != null ? "item-grant:" + Item.Id : string.Empty;

        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => context.Inventory == null || Item == null || context.Inventory.CanAdd(Item, Count);

        public void AddTo(List<InventoryGrant> costs, List<InventoryGrant> rewards) { if (Item != null) rewards.Add(new InventoryGrant(Item, Count)); }

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (context.Inventory == null || Item == null) return false;
            context.Inventory.Add(Item, Count);
            collector?.AddItemGrant(Item.Id + ":" + Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
    }

    /// <summary>물건을 쓴다(수량 1 이상). 가진 것이 모자라면 고를 수 없다.</summary>
    public sealed class ExplorationItemSpendOutcome : IExplorationOutcome, IExplorationInventoryChange
    {
        public ExplorationItemSpendOutcome(ItemDefinition item, int count) { Item = item; Count = Math.Max(1, count); }
        public ItemDefinition Item { get; }
        public int Count { get; }
        public string OutcomeId => Item != null ? "item-spend:" + Item.Id : string.Empty;

        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => context.Inventory != null && Item != null && context.Inventory.CountOf(Item) >= Count;

        public void AddTo(List<InventoryGrant> costs, List<InventoryGrant> rewards) { if (Item != null) costs.Add(new InventoryGrant(Item, Count)); }

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (!CanApply(in context, interaction, choice)) return false;
            bool removed = context.Inventory.Remove(Item, Count);
            if (removed) collector?.AddItemSpend(Item.Id + ":" + Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return removed;
        }
    }

    /// <summary>단서를 본 것으로 남긴다. 처음 볼 때만 새 결과로 센다. 출처를 비우면 단서의 첫 출처(구현 쪽이 정함).</summary>
    public sealed class ExplorationClueOutcome : IExplorationOutcome
    {
        public ExplorationClueOutcome(string clueId, string sourceId) { ClueId = clueId ?? string.Empty; SourceId = sourceId ?? string.Empty; }
        public string ClueId { get; }
        public string SourceId { get; }
        public string OutcomeId => ClueId.Length > 0 ? "clue:" + ClueId : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => ClueId.Length > 0;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (ClueId.Length == 0) return false;
            bool alreadySeen = context.Clues != null && context.Clues.HasSeen(ClueId);
            context.Clues?.MarkSourceSeen(ClueId, SourceId, context.CurrentDay);
            collector?.AddClue(ClueId);
            return !alreadySeen;
        }
    }

    /// <summary>도감 항목을 연다(공개 단계 1 이상). 도감 진행이 없으면 새 결과로 센다(원본과 같음).</summary>
    public sealed class ExplorationEncyclopediaOutcome : IExplorationOutcome
    {
        public ExplorationEncyclopediaOutcome(string entryId, int revealStage) { EntryId = entryId ?? string.Empty; RevealStage = Math.Max(1, revealStage); }
        public string EntryId { get; }
        public int RevealStage { get; }
        public string OutcomeId => EntryId.Length > 0 ? "encyclopedia:" + EntryId : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => EntryId.Length > 0;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (EntryId.Length == 0) return false;
            bool applied = context.Encyclopedia == null || context.Encyclopedia.TryUnlock(EntryId, RevealStage);
            collector?.AddEncyclopediaEntry(EntryId);
            return applied;
        }
    }

    /// <summary>
    /// 이어지는 퀘스트를 퀘스트 기록에 더하고 받는다. 퀘스트 기록이 없으면 새 결과로 센다(원본과 같음).
    /// 이미 시작한 퀘스트는 건드리지 않는다(원본은 다시 더해 진행을 지우고 다시 받게 했다).
    /// </summary>
    public sealed class ExplorationFollowUpQuestOutcome : IExplorationOutcome
    {
        public ExplorationFollowUpQuestOutcome(QuestDefinition quest) { Quest = quest; }
        public QuestDefinition Quest { get; }
        public string OutcomeId => Quest != null ? "quest:" + Quest.Id : string.Empty;
        public bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice) => Quest != null;

        public bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector)
        {
            if (Quest == null || Quest.Id.Length == 0) return false;
            bool accepted = false;
            if (context.QuestLog != null && context.QuestLog.GetState(Quest.Id) == QuestStage.NotStarted)
            {
                context.QuestLog.AddQuest(Quest);
                accepted = context.QuestLog.Accept(Quest);
            }
            collector?.AddFollowUpQuest(Quest.Id);
            return context.QuestLog == null || accepted;
        }
    }
}
