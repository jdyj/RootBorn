using System;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    /// <summary>Publishes committed player actions. Restore/replay operations do not publish here.</summary>
    public static class QuestGameplayEventBridge
    {
        public static event Action<QuestEvent, StudentLifeProgress> OnRecorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => OnRecorded = null;

        public static string CreateRequestKey(string action)
        {
            return action + ":" + Guid.NewGuid().ToString("N");
        }

        public static void Record(QuestEvent questEvent, StudentLifeProgress progress)
        {
            if (progress == null || string.IsNullOrEmpty(questEvent.EventKey) || questEvent.Count <= 0) return;
            OnRecorded?.Invoke(questEvent, progress);
        }
    }
}
