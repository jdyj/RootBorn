using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 장소 활동의 성장 길. 원본 열거형 LocationGrowthRoute의 이름 그대로를 문자열로 쓴다(세이브·데이터와 같은 이름).
    /// 새 길은 상수 하나를 더하면 된다.
    /// </summary>
    public static class LocationGrowthRoutes
    {
        public const string FormalStudy = "FormalStudy";
        public const string SelfStudy = "SelfStudy";
        public const string Work = "Work";
        public const string Commerce = "Commerce";
        public const string SocialHelp = "SocialHelp";
        public const string Rest = "Rest";
        public const string Exploration = "Exploration";
    }

    public interface ILocationActivityRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress, LocationActivity activity);
    }

    /// <summary>결과를 적용하고 기록 한 줄을 돌려준다. 바뀐 것이 없으면 빈 문자열.</summary>
    public interface ILocationActivityOutcome
    {
        string Apply(StudentLifeProgress progress, LocationActivity activity);
    }

    /// <summary>장소에서 하는 활동. 장소는 id로 가리킨다. 일과 계획이 보는 IDailyPlanLocationActivity를 구현한다.</summary>
    public sealed class LocationActivity : IDailyPlanLocationActivity
    {
        private readonly ILocationActivityRequirement[] _requirements;
        private readonly ILocationActivityOutcome[] _outcomes;

        public LocationActivity(string id, string displayNameKey, string resultTextKey, string locationId, string growthRoute, ActivityTimePolicy timePolicy,
            int timeCostMinutes, int energyCost, int focusCost, int stressDelta, ILocationActivityRequirement[] requirements, ILocationActivityOutcome[] outcomes)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            ResultTextKey = resultTextKey ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            GrowthRoute = string.IsNullOrEmpty(growthRoute) ? LocationGrowthRoutes.FormalStudy : growthRoute;
            TimePolicy = timePolicy;
            TimeCostMinutes = Math.Max(0, timeCostMinutes);
            EnergyCost = Math.Max(0, energyCost);
            FocusCost = Math.Max(0, focusCost);
            StressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<ILocationActivityRequirement>();
            _outcomes = outcomes ?? Array.Empty<ILocationActivityOutcome>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string ResultTextKey { get; }
        public string LocationId { get; }
        public string GrowthRoute { get; }
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public IReadOnlyList<ILocationActivityRequirement> Requirements => _requirements;
        public IReadOnlyList<ILocationActivityOutcome> Outcomes => _outcomes;

        public bool HasSatisfiedRequirements(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress, this)) return false;
            }
            return true;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            var logs = new List<string>(_outcomes.Length);
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, this) : string.Empty;
                if (!string.IsNullOrEmpty(log)) logs.Add(log);
            }
            return logs.ToArray();
        }
    }

    public readonly struct LocationActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string LocationId;
        public readonly string GrowthRoute;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public LocationActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string locationId, string growthRoute, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = activityId ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            GrowthRoute = growthRoute ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    /// <summary>장소 활동 실행. 순서: 요청 확인 → 중복 요청 → 시간·체력·집중 → 요구 조건 → 적용. 거절하면 아무것도 바꾸지 않는다.</summary>
    public sealed class LocationActivityRunner
    {
        public bool TryPerform(LocationActivity activity, StudentLifeProgress progress, string requestId, out LocationActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            string locationId = activity == null ? string.Empty : activity.LocationId;
            string growthRoute = activity == null ? LocationGrowthRoutes.Exploration : activity.GrowthRoute;
            LocationActivityResult Result(LifeActivityResultKind kind, string[] logs = null) => new LocationActivityResult(kind, saveSlot, playerId, activityId, locationId, growthRoute, requestId, logs);

            result = Result(LifeActivityResultKind.InvalidRequest);
            if (activity == null || progress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = Result(LifeActivityResultKind.DuplicateRequest);
                return false;
            }
            if (!progress.CanUseTimePolicy(activity.TimePolicy) || !progress.CanSpend(activity.EnergyCost, activity.FocusCost))
            {
                result = Result(LifeActivityResultKind.InsufficientResources);
                return false;
            }
            if (!activity.HasSatisfiedRequirements(progress))
            {
                result = Result(LifeActivityResultKind.RequirementFailed);
                return false;
            }

            progress.TryUseTimePolicy(activity.TimePolicy);
            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            string[] outcomeLogs = activity.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, outcomeLogs);
            result = Result(LifeActivityResultKind.Applied, outcomeLogs);
            return true;
        }
    }

    public readonly struct LocationActivityGrowthSummaryEntry
    {
        public readonly string ActivityId;
        public readonly string TargetId;
        public readonly int Delta;
        public readonly bool IsUnlock;

        public LocationActivityGrowthSummaryEntry(string activityId, string targetId, int delta, bool isUnlock)
        {
            ActivityId = activityId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Delta = delta;
            IsUnlock = isUnlock;
        }
    }

    public readonly struct LocationActivityGrowthSummary
    {
        public readonly LocationActivityGrowthSummaryEntry[] Entries;

        public LocationActivityGrowthSummary(LocationActivityGrowthSummaryEntry[] entries) { Entries = entries ?? Array.Empty<LocationActivityGrowthSummaryEntry>(); }

        public static LocationActivityGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<LocationActivityGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++) if (LocationActivityLogCodec.TryParse(resultLogIds[i], out var entry)) entries.Add(entry);
            }
            return new LocationActivityGrowthSummary(entries.ToArray());
        }
    }

    /// <summary>장소 활동 결과 기록 형식: "location-activity:&lt;활동&gt;:+&lt;대상&gt;=&lt;변화&gt;", "location-activity:&lt;활동&gt;:unlock=&lt;대상&gt;".</summary>
    public static class LocationActivityLogCodec
    {
        private const string Prefix = "location-activity:";
        private const string UnlockMarker = ":unlock=";

        public static string EncodeDelta(string activityId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + activityId + ":" + sign + targetId + "=" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string EncodeUnlock(string activityId, string targetId)
        {
            if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(targetId)) return string.Empty;
            return Prefix + activityId + UnlockMarker + targetId;
        }

        public static bool TryParse(string value, out LocationActivityGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string payload = value.Substring(Prefix.Length);
            int unlockIndex = payload.IndexOf(UnlockMarker, StringComparison.Ordinal);
            if (unlockIndex > 0)
            {
                entry = new LocationActivityGrowthSummaryEntry(payload.Substring(0, unlockIndex), payload.Substring(unlockIndex + UnlockMarker.Length), 1, true);
                return true;
            }
            int actionEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (actionEnd <= 0 || equals <= actionEnd + 1 || equals + 1 >= payload.Length) return false;
            string activityId = payload.Substring(0, actionEnd);
            string target = payload.Substring(actionEnd + 1, equals - actionEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal)) target = target.Substring(1);
            if (!int.TryParse(payload.Substring(equals + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int delta)) return false;
            entry = new LocationActivityGrowthSummaryEntry(activityId, target, delta, false);
            return true;
        }
    }
}
