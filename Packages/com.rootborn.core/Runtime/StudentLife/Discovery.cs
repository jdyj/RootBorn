using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>발견의 범위. 이름이 적용 요청 id("discovery:&lt;범위&gt;:&lt;id&gt;")에 들어가 세이브에 남으므로 이름을 바꾸지 않는다.</summary>
    public enum DiscoveryScope
    {
        Personal,
        SharedWorld
    }

    public interface IDiscoveryRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    /// <summary>결과를 적용하고 기록 한 줄을 돌려준다. 바뀐 것이 없으면 빈 문자열.</summary>
    public interface IDiscoveryOutcome
    {
        string Apply(StudentLifeProgress progress, string discoveryId);
    }

    /// <summary>마을의 발견 하나. 월드 위치·상호작용 반경은 화면 배치라 여기 두지 않는다.</summary>
    public sealed class Discovery
    {
        private readonly IDiscoveryRequirement[] _requirements;
        private readonly IDiscoveryOutcome[] _outcomes;

        public Discovery(string id, string displayNameKey, DiscoveryScope scope, IDiscoveryRequirement[] requirements, IDiscoveryOutcome[] outcomes)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            Scope = scope;
            _requirements = requirements ?? Array.Empty<IDiscoveryRequirement>();
            _outcomes = outcomes ?? Array.Empty<IDiscoveryOutcome>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public DiscoveryScope Scope { get; }
        public IReadOnlyList<IDiscoveryRequirement> Requirements => _requirements;
        public IReadOnlyList<IDiscoveryOutcome> Outcomes => _outcomes;

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

    public readonly struct DiscoveryResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string DiscoveryId;
        public readonly DiscoveryScope Scope;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public DiscoveryResult(LifeActivityResultKind kind, string saveSlot, string playerId, string discoveryId, DiscoveryScope scope, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            DiscoveryId = discoveryId ?? string.Empty;
            Scope = scope;
            RequestId = requestId ?? string.Empty;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    /// <summary>발견 실행. 기본 요청 id는 발견마다 하나라 한 번만 발견된다. 순서: 요청 확인 → 중복 → 요구 조건 → 적용. 거절하면 아무것도 바꾸지 않는다.</summary>
    public sealed class DiscoveryRunner
    {
        public static string RequestIdFor(Discovery discovery) => discovery == null ? string.Empty : RequestIdFor(discovery.Scope, discovery.Id);

        public static string RequestIdFor(DiscoveryScope scope, string discoveryId) => string.IsNullOrEmpty(discoveryId) ? string.Empty : "discovery:" + scope + ":" + discoveryId;

        public bool TryDiscover(Discovery discovery, StudentLifeProgress progress, out DiscoveryResult result) => TryDiscover(discovery, progress, RequestIdFor(discovery), out result);

        public bool TryDiscover(Discovery discovery, StudentLifeProgress progress, string requestId, out DiscoveryResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            var scope = discovery == null ? DiscoveryScope.Personal : discovery.Scope;
            DiscoveryResult Result(LifeActivityResultKind kind, string[] logs = null) => new DiscoveryResult(kind, saveSlot, playerId, discovery?.Id, scope, requestId, logs);

            result = Result(LifeActivityResultKind.InvalidRequest);
            if (discovery == null || progress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = Result(LifeActivityResultKind.DuplicateRequest);
                return false;
            }
            if (!discovery.HasSatisfiedRequirements(progress))
            {
                result = Result(LifeActivityResultKind.RequirementFailed);
                return false;
            }

            string[] outcomeLogs = discovery.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(discovery.Id, outcomeLogs);
            result = Result(LifeActivityResultKind.Applied, outcomeLogs);
            return true;
        }
    }

    public readonly struct DiscoveryDayLog
    {
        public readonly string[] DiscoveryIds;
        public readonly string[] ResultLogIds;

        public DiscoveryDayLog(string[] discoveryIds, string[] resultLogIds)
        {
            DiscoveryIds = discoveryIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }

        public static DiscoveryDayLog FromResultLogs(string[] resultLogIds)
        {
            var discoveryIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (!DiscoveryLogCodec.TryParseDelta(resultLogIds[i], out var entry)) continue;
                    if (entry.DiscoveryId.Length > 0 && !discoveryIds.Contains(entry.DiscoveryId)) discoveryIds.Add(entry.DiscoveryId);
                    filteredLogs.Add(resultLogIds[i]);
                }
            }
            return new DiscoveryDayLog(discoveryIds.ToArray(), filteredLogs.ToArray());
        }
    }

    public readonly struct DiscoveryGrowthSummaryEntry
    {
        public readonly string DiscoveryId;
        public readonly string TargetId;
        public readonly int Delta;

        public DiscoveryGrowthSummaryEntry(string discoveryId, string targetId, int delta)
        {
            DiscoveryId = discoveryId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Delta = delta;
        }
    }

    public readonly struct DiscoveryGrowthSummary
    {
        public readonly DiscoveryGrowthSummaryEntry[] Entries;

        public DiscoveryGrowthSummary(DiscoveryGrowthSummaryEntry[] entries) { Entries = entries ?? Array.Empty<DiscoveryGrowthSummaryEntry>(); }

        public static DiscoveryGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<DiscoveryGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++) if (DiscoveryLogCodec.TryParseDelta(resultLogIds[i], out var entry)) entries.Add(entry);
            }
            return new DiscoveryGrowthSummary(entries.ToArray());
        }
    }

    /// <summary>발견 결과 기록 형식: "discovery:&lt;발견&gt;:+&lt;대상&gt;=&lt;변화&gt;".</summary>
    public static class DiscoveryLogCodec
    {
        private const string Prefix = "discovery:";

        public static string EncodeDelta(string discoveryId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(discoveryId) || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + discoveryId + ":" + sign + targetId + "=" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool TryParseDelta(string value, out DiscoveryGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string payload = value.Substring(Prefix.Length);
            int discoveryEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (discoveryEnd <= 0 || equals <= discoveryEnd + 1 || equals + 1 >= payload.Length) return false;
            string discovery = payload.Substring(0, discoveryEnd);
            string target = payload.Substring(discoveryEnd + 1, equals - discoveryEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal)) target = target.Substring(1);
            if (!int.TryParse(payload.Substring(equals + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int delta)) return false;
            entry = new DiscoveryGrowthSummaryEntry(discovery, target, delta);
            return true;
        }
    }

    /// <summary>그 발견을 이미 했을 때. 발견은 범위와 id로 가리킨다(적용 요청 id로 본다).</summary>
    public sealed class PreviousDiscoveryRequirement : IDiscoveryRequirement
    {
        public PreviousDiscoveryRequirement(string discoveryId, DiscoveryScope scope) { DiscoveryId = discoveryId ?? string.Empty; Scope = scope; }
        public string DiscoveryId { get; }
        public DiscoveryScope Scope { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && DiscoveryId.Length > 0 && progress.HasAppliedRequest(DiscoveryRunner.RequestIdFor(Scope, DiscoveryId));
    }

    public sealed class DiscoveryTraitThresholdRequirement : IDiscoveryRequirement
    {
        public DiscoveryTraitThresholdRequirement(string traitId, int minimumValue) { TraitId = traitId ?? string.Empty; MinimumValue = minimumValue; }
        public string TraitId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && TraitId.Length > 0 && progress.GetTraitValue(TraitId) >= Math.Max(0, MinimumValue);
    }

    public sealed class DiscoveryCareerHintRequirement : IDiscoveryRequirement
    {
        public DiscoveryCareerHintRequirement(string careerId) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }
        public bool IsSatisfied(StudentLifeProgress progress) => progress != null && CareerId.Length > 0 && progress.IsCareerHintUnlocked(CareerId);
    }

    public sealed class DiscoveryTraitDeltaOutcome : IDiscoveryOutcome
    {
        public DiscoveryTraitDeltaOutcome(string traitId, int delta) { TraitId = traitId ?? string.Empty; Delta = delta; }
        public string TraitId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || TraitId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddTrait(TraitId, Delta);
            return DiscoveryLogCodec.EncodeDelta(discoveryId, TraitId, Delta);
        }
    }

    public sealed class DiscoverySkillDeltaOutcome : IDiscoveryOutcome
    {
        public DiscoverySkillDeltaOutcome(string skillId, int delta) { SkillId = skillId ?? string.Empty; Delta = delta; }
        public string SkillId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || SkillId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddSkill(SkillId, Delta);
            return DiscoveryLogCodec.EncodeDelta(discoveryId, SkillId, Delta);
        }
    }

    public sealed class DiscoveryStatusDeltaOutcome : IDiscoveryOutcome
    {
        public DiscoveryStatusDeltaOutcome(string statusId, int delta) { StatusId = statusId ?? string.Empty; Delta = delta; }
        public string StatusId { get; }
        public int Delta { get; }

        public string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || StatusId.Length == 0 || Delta == 0) return string.Empty;
            progress.AddStatus(StatusId, Delta);
            return DiscoveryLogCodec.EncodeDelta(discoveryId, StatusId, Delta);
        }
    }

    /// <summary>진로 힌트를 연다. 처음 열 때만 "+&lt;진로&gt;=1"로 기록한다.</summary>
    public sealed class DiscoveryCareerHintOutcome : IDiscoveryOutcome
    {
        public DiscoveryCareerHintOutcome(string careerId) { CareerId = careerId ?? string.Empty; }
        public string CareerId { get; }

        public string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || CareerId.Length == 0) return string.Empty;
            bool wasUnlocked = progress.IsCareerHintUnlocked(CareerId);
            progress.UnlockCareerHint(CareerId);
            return wasUnlocked ? string.Empty : DiscoveryLogCodec.EncodeDelta(discoveryId, CareerId, 1);
        }
    }
}
