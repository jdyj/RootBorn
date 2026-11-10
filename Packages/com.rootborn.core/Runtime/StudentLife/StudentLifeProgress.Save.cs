using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다(바꾸면 옛 세이브를 못 읽는다).
    /// 일과·미니게임·진로 후보 필드는 그 부분을 옮길 때 더한다.
    /// </summary>
    [Serializable]
    public sealed class StudentLifeProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public int Energy;
        public int Focus;
        public int Stress;
        public int TimeMinutes;
        public int CurrentDay = 1;
        public int TimePointsSpentToday;
        public string[] DailyTimePolicyIds = Array.Empty<string>();
        public string DayState = StudentDayState.InProgress.ToString();
        public int LastSettledDay;
        public string NextDayEntryPointId;
        public string LastActivityId;
        public string TutorialStageId;
        public string NextObjectiveId;
        public string NextGuideText;
        public StatEntry[] Traits = Array.Empty<StatEntry>();
        public StatEntry[] Skills = Array.Empty<StatEntry>();
        public StatEntry[] Relationships = Array.Empty<StatEntry>();
        public StatEntry[] StudentConditionStatuses = Array.Empty<StatEntry>();
        public string[] CareerHintIds = Array.Empty<string>();
        public string[] AppliedRequestIds = Array.Empty<string>();
        public string[] ActivityLogIds = Array.Empty<string>();
        public string[] TodayActivityIds = Array.Empty<string>();
        public string[] TodayResultLogIds = Array.Empty<string>();
        public string[] PreviousDayActivityIds = Array.Empty<string>();
        public string[] PreviousDayResultLogIds = Array.Empty<string>();

        [Serializable]
        public struct StatEntry
        {
            public string Id;
            public int Value;
        }
    }

    public sealed partial class StudentLifeProgress
    {
        public StudentLifeProgressSaveData ToSaveData()
        {
            return new StudentLifeProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Energy = Energy,
                Focus = Focus,
                Stress = Stress,
                TimeMinutes = TimeMinutes,
                CurrentDay = CurrentDay,
                TimePointsSpentToday = TimePointsSpentToday,
                DailyTimePolicyIds = ToArray(_dailyTimePolicyIds),
                DayState = DayState.ToString(),
                LastSettledDay = LastSettledDay,
                NextDayEntryPointId = NextDayEntryPointId,
                LastActivityId = LastActivityId,
                TutorialStageId = TutorialStageId,
                NextObjectiveId = NextObjectiveId,
                NextGuideText = NextGuideText,
                Traits = ToEntries(_traitValues),
                Skills = ToEntries(_skillValues),
                Relationships = ToEntries(_relationshipValues),
                StudentConditionStatuses = ToEntries(_statusValues),
                CareerHintIds = ToArray(_careerHintIds),
                AppliedRequestIds = ToArray(_appliedRequestIds),
                ActivityLogIds = _activityLogIds.ToArray(),
                TodayActivityIds = _todayActivityIds.ToArray(),
                TodayResultLogIds = _todayResultLogIds.ToArray(),
                PreviousDayActivityIds = _previousDayActivityIds.ToArray(),
                PreviousDayResultLogIds = _previousDayResultLogIds.ToArray(),
            };
        }

        /// <summary>세이브에서 되살린다. 없으면 빈 기본 진행 상태를 준다. 음수와 빈 id는 버린다.</summary>
        public static StudentLifeProgress FromSaveData(StudentLifeProgressSaveData saveData)
        {
            if (saveData == null) return new StudentLifeProgress("default", "player", 0, 0);
            var progress = new StudentLifeProgress(saveData.SaveSlot, saveData.PlayerId, saveData.Energy, saveData.Focus, saveData.Stress, saveData.TimeMinutes);
            progress.CurrentDay = Math.Max(1, saveData.CurrentDay);
            progress.DayState = Enum.TryParse(saveData.DayState, out StudentDayState state) ? state : StudentDayState.InProgress;
            progress.TimePointsSpentToday = Math.Max(0, saveData.TimePointsSpentToday);
            RestoreSet(progress._dailyTimePolicyIds, saveData.DailyTimePolicyIds);
            progress.LastSettledDay = Math.Max(0, saveData.LastSettledDay);
            progress.NextDayEntryPointId = saveData.NextDayEntryPointId ?? string.Empty;
            progress.LastActivityId = saveData.LastActivityId ?? string.Empty;
            progress.TutorialStageId = saveData.TutorialStageId ?? string.Empty;
            progress.NextObjectiveId = saveData.NextObjectiveId ?? string.Empty;
            progress.NextGuideText = saveData.NextGuideText ?? string.Empty;
            RestoreEntries(progress._traitValues, saveData.Traits);
            RestoreEntries(progress._skillValues, saveData.Skills);
            RestoreEntries(progress._relationshipValues, saveData.Relationships);
            RestoreEntries(progress._statusValues, saveData.StudentConditionStatuses);
            RestoreSet(progress._careerHintIds, saveData.CareerHintIds);
            RestoreSet(progress._appliedRequestIds, saveData.AppliedRequestIds);
            RestoreList(progress._activityLogIds, saveData.ActivityLogIds);
            RestoreList(progress._todayActivityIds, saveData.TodayActivityIds);
            RestoreList(progress._todayResultLogIds, saveData.TodayResultLogIds);
            RestoreList(progress._previousDayActivityIds, saveData.PreviousDayActivityIds);
            RestoreList(progress._previousDayResultLogIds, saveData.PreviousDayResultLogIds);
            return progress;
        }

        private static StudentLifeProgressSaveData.StatEntry[] ToEntries(Dictionary<string, int> values)
        {
            var entries = new StudentLifeProgressSaveData.StatEntry[values.Count];
            int index = 0;
            foreach (var pair in values) entries[index++] = new StudentLifeProgressSaveData.StatEntry { Id = pair.Key, Value = pair.Value };
            return entries;
        }

        private static void RestoreEntries(Dictionary<string, int> target, StudentLifeProgressSaveData.StatEntry[] entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
                if (!string.IsNullOrEmpty(entries[i].Id)) target[entries[i].Id] = Math.Max(0, entries[i].Value);
        }

        private static void RestoreSet(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static void RestoreList(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }
    }
}
