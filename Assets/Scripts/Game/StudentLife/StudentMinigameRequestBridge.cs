using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public static class StudentMinigameRequestBridge
    {
        public static event Func<GameObject, CareerPracticeDefinition, string, bool> PracticeRequested;
        public static event Func<GameObject, PartTimeWorkDefinition, string, bool> PartTimeWorkRequested;
        public static bool TryRequestPractice(GameObject player, CareerPracticeDefinition practice, string requestId)
        {
            if (player == null || practice == null || practice.Minigame == null || string.IsNullOrEmpty(requestId) || PracticeRequested == null) return false;
            foreach (Func<GameObject, CareerPracticeDefinition, string, bool> handler in PracticeRequested.GetInvocationList()) if (handler(player, practice, requestId)) return true;
            return false;
        }
        public static bool TryRequestPartTimeWork(GameObject player, PartTimeWorkDefinition work, string requestId)
        {
            if (player == null || work == null || work.Minigame == null || string.IsNullOrEmpty(requestId) || PartTimeWorkRequested == null) return false;
            foreach (Func<GameObject, PartTimeWorkDefinition, string, bool> handler in PartTimeWorkRequested.GetInvocationList()) if (handler(player, work, requestId)) return true;
            return false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { PracticeRequested = null; PartTimeWorkRequested = null; }
    }
}
