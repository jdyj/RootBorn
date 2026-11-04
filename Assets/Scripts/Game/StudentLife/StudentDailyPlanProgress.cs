namespace Rootborn.Game.StudentLife
{
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

        internal bool TryImportLegacyDailyPlan(DailyPlanProgressSaveData data)
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
            // Unity materializes absent inline DTOs as empty shells. They are legacy,
            // but a populated foreign identity must never migrate into this student.
            _hasSavedDailyPlan = data != null && (!string.IsNullOrEmpty(data.SaveSlot) || !string.IsNullOrEmpty(data.PlayerId));
            _dailyPlan = DailyPlanProgress.FromSaveData(data, SaveSlot, PlayerId, CurrentDay);
            _dailyPlan.SynchronizeDay(this);
        }
    }
}
