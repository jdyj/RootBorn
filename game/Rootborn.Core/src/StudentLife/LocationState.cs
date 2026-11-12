using System;
using System.Collections.Generic;
using Rootborn.Core.Quests;

namespace Rootborn.Core.StudentLife
{
    /// <summary>세계 상태 깃발 조회(세계 상태 영역은 아직 Core에 없다).</summary>
    public interface ILocationStateWorldFlags
    {
        bool IsActive(string flagId);
    }

    /// <summary>NPC 일정 조회(대화·일정 영역은 아직 Core에 없다). 그 시간에 NPC가 있는 장소 id를 준다. 없으면 빈 문자열.</summary>
    public interface ILocationStateNpcSchedules
    {
        string ResolveLocationId(string scheduleId, int day, string timeSlotId, StudentLifeProgress progress);
    }

    /// <summary>장소 상태를 고를 때 보는 값. 장소와 시간대는 id로 받는다. 시간대가 비면 진행 상태의 시각으로 정한다.</summary>
    public readonly struct LocationStateContext
    {
        public readonly string LocationId;
        public readonly string TimeSlotId;
        public readonly StudentLifeProgress StudentProgress;
        public readonly ILocationStateWorldFlags WorldFlags;
        public readonly QuestLog QuestLog;
        public readonly ILocationStateNpcSchedules NpcSchedules;
        public readonly LocationStateProgress LocationStateProgress;

        public LocationStateContext(string locationId, string timeSlotId, StudentLifeProgress studentProgress, ILocationStateWorldFlags worldFlags, QuestLog questLog, ILocationStateNpcSchedules npcSchedules, LocationStateProgress locationStateProgress)
        {
            LocationId = locationId ?? string.Empty;
            TimeSlotId = timeSlotId ?? string.Empty;
            StudentProgress = studentProgress;
            WorldFlags = worldFlags;
            QuestLog = questLog;
            NpcSchedules = npcSchedules;
            LocationStateProgress = locationStateProgress;
        }

        /// <summary>주어진 시간대, 없으면 진행 상태의 시각으로 정한 시간대.</summary>
        public string ResolveTimeSlotId() => !string.IsNullOrEmpty(TimeSlotId) ? TimeSlotId : LocationStateTimeSlots.FromProgress(StudentProgress);
    }

    /// <summary>
    /// 진행 상태 시각을 시간대 id로 바꾼다. 원본 NpcScheduleResolver.ResolveTimeSlotId를 따르되:
    /// 하루 시작(흐르는 시간의 07:00, 시간 규칙에서) 전의 새벽은 밤, 12:00 전은 오전, 17:00 전은 오후, 21:00 전은 저녁, 그 뒤는 밤.
    /// 시각은 24시간으로 감아서 본다(같은 날의 26:00 = 02:00). 원본은 정확히 8:00과 0분 이하만 오전이라 7:00과 9:00이 오후였다.
    /// 흐르는 시간(2026-11-12) 전에는 새벽에 깨어 있을 일이 없어 00:00~11:59를 모두 오전으로 봤다.
    /// </summary>
    public static class LocationStateTimeSlots
    {
        public const string Morning = "time.morning";
        public const string Afternoon = "time.afternoon";
        public const string Evening = "time.evening";
        public const string Night = "time.night";

        /// <summary>시간 규칙을 모를 때의 하루 시작(game/time.json의 dayStart와 같은 07:00).</summary>
        public const int DefaultDayStartMinutes = 7 * 60;

        private const int MinutesPerDay = 24 * 60;

        /// <summary>진행의 시각과 진행에 넣은 시간 규칙의 하루 시작으로 정한다(진행이 없으면 08:00).</summary>
        public static string FromProgress(StudentLifeProgress progress)
            => progress == null ? FromMinutes(8 * 60) : FromMinutes(progress.TimeMinutes, progress.TimeRules?.DayStartMinutes ?? DefaultDayStartMinutes);

        public static string FromMinutes(int minutes, int dayStartMinutes = DefaultDayStartMinutes)
        {
            int clock = minutes % MinutesPerDay;
            if (clock < 0) clock += MinutesPerDay;
            if (clock < dayStartMinutes) return Night;
            if (clock < 12 * 60) return Morning;
            if (clock < 17 * 60) return Afternoon;
            if (clock < 21 * 60) return Evening;
            return Night;
        }

