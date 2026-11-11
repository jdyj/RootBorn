using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.StudentLife
{
    public interface IWorkRequirement
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    /// <summary>결과를 적용하고 기록 한 줄을 돌려준다. 바뀐 것이 없으면 빈 문자열.</summary>
    public interface IWorkOutcome
    {
        string Apply(StudentLifeProgress progress, string workId);
    }

    /// <summary>알바하는 곳. 장소·NPC·할 수 있는 알바는 id로 가리킨다.</summary>
    public sealed class Workplace
    {
        private readonly string[] _availableWorkIds;

        public Workplace(string id, string displayNameKey, string locationId, string npcId, string[] availableWorkIds)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            NpcId = npcId ?? string.Empty;
            _availableWorkIds = availableWorkIds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string LocationId { get; }
        public string NpcId { get; }
        public IReadOnlyList<string> AvailableWorkIds => _availableWorkIds;
    }

    /// <summary>
    /// 알바 하나. 보상 물건, 비용, 요구 조건, 성장 결과를 값으로 가진다. 미니게임이 있으면 미니게임으로 하는 알바다
    /// (main의 StudentMinigameService가 보는 IMinigamePartTimeWork를 구현한다).
    /// </summary>
    public sealed class PartTimeWork : IMinigamePartTimeWork
    {
        private readonly InventoryGrant[] _rewards;
        private readonly IWorkRequirement[] _requirements;
        private readonly IWorkOutcome[] _outcomes;

        public PartTimeWork(string id, string displayNameKey, Minigame minigame, Workplace workplace, ActivityTimePolicy timePolicy, int timeCostMinutes, int energyCost, int focusCost, int stressDelta,
            InventoryGrant[] rewards, IWorkRequirement[] requirements, IWorkOutcome[] outcomes)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            Minigame = minigame;
            Workplace = workplace;
            TimePolicy = timePolicy;
            TimeCostMinutes = Math.Max(0, timeCostMinutes);
            EnergyCost = Math.Max(0, energyCost);
            FocusCost = Math.Max(0, focusCost);
            StressDelta = stressDelta;
            _rewards = rewards ?? Array.Empty<InventoryGrant>();
            _requirements = requirements ?? Array.Empty<IWorkRequirement>();
            _outcomes = outcomes ?? Array.Empty<IWorkOutcome>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public Minigame Minigame { get; }
        public Workplace Workplace { get; }
        public string WorkplaceId => Workplace != null ? Workplace.Id : string.Empty;
        public string LocationId => Workplace != null ? Workplace.LocationId : string.Empty;
        public string NpcId => Workplace != null ? Workplace.NpcId : string.Empty;
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public IReadOnlyList<InventoryGrant> Rewards => _rewards;
        public IReadOnlyList<IWorkRequirement> Requirements => _requirements;
        public IReadOnlyList<IWorkOutcome> Outcomes => _outcomes;

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

    public readonly struct PartTimeWorkResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string WorkId;
        public readonly string WorkplaceId;
        public readonly string LocationId;
        public readonly string NpcId;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public PartTimeWorkResult(LifeActivityResultKind kind, string saveSlot, string playerId, string workId, string workplaceId, string locationId, string npcId, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            WorkId = workId ?? string.Empty;
            WorkplaceId = workplaceId ?? string.Empty;
            LocationId = locationId ?? string.Empty;
            NpcId = npcId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 알바 실행. 바로 하는 알바는 요청 확인 → 중복 요청 → 시간·체력·집중·보상 자리 → 요구 조건 → 적용 순서다.
    /// 미니게임 알바는 시작할 때 비용을 내고(StudentMinigameService), 성공하면 여기서 보상·성장만 정산한다.
    /// 같은 알바를 다른 요청으로 여러 번 하면 결과 기록을 모두 남긴다(하루 결과가 합친다). 거절하면 아무것도 바꾸지 않는다.
    /// </summary>
    public sealed class PartTimeWorkRunner
    {
        public bool TryPerform(PartTimeWork work, StudentLifeProgress progress, ItemInventory inventory, string requestId, out PartTimeWorkResult result)
        {
            result = Result(LifeActivityResultKind.InvalidRequest, work, progress, requestId, null);
            if (work == null || work.Minigame != null || progress == null || inventory == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = Result(LifeActivityResultKind.DuplicateRequest, work, progress, requestId, null);
                return false;
            }
            if (!progress.CanUseTimePolicy(work.TimePolicy) || !progress.CanSpend(work.EnergyCost, work.FocusCost) || !inventory.CanAddAll(work.Rewards))
            {
                result = Result(LifeActivityResultKind.InsufficientResources, work, progress, requestId, null);
                return false;
            }
            if (!work.HasSatisfiedRequirements(progress))
            {
                result = Result(LifeActivityResultKind.RequirementFailed, work, progress, requestId, null);
                return false;
            }

            progress.TryUseTimePolicy(work.TimePolicy);
            progress.Spend(work.TimeCostMinutes, work.EnergyCost, work.FocusCost, work.StressDelta);
            var logs = new List<string>();
            var rewards = work.Rewards;
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward.Item == null || reward.Count <= 0) continue;
                inventory.Add(reward.Item, reward.Count);
                string log = PartTimeWorkLogCodec.EncodeDelta(work.Id, reward.Item.Id, reward.Count);
                if (log.Length > 0) logs.Add(log);
            }
            logs.AddRange(work.ApplyOutcomes(progress));
            string[] outcomeLogs = logs.ToArray();
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(work.Id, outcomeLogs, true);
            result = Result(LifeActivityResultKind.Applied, work, progress, requestId, outcomeLogs);
            return true;
        }

        /// <summary>
        /// 예약한 미니게임 알바를 정산한다. 예약(종류·알바 id·요청 id)이 맞고 아직 적용하지 않은 요청이어야 한다.
        /// 보상 하나라도 잘못됐거나 자리가 없으면 아무것도 바꾸지 않는다.
        /// </summary>
        public bool TryPerformReserved(PartTimeWork work, StudentLifeProgress progress, ItemInventory inventory, string requestId, out PartTimeWorkResult result)
        {
            result = Result(LifeActivityResultKind.InvalidRequest, work, progress, requestId, null);
            var pending = progress?.PendingMinigame;
            if (work == null || inventory == null || pending == null || pending.Kind != StudentMinigameKind.PartTimeWork || pending.DefinitionId != work.Id || pending.RequestId != requestId || progress.HasAppliedRequest(requestId)) return false;
            var logs = new List<string>();
            var rewards = work.Rewards;
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward.Item == null || reward.Count <= 0) return false;
                logs.Add(PartTimeWorkLogCodec.EncodeDelta(work.Id, reward.Item.Id, reward.Count));
            }
            bool applied = inventory.TryAddAll(rewards, () =>
            {
                logs.AddRange(work.ApplyOutcomes(progress));
                progress.MarkRequestApplied(requestId);
                progress.RecordActivityCompleted(work.Id, logs.ToArray(), true);
            });
            result = Result(applied ? LifeActivityResultKind.Applied : LifeActivityResultKind.InsufficientResources, work, progress, requestId, applied ? logs.ToArray() : null);
            return applied;
        }

        private static PartTimeWorkResult Result(LifeActivityResultKind kind, PartTimeWork work, StudentLifeProgress progress, string requestId, string[] logs)
            => new PartTimeWorkResult(kind, progress?.SaveSlot, progress?.PlayerId, work?.Id, work?.WorkplaceId, work?.LocationId, work?.NpcId, requestId, logs);
    }

    /// <summary>미니게임 알바의 보상 확인과 정산. main의 IPartTimeWorkPayroll을 이 인벤토리로 구현한다. 이 영역의 PartTimeWork만 받는다.</summary>
    public sealed class PartTimeWorkPayroll : IPartTimeWorkPayroll
    {
        private readonly ItemInventory _inventory;
        private readonly PartTimeWorkRunner _runner;

        public PartTimeWorkPayroll(ItemInventory inventory, PartTimeWorkRunner runner = null)
        {
            _inventory = inventory;
            _runner = runner ?? new PartTimeWorkRunner();
        }

        public bool CanReceiveRewards(IMinigamePartTimeWork work) => _inventory != null && work is PartTimeWork partTime && _inventory.CanAddAll(partTime.Rewards);

        public bool TryPerformReserved(IMinigamePartTimeWork work, StudentLifeProgress student, string requestId)
            => work is PartTimeWork partTime && _runner.TryPerformReserved(partTime, student, _inventory, requestId, out _);
    }

    /// <summary>main의 IPartTimeWorkLogReader 구현: 하루 결과 기록에서 알바 id만 고른다.</summary>
    public sealed class PartTimeWorkLogReader : IPartTimeWorkLogReader
    {
        public string[] ReadWorkIds(string[] resultLogIds) => PartTimeWorkDayLog.FromResultLogs(resultLogIds).WorkIds;
    }

    public readonly struct PartTimeWorkDayLog
    {
        public readonly string[] WorkIds;
        public readonly string[] ResultLogIds;

        public PartTimeWorkDayLog(string[] workIds, string[] resultLogIds)
        {
            WorkIds = workIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }

        public static PartTimeWorkDayLog FromResultLogs(string[] resultLogIds)
        {
            var workIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (!PartTimeWorkLogCodec.TryParseDelta(resultLogIds[i], out var entry)) continue;
                    if (entry.WorkId.Length > 0 && !workIds.Contains(entry.WorkId)) workIds.Add(entry.WorkId);
                    filteredLogs.Add(resultLogIds[i]);
                }
            }
            return new PartTimeWorkDayLog(workIds.ToArray(), filteredLogs.ToArray());
        }
    }

    public readonly struct PartTimeWorkGrowthSummaryEntry
    {
        public readonly string WorkId;
        public readonly string TargetId;
        public readonly int Delta;

        public PartTimeWorkGrowthSummaryEntry(string workId, string targetId, int delta)
        {
            WorkId = workId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Delta = delta;
        }
    }

    public readonly struct PartTimeWorkGrowthSummary
    {
        public readonly PartTimeWorkGrowthSummaryEntry[] Entries;

        public PartTimeWorkGrowthSummary(PartTimeWorkGrowthSummaryEntry[] entries) { Entries = entries ?? Array.Empty<PartTimeWorkGrowthSummaryEntry>(); }

        /// <summary>합치지 않고 기록마다 한 줄(원본과 같음). 합친 값이 필요하면 PartTimeWorkLogCodec.AggregateDeltas를 먼저 쓴다.</summary>
        public static PartTimeWorkGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<PartTimeWorkGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++) if (PartTimeWorkLogCodec.TryParseDelta(resultLogIds[i], out var entry)) entries.Add(entry);
            }
            return new PartTimeWorkGrowthSummary(entries.ToArray());
        }
    }

    /// <summary>알바 결과 기록 형식: "part-time-work:&lt;알바&gt;:+&lt;대상&gt;=&lt;변화&gt;". 보상 물건도 같은 형식이다.</summary>
    public static class PartTimeWorkLogCodec
    {
        private const string Prefix = "part-time-work:";

        public static string EncodeDelta(string workId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(workId) || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + workId + ":" + sign + targetId + "=" + delta.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>같은 알바·대상 기록을 첫 자리에 하나로 합친다. 다른 기록은 그대로 둔다. 합이 0이면 그 자리는 빈 문자열이 된다(원본과 같음).</summary>
        public static string[] AggregateDeltas(string[] values)
        {
            if (values == null) return Array.Empty<string>();
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
            {
                if (!TryParseDelta(values[i], out var entry)) continue;
                string key = entry.WorkId + "\n" + entry.TargetId;
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
                string key = entry.WorkId + "\n" + entry.TargetId;
                if (emitted.Add(key)) result.Add(EncodeDelta(entry.WorkId, entry.TargetId, totals[key]));
            }
            return result.ToArray();
        }

        public static bool TryParseDelta(string value, out PartTimeWorkGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string payload = value.Substring(Prefix.Length);
            int workEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (workEnd <= 0 || equals <= workEnd + 1 || equals + 1 >= payload.Length) return false;
            string workId = payload.Substring(0, workEnd);
            string target = payload.Substring(workEnd + 1, equals - workEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal)) target = target.Substring(1);
            if (!int.TryParse(payload.Substring(equals + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int delta)) return false;
            entry = new PartTimeWorkGrowthSummaryEntry(workId, target, delta);
            return true;
        }
    }
}
