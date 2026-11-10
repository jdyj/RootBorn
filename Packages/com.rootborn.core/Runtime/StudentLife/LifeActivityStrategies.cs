using System;

namespace Rootborn.Core.StudentLife
{
    public sealed class TraitDeltaEffect : ILifeActivityEffect
    {
        public TraitDeltaEffect(string traitId, int delta) { TraitId = traitId; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }
        public void Apply(StudentLifeProgress progress) => progress?.AddTrait(TraitId, Delta);
    }

    public sealed class SkillProgressEffect : ILifeActivityEffect
    {
        public SkillProgressEffect(string skillId, int delta) { SkillId = skillId; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }
        public void Apply(StudentLifeProgress progress) => progress?.AddSkill(SkillId, Delta);
    }

    public sealed class RelationshipDeltaEffect : ILifeActivityEffect
    {
        public RelationshipDeltaEffect(string relationshipId, int delta) { RelationshipId = relationshipId; Delta = delta; }
        public string RelationshipId { get; }
        public int Delta { get; }
        public void Apply(StudentLifeProgress progress) => progress?.AddRelationship(RelationshipId, Delta);
    }

    public sealed class StatusDeltaEffect : ILifeActivityEffect
    {
        public StatusDeltaEffect(string statusId, int delta) { StatusId = statusId; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }
        public void Apply(StudentLifeProgress progress) => progress?.AddStatus(StatusId, Delta);
    }

    public sealed class CareerHintUnlockEffect : ILifeActivityEffect
    {
        public CareerHintUnlockEffect(string careerId) { CareerId = careerId; }
        public string CareerId { get; }
        public void Apply(StudentLifeProgress progress) => progress?.UnlockCareerHint(CareerId);
    }

    public sealed class TraitThresholdRequirement : ILifeActivityRequirement
    {
        public TraitThresholdRequirement(string traitId, int minimumValue) { TraitId = traitId; MinimumValue = Math.Max(0, minimumValue); }
        public string TraitId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && progress.GetTraitValue(TraitId) >= MinimumValue;
    }

    public sealed class SkillThresholdRequirement : ILifeActivityRequirement
    {
        public SkillThresholdRequirement(string skillId, int minimumValue) { SkillId = skillId; MinimumValue = Math.Max(0, minimumValue); }
        public string SkillId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && progress.GetSkillValue(SkillId) >= MinimumValue;
    }
}