        /// <summary>
        /// 그날의 분 축 [dayStartMinutes, dayEndMinutes)에서 시간대가 시작하는 때를 순서대로 into에 채운다(먼저 비운다).
        /// 경계를 따로 적지 않고 <see cref="FromMinutes"/>가 바뀌는 분을 찾는다 — 그래서 두 곳의 경계가 어긋날 수 없다.
        /// 하루 시작 07:00이면 (420, 오전), (720, 오후), (1020, 저녁), (1260, 밤). 자정 뒤 새벽도 밤이라 새 시간대가 아니다.
        /// </summary>
        public static void SlotStarts(int dayStartMinutes, int dayEndMinutes, List<(int Minutes, string SlotId)> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            int dayStartClock = dayStartMinutes % MinutesPerDay;
            if (dayStartClock < 0) dayStartClock += MinutesPerDay;
            string last = null;
            for (int m = dayStartMinutes; m < dayEndMinutes; m++)
            {
                string slot = FromMinutes(m, dayStartClock);
                if (string.Equals(slot, last, StringComparison.Ordinal)) continue;
                into.Add((m, slot));
                last = slot;
            }
        }
    }

    public interface ILocationStateCondition
    {
        bool IsSatisfied(in LocationStateContext context);
    }

    public interface ILocationStateEffect
    {
        void AppendTo(LocationStateSummaryBuilder builder);
    }

    public enum LocationStateConflictMode
    {
        HighestPriority,
        MergeAll,
        FallbackFirst
    }

    /// <summary>장소 하나의 시간·조건별 상태. 조건을 모두 만족하면 고를 수 있고, 효과는 그 장소에 보이는 것(활동, NPC, 단서 등)을 더한다.</summary>
    public sealed class LocationState
    {
        private readonly ILocationStateCondition[] _conditions;
        private readonly ILocationStateEffect[] _effects;

        public LocationState(string id, string displayNameKey, string descriptionKey, string locationId, int priority, ILocationStateCondition[] conditions, ILocationStateEffect[] effects, bool isHidden = false)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            LocationId = locationId ?? string.Empty;
            Priority = priority;
            IsHidden = isHidden;
            _conditions = conditions ?? Array.Empty<ILocationStateCondition>();
            _effects = effects ?? Array.Empty<ILocationStateEffect>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string LocationId { get; }
        public int Priority { get; }
        /// <summary>아직 알려지지 않은 상태. 요약과 오늘 기록에 숨김으로 남는다.</summary>
        public bool IsHidden { get; }
        public IReadOnlyList<ILocationStateCondition> Conditions => _conditions;
        public IReadOnlyList<ILocationStateEffect> Effects => _effects;

