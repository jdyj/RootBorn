using System;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class DailyPlanProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";

        public event Action Changed;

        public DailyPlanProgress Progress { get; private set; }

        private void Awake()
        {
            EnsureProgress(null);
        }

        public void Load(string saveSlot, string playerId, int currentDay)
        {
            string safeSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string safePlayer = string.IsNullOrEmpty(playerId) ? ResolvePlayerId() : playerId;
            Progress = DailyPlanProgressPersistence.LoadOrCreate(safeSlot, safePlayer, currentDay);
            Changed?.Invoke();
        }

        public DailyPlanProgress EnsureProgress(StudentLifeProgress studentProgress)
        {
            if (Progress != null) return Progress;

            string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId)
                ? ActiveSaveContext.Metadata.SlotId
                : studentProgress != null && !string.IsNullOrEmpty(studentProgress.SaveSlot)
                    ? studentProgress.SaveSlot
                    : "default";
            string playerId = studentProgress != null && !string.IsNullOrEmpty(studentProgress.PlayerId)
                ? studentProgress.PlayerId
                : ResolvePlayerId();
            int currentDay = studentProgress != null ? studentProgress.CurrentDay : 1;
            Progress = DailyPlanProgressPersistence.LoadOrCreate(saveSlot, playerId, currentDay);
            return Progress;
        }

        public bool TrySelect(DailyPlanRouteDefinition route, StudentLifeProgress studentProgress)
        {
            var progress = EnsureProgress(studentProgress);
            bool changed = new DailyPlanRouteRunner().TrySelectRoute(progress, route, studentProgress);
            if (!changed) return false;

            DailyPlanProgressPersistence.Save(progress);
            Changed?.Invoke();
            return true;
        }

        public bool TryRecordActivity(
            DailyPlanRouteSetDefinition[] routeSets,
            LocationActivityDefinition activity,
            StudentLifeProgress studentProgress,
            string requestKey,
            out DailyPlanCompletionResult result)
        {
            var progress = EnsureProgress(studentProgress);
            bool changed = new DailyPlanRouteRunner().TryRecordActivity(progress, routeSets, activity, studentProgress, requestKey, out result);
            if (!changed) return false;

            DailyPlanProgressPersistence.Save(progress);
            Changed?.Invoke();
            return true;
        }

        public void RestoreFromSaveData(DailyPlanProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId, int fallbackDay)
        {
            Progress = DailyPlanProgress.FromSaveData(saveData, fallbackSaveSlot, fallbackPlayerId, fallbackDay);
            Changed?.Invoke();
        }

        private string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(_fallbackPlayerId) ? PlayerIdentity.DefaultPlayerId : _fallbackPlayerId;
        }
    }
}
