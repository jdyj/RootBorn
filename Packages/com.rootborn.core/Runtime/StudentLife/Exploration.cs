using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;

namespace Rootborn.Core.StudentLife
{
    /// <summary>발견 단서 진행(단서 영역은 아직 Core에 없다). 출처를 비우면 구현 쪽이 단서의 첫 출처를 쓴다.</summary>
    public interface IExplorationClues
    {
        bool HasSeen(string clueId);
        void MarkSourceSeen(string clueId, string sourceId, int day);
    }

    /// <summary>도감 진행(도감 영역은 아직 Core에 없다). 시각은 구현 쪽이 정한다. 새로 열었으면 true.</summary>
    public interface IExplorationEncyclopedia
    {
        bool TryUnlock(string entryId, int revealStage);
    }

    public readonly struct ExplorationInteractionContext
    {
        public readonly ExplorationProgress ExplorationProgress;
        public readonly StudentLifeProgress StudentProgress;
        public readonly ItemInventory Inventory;
        public readonly QuestLog QuestLog;
        public readonly IExplorationClues Clues;
        public readonly IExplorationEncyclopedia Encyclopedia;
        public readonly int CurrentDay;
        public readonly int CurrentTimeMinutes;

        public ExplorationInteractionContext(ExplorationProgress explorationProgress, StudentLifeProgress studentProgress, ItemInventory inventory, QuestLog questLog,
            IExplorationClues clues, IExplorationEncyclopedia encyclopedia, int currentDay, int currentTimeMinutes)
        {
            ExplorationProgress = explorationProgress;
            StudentProgress = studentProgress;
            Inventory = inventory;
            QuestLog = questLog;
            Clues = clues;
            Encyclopedia = encyclopedia;
            CurrentDay = Math.Max(1, currentDay);
            CurrentTimeMinutes = Math.Max(0, currentTimeMinutes);
        }
    }

    /// <summary>선택지 조건. 만족하지 않으면 잠긴 이유 키를 보여 준다.</summary>
    public interface IExplorationCondition
    {
        string LockedReasonKey { get; }
        bool Evaluate(in ExplorationInteractionContext context, ExplorationChoice choice);
    }

