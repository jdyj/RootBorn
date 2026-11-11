using System;

namespace Rootborn.Core.StudentLife
{
    public sealed class HelpActionTraitThresholdRequirement : IHelpActionRequirement
    {
        public HelpActionTraitThresholdRequirement(string traitId, int minimumValue) { TraitId = traitId ?? string.Empty; MinimumValue = minimumValue; }
        public string TraitId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && TraitId.Length > 0 && progress.GetTraitValue(TraitId) >= Math.Max(0, MinimumValue);
    }

    public sealed class HelpActionSkillThresholdRequirement : IHelpActionRequirement
    {
        public HelpActionSkillThresholdRequirement(string skillId, int minimumValue) { SkillId = skillId ?? string.Empty; MinimumValue = minimumValue; }
        public string SkillId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && SkillId.Length > 0 && progress.GetSkillValue(SkillId) >= Math.Max(0, MinimumValue);
    }

    public sealed class HelpActionRelationshipThresholdRequirement : IHelpActionRequirement
    {
        public HelpActionRelationshipThresholdRequirement(string relationshipId, int minimumValue) { RelationshipId = relationshipId ?? string.Empty; MinimumValue = minimumValue; }
        public string RelationshipId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && RelationshipId.Length > 0 && progress.GetRelationshipValue(RelationshipId) >= Math.Max(0, MinimumValue);
    }

    public sealed class HelpActionStatusThresholdRequirement : IHelpActionRequirement
    {
        public HelpActionStatusThresholdRequirement(string statusId, int minimumValue) { StatusId = statusId ?? string.Empty; MinimumValue = minimumValue; }
        public string StatusId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && StatusId.Length > 0 && progress.GetStatusValue(StatusId) >= Math.Max(0, MinimumValue);
    }

    public sealed class HelpActionTraitDeltaOutcome : IHelpActionOutcome
    {
        public HelpActionTraitDeltaOutcome(string traitId, int delta) { TraitId = traitId ?? string.Empty; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || TraitId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddTrait(TraitId, Delta);
            return TownHelpActionLogCodec.EncodeDelta(actionId, TraitId, Delta);
        }
    }

    public sealed class HelpActionSkillDeltaOutcome : IHelpActionOutcome
    {
        public HelpActionSkillDeltaOutcome(string skillId, int delta) { SkillId = skillId ?? string.Empty; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || SkillId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddSkill(SkillId, Delta);
            return TownHelpActionLogCodec.EncodeDelta(actionId, SkillId, Delta);
        }
    }

    public sealed class HelpActionRelationshipDeltaOutcome : IHelpActionOutcome
    {
        public HelpActionRelationshipDeltaOutcome(string relationshipId, int delta) { RelationshipId = relationshipId ?? string.Empty; Delta = delta; }
        public string RelationshipId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || RelationshipId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddRelationship(RelationshipId, Delta);
            return TownHelpActionLogCodec.EncodeDelta(actionId, RelationshipId, Delta);
        }
    }

    public sealed class HelpActionStatusDeltaOutcome : IHelpActionOutcome
    {
        public HelpActionStatusDeltaOutcome(string statusId, int delta) { StatusId = statusId ?? string.Empty; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || StatusId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddStatus(StatusId, Delta);
            return TownHelpActionLogCodec.EncodeDelta(actionId, StatusId, Delta);
        }
    }

    /// <summary>진로 힌트를 연다. 처음 열 때만 "+&lt;진로&gt;=1"로 기록한다.</summary>
    public sealed class HelpActionCareerHintOutcome : IHelpActionOutcome
    {
        public HelpActionCareerHintOutcome(string careerId) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }

        public string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || CareerId.Length == 0) return string.Empty;
            bool wasUnlocked = progress.IsCareerHintUnlocked(CareerId);
            progress.UnlockCareerHint(CareerId);
            return wasUnlocked ? string.Empty : TownHelpActionLogCodec.EncodeDelta(actionId, CareerId, 1);
        }
    }
}
