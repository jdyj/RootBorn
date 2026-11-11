using System;

namespace Rootborn.Core.StudentLife
{
    public sealed class OutsideSchoolTraitThresholdRequirement : IOutsideSchoolRequirement
    {
        public OutsideSchoolTraitThresholdRequirement(string traitId, int minimumValue) { TraitId = traitId ?? string.Empty; MinimumValue = Math.Max(0, minimumValue); }
        public string TraitId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && TraitId.Length > 0 && progress.GetTraitValue(TraitId) >= MinimumValue;
    }

    public sealed class OutsideSchoolSkillThresholdRequirement : IOutsideSchoolRequirement
    {
        public OutsideSchoolSkillThresholdRequirement(string skillId, int minimumValue) { SkillId = skillId ?? string.Empty; MinimumValue = Math.Max(0, minimumValue); }
        public string SkillId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && SkillId.Length > 0 && progress.GetSkillValue(SkillId) >= MinimumValue;
    }

    public sealed class OutsideSchoolTraitDeltaOutcome : IOutsideSchoolOutcome
    {
        public OutsideSchoolTraitDeltaOutcome(string traitId, int delta) { TraitId = traitId ?? string.Empty; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || TraitId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddTrait(TraitId, Delta);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, TraitId, Delta);
        }
    }

    public sealed class OutsideSchoolSkillDeltaOutcome : IOutsideSchoolOutcome
    {
        public OutsideSchoolSkillDeltaOutcome(string skillId, int delta) { SkillId = skillId ?? string.Empty; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || SkillId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddSkill(SkillId, Delta);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, SkillId, Delta);
        }
    }

    public sealed class OutsideSchoolRelationshipDeltaOutcome : IOutsideSchoolOutcome
    {
        public OutsideSchoolRelationshipDeltaOutcome(string relationshipId, int delta) { RelationshipId = relationshipId ?? string.Empty; Delta = delta; }
        public string RelationshipId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || RelationshipId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddRelationship(RelationshipId, Delta);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, RelationshipId, Delta);
        }
    }

    public sealed class OutsideSchoolStatusDeltaOutcome : IOutsideSchoolOutcome
    {
        public OutsideSchoolStatusDeltaOutcome(string statusId, int delta) { StatusId = statusId ?? string.Empty; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || StatusId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddStatus(StatusId, Delta);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, StatusId, Delta);
        }
    }
}
