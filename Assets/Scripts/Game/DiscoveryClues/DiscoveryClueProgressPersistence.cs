using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    public static class DiscoveryClueProgressPersistence
    {
        private const string FilePrefix = "discovery-clues-";
        private const string FileExtension = ".json";

        public static string FileNameFor(string playerId)
        {
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            return FilePrefix + player + FileExtension;
        }

        public static DiscoveryClueProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json)) return new DiscoveryClueProgress(slot, player);
            try
            {
                return DiscoveryClueProgress.FromSaveData(JsonUtility.FromJson<DiscoveryClueProgressSaveData>(json));
            }
            catch (ArgumentException)
            {
                return new DiscoveryClueProgress(slot, player);
            }
        }

        public static void Save(DiscoveryClueProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }
    }
}
