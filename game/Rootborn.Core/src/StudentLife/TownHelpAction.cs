using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public interface IHelpActionRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    /// <summary>결과를 적용하고 기록 한 줄을 돌려준다. 바뀐 것이 없으면 빈 문자열.</summary>
    public interface IHelpActionOutcome
    {
        string Apply(StudentLifeProgress progress, string actionId);
    }

    /// <summary>마을 사람 돕기 하나. NPC와 장소는 id로 가리킨다.</summary>
    public sealed class TownHelpAction
    {
        private readonly IHelpActionRequirement[] _requirements;
        private readonly IHelpActionOutcome[] _outcomes;

        public TownHelpAction(string id, string displayNameKey, string npcId, string locationId, ActivityTimePolicy timePolicy, int timeCostMinutes, int energyCost, int focusCost, int stressDelta,
            IHelpActionRequirement[] requirements, IHelpActionOutcome[] outcomes)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            NpcId = npcId ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            TimePolicy = timePolicy;
            TimeCostMinutes = Math.Max(0, timeCostMinutes);
            EnergyCost = Math.Max(0, energyCost);
            FocusCost = Math.Max(0, focusCost);
            StressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<IHelpActionRequirement>();
            _outcomes = outcomes ?? Array.Empty<IHelpActionOutcome>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string NpcId { get; }
        public string LocationId { get; }
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public IReadOnlyList<IHelpActionRequirement> Requirements => _requirements;
        public IReadOnlyList<IHelpActionOutcome> Outcomes => _outcomes;

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

    public readonly struct TownHelpActionResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActionId;
        public readonly string NpcId;
        public readonly string LocationId;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public TownHelpActionResult(LifeActivityResultKind kind, string saveSlot, string playerId, string actionId, string npcId, string locationId, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActionId = actionId ?? string.Empty;
            NpcId = npcId ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    /// <summary>마을 돕기 실행. 순서: 요청 확인 → 중복 요청 → 시간·체력·집중 → 요구 조건 → 적용. 거절하면 아무것도 바꾸지 않는다.</summary>
    public sealed class TownHelpActionRunner
    {
        public bool TryPerform(TownHelpAction action, StudentLifeProgress progress, string requestId, out TownHelpActionResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            TownHelpActionResult Result(LifeActivityResultKind kind, string[] logs = null) => new TownHelpActionResult(kind, saveSlot, playerId, action?.Id, action?.NpcId, action?.LocationId, requestId, logs);

            result = Result(LifeActivityResultKind.InvalidRequest);
            if (action == null || progress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = Result(LifeActivityResultKind.DuplicateRequest);
                return false;
            }
            if (!progress.CanUseTimePolicy(action.TimePolicy) || !progress.CanSpend(action.EnergyCost, action.FocusCost))
            {
                result = Result(LifeActivityResultKind.InsufficientResources);
                return false;
            }
            if (!action.HasSatisfiedRequirements(progress))
            {
                result = Result(LifeActivityResultKind.RequirementFailed);
                return false;
            }

            progress.TryUseTimePolicy(action.TimePolicy);
            progress.Spend(action.TimeCostMinutes, action.EnergyCost, action.FocusCost, action.StressDelta);
            string[] outcomeLogs = action.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(action.Id, outcomeLogs);
            result = Result(LifeActivityResultKind.Applied, outcomeLogs);
            return true;
        }
    }

    public readonly struct TownHelpActionDayLog
    {
        public readonly string[] ActionIds;
        public readonly string[] ResultLogIds;

        public TownHelpActionDayLog(string[] actionIds, string[] resultLogIds)
        {
            ActionIds = actionIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }

        public static TownHelpActionDayLog FromResultLogs(string[] resultLogIds)
        {
            var actionIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (!TownHelpActionLogCodec.TryParseDelta(resultLogIds[i], out var entry)) continue;
                    if (entry.ActionId.Length > 0 && !actionIds.Contains(entry.ActionId)) actionIds.Add(entry.ActionId);
                    filteredLogs.Add(resultLogIds[i]);
                }
            }
            return new TownHelpActionDayLog(actionIds.ToArray(), filteredLogs.ToArray());
        }
    }

    public readonly struct TownHelpActionGrowthSummaryEntry
    {
        public readonly string ActionId;
        public readonly string TargetId;
        public readonly int Delta;

        public TownHelpActionGrowthSummaryEntry(string actionId, string targetId, int delta)
        {
            ActionId = actionId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Delta = delta;
        }
    }

    public readonly struct TownHelpActionGrowthSummary
    {
        public readonly TownHelpActionGrowthSummaryEntry[] Entries;

        public TownHelpActionGrowthSummary(TownHelpActionGrowthSummaryEntry[] entries) { Entries = entries ?? Array.Empty<TownHelpActionGrowthSummaryEntry>(); }

        public static TownHelpActionGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<TownHelpActionGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++) if (TownHelpActionLogCodec.TryParseDelta(resultLogIds[i], out var entry)) entries.Add(entry);
            }
            return new TownHelpActionGrowthSummary(entries.ToArray());
        }
    }

    /// <summary>마을 돕기 결과 기록 형식: "town-help:&lt;돕기&gt;:+&lt;대상&gt;=&lt;변화&gt;".</summary>
    public static class TownHelpActionLogCodec
    {
        private const string Prefix = "town-help:";

        public static string EncodeDelta(string actionId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + actionId + ":" + sign + targetId + "=" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool TryParseDelta(string value, out TownHelpActionGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string payload = value.Substring(Prefix.Length);
            int actionEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (actionEnd <= 0 || equals <= actionEnd + 1 || equals + 1 >= payload.Length) return false;
            string actionId = payload.Substring(0, actionEnd);
            string target = payload.Substring(actionEnd + 1, equals - actionEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal)) target = target.Substring(1);
            if (!int.TryParse(payload.Substring(equals + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int delta)) return false;
            entry = new TownHelpActionGrowthSummaryEntry(actionId, target, delta);
            return true;
        }
    }
}