        public bool IsSatisfied(in LocationStateContext context)
        {
            if (LocationId.Length > 0 && context.LocationId.Length > 0 && LocationId != context.LocationId) return false;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.IsSatisfied(in context)) return false;
            }
            return true;
        }

        public void AppendEffects(LocationStateSummaryBuilder builder)
        {
            if (builder == null) return;
            for (int i = 0; i < _effects.Length; i++) _effects[i]?.AppendTo(builder);
        }
    }

    /// <summary>장소 id별로 상태를 한 번만 묶어 둔다.</summary>
    public sealed class LocationStateLookupCache
    {
        private readonly Dictionary<string, List<LocationState>> _statesByLocationId = new Dictionary<string, List<LocationState>>(StringComparer.Ordinal);

        public LocationStateLookupCache(IReadOnlyList<LocationState> states)
        {
            BuildCount = 1;
            if (states == null) return;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null || string.IsNullOrEmpty(state.LocationId)) continue;
                if (!_statesByLocationId.TryGetValue(state.LocationId, out var list))
                {
                    list = new List<LocationState>();
                    _statesByLocationId.Add(state.LocationId, list);
                }
                list.Add(state);
            }
        }

        public int BuildCount { get; }

        public IReadOnlyList<LocationState> GetStatesForLocation(string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return Array.Empty<LocationState>();
            return _statesByLocationId.TryGetValue(locationId, out var list) ? list : (IReadOnlyList<LocationState>)Array.Empty<LocationState>();
        }
    }

    /// <summary>장소의 지금 상태를 고르고 요약을 만든다. 진행 상태가 있으면 첫 상태를 발견으로 기록한다.</summary>
    public sealed class LocationStateResolver
    {
        private readonly LocationStateLookupCache _cache;
        private readonly LocationStateConflictMode _mode;

        public LocationStateResolver(LocationStateLookupCache cache, LocationStateConflictMode mode)
        {
            _cache = cache;
            _mode = mode;
        }

        public LocationStateSummaryModel Resolve(in LocationStateContext context)
        {
            var candidates = _cache != null ? _cache.GetStatesForLocation(context.LocationId) : Array.Empty<LocationState>();
            var matches = new List<LocationState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate != null && candidate.IsSatisfied(in context)) matches.Add(candidate);
            }
            if (matches.Count == 0) return LocationStateSummaryModel.Empty(context.LocationId);

            var selected = Select(matches);
            var builder = new LocationStateSummaryBuilder(context.LocationId);
            for (int i = 0; i < selected.Count; i++) builder.AddState(selected[i]);
            var summary = builder.Build();
            if (context.LocationStateProgress != null && summary.PrimaryState != null)
            {
                context.LocationStateProgress.MarkDiscovered(summary.LocationId, summary.PrimaryState.Id, context.StudentProgress != null ? context.StudentProgress.CurrentDay : 1, context.TimeSlotId, summary.DisplayName, summary.IsHidden);
            }
            return summary;
        }

        private List<LocationState> Select(List<LocationState> matches)
        {
            if (_mode == LocationStateConflictMode.MergeAll) return matches;
            var selected = matches[0];
            if (_mode == LocationStateConflictMode.HighestPriority)
            {
                for (int i = 1; i < matches.Count; i++) if (matches[i].Priority > selected.Priority) selected = matches[i];
            }
            return new List<LocationState> { selected };
        }
    }

    public sealed class LocationStateSummaryBuilder
    {
        private readonly string _locationId;
        private readonly List<LocationState> _states = new List<LocationState>();
        private readonly List<string> _activityIds = new List<string>();
        private readonly List<string> _npcIds = new List<string>();
        private readonly List<string> _clueIds = new List<string>();
        private readonly List<string> _objectIds = new List<string>();
        private readonly List<string> _portalIds = new List<string>();
        private readonly List<string> _shopItemIds = new List<string>();

        public LocationStateSummaryBuilder(string locationId) { _locationId = locationId ?? string.Empty; }

        public void AddState(LocationState state)
        {
            if (state == null) return;
            _states.Add(state);
            state.AppendEffects(this);
        }

        public void AddActivities(string[] values) => AddIds(_activityIds, values);
        public void AddNpcs(string[] values) => AddIds(_npcIds, values);
        public void AddClues(string[] values) => AddIds(_clueIds, values);
        public void AddInteractableObjects(string[] values) => AddIds(_objectIds, values);
        public void AddPortals(string[] values) => AddIds(_portalIds, values);
        public void AddShopItems(string[] values) => AddIds(_shopItemIds, values);

        /// <summary>숨김 여부는 첫 상태를 따른다(원본은 항상 false였다).</summary>
        public LocationStateSummaryModel Build()
        {
            var primary = _states.Count > 0 ? _states[0] : null;
            var stateIds = new string[_states.Count];
            for (int i = 0; i < _states.Count; i++) stateIds[i] = _states[i].Id;
            return new LocationStateSummaryModel(_locationId, primary, stateIds, primary != null ? primary.DisplayNameKey : string.Empty, primary != null ? primary.DescriptionKey : string.Empty, primary != null && primary.IsHidden,
                _activityIds.ToArray(), _npcIds.ToArray(), _clueIds.ToArray(), _objectIds.ToArray(), _portalIds.ToArray(), _shopItemIds.ToArray());
        }

        private static void AddIds(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                var value = values[i];
                if (!string.IsNullOrEmpty(value) && !target.Contains(value)) target.Add(value);
            }
        }
    }

    public readonly struct LocationStateSummaryModel
    {
        public readonly string LocationId;
        public readonly LocationState PrimaryState;
        public readonly string[] StateIds;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly bool IsHidden;
        public readonly string[] AvailableActivityIds;
        public readonly string[] PresentNpcIds;
        public readonly string[] ClueIds;
        public readonly string[] InteractableObjectIds;
        public readonly string[] PortalIds;
        public readonly string[] ShopItemIds;

        public LocationStateSummaryModel(string locationId, LocationState primaryState, string[] stateIds, string displayName, string description, bool isHidden,
            string[] availableActivityIds, string[] presentNpcIds, string[] clueIds, string[] interactableObjectIds, string[] portalIds, string[] shopItemIds)
        {
            LocationId = locationId ?? string.Empty;
            PrimaryState = primaryState;
            StateIds = stateIds ?? Array.Empty<string>();
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IsHidden = isHidden;
            AvailableActivityIds = availableActivityIds ?? Array.Empty<string>();
            PresentNpcIds = presentNpcIds ?? Array.Empty<string>();
            ClueIds = clueIds ?? Array.Empty<string>();
            InteractableObjectIds = interactableObjectIds ?? Array.Empty<string>();
            PortalIds = portalIds ?? Array.Empty<string>();
            ShopItemIds = shopItemIds ?? Array.Empty<string>();
        }

        public static LocationStateSummaryModel Empty(string locationId)
        {
            return new LocationStateSummaryModel(locationId, null, null, null, null, false, null, null, null, null, null, null);
        }
    }
}
