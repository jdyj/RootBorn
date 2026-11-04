using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Game.Careers;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public sealed class DailyPlanProgressSaveData
    {
        public static bool IsEmpty(DailyPlanProgressSaveData data) => data == null || (data.Version == 0
            && string.IsNullOrEmpty(data.SaveSlot) && string.IsNullOrEmpty(data.PlayerId)
            && string.IsNullOrEmpty(data.SelectedRouteId) && string.IsNullOrEmpty(data.CompletedRouteId)
            && string.IsNullOrEmpty(data.MatchedActivityId) && string.IsNullOrEmpty(data.LastNextHintKey)
            && (data.AppliedCompletionKeys == null || data.AppliedCompletionKeys.Length == 0)
            && (data.Slots == null || data.Slots.Length == 0) && (data.Diary == null || data.Diary.Length == 0));
        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public int CurrentDay = 1;
        public string SelectedRouteId;
        public string CompletedRouteId;
        public string MatchedActivityId;
        public string[] AppliedCompletionKeys = Array.Empty<string>();
        public string LastNextHintKey;
        public DailyPlanSlotSaveData[] Slots = Array.Empty<DailyPlanSlotSaveData>();
        public DailyPlanDaySaveData[] Diary = Array.Empty<DailyPlanDaySaveData>();
    }

    public sealed class DailyPlanProgress
    {
        private readonly HashSet<string> _appliedCompletionKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<DailyPlanSlotRecord> _slots = new List<DailyPlanSlotRecord>();
        private readonly List<DailyPlanDayRecord> _diary = new List<DailyPlanDayRecord>();
        private readonly IReadOnlyList<DailyPlanSlotRecord> _slotView;
        private readonly IReadOnlyList<DailyPlanDayRecord> _diaryView;

        public DailyPlanProgress(string saveSlot, string playerId, int currentDay)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            CurrentDay = Mathf.Max(1, currentDay);
            _slotView = _slots.AsReadOnly(); _diaryView = _diary.AsReadOnly();
            _slots.Add(new DailyPlanSlotRecord(0, "daily-plan.period.after-school"));
        }
        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int CurrentDay { get; private set; }
        public int SlotCount => _slots.Count;
        public IReadOnlyList<DailyPlanSlotRecord> Slots => _slotView;
        public IReadOnlyList<DailyPlanDayRecord> Diary => _diaryView;
        public IReadOnlyCollection<string> AppliedCompletionKeys => _appliedCompletionKeys;
        internal bool HasAssignedSlots { get { foreach (var slot in _slots) if (!string.IsNullOrEmpty(slot.RouteId)) return true; return false; } }
        public string SelectedRouteId => PendingOrLast()?.RouteId ?? string.Empty;
        public string CompletedRouteId => LastCompleted()?.RouteId ?? string.Empty;
        public string MatchedActivityId => LastCompleted()?.MatchedActivityId ?? string.Empty;
        public string LastNextHintKey => PendingOrLast()?.NextHintKey ?? string.Empty;

        public bool SynchronizeDay(StudentLifeProgress student)
        {
            if (!SameIdentity(student) || student.CurrentDay < CurrentDay) return false;
            bool changed = false;
            if (student.CurrentDay > CurrentDay)
            {
                var prior = ToSaveData();
                _diary.Add(new DailyPlanDayRecord(CurrentDay, prior.Slots, prior.AppliedCompletionKeys));
                _slots.Clear(); _appliedCompletionKeys.Clear();
                CurrentDay = student.CurrentDay; changed = true;
            }
            int desired = student.Calendar != null ? student.Calendar.GetBudget(CurrentDay) : Mathf.Max(1, _slots.Count);
            // Calendar edits cannot silently discard a previously planned/completed slot.
            while (_slots.Count > desired && string.IsNullOrEmpty(_slots[_slots.Count - 1].RouteId))
            { _slots.RemoveAt(_slots.Count - 1); changed = true; }
            while (_slots.Count < desired) { _slots.Add(new DailyPlanSlotRecord(_slots.Count, string.Empty)); changed = true; }
            if (student.Calendar != null)
                for (int i = 0; i < _slots.Count; i++)
                {
                    string period = PeriodKey(student.Calendar.IsWeekend(CurrentDay), i, _slots.Count);
                    if (_slots[i].PeriodKey != period) { _slots[i].PeriodKey = period; changed = true; }
                }
            return changed;
        }

        public bool TryAssignSlot(int slotIndex, DailyPlanRouteDefinition route, StudentLifeProgress student)
        {
            if (route == null || !route.IsAvailable(student)) return false;
            return AssignSlotValidated(slotIndex, route, student);
        }
        internal bool AssignSlotValidated(int index, DailyPlanRouteDefinition route, StudentLifeProgress student)
        {
            if (!CanMutate(student) || route == null || string.IsNullOrEmpty(route.Id)) return false;
            SynchronizeDay(student);
            return AssignAt(index, route);
        }
        internal bool CanMutate(StudentLifeProgress student) => SameIdentity(student)
            && student.DayState == StudentDayState.InProgress && student.CurrentDay >= CurrentDay;
        private bool SameIdentity(StudentLifeProgress student) => student != null && student.SaveSlot == SaveSlot && student.PlayerId == PlayerId;
        private bool AssignAt(int index, DailyPlanRouteDefinition route)
        {
            if (index < 0 || index >= _slots.Count || _slots[index].Completed || _slots[index].RouteId == route.Id) return false;
            var slot = _slots[index]; slot.RouteId = route.Id; slot.NextHintKey = route.NextHintKey;
            return true;
        }

        // Compatibility adapter: select the first unfinished slot, retaining all receipts.
        public bool SelectRoute(DailyPlanRouteDefinition route)
        {
            if (route == null || string.IsNullOrEmpty(route.Id)) return false;
            for (int i = 0; i < _slots.Count; i++) if (!_slots[i].Completed) return AssignAt(i, route);
            return false;
        }
        public bool CompleteRoute(DailyPlanRouteDefinition route, LocationActivityDefinition activity, string requestKey)
        {
            if (route == null || activity == null || string.IsNullOrEmpty(requestKey) || _appliedCompletionKeys.Contains(requestKey)) return false;
            for (int i = 0; i < _slots.Count; i++)
                if (!_slots[i].Completed && _slots[i].RouteId == route.Id) return CompleteSlot(i, route, activity, requestKey);
            return false;
        }
        internal bool CompleteSlot(int index, DailyPlanRouteDefinition route, LocationActivityDefinition activity, string key)
            => activity != null && CompleteTypedSlot(index, route, activity.Id, DailyPlanActivityKind.LocationActivity, key);
        internal bool CompleteTypedSlot(int index, DailyPlanRouteDefinition route, string definitionId, DailyPlanActivityKind kind, string key)
        {
            if (index < 0 || index >= _slots.Count || route == null || string.IsNullOrEmpty(definitionId) || string.IsNullOrEmpty(key)
                || _appliedCompletionKeys.Contains(key) || _slots[index].Completed || _slots[index].RouteId != route.Id) return false;
            var slot = _slots[index]; slot.Completed = true; slot.MatchedActivityId = definitionId; slot.MatchedActivityKind = kind;
            slot.CompletionRequestKey = key; slot.NextHintKey = route.NextHintKey; _appliedCompletionKeys.Add(key);
            return true;
        }
        internal void RecordResultLogs(int index, string[] logs) => _slots[index].SetResultLogs(logs);
        private DailyPlanSlotRecord PendingOrLast()
        {
            foreach (var slot in _slots) if (!slot.Completed && !string.IsNullOrEmpty(slot.RouteId)) return slot;
            return LastCompleted();
        }
        private DailyPlanSlotRecord LastCompleted()
        { for (int i = _slots.Count - 1; i >= 0; i--) if (_slots[i].Completed) return _slots[i]; return null; }
        private static string PeriodKey(bool weekend, int index, int count)
        {
            if (index == count - 1) return "daily-plan.period.evening";
            if (weekend) return index == 0 ? "daily-plan.period.morning" : "daily-plan.period.afternoon";
            return "daily-plan.period.after-school";
        }

        public DailyPlanProgressSaveData ToSaveData()
        {
            var keys = new string[_appliedCompletionKeys.Count]; _appliedCompletionKeys.CopyTo(keys); Array.Sort(keys, StringComparer.Ordinal);
            var slots = new DailyPlanSlotSaveData[_slots.Count]; for (int i = 0; i < slots.Length; i++) slots[i] = _slots[i].ToSaveData();
            var diary = new DailyPlanDaySaveData[_diary.Count]; for (int i = 0; i < diary.Length; i++) diary[i] = _diary[i].ToSaveData();
            return new DailyPlanProgressSaveData { Version = 2, SaveSlot = SaveSlot, PlayerId = PlayerId, CurrentDay = CurrentDay,
                SelectedRouteId = SelectedRouteId, CompletedRouteId = CompletedRouteId, MatchedActivityId = MatchedActivityId,
                AppliedCompletionKeys = keys, LastNextHintKey = LastNextHintKey, Slots = slots, Diary = diary };
        }

        public static DailyPlanProgress FromSaveData(DailyPlanProgressSaveData data, string fallbackSaveSlot, string fallbackPlayerId, int fallbackDay)
        {
            var result = new DailyPlanProgress(fallbackSaveSlot, fallbackPlayerId, fallbackDay);
            if (DailyPlanProgressSaveData.IsEmpty(data) || (!string.IsNullOrEmpty(data.SaveSlot) && data.SaveSlot != result.SaveSlot)
                || (!string.IsNullOrEmpty(data.PlayerId) && data.PlayerId != result.PlayerId)
                || data.CurrentDay > Mathf.Max(1, fallbackDay)) return result;
            result.CurrentDay = Mathf.Max(1, data.CurrentDay);
            result._slots.Clear();
            if (data.Version >= 2 && data.Slots != null)
            {
                for (int i = 0; i < data.Slots.Length; i++) result._slots.Add(DailyPlanSlotRecord.FromSaveData(data.Slots[i], i));
            }
            else
            {
                var slot = new DailyPlanSlotSaveData { RouteId = data.SelectedRouteId, Completed = !string.IsNullOrEmpty(data.CompletedRouteId)
                    && data.CompletedRouteId == data.SelectedRouteId, MatchedActivityId = data.MatchedActivityId, NextHintKey = data.LastNextHintKey,
                    CompletionRequestKey = data.AppliedCompletionKeys != null && data.AppliedCompletionKeys.Length > 0 ? data.AppliedCompletionKeys[0] : string.Empty };
                result._slots.Add(DailyPlanSlotRecord.FromSaveData(slot, 0));
            }
            if (data.AppliedCompletionKeys != null)
                foreach (string key in data.AppliedCompletionKeys) if (!string.IsNullOrEmpty(key)) result._appliedCompletionKeys.Add(key);
            foreach (var slot in result._slots)
                if (slot.Completed && !string.IsNullOrEmpty(slot.CompletionRequestKey)) result._appliedCompletionKeys.Add(slot.CompletionRequestKey);
            if (data.Diary != null)
            {
                var days = new HashSet<int>();
                foreach (var day in data.Diary)
                    if (day != null && day.DayNumber > 0 && day.DayNumber < result.CurrentDay && days.Add(day.DayNumber))
                        result._diary.Add(new DailyPlanDayRecord(day.DayNumber, day.Slots, day.CompletionKeys));
                result._diary.Sort((left, right) => left.DayNumber.CompareTo(right.DayNumber));
            }
            return result;
        }
    }

    public readonly struct DailyPlanCompletionResult
    {
        public DailyPlanCompletionResult(bool isCompleted, DailyPlanRouteDefinition route, LocationActivityDefinition activity, string[] resultLogs, CareerPracticeDefinition practice = null, PartTimeWorkDefinition work = null)
        { IsCompleted = isCompleted; Route = route; Activity = activity; ResultLogs = resultLogs ?? Array.Empty<string>(); Practice = practice; Work = work; }
        public bool IsCompleted { get; }
        public DailyPlanRouteDefinition Route { get; }
        public LocationActivityDefinition Activity { get; }
        public CareerPracticeDefinition Practice { get; }
        public PartTimeWorkDefinition Work { get; }
        public string[] ResultLogs { get; }
        public static DailyPlanCompletionResult Empty => new DailyPlanCompletionResult(false, null, null, Array.Empty<string>());
    }

    public sealed class DailyPlanRouteRunner
    {
        public DailyPlanRouteDefinition[] GetAvailableRoutes(DailyPlanRouteSetDefinition[] sets, StudentLifeProgress student) => GetAvailableRoutes(sets, new DailyPlanRouteContext(student));
        public DailyPlanRouteDefinition[] GetAvailableRoutes(DailyPlanRouteSetDefinition[] sets, in DailyPlanRouteContext context)
        {
            if (sets == null || context.StudentProgress == null) return Array.Empty<DailyPlanRouteDefinition>();
            var routes = new List<DailyPlanRouteDefinition>();
            foreach (var set in sets) if (set != null) foreach (var route in set.GetAvailableRoutes(in context)) if (!routes.Contains(route)) routes.Add(route);
            return routes.ToArray();
        }
        public bool TrySelectRoute(DailyPlanProgress daily, DailyPlanRouteDefinition route, StudentLifeProgress student) => TrySelectRoute(daily, route, new DailyPlanRouteContext(student));
        public bool TrySelectRoute(DailyPlanProgress daily, DailyPlanRouteDefinition route, in DailyPlanRouteContext context)
        {
            if (daily == null || !daily.CanMutate(context.StudentProgress) || route == null || !route.IsAvailable(in context)) return false;
            daily.SynchronizeDay(context.StudentProgress);
            return daily.SelectRoute(route);
        }
        public bool TryRecordActivity(DailyPlanProgress daily, DailyPlanRouteSetDefinition[] sets, LocationActivityDefinition activity,
            StudentLifeProgress student, string requestKey, out DailyPlanCompletionResult result)
            => TryRecordScheduledActivity(daily, sets, activity, student, student?.CurrentDay ?? 0, requestKey, out result);

        public bool TryRecordScheduledActivity(DailyPlanProgress daily, DailyPlanRouteSetDefinition[] sets, LocationActivityDefinition activity,
            StudentLifeProgress student, int activityDay, string requestKey, out DailyPlanCompletionResult result)
            => TryRecordTyped(daily, sets, student, activityDay, requestKey, activity != null ? activity.Id : null,
                DailyPlanActivityKind.LocationActivity, route => route.Matches(activity), activity, null, null, out result);

        public bool TryRecordPractice(DailyPlanProgress daily, DailyPlanRouteSetDefinition[] sets, CareerPracticeDefinition practice,
            StudentLifeProgress student, int activityDay, string requestKey, out DailyPlanCompletionResult result)
            => TryRecordTyped(daily, sets, student, activityDay, requestKey, practice != null ? practice.Id : null,
                DailyPlanActivityKind.Practice, route => route.MatchesPractice(practice), null, practice, null, out result);

        public bool TryRecordPartTimeWork(DailyPlanProgress daily, DailyPlanRouteSetDefinition[] sets, PartTimeWorkDefinition work,
            StudentLifeProgress student, int activityDay, string requestKey, out DailyPlanCompletionResult result)
            => TryRecordTyped(daily, sets, student, activityDay, requestKey, work != null ? work.Id : null,
                DailyPlanActivityKind.PartTimeWork, route => route.MatchesWork(work), null, null, work, out result);

        private bool TryRecordTyped(DailyPlanProgress daily, DailyPlanRouteSetDefinition[] sets, StudentLifeProgress student,
            int activityDay, string requestKey, string definitionId, DailyPlanActivityKind kind, Func<DailyPlanRouteDefinition, bool> matches,
            LocationActivityDefinition activity, CareerPracticeDefinition practice, PartTimeWorkDefinition work, out DailyPlanCompletionResult result)
        {
            result = DailyPlanCompletionResult.Empty;
            if (daily == null || sets == null || string.IsNullOrEmpty(definitionId) || !daily.CanMutate(student)
                || activityDay != student.CurrentDay || string.IsNullOrEmpty(requestKey)) return false;
            daily.SynchronizeDay(student);
            for (int i = 0; i < daily.Slots.Count; i++)
            {
                var slot = daily.Slots[i];
                if (slot.Completed || string.IsNullOrEmpty(slot.RouteId)) continue;
                var route = FindRoute(sets, slot.RouteId);
                if (route == null || !matches(route)) continue;
                if (!daily.CompleteTypedSlot(i, route, definitionId, kind, requestKey)) return false;
                var logs = route.ApplyOutcomes(student);
                daily.RecordResultLogs(i, logs);
                result = new DailyPlanCompletionResult(true, route, activity, logs, practice, work);
                return true;
            }
            return false;
        }
        public DailyPlanRouteDefinition FindRoute(DailyPlanRouteSetDefinition[] sets, string routeId)
        {
            if (sets == null || string.IsNullOrEmpty(routeId)) return null;
            foreach (var set in sets) if (set != null) foreach (var route in set.Routes) if (route != null && route.Id == routeId) return route;
            return null;
        }
    }

    public static class DailyPlanProgressPersistence
    {
        private const string DailyPlanFileName = "daily-plan-progress.json";
        public static DailyPlanProgress LoadOrCreate(string saveSlot, string playerId, int currentDay)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            var storage = new SaveService(slot);
            if (AdultCareerSnapshotPersistence.HasCommittedFile(storage, player))
            {
                var primary = AdultCareerSnapshotPersistence.Load(storage, player);
                return DailyPlanProgress.FromSaveData(primary?.Student?.DailyPlan, slot, player, primary?.Student?.CurrentDay ?? currentDay);
            }
            return DailyPlanProgress.FromSaveData(ReadLegacy(storage, player), slot, player, currentDay);
        }
        public static bool MigrateInto(StudentLifeProgress student)
        {
            if (student == null) return false;
            var storage = new SaveService(student.SaveSlot);
            if (AdultCareerSnapshotPersistence.HasCommittedFile(storage, student.PlayerId)) return false;
            return student.TryImportLegacyDailyPlan(ReadLegacy(storage, student.PlayerId));
        }
        private static DailyPlanProgressSaveData ReadLegacy(SaveService storage, string player)
        {
            string json = storage.ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json)) json = storage.ReadJson(LegacyFileNameFor(player));
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<DailyPlanProgressSaveData>(json);
                return data != null && data.SaveSlot == storage.Slot && data.PlayerId == player ? data : null;
            }
            catch (ArgumentException) { return null; }
        }
        public static void Save(DailyPlanProgress progress)
        {
            if (progress == null) return;
            var storage = new SaveService(progress.SaveSlot);
            if (AdultCareerSnapshotPersistence.HasCommittedFile(storage, progress.PlayerId)) return;
            storage.WriteJson(FileNameFor(progress.PlayerId), JsonUtility.ToJson(progress.ToSaveData(), true));
        }
        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player") return DailyPlanFileName;
            var encoded = new StringBuilder(playerId.Length * 4);
            foreach (char value in playerId) encoded.Append(((int)value).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            return "daily-plan-progress-" + encoded + ".json";
        }
        private static string LegacyFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player") return DailyPlanFileName;
            var chars = playerId.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return "daily-plan-progress-" + new string(chars) + ".json";
        }
    }
}
