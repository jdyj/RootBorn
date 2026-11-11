using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.StudentLife
{
    public readonly struct MilestoneEvaluationContext
    {
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly ItemInventory Inventory;
        public readonly string[] AdditionalResultLogIds;

        public MilestoneEvaluationContext(StudentLifeProgress studentLifeProgress, ItemInventory inventory, string[] additionalResultLogIds)
        {
            StudentLifeProgress = studentLifeProgress;
            Inventory = inventory;
            AdditionalResultLogIds = additionalResultLogIds ?? Array.Empty<string>();
        }
    }

    public readonly struct MilestoneObjectiveEvaluation
    {
        public readonly string ObjectiveId;
        public readonly int Current;
        public readonly int Required;
        public readonly bool IsComplete;

        public MilestoneObjectiveEvaluation(string objectiveId, int current, int required)
        {
            ObjectiveId = objectiveId ?? string.Empty;
            Current = Math.Max(0, current);
            Required = Math.Max(1, required);
            IsComplete = Current >= Required;
        }
    }

    /// <summary>마일스톤 목표. 새 종류는 이 클래스를 상속해 지금 값을 계산하면 된다. 필요한 값은 1 이상.</summary>
    public abstract class MilestoneObjective
    {
        protected MilestoneObjective(string id, int requiredValue)
        {
            Id = id ?? string.Empty;
            RequiredValue = Math.Max(1, requiredValue);
        }

        public string Id { get; }
        public int RequiredValue { get; }

        public MilestoneObjectiveEvaluation Evaluate(in MilestoneEvaluationContext context) => new MilestoneObjectiveEvaluation(Id, EvaluateCurrent(in context), RequiredValue);

        /// <summary>하루 결과 적용에서 평가값이 0 이하일 때 대신 쓸 값. 기본은 그대로.</summary>
        public virtual int DayResultFallback(in MilestoneEvaluationContext context, MilestoneObjectiveEvaluation evaluation) => evaluation.Current;

        protected abstract int EvaluateCurrent(in MilestoneEvaluationContext context);
    }

    /// <summary>마일스톤 보상. 받을 수 있는지 먼저 모두 확인한 뒤 적용한다.</summary>
    public interface IMilestoneReward
    {
        bool CanApply(ItemInventory inventory);
        void Apply(ItemInventory inventory);
    }

    /// <summary>물건을 주는 보상. 실행기가 모든 물건 보상을 합쳐 한 번에 들어가는지 확인한다.</summary>
    public interface IMilestoneItemReward : IMilestoneReward
    {
        InventoryGrant Grant { get; }
    }

    /// <summary>같은 목표로 가는 길 하나와 그 길에서 추천할 행동 id.</summary>
    public sealed class MilestoneRoute
    {
        public MilestoneRoute(string id, MilestoneObjective objective, string recommendedActionId)
        {
            Id = id ?? string.Empty;
            Objective = objective;
            RecommendedActionId = recommendedActionId ?? string.Empty;
        }

        public string Id { get; }
        public MilestoneObjective Objective { get; }
        public string RecommendedActionId { get; }
    }

    /// <summary>
    /// 마일스톤. 목표 중 필요한 개수(0 이하면 전부)를 채우면 끝난다. 앞선 마일스톤은 id로 가리키며, 모두 끝나야 평가한다.
    /// </summary>
    public sealed class Milestone
    {
        private readonly MilestoneObjective[] _objectives;
        private readonly IMilestoneReward[] _rewards;
        private readonly MilestoneRoute[] _routes;
        private readonly string[] _prerequisiteMilestoneIds;
        private readonly int _requiredObjectiveCount;

        public Milestone(string id, string displayNameKey, MilestoneObjective[] objectives, IMilestoneReward[] rewards, MilestoneRoute[] routes, string[] prerequisiteMilestoneIds, int requiredObjectiveCount)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            _objectives = objectives ?? Array.Empty<MilestoneObjective>();
            _rewards = rewards ?? Array.Empty<IMilestoneReward>();
            _routes = routes ?? Array.Empty<MilestoneRoute>();
            _prerequisiteMilestoneIds = prerequisiteMilestoneIds ?? Array.Empty<string>();
            _requiredObjectiveCount = Math.Max(0, requiredObjectiveCount);
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public IReadOnlyList<MilestoneObjective> Objectives => _objectives;
        public IReadOnlyList<IMilestoneReward> Rewards => _rewards;
        public IReadOnlyList<MilestoneRoute> Routes => _routes;
        public IReadOnlyList<string> PrerequisiteMilestoneIds => _prerequisiteMilestoneIds;
        public int RequiredObjectiveCount => _requiredObjectiveCount <= 0 ? _objectives.Length : _requiredObjectiveCount;
    }

    /// <summary>
    /// 원본 MilestoneRegistryResolver가 등록부에 마일스톤이 없을 때 만들던 기본 마일스톤
    /// (학교 밖 기록 하나면 끝나는 "open-ended-town-growth"). 추천 행동이 비면 "outside-school".
    /// </summary>
    public static class MilestoneFallback
    {
        public static Milestone Create(string recommendedActionId)
        {
            string action = string.IsNullOrEmpty(recommendedActionId) ? "outside-school" : recommendedActionId;
            var objective = new OutsideSchoolLogMilestoneObjective("milestone.objective.any-outside-school-log", string.Empty, 1);
            var route = new MilestoneRoute("milestone.route.first-outside-school", objective, action);
            return new Milestone("milestone.open-ended-town-growth", "milestone.open-ended-town-growth", new MilestoneObjective[] { objective }, null, new[] { route }, null, 1);
        }
    }

    public sealed class TraitMilestoneObjective : MilestoneObjective
    {
        public TraitMilestoneObjective(string id, string traitId, int requiredValue) : base(id, requiredValue) { TraitId = traitId ?? string.Empty; }
        public string TraitId { get; }
        protected override int EvaluateCurrent(in MilestoneEvaluationContext context) => context.StudentLifeProgress == null || TraitId.Length == 0 ? 0 : context.StudentLifeProgress.GetTraitValue(TraitId);
    }

    public sealed class SkillMilestoneObjective : MilestoneObjective
    {
        public SkillMilestoneObjective(string id, string skillId, int requiredValue) : base(id, requiredValue) { SkillId = skillId ?? string.Empty; }
        public string SkillId { get; }
        protected override int EvaluateCurrent(in MilestoneEvaluationContext context) => context.StudentLifeProgress == null || SkillId.Length == 0 ? 0 : context.StudentLifeProgress.GetSkillValue(SkillId);
    }

    public sealed class StatusMilestoneObjective : MilestoneObjective
    {
        public StatusMilestoneObjective(string id, string statusId, int requiredValue) : base(id, requiredValue) { StatusId = statusId ?? string.Empty; }
        public string StatusId { get; }
        protected override int EvaluateCurrent(in MilestoneEvaluationContext context) => context.StudentLifeProgress == null || StatusId.Length == 0 ? 0 : context.StudentLifeProgress.GetStatusValue(StatusId);
    }

    public sealed class RelationshipMilestoneObjective : MilestoneObjective
    {
        public RelationshipMilestoneObjective(string id, string relationshipId, int requiredValue) : base(id, requiredValue) { RelationshipId = relationshipId ?? string.Empty; }
        public string RelationshipId { get; }
        protected override int EvaluateCurrent(in MilestoneEvaluationContext context) => context.StudentLifeProgress == null || RelationshipId.Length == 0 ? 0 : context.StudentLifeProgress.GetRelationshipValue(RelationshipId);
    }

    /// <summary>
    /// 오늘·어제 학교 밖 기록과 추가 기록에서 대상(비면 모두)의 양의 변화를 더한다.
    /// 하루 결과 적용에서는 평가값이 0 이하여도 이 목표의 대상(비면 모두)에 해당하는 학교 밖 기록이 하나라도 있으면 목표를 채운다.
    /// 원본은 대상을 보지 않아 다른 대상의 기록으로도 채워졌다.
    /// </summary>
    public sealed class OutsideSchoolLogMilestoneObjective : MilestoneObjective
    {
        public OutsideSchoolLogMilestoneObjective(string id, string targetId, int requiredDelta) : base(id, requiredDelta) { TargetId = targetId ?? string.Empty; }
        public string TargetId { get; }

        protected override int EvaluateCurrent(in MilestoneEvaluationContext context)
        {
            int total = 0;
            var student = context.StudentLifeProgress;
            AddLogs(student != null ? student.GetTodayResultLogIds() : null, ref total);
            AddLogs(student != null ? student.GetPreviousDayResultLogIds() : null, ref total);
            AddLogs(context.AdditionalResultLogIds, ref total);
            return total;
        }

        public override int DayResultFallback(in MilestoneEvaluationContext context, MilestoneObjectiveEvaluation evaluation)
        {
            if (evaluation.Current > 0) return evaluation.Current;
            var student = context.StudentLifeProgress;
            if (student == null) return evaluation.Current;
            return HasAny(student.GetTodayResultLogIds()) || HasAny(student.GetPreviousDayResultLogIds()) ? evaluation.Required : evaluation.Current;
        }

        private void AddLogs(string[] logs, ref int total)
        {
            if (logs == null) return;
            for (int i = 0; i < logs.Length; i++)
            {
                if (!OutsideSchoolLogCodec.TryParseDelta(logs[i], out var entry)) continue;
                if (TargetId.Length > 0 && !string.Equals(entry.TargetId, TargetId, StringComparison.Ordinal)) continue;
                total += Math.Max(0, entry.Delta);
            }
        }

        private bool HasAny(string[] logs)
        {
            if (logs == null) return false;
            for (int i = 0; i < logs.Length; i++)
                if (OutsideSchoolLogCodec.TryParseDelta(logs[i], out var entry) && (TargetId.Length == 0 || string.Equals(entry.TargetId, TargetId, StringComparison.Ordinal))) return true;
            return false;
        }
    }

    public sealed class ItemMilestoneReward : IMilestoneItemReward
    {
        public ItemMilestoneReward(ItemDefinition item, int count) { Item = item; Count = Math.Max(1, count); }
        public ItemDefinition Item { get; }
        public int Count { get; }
        public InventoryGrant Grant => new InventoryGrant(Item, Count);
        public bool CanApply(ItemInventory inventory) => inventory != null && Item != null && inventory.CanAdd(Item, Count);
        public void Apply(ItemInventory inventory) { if (CanApply(inventory)) inventory.Add(Item, Count); }
    }
}
