using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 한 플레이어의 학생 생활 진행 상태. 체력·집중·스트레스·시각, 특성·기술·관계·상태 수치, 진로 힌트, 하루 기록을 가진다.
    /// 엔티티는 id 문자열로만 다룬다. 수치는 0 아래로 내려가지 않는다.
    /// </summary>
    public sealed partial class StudentLifeProgress
    {
        private readonly Dictionary<string, int> _traitValues = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _skillValues = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _relationshipValues = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _statusValues = new Dictionary<string, int>();
        private readonly HashSet<string> _careerHintIds = new HashSet<string>();
        private readonly HashSet<string> _appliedRequestIds = new HashSet<string>();
        private readonly List<string> _activityLogIds = new List<string>();
        private readonly List<string> _todayActivityIds = new List<string>();
        private readonly List<string> _todayResultLogIds = new List<string>();
        private readonly List<string> _previousDayActivityIds = new List<string>();
        private readonly List<string> _previousDayResultLogIds = new List<string>();

        public StudentLifeProgress(string saveSlot, string playerId, int energy, int focus, int stress = 0, int timeMinutes = 0)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            Energy = Math.Max(0, energy);
            Focus = Math.Max(0, focus);
            Stress = Math.Max(0, stress);
            TimeMinutes = Math.Max(0, timeMinutes);
            CurrentDay = 1;
            DayState = StudentDayState.InProgress;
            LastSettledDay = 0;
            NextDayEntryPointId = string.Empty;
            LastActivityId = string.Empty;
            TutorialStageId = string.Empty;
            NextObjectiveId = string.Empty;
            NextGuideText = string.Empty;
        }

        public event Action TimeChanged;
        public event Action VitalsChanged;

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int Energy { get; private set; }
        public int Focus { get; private set; }
        public int Stress { get; private set; }
        public int TimeMinutes { get; private set; }
        public int CurrentDay { get; private set; }
        public StudentDayState DayState { get; private set; }
        public int LastSettledDay { get; private set; }
        public string NextDayEntryPointId { get; private set; }
        public string LastActivityId { get; private set; }
        public string TutorialStageId { get; private set; }
        public string NextObjectiveId { get; private set; }
        public string NextGuideText { get; private set; }

        public bool CanSpend(int energyCost, int focusCost) => Energy >= Math.Max(0, energyCost) && Focus >= Math.Max(0, focusCost);

        public void Spend(int timeCostMinutes, int energyCost, int focusCost, int stressDelta)
        {
            int previousEnergy = Energy, previousFocus = Focus, previousStress = Stress;
            Energy = Math.Max(0, Energy - Math.Max(0, energyCost));
            Focus = Math.Max(0, Focus - Math.Max(0, focusCost));
            Stress = Math.Max(0, Stress + stressDelta);
            CheckExhaustion();
            if (Energy != previousEnergy || Focus != previousFocus || Stress != previousStress) VitalsChanged?.Invoke();
            // 걸리는 시간: 혼자면 시계가 건너뛰고(밤이면 체력도 준다), 같이 하면 끝나는 시각까지 하는 중이다(StudentLifeProgress.Clock).
            SpendActivityTime(Math.Max(0, timeCostMinutes));
        }

        public void RestoreEnergyForDay(int value)
        {
            int next = Math.Max(0, value);
            if (Energy == next) return;
            Energy = next;
            VitalsChanged?.Invoke();
        }

        /// <summary>하루에 채워지는 집중. 0이면 정해지지 않은 것이다. 세이브에 넣지 않고, 진행 상태를 만든 쪽이 하루 끝 규칙에서 읽어 넣는다.</summary>
        public int DailyFocus { get; private set; }

        public void ConfigureDailyFocus(int dailyFocus) => DailyFocus = Math.Max(0, dailyFocus);

        /// <summary>집중을 늘리되 하루 집중을 넘기지 않는다. 이미 넘어 있으면 그대로 둔다. 하루 집중이 정해지지 않았으면 막지 않는다.</summary>
        public void AddFocusUpToDailyFocus(int delta)
        {
            int target = Focus + Math.Max(0, delta);
            if (DailyFocus > 0) target = Math.Max(Focus, Math.Min(target, DailyFocus));
            RestoreFocusForDay(target);
        }

        public void RestoreFocusForDay(int value)
        {
            int next = Math.Max(0, value);
            if (Focus == next) return;
            Focus = next;
            VitalsChanged?.Invoke();
        }

        /// <summary>안내 단계와 다음 목표를 바꾼다. 실제로 바뀐 것이 있으면 true.</summary>
        public bool SetTutorialStage(TutorialStage stage, string nextObjectiveId)
        {
            if (stage == null || string.IsNullOrEmpty(stage.Id)) return false;
            string objective = string.IsNullOrEmpty(nextObjectiveId) ? string.Empty : nextObjectiveId;
            string guideText = string.IsNullOrEmpty(stage.GuideText) ? string.Empty : stage.GuideText;
            bool changed = TutorialStageId != stage.Id || NextObjectiveId != objective || NextGuideText != guideText;
            TutorialStageId = stage.Id;
            NextObjectiveId = objective;
            NextGuideText = guideText;
            return changed;
        }

        public void SetTutorialStageForTests(string tutorialStageId) => TutorialStageId = string.IsNullOrEmpty(tutorialStageId) ? string.Empty : tutorialStageId;

        public void AddTrait(string traitId, int delta) => AddValue(_traitValues, traitId, delta);
        public int GetTraitValue(string traitId) => GetValue(_traitValues, traitId);
        public string[] GetTraitIds() => KeysToArray(_traitValues);

        public void AddSkill(string skillId, int delta) => AddValue(_skillValues, skillId, delta);
        public int GetSkillValue(string skillId) => GetValue(_skillValues, skillId);
        public string[] GetSkillIds() => KeysToArray(_skillValues);

        public void AddRelationship(string relationshipId, int delta) => AddValue(_relationshipValues, relationshipId, delta);
        public int GetRelationshipValue(string relationshipId) => GetValue(_relationshipValues, relationshipId);
        public string[] GetRelationshipIds() => KeysToArray(_relationshipValues);

        public void AddStatus(string statusId, int delta) => AddValue(_statusValues, statusId, delta);
        public int GetStatusValue(string statusId) => GetValue(_statusValues, statusId);
        public string[] GetStatusIds() => KeysToArray(_statusValues);

        public void UnlockCareerHint(string careerId)
        {
            if (!string.IsNullOrEmpty(careerId)) _careerHintIds.Add(careerId);
        }

        public bool IsCareerHintUnlocked(string careerId) => !string.IsNullOrEmpty(careerId) && _careerHintIds.Contains(careerId);
        public string[] GetCareerHintIds() => ToArray(_careerHintIds);

        /// <summary>같은 요청을 두 번 적용하지 않기 위한 표시. 처음 표시할 때만 true.</summary>
        public bool MarkRequestApplied(string requestId) => !string.IsNullOrEmpty(requestId) && _appliedRequestIds.Add(requestId);
        public bool HasAppliedRequest(string requestId) => !string.IsNullOrEmpty(requestId) && _appliedRequestIds.Contains(requestId);

        public void RecordActivityCompleted(string activityId) => RecordActivityCompleted(activityId, Array.Empty<string>(), false);
        public void RecordActivityCompleted(string activityId, string[] resultLogIds) => RecordActivityCompleted(activityId, resultLogIds, false);

        public void RecordActivityCompleted(string activityId, string[] resultLogIds, bool preserveRepeatedResults)
        {
            if (string.IsNullOrEmpty(activityId)) return;
            LastActivityId = activityId;
            _activityLogIds.Add(activityId);
            _todayActivityIds.Add(activityId);
            if (preserveRepeatedResults)
            {
                // 요청 중복은 실행기가 막는다. 같은 변화량이 서로 다른 행동에서 나올 수 있다.
                if (resultLogIds != null)
                    foreach (string result in resultLogIds)
                        if (!string.IsNullOrEmpty(result)) _todayResultLogIds.Add(result);
            }
            else AddOrderedUnique(_todayResultLogIds, resultLogIds);
        }

        public string[] GetActivityLogIds() => _activityLogIds.ToArray();
        public string[] GetTodayActivityIds() => _todayActivityIds.ToArray();
        public string[] GetTodayResultLogIds() => _todayResultLogIds.ToArray();
        public string[] GetPreviousDayActivityIds() => _previousDayActivityIds.ToArray();
        public string[] GetPreviousDayResultLogIds() => _previousDayResultLogIds.ToArray();
        public string[] GetPreviousDayRelationshipDeltas() => FilterLogs(_previousDayResultLogIds, "relationship.");
        public string[] GetPreviousDayStatusDeltas() => FilterLogs(_previousDayResultLogIds, "status.");

        /// <summary>하루를 정산한다(지금 시각에 잔 것으로 본다 — <see cref="TrySleep"/>와 같다). 같은 날을 두 번 정산하지 않는다. 요약은 실패해도 채워진다.</summary>
        public bool TryEndDay(DayEndRuleSet rules, out StudentDaySummary summary) => TrySleep(rules, out summary);

        private bool TryEndDay(DayEndRuleSet rules, NightRecord night, out StudentDaySummary summary)
        {
            summary = BuildCurrentDaySummary(rules);
            if (DayState != StudentDayState.InProgress || LastSettledDay == CurrentDay) return false;
            LastNight = night;
            rules?.Apply(this);
            AfterNight();
            _previousDayActivityIds.Clear();
            _previousDayActivityIds.AddRange(_todayActivityIds);
            _previousDayResultLogIds.Clear();
            _previousDayResultLogIds.AddRange(_todayResultLogIds);
            NextDayEntryPointId = rules != null ? rules.NextDayEntryPointId : string.Empty;
            LastSettledDay = CurrentDay;
            DayState = StudentDayState.ResultReady;
            summary = BuildCurrentDaySummary(rules);
            return true;
        }

        /// <summary>
        /// 정산이 끝난 뒤에만 다음 날로 넘어간다. 시각은 시간 규칙의 하루 시작(07:00)으로, 규칙이 없으면 Unity 판처럼 아침 8시로 돌아간다.
        /// 날이 넘어가는 시각을 넘긴 활동 시간은 다음 날 아침으로 잇는다.
        /// </summary>
        public bool TryStartNextDay()
        {
            if (DayState != StudentDayState.ResultReady) return false;
            CurrentDay = Math.Max(1, CurrentDay + 1);
            DayState = StudentDayState.InProgress;
            TimeMinutes = NextDayStartMinutes();
            ResetNight();
            ResetDailyTimeBudget();
            _todayActivityIds.Clear();
            _todayResultLogIds.Clear();
            OnNextDayStarted();
            TimeChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 방에 없던 날을 쉰 날로 넘겨 day에 닿는다(멀티플레이 설계 10절 7번). 진행 중인 날은 그 날을 정산하고, 사이의 날마다
        /// 하루 끝 규칙(체력·집중 회복)을 적용한다. 그날 못 한 활동에 대한 불이익은 없다. 닿은 날은 처음부터 한다(진행 중).
        /// 지금 날 이하면 아무것도 바꾸지 않는다.
        /// </summary>
        /// <returns>쉰 날로 넘긴 날 수.</returns>
        public int RestUntil(int day, DayEndRuleSet rules)
        {
            int rested = 0;
            while (CurrentDay < day)
            {
                if (DayState == StudentDayState.InProgress) TryEndDay(rules, out _);
                if (!TryStartNextDay()) break;
                rested++;
            }

            return rested;
        }

        // 일과·진로 후보처럼 뒤에 옮겨 오는 부분이 다음 날 시작에 할 일을 여기에 잇는다.
        partial void OnNextDayStarted();

        private StudentDaySummary BuildCurrentDaySummary(DayEndRuleSet rules)
        {
            string entry = rules != null ? rules.NextDayEntryPointId : NextDayEntryPointId;
            return new StudentDaySummary(CurrentDay, _todayActivityIds.ToArray(), _todayResultLogIds.ToArray(), entry, TutorialStageId, NextObjectiveId, NextGuideText);
        }

        private static void AddValue(Dictionary<string, int> values, string key, int delta)
        {
            if (string.IsNullOrEmpty(key) || delta == 0) return;
            values.TryGetValue(key, out int current);
            values[key] = Math.Max(0, current + delta);
        }

        private static int GetValue(Dictionary<string, int> values, string key) => !string.IsNullOrEmpty(key) && values.TryGetValue(key, out int value) ? value : 0;

        private static string[] KeysToArray(Dictionary<string, int> values)
        {
            var result = new string[values.Count];
            values.Keys.CopyTo(result, 0);
            return result;
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static void AddOrderedUnique(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrEmpty(values[i]) && !target.Contains(values[i])) target.Add(values[i]);
        }

        private static string[] FilterLogs(List<string> logs, string marker)
        {
            var result = new List<string>();
            for (int i = 0; i < logs.Count; i++)
                if (StudentResultLogDeltas.Has(logs[i], marker)) result.Add(logs[i]);
            return result.ToArray();
        }
    }
}