    /// <summary>선택지 결과. 먼저 모두 CanApply를 확인한 뒤 Apply한다. Apply가 true면 새 결과로 세어 OutcomeId를 남긴다.</summary>
    public interface IExplorationOutcome
    {
        string OutcomeId { get; }
        bool CanApply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice);
        bool Apply(in ExplorationInteractionContext context, ExplorationInteraction interaction, ExplorationChoice choice, ExplorationOutcomeCollector collector);
    }

    /// <summary>인벤토리를 바꾸는 결과. 실행기가 한 선택지의 모든 지급·소모를 합쳐 한 번에 들어가는지 확인한다.</summary>
    public interface IExplorationInventoryChange
    {
        void AddTo(List<InventoryGrant> costs, List<InventoryGrant> rewards);
    }

    /// <summary>위험 비용: 스트레스·체력·집중과 상태 하나. 시간은 쓰지 않는다.</summary>
    public sealed class ExplorationRiskPolicy
    {
        public ExplorationRiskPolicy(int stressDelta, int energyCost, int focusCost, string statusId, int statusDelta)
        {
            StressDelta = stressDelta;
            RawEnergyCost = energyCost;
            RawFocusCost = focusCost;
            StatusId = statusId ?? string.Empty;
            StatusDelta = statusDelta;
        }

        public int StressDelta { get; }
        public int RawEnergyCost { get; }
        public int RawFocusCost { get; }
        public int EnergyCost => Math.Max(0, RawEnergyCost);
        public int FocusCost => Math.Max(0, RawFocusCost);
        public string StatusId { get; }
        public int StatusDelta { get; }

        public bool CanApply(in ExplorationInteractionContext context) => context.StudentProgress == null || context.StudentProgress.CanSpend(EnergyCost, FocusCost);

        /// <summary>원본처럼 원래 값이 0이 아니면 기록한다(음수 비용이면 "energy:-0"으로 남는다).</summary>
        public void Apply(in ExplorationInteractionContext context, ExplorationOutcomeCollector collector)
        {
            var progress = context.StudentProgress;
            if (progress == null) return;
            progress.Spend(0, EnergyCost, FocusCost, StressDelta);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (StressDelta != 0) collector?.AddFatigueStatus("stress:" + StressDelta.ToString(inv));
            if (RawEnergyCost != 0) collector?.AddFatigueStatus("energy:-" + EnergyCost.ToString(inv));
            if (RawFocusCost != 0) collector?.AddFatigueStatus("focus:-" + FocusCost.ToString(inv));
            if (StatusId.Length > 0 && StatusDelta != 0)
            {
                progress.AddStatus(StatusId, StatusDelta);
                collector?.AddFatigueStatus(StatusId);
            }
        }
    }

    public sealed class ExplorationChoice
    {
        private readonly IExplorationCondition[] _conditions;
        private readonly IExplorationOutcome[] _outcomes;

        public ExplorationChoice(string id, string displayNameKey, string descriptionKey, IExplorationCondition[] conditions, IExplorationOutcome[] outcomes, ExplorationRiskPolicy riskPolicy,
            bool repeatable, string previewHintKey, bool dismissesInteraction = false)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            PreviewHintKey = string.IsNullOrEmpty(previewHintKey) ? DescriptionKey : previewHintKey;
            _conditions = conditions ?? Array.Empty<IExplorationCondition>();
            _outcomes = outcomes ?? Array.Empty<IExplorationOutcome>();
            RiskPolicy = riskPolicy;
            Repeatable = repeatable;
            DismissesInteraction = dismissesInteraction;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string PreviewHintKey { get; }
        public IReadOnlyList<IExplorationCondition> Conditions => _conditions;
        public IReadOnlyList<IExplorationOutcome> Outcomes => _outcomes;
        public ExplorationRiskPolicy RiskPolicy { get; }
        public bool Repeatable { get; }
        public bool DismissesInteraction { get; }

        public bool ConditionsSatisfied(in ExplorationInteractionContext context, out string lockedReasonKey)
        {
            lockedReasonKey = string.Empty;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition == null || condition.Evaluate(in context, this)) continue;
                lockedReasonKey = condition.LockedReasonKey;
                return false;
            }
            return true;
        }
    }

    /// <summary>탐색 지점 하나. 월드 위치·상호작용 반경은 화면 배치라 여기 두지 않는다.</summary>
    public sealed class ExplorationInteraction
    {
        private readonly ExplorationChoice[] _choices;

        public ExplorationInteraction(string id, string displayNameKey, string descriptionKey, bool repeatable, int cooldownDays, ExplorationRiskPolicy defaultRiskPolicy, ExplorationChoice[] choices)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            Repeatable = repeatable;
            CooldownDays = Math.Max(0, cooldownDays);
            DefaultRiskPolicy = defaultRiskPolicy;
            _choices = choices ?? Array.Empty<ExplorationChoice>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public bool Repeatable { get; }
        public int CooldownDays { get; }
        public ExplorationRiskPolicy DefaultRiskPolicy { get; }
        public IReadOnlyList<ExplorationChoice> Choices => _choices;

        /// <summary>한 번만 하는 지점을 이미 끝냈거나 대기 날수가 안 지났으면 거절한다. 기록을 만들지 않는다(원본은 빈 기록을 만들었다).</summary>
        public bool CanAttempt(ExplorationProgress progress, int currentDay, out string reason)
        {
            reason = string.Empty;
            if (progress == null || !progress.TryGetRecord(Id, out var record)) return true;
            if (!Repeatable && record.Completed)
            {
                reason = "exploration.completed";
                return false;
            }
            if (CooldownDays > 0 && record.LastCompletedDay > 0 && Math.Max(1, currentDay) < record.LastCompletedDay + CooldownDays)
            {
                reason = "exploration.cooldown";
                return false;
            }
            return true;
        }
    }

    public enum ExplorationChoiceResultKind
    {
        Applied,
        InvalidRequest,
        ConditionFailed,
        DuplicateRequest,
        CooldownActive,
        InsufficientResources,
        Dismissed
    }

    /// <summary>선택지를 고른 결과를 모은다. 같은 값은 한 번만.</summary>
    public sealed class ExplorationOutcomeCollector
    {
        private readonly List<string> _outcomeIds = new List<string>();
        private readonly List<string> _itemGrantIds = new List<string>();
        private readonly List<string> _itemSpendIds = new List<string>();
        private readonly List<string> _clueIds = new List<string>();
        private readonly List<string> _encyclopediaEntryIds = new List<string>();
        private readonly List<string> _careerHintIds = new List<string>();
        private readonly List<string> _followUpQuestIds = new List<string>();
        private readonly List<string> _fatigueStatusIds = new List<string>();

        public string[] OutcomeIds => _outcomeIds.ToArray();
        public string[] ItemGrantIds => _itemGrantIds.ToArray();
        public string[] ItemSpendIds => _itemSpendIds.ToArray();
        public string[] ClueIds => _clueIds.ToArray();
        public string[] EncyclopediaEntryIds => _encyclopediaEntryIds.ToArray();
        public string[] CareerHintIds => _careerHintIds.ToArray();
        public string[] FollowUpQuestIds => _followUpQuestIds.ToArray();
        public string[] FatigueStatusIds => _fatigueStatusIds.ToArray();

        public void AddOutcome(string id) => AddUnique(_outcomeIds, id);
        public void AddItemGrant(string id) => AddUnique(_itemGrantIds, id);
        public void AddItemSpend(string id) => AddUnique(_itemSpendIds, id);
        public void AddClue(string id) => AddUnique(_clueIds, id);
        public void AddEncyclopediaEntry(string id) => AddUnique(_encyclopediaEntryIds, id);
        public void AddCareerHint(string id) => AddUnique(_careerHintIds, id);
        public void AddFollowUpQuest(string id) => AddUnique(_followUpQuestIds, id);
        public void AddFatigueStatus(string id) => AddUnique(_fatigueStatusIds, id);

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
        }
    }

    public readonly struct ExplorationChoiceResult
    {
        public readonly ExplorationChoiceResultKind Kind;
        public readonly string InteractionId;
        public readonly string ChoiceId;
        public readonly string LockedReasonKey;
        public readonly string[] OutcomeIds;
        public readonly string[] ItemGrantIds;
        public readonly string[] ItemSpendIds;
        public readonly string[] ClueIds;
        public readonly string[] EncyclopediaEntryIds;
        public readonly string[] CareerHintIds;
        public readonly string[] FollowUpQuestIds;
        public readonly string[] FatigueStatusIds;

        public ExplorationChoiceResult(ExplorationChoiceResultKind kind, string interactionId, string choiceId, string lockedReasonKey, ExplorationOutcomeCollector collector)
        {
            Kind = kind;
            InteractionId = interactionId ?? string.Empty;
            ChoiceId = choiceId ?? string.Empty;
            LockedReasonKey = lockedReasonKey ?? string.Empty;
            OutcomeIds = collector != null ? collector.OutcomeIds : Array.Empty<string>();
            ItemGrantIds = collector != null ? collector.ItemGrantIds : Array.Empty<string>();
            ItemSpendIds = collector != null ? collector.ItemSpendIds : Array.Empty<string>();
            ClueIds = collector != null ? collector.ClueIds : Array.Empty<string>();
            EncyclopediaEntryIds = collector != null ? collector.EncyclopediaEntryIds : Array.Empty<string>();
            CareerHintIds = collector != null ? collector.CareerHintIds : Array.Empty<string>();
            FollowUpQuestIds = collector != null ? collector.FollowUpQuestIds : Array.Empty<string>();
            FatigueStatusIds = collector != null ? collector.FatigueStatusIds : Array.Empty<string>();
        }
    }

    /// <summary>
    /// 탐색 선택지 고르기. 그만두기 선택지는 조건만 보고 아무것도 바꾸지 않는다(위험·결과를 가진 그만두기는 잘못된 데이터라 거절).
    /// 보통 선택지 순서: 지점 반복·대기 → 선택지 중복 → 조건 → 위험 비용 → 결과별 확인 → 인벤토리 합산 확인 → 적용.
    /// 인벤토리 합산 확인은 원본에 없던 것이다(여러 지급이 함께 넘치면 일부만 주던 것을 막는다).
    /// 거절하면 탐색 기록도 만들지 않는다(원본은 빈 기록을 남겨 세이브에 RepeatCount 0인 기록이 생겼다).
    /// </summary>
    public sealed class ExplorationInteractionRunner
    {
        public bool TryChoose(ExplorationInteraction interaction, ExplorationChoice choice, in ExplorationInteractionContext context, out ExplorationChoiceResult result)
        {
            string interactionId = interaction != null ? interaction.Id : string.Empty;
            string choiceId = choice != null ? choice.Id : string.Empty;
            result = new ExplorationChoiceResult(ExplorationChoiceResultKind.InvalidRequest, interactionId, choiceId, "exploration.invalid", null);
            if (interaction == null || choice == null || context.ExplorationProgress == null || context.StudentProgress == null) return false;

            if (choice.DismissesInteraction)
            {
                if (choice.RiskPolicy != null || choice.Outcomes.Count > 0) return false;
                if (!choice.ConditionsSatisfied(in context, out string dismissalReason))
                {
                    result = new ExplorationChoiceResult(ExplorationChoiceResultKind.ConditionFailed, interactionId, choiceId, dismissalReason, null);
                    return false;
                }
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.Dismissed, interactionId, choiceId, string.Empty, null);
                return true;
            }

            if (!interaction.CanAttempt(context.ExplorationProgress, context.CurrentDay, out string interactionReason))
            {
                var kind = interactionReason == "exploration.cooldown" ? ExplorationChoiceResultKind.CooldownActive : ExplorationChoiceResultKind.DuplicateRequest;
                result = new ExplorationChoiceResult(kind, interactionId, choiceId, interactionReason, null);
                return false;
            }
            if (!choice.Repeatable && context.ExplorationProgress.TryGetRecord(interaction.Id, out var record) && record.IsRewardClaimed(choice.Id))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.DuplicateRequest, interactionId, choiceId, "exploration.choice.completed", null);
                return false;
            }
            if (!choice.ConditionsSatisfied(in context, out string reason))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.ConditionFailed, interactionId, choiceId, reason, null);
                return false;
            }
            var risk = choice.RiskPolicy ?? interaction.DefaultRiskPolicy;
            if (risk != null && !risk.CanApply(in context))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.InsufficientResources, interactionId, choiceId, "exploration.resources", null);
                return false;
            }
            if (!CanApplyAll(choice, interaction, in context))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.InsufficientResources, interactionId, choiceId, "exploration.outcome.blocked", null);
                return false;
            }

            var collector = new ExplorationOutcomeCollector();
            risk?.Apply(in context, collector);
            for (int i = 0; i < choice.Outcomes.Count; i++)
            {
                var outcome = choice.Outcomes[i];
                if (outcome != null && outcome.Apply(in context, interaction, choice, collector)) collector.AddOutcome(outcome.OutcomeId);
            }
            context.ExplorationProgress.MarkCompleted(interaction.Id, choice.Id, context.CurrentDay, context.CurrentTimeMinutes, collector);
            context.StudentProgress.RecordActivityCompleted(interaction.Id, collector.OutcomeIds);
            result = new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, interactionId, choiceId, string.Empty, collector);
            return true;
        }

        private static bool CanApplyAll(ExplorationChoice choice, ExplorationInteraction interaction, in ExplorationInteractionContext context)
        {
            var costs = new List<InventoryGrant>();
            var rewards = new List<InventoryGrant>();
            for (int i = 0; i < choice.Outcomes.Count; i++)
            {
                var outcome = choice.Outcomes[i];
                if (outcome == null) continue;
                if (!outcome.CanApply(in context, interaction, choice)) return false;
                if (outcome is IExplorationInventoryChange change) change.AddTo(costs, rewards);
            }
            if (context.Inventory == null || (costs.Count == 0 && rewards.Count == 0)) return true;
            return context.Inventory.CanExchange(costs, rewards);
        }
    }

    /// <summary>id로 탐색 지점을 찾는다. 찾은 횟수를 센다.</summary>
    public sealed class ExplorationLookupCache
    {
        private readonly Dictionary<string, ExplorationInteraction> _interactions = new Dictionary<string, ExplorationInteraction>(StringComparer.Ordinal);

        public ExplorationLookupCache(IEnumerable<ExplorationInteraction> interactions)
        {
            if (interactions == null) return;
            foreach (var interaction in interactions) if (interaction != null && interaction.Id.Length > 0) _interactions[interaction.Id] = interaction;
        }

        public int LookupCount { get; private set; }

        public bool TryGetInteraction(string interactionId, out ExplorationInteraction interaction)
        {
            LookupCount++;
            return _interactions.TryGetValue(interactionId ?? string.Empty, out interaction);
        }
    }

    public readonly struct ExplorationChoiceSummaryModel
    {
        public readonly string ChoiceId;
        public readonly string DisplayNameKey;
        public readonly string PreviewHintKey;
        public readonly bool Available;
        public readonly string LockedReasonKey;

        public ExplorationChoiceSummaryModel(string choiceId, string displayNameKey, string previewHintKey, bool available, string lockedReasonKey)
        {
            ChoiceId = choiceId ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? ChoiceId : displayNameKey;
            PreviewHintKey = string.IsNullOrEmpty(previewHintKey) ? DisplayNameKey : previewHintKey;
            Available = available;
            LockedReasonKey = lockedReasonKey ?? string.Empty;
        }
    }

    public readonly struct ExplorationSummaryModel
    {
        public readonly string InteractionId;
        public readonly string DisplayNameKey;
        public readonly string DescriptionKey;
        public readonly bool Completed;
        public readonly ExplorationChoiceSummaryModel[] Choices;

        public ExplorationSummaryModel(string interactionId, string displayNameKey, string descriptionKey, bool completed, ExplorationChoiceSummaryModel[] choices)
        {
            InteractionId = interactionId ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? InteractionId : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            Completed = completed;
            Choices = choices ?? Array.Empty<ExplorationChoiceSummaryModel>();
        }
    }

    /// <summary>탐색 지점과 선택지 표시 모델. 가용 여부는 조건만 본다(위험·자원은 고를 때 본다). 기록을 만들지 않는다.</summary>
    public static class ExplorationSummaryBuilder
    {
        public static ExplorationSummaryModel Build(string interactionId, ExplorationLookupCache cache, ExplorationProgress progress, StudentLifeProgress student, ItemInventory inventory,
            QuestLog questLog, IExplorationClues clues, int day, int timeMinutes)
        {
            if (cache == null || !cache.TryGetInteraction(interactionId, out var interaction) || interaction == null)
            {
                return new ExplorationSummaryModel(interactionId, interactionId, interactionId, false, Array.Empty<ExplorationChoiceSummaryModel>());
            }
            var context = new ExplorationInteractionContext(progress, student, inventory, questLog, clues, null, day, timeMinutes);
            var choices = new List<ExplorationChoiceSummaryModel>(interaction.Choices.Count);
            for (int i = 0; i < interaction.Choices.Count; i++)
            {
                var choice = interaction.Choices[i];
                if (choice == null) continue;
                bool available = choice.ConditionsSatisfied(in context, out string reason);
                choices.Add(new ExplorationChoiceSummaryModel(choice.Id, choice.DisplayNameKey, choice.PreviewHintKey, available, reason));
            }
            bool completed = progress != null && progress.TryGetRecord(interaction.Id, out var record) && record.Completed;
            return new ExplorationSummaryModel(interaction.Id, interaction.DisplayNameKey, interaction.DescriptionKey, completed, choices.ToArray());
        }
    }
}
