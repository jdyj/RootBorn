namespace Rootborn.Core.StudentLife
{
    // 원본 LocationActivityProgressDeltaOutcome(대상 열거형 + 대상별 필드)은 대상마다 아래 결과 하나와 같은 동작이다.
    // 데이터를 읽을 때 대상에 맞는 결과로 만든다.

    public sealed class LocationActivityTraitDeltaOutcome : ILocationActivityOutcome
    {
        public LocationActivityTraitDeltaOutcome(string traitId, int delta) { TraitId = traitId ?? string.Empty; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, LocationActivity activity)
        {
            if (progress == null || activity == null || TraitId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddTrait(TraitId, Delta);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, TraitId, Delta);
        }
    }

    public sealed class LocationActivitySkillDeltaOutcome : ILocationActivityOutcome
    {
        public LocationActivitySkillDeltaOutcome(string skillId, int delta) { SkillId = skillId ?? string.Empty; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, LocationActivity activity)
        {
            if (progress == null || activity == null || SkillId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddSkill(SkillId, Delta);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, SkillId, Delta);
        }
    }

    public sealed class LocationActivityRelationshipDeltaOutcome : ILocationActivityOutcome
    {
        public LocationActivityRelationshipDeltaOutcome(string relationshipId, int delta) { RelationshipId = relationshipId ?? string.Empty; Delta = delta; }
        public string RelationshipId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, LocationActivity activity)
        {
            if (progress == null || activity == null || RelationshipId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddRelationship(RelationshipId, Delta);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, RelationshipId, Delta);
        }
    }

    public sealed class LocationActivityStatusDeltaOutcome : ILocationActivityOutcome
    {
        public LocationActivityStatusDeltaOutcome(string statusId, int delta) { StatusId = statusId ?? string.Empty; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, LocationActivity activity)
        {
            if (progress == null || activity == null || StatusId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddStatus(StatusId, Delta);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, StatusId, Delta);
        }
    }

    /// <summary>진로 힌트를 연다. 처음 열 때만 기록한다. logEveryTime이면 이미 열려 있어도 기록한다(다른 시스템이 먼저 열었어도 힌트 출처가 읽게).</summary>
    public sealed class LocationActivityCareerHintOutcome : ILocationActivityOutcome
    {
        public LocationActivityCareerHintOutcome(string careerId, bool logEveryTime = false) { CareerId = careerId ?? string.Empty; LogEveryTime = logEveryTime; }
        public string CareerId { get; }
        public bool LogEveryTime { get; }

        public string Apply(StudentLifeProgress progress, LocationActivity activity)
        {
            if (progress == null || activity == null || CareerId.Length == 0) return string.Empty;
            bool wasUnlocked = progress.IsCareerHintUnlocked(CareerId);
            progress.UnlockCareerHint(CareerId);
            return wasUnlocked && !LogEveryTime ? string.Empty : LocationActivityLogCodec.EncodeUnlock(activity.Id, CareerId);
        }
    }

    /// <summary>자원·레시피 공개(자원·레시피 진행 영역은 아직 Core에 없다). 불러오기·저장은 구현하는 쪽이 한다. 새로 공개했으면 true.</summary>
    public interface ILocationActivityRecipeReveal
    {
        bool TryReveal(StudentLifeProgress progress, string resourceId, string recipeId, string sourceActivityId);
    }

    public sealed class LocationActivityResourceRecipeOutcome : ILocationActivityOutcome
    {
        private readonly ILocationActivityRecipeReveal _reveal;

        public LocationActivityResourceRecipeOutcome(string resourceId, string recipeId, string nextHintKey, ILocationActivityRecipeReveal reveal)
        {
            ResourceId = resourceId ?? string.Empty;
            RecipeId = recipeId ?? string.Empty;
            NextHintKey = string.IsNullOrEmpty(nextHintKey) ? "objective.resourceRecipe.next" : nextHintKey;
            _reveal = reveal;
        }

        public string ResourceId { get; }
        public string RecipeId { get; }
        public string NextHintKey { get; }

        public string Apply(StudentLifeProgress progress, LocationActivity activity)
        {
            if (progress == null || activity == null || RecipeId.Length == 0 || _reveal == null) return string.Empty;
            if (!_reveal.TryReveal(progress, ResourceId, RecipeId, activity.Id)) return string.Empty;
            return LocationActivityLogCodec.EncodeUnlock(activity.Id, RecipeId);
        }
    }
}
