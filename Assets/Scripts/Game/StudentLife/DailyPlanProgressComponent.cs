using System;
using Rootborn.Game.Careers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class DailyPlanProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";
        private Func<StudentLifeProgress> _resolveStudent;
        private Func<Func<StudentLifeProgress, bool>, bool> _execute;
        public event Action Changed;
        public DailyPlanProgress Progress { get; private set; }
        private void Awake() => EnsureProgress(null);

        public void BindSession(Func<StudentLifeProgress> resolver, Func<Func<StudentLifeProgress, bool>, bool> execute)
        {
            _resolveStudent = resolver; _execute = execute;
            var student = resolver?.Invoke();
            if (student != null) Progress = student.DailyPlan;
        }
        public void RefreshBoundProgress()
        {
            var student = _resolveStudent?.Invoke();
            if (student != null) Progress = student.DailyPlan;
        }
        public void Load(string saveSlot, string playerId, int currentDay)
        {
            if (_resolveStudent != null) { RefreshBoundProgress(); return; }
            Progress = DailyPlanProgressPersistence.LoadOrCreate(string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot,
                string.IsNullOrEmpty(playerId) ? ResolvePlayerId() : playerId, currentDay);
            Changed?.Invoke();
        }
        public DailyPlanProgress EnsureProgress(StudentLifeProgress studentProgress)
        {
            var student = _resolveStudent != null ? _resolveStudent() : studentProgress;
            if (student != null)
            {
                if (_resolveStudent == null && !HasPrimary(student))
                {
                    if (Progress != null && Progress.SaveSlot == student.SaveSlot && Progress.PlayerId == student.PlayerId)
                        student.TryImportLegacyDailyPlan(Progress.ToSaveData());
                    DailyPlanProgressPersistence.MigrateInto(student);
                }
                Progress = student.DailyPlan;
                return Progress;
            }
            if (Progress == null) Progress = DailyPlanProgressPersistence.LoadOrCreate(
                string.IsNullOrEmpty(ActiveSaveContext.SlotId) ? "default" : ActiveSaveContext.SlotId, ResolvePlayerId(), 1);
            return Progress;
        }
        public bool TrySelect(DailyPlanRouteDefinition route, StudentLifeProgress studentProgress)
        {
            bool changed = Execute(studentProgress, student =>
            {
                var context = DailyPlanRouteContextBuilder.Build(gameObject, student);
                return new DailyPlanRouteRunner().TrySelectRoute(student.DailyPlan, route, in context);
            });
            if (changed) PublishSelection(route, studentProgress);
            return changed;
        }
        public bool TryAssignScheduleSlot(int slotIndex, DailyPlanRouteDefinition route, StudentLifeProgress studentProgress)
        {
            bool changed = Execute(studentProgress, student =>
            {
                var context = DailyPlanRouteContextBuilder.Build(gameObject, student);
                return route != null && route.IsAvailable(in context) && student.DailyPlan.AssignSlotValidated(slotIndex, route, student);
            });
            if (changed) PublishSelection(route, studentProgress);
            return changed;
        }
        public bool TryRecordActivity(DailyPlanRouteSetDefinition[] routeSets, LocationActivityDefinition activity,
            StudentLifeProgress studentProgress, string requestKey, out DailyPlanCompletionResult result)
        {
            var observed = _resolveStudent != null ? _resolveStudent() : studentProgress;
            return TryRecordScheduledActivity(routeSets, activity, studentProgress, observed?.CurrentDay ?? 0, requestKey, out result);
        }
        public bool TryRecordScheduledActivity(DailyPlanRouteSetDefinition[] routeSets, LocationActivityDefinition activity,
            StudentLifeProgress studentProgress, int activityDay, string requestKey, out DailyPlanCompletionResult result)
        {
            DailyPlanCompletionResult pending = DailyPlanCompletionResult.Empty;
            bool changed = Execute(studentProgress, student => new DailyPlanRouteRunner().TryRecordScheduledActivity(
                student.DailyPlan, routeSets, activity, student, activityDay, requestKey, out pending));
            result = changed ? pending : DailyPlanCompletionResult.Empty;
            return changed;
        }
        public void RestoreFromSaveData(DailyPlanProgressSaveData data, string fallbackSaveSlot, string fallbackPlayerId, int fallbackDay)
        {
            if (_resolveStudent != null) { RefreshBoundProgress(); return; }
            Progress = DailyPlanProgress.FromSaveData(data, fallbackSaveSlot, fallbackPlayerId, fallbackDay);
            Changed?.Invoke();
        }
        private bool Execute(StudentLifeProgress supplied, Func<StudentLifeProgress, bool> command)
        {
            var student = _resolveStudent != null ? _resolveStudent() : supplied;
            if (student == null || command == null || (supplied != null && (supplied.SaveSlot != student.SaveSlot || supplied.PlayerId != student.PlayerId))) return false;
            EnsureProgress(student);
            if (_execute != null)
            {
                if (!_execute(command)) return false;
                RefreshBoundProgress();
            }
            else
            {
                // A primary owner must provide the atomic command path. Legacy/offline tests
                // remain supported before any shared snapshot has been committed.
                if (_resolveStudent != null || HasPrimary(student) || !command(student)) return false;
                Progress = student.DailyPlan; DailyPlanProgressPersistence.Save(Progress);
            }
            Changed?.Invoke();
            return true;
        }
        private void PublishSelection(DailyPlanRouteDefinition route, StudentLifeProgress fallback)
        {
            var student = _resolveStudent != null ? _resolveStudent() : fallback;
            QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.DailyPlanSelected,
                QuestGameplayEventBridge.CreateRequestKey("daily-plan-selected"), dailyPlanRoute: route), student);
        }
        private static bool HasPrimary(StudentLifeProgress student) => AdultCareerSnapshotPersistence.HasCommittedFile(new SaveService(student.SaveSlot), student.PlayerId);
        private string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId
                : string.IsNullOrEmpty(_fallbackPlayerId) ? PlayerIdentity.DefaultPlayerId : _fallbackPlayerId;
        }
    }
}
