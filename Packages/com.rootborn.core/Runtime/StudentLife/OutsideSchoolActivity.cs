using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public interface IOutsideSchoolRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    /// <summary>결과를 적용하고 기록 한 줄을 돌려준다. 바뀐 것이 없으면 빈 문자열.</summary>
    public interface IOutsideSchoolOutcome
    {
        string Apply(StudentLifeProgress progress, string activityId);
    }

    /// <summary>학교 밖 활동(돕기, 독학, 일, 탐험 등). 분류는 id로 가리킨다(분류 정의는 id와 이름 키뿐이다).</summary>
    public sealed class OutsideSchoolActivity
    {
        private readonly IOutsideSchoolRequirement[] _requirements;
        private readonly IOutsideSchoolOutcome[] _outcomes;

        public OutsideSchoolActivity(string id, string displayNameKey, string categoryId, ActivityTimePolicy timePolicy, int timeCostMinutes, int energyCost, int focusCost, int stressDelta,
            IOutsideSchoolRequirement[] requirements, IOutsideSchoolOutcome[] outcomes)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            CategoryId = categoryId ?? string.Empty;
            TimePolicy = timePolicy;
            TimeCostMinutes = Math.Max(0, timeCostMinutes);
            EnergyCost = Math.Max(0, energyCost);
            FocusCost = Math.Max(0, focusCost);
            StressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<IOutsideSchoolRequirement>();
            _outcomes = outcomes ?? Array.Empty<IOutsideSchoolOutcome>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string CategoryId { get; }
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public IReadOnlyList<IOutsideSchoolRequirement> Requirements => _requirements;
        public IReadOnlyList<IOutsideSchoolOutcome> Outcomes => _outcomes;

        public bool HasSatisfiedRequirements(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress)) return false;
            }
            return true;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            var logs = new List<string>(_outcomes.Length);
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, Id) : string.Empty;
                if (!string.IsNullOrEmpty(log)) logs.Add(log);
            }
            return logs.ToArray();
        }
    }

    public readonly struct OutsideSchoolActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string CategoryId;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public OutsideSchoolActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string categoryId, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = activityId ?? string.Empty;
            CategoryId = categoryId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 학교 밖 활동 실행. 순서: 요청 확인 → 중복 요청 → 시간·체력·집중 → 요구 조건 → 적용. 거절하면 아무것도 바꾸지 않는다.
    /// 같은 활동을 다른 요청으로 여러 번 하면 결과 기록을 겹쳐 지우지 않고 모두 남긴다.
    /// </summary>
    public sealed class OutsideSchoolActivityRunner
    {
        public bool TryPerform(OutsideSchoolActivity activity, StudentLifeProgress progress, string requestId, out OutsideSchoolActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            string categoryId = activity == null ? string.Empty : activity.CategoryId;
            OutsideSchoolActivityResult Result(LifeActivityResultKind kind, string[] logs = null) => new OutsideSchoolActivityResult(kind, saveSlot, playerId, activityId, categoryId, requestId, logs);

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
            progress.RecordActivityCompleted(activity.Id, outcomeLogs, true);
            result = Result(LifeActivityResultKind.Applied, outcomeLogs);
            return true;
        }
    }

    public readonly struct OutsideSchoolDayLog
    {
        public readonly string[] ActivityIds;
        public readonly string[] ResultLogIds;
        public readonly string[] RequestIds;

        public OutsideSchoolDayLog(string[] activityIds, string[] resultLogIds, string[] requestIds)
        {
            ActivityIds = activityIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
            RequestIds = requestIds ?? Array.Empty<string>();
        }

        /// <summary>하루 결과 기록에서 학교 밖 활동 기록만 고른다. 요청 id는 기록에 없어 비어 있다.</summary>
        public static OutsideSchoolDayLog FromResultLogs(string[] resultLogIds)
        {
            var activityIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (!OutsideSchoolLogCodec.TryParseDelta(resultLogIds[i], out var entry)) continue;
                    if (entry.ActivityId.Length > 0 && !activityIds.Contains(entry.ActivityId)) activityIds.Add(entry.ActivityId);
                    filteredLogs.Add(resultLogIds[i]);
                }
            }
            return new OutsideSchoolDayLog(activityIds.ToArray(), filteredLogs.ToArray(), Array.Empty<string>());
        }
    }

    public readonly struct OutsideSchoolGrowthSummaryEntry
    {
        public readonly string ActivityId;
        public readonly string TargetId;
        public readonly int Delta;

        public OutsideSchoolGrowthSummaryEntry(string activityId, string targetId, int delta)
        {
            ActivityId = activityId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Delta = delta;
        }
    }

    public readonly struct OutsideSchoolGrowthSummary
    {
        public readonly OutsideSchoolGrowthSummaryEntry[] Entries;

        public OutsideSchoolGrowthSummary(OutsideSchoolGrowthSummaryEntry[] entries) { Entries = entries ?? Array.Empty<OutsideSchoolGrowthSummaryEntry>(); }

        public static OutsideSchoolGrowthSummary FromDayLog(OutsideSchoolDayLog log) => FromResultLogs(log.ResultLogIds);

        /// <summary>같은 활동·대상의 변화를 합쳐 처음 나온 순서로 준다.</summary>
        public static OutsideSchoolGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<OutsideSchoolGrowthSummaryEntry>();
            string[] aggregated = OutsideSchoolLogCodec.AggregateDeltas(resultLogIds);
            for (int i = 0; i < aggregated.Length; i++) if (OutsideSchoolLogCodec.TryParseDelta(aggregated[i], out var entry)) entries.Add(entry);
            return new OutsideSchoolGrowthSummary(entries.ToArray());
        }
    }

    /// <summary>학교 밖 활동 결과 기록 형식: "outside-school:&lt;활동&gt;:+&lt;대상&gt;=&lt;변화&gt;".</summary>
    public static class OutsideSchoolLogCodec
    {
        private const string Prefix = "outside-school:";

        public static string EncodeDelta(string activityId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + activityId + ":" + sign + targetId + "=" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 같은 활동·대상 기록을 첫 자리에 하나로 합친다. 다른 기록은 그대로 둔다. 합이 0이면 그 자리는 빈 문자열이 된다(원본과 같음).
        /// </summary>
        public static string[] AggregateDeltas(string[] values)
        {
            if (values == null) return Array.Empty<string>();
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
            {
                if (!TryParseDelta(values[i], out var entry)) continue;
                string key = Key(entry);
                totals.TryGetValue(key, out int prior);
                totals[key] = prior + entry.Delta;
            }
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (!TryParseDelta(values[i], out var entry))
                {
                    result.Add(values[i]);
                    continue;
                }
                string key = Key(entry);
                if (emitted.Add(key)) result.Add(EncodeDelta(entry.ActivityId, entry.TargetId, totals[key]));
            }
            return result.ToArray();
        }

        public static bool TryParseDelta(string value, out OutsideSchoolGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string payload = value.Substring(Prefix.Length);
            int activityEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (activityEnd <= 0 || equals <= activityEnd + 1 || equals + 1 >= payload.Length) return false;
            string activityId = payload.Substring(0, activityEnd);
            string target = payload.Substring(activityEnd + 1, equals - activityEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal)) target = target.Substring(1);
            if (!int.TryParse(payload.Substring(equals + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int delta)) return false;
            entry = new OutsideSchoolGrowthSummaryEntry(activityId, target, delta);
            return true;
        }

        private static string Key(OutsideSchoolGrowthSummaryEntry entry) => entry.ActivityId + "\n" + entry.TargetId;
    }
}
