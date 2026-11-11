using System;

namespace Rootborn.Core.StudentLife
{
    /// <summary>지금 장소가 정한 장소일 때만. 장소를 비워 두면 이벤트의 장소를 쓴다.</summary>
    public sealed class DailyEventLocationAvailabilityRule : IDailyEventAvailabilityRule
    {
        public DailyEventLocationAvailabilityRule(string locationId) { LocationId = locationId ?? string.Empty; }
        public string LocationId { get; }

        public bool IsSatisfied(DailyEventContext context, DailyEvent dailyEvent)
        {
            string expected = LocationId.Length > 0 ? LocationId : dailyEvent != null ? dailyEvent.LocationId : string.Empty;
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(context.LocationId)) return false;
            return expected == context.LocationId;
        }
    }

    public sealed class DailyEventRelationshipThresholdRule : IDailyEventAvailabilityRule
    {
        public DailyEventRelationshipThresholdRule(string relationshipId, int minimumValue) { RelationshipId = relationshipId ?? string.Empty; MinimumValue = minimumValue; }
        public string RelationshipId { get; }
        public int MinimumValue { get; }

        public bool IsSatisfied(DailyEventContext context, DailyEvent dailyEvent)
            => context.StudentLifeProgress != null && RelationshipId.Length > 0 && context.StudentLifeProgress.GetRelationshipValue(RelationshipId) >= Math.Max(0, MinimumValue);
    }

    public sealed class DailyEventTraitDeltaOutcome : IDailyEventOutcome
    {
        public DailyEventTraitDeltaOutcome(string traitId, int delta) { TraitId = traitId ?? string.Empty; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, DailyEvent dailyEvent, DailyEventChoice choice)
        {
            if (progress == null || TraitId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddTrait(TraitId, Delta);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, TraitId, Delta);
        }
    }

    public sealed class DailyEventSkillDeltaOutcome : IDailyEventOutcome
    {
        public DailyEventSkillDeltaOutcome(string skillId, int delta) { SkillId = skillId ?? string.Empty; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, DailyEvent dailyEvent, DailyEventChoice choice)
        {
            if (progress == null || SkillId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddSkill(SkillId, Delta);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, SkillId, Delta);
        }
    }

    public sealed class DailyEventRelationshipDeltaOutcome : IDailyEventOutcome
    {
        public DailyEventRelationshipDeltaOutcome(string relationshipId, int delta) { RelationshipId = relationshipId ?? string.Empty; Delta = delta; }
        public string RelationshipId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, DailyEvent dailyEvent, DailyEventChoice choice)
        {
            if (progress == null || RelationshipId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddRelationship(RelationshipId, Delta);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, RelationshipId, Delta);
        }
    }

    public sealed class DailyEventStatusDeltaOutcome : IDailyEventOutcome
    {
        public DailyEventStatusDeltaOutcome(string statusId, int delta) { StatusId = statusId ?? string.Empty; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, DailyEvent dailyEvent, DailyEventChoice choice)
        {
            if (progress == null || StatusId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddStatus(StatusId, Delta);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, StatusId, Delta);
        }
    }

    /// <summary>진로 힌트를 연다. 이미 열려 있었으면 기록을 남기지 않는다.</summary>
    public sealed class DailyEventCareerHintOutcome : IDailyEventOutcome
    {
        public DailyEventCareerHintOutcome(string careerId) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }

        public string Apply(StudentLifeProgress progress, DailyEvent dailyEvent, DailyEventChoice choice)
        {
            if (progress == null || CareerId.Length == 0) return string.Empty;
            bool wasUnlocked = progress.IsCareerHintUnlocked(CareerId);
            progress.UnlockCareerHint(CareerId);
            return wasUnlocked ? string.Empty : DailyEventLogCodec.EncodeUnlock(dailyEvent, choice, CareerId);
        }
    }

    /// <summary>"daily-event:이벤트:선택지:+대상=변화량", "daily-event:이벤트:선택지:unlock=대상". 음수 변화량에는 + 를 붙이지 않는다.</summary>
    public static class DailyEventLogCodec
    {
        private const string Prefix = "daily-event:";
        private const string UnlockMarker = ":unlock=";

        public static string EncodeDelta(DailyEvent dailyEvent, DailyEventChoice choice, string targetId, int delta)
        {
            if (dailyEvent == null || choice == null || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + dailyEvent.Id + ":" + choice.Id + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static string EncodeUnlock(DailyEvent dailyEvent, DailyEventChoice choice, string targetId)
        {
            if (dailyEvent == null || choice == null || string.IsNullOrEmpty(targetId)) return string.Empty;
            return Prefix + dailyEvent.Id + ":" + choice.Id + UnlockMarker + targetId;
        }
    }
}
