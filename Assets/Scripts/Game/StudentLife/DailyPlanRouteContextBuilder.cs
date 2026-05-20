using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public static class DailyPlanRouteContextBuilder
    {
        public static DailyPlanRouteContext Build(GameObject player, StudentLifeProgress studentProgress)
        {
            if (studentProgress == null) return new DailyPlanRouteContext(null);
            string saveSlot = ResolveSaveSlot(studentProgress);
            string playerId = ResolvePlayerId(player, studentProgress);
            return new DailyPlanRouteContext(
                studentProgress,
                WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId),
                DiscoveryClueProgressPersistence.LoadOrCreate(saveSlot, playerId),
                ClueInterpretationProgressPersistence.LoadOrCreate(saveSlot, playerId));
        }

        private static string ResolveSaveSlot(StudentLifeProgress studentProgress)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata != null && !string.IsNullOrEmpty(metadata.SlotId)) return metadata.SlotId;
            return string.IsNullOrEmpty(studentProgress.SaveSlot) ? "default" : studentProgress.SaveSlot;
        }

        private static string ResolvePlayerId(GameObject player, StudentLifeProgress studentProgress)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(studentProgress.PlayerId) ? PlayerIdentity.DefaultPlayerId : studentProgress.PlayerId;
        }
    }
}