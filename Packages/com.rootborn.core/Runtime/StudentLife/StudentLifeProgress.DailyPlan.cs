namespace Rootborn.Core.StudentLife
{
    // 학생이 가진 일과. 읽을 때마다 학생의 날짜와 달력에 맞춘다.
    public sealed partial class StudentLifeProgress
    {
        private DailyPlanProgress _dailyPlan;
        private bool _hasSavedDailyPlan;

        public DailyPlanProgress DailyPlan
        {
            get
            {
                if (_dailyPlan == null) _dailyPlan = new DailyPlanProgress(SaveSlot, PlayerId, CurrentDay);
                _dailyPlan.SynchronizeDay(this);
                return _dailyPlan;
            }
        }

        /// <summary>
        /// 따로 저장했던 옛 일과 파일을 한 번만 가져온다. 학생 세이브에 이미 일과가 있었거나, 다른 학생 것이거나,
        /// 학생보다 앞선 날짜거나, 이미 계획을 세웠으면 가져오지 않는다.
        /// </summary>
        public bool TryImportLegacyDailyPlan(DailyPlanProgressSaveData data)
        {
            if (_hasSavedDailyPlan || data == null || data.SaveSlot != SaveSlot || data.PlayerId != PlayerId || data.CurrentDay > CurrentDay) return false;
            if (_dailyPlan != null && (_dailyPlan.Diary.Count > 0 || _dailyPlan.HasAssignedSlots)) return false;
            _dailyPlan = DailyPlanProgress.FromSaveData(data, SaveSlot, PlayerId, CurrentDay);
            _dailyPlan.SynchronizeDay(this);
            _hasSavedDailyPlan = true;
            return true;
        }

        private void RestoreDailyPlan(DailyPlanProgressSaveData data)
        {
            // 엔진 세이브 도구는 빠진 안쪽 객체를 빈 껍데기로 채운다. 그것은 옛 세이브로 보지만,
            // 다른 학생의 일과가 이 학생에게 옮겨 오면 안 된다.
            _hasSavedDailyPlan = data != null && (!string.IsNullOrEmpty(data.SaveSlot) || !string.IsNullOrEmpty(data.PlayerId));
            _dailyPlan = DailyPlanProgress.FromSaveData(data, SaveSlot, PlayerId, CurrentDay);
            _dailyPlan.SynchronizeDay(this);
        }
    }
}
