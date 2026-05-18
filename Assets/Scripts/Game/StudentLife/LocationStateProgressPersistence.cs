using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public static class LocationStateProgressPersistence
    {
        private const string FileName = "location-state-progress.json";

        public static LocationStateProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json)) return new LocationStateProgress(slot, player);
            return LocationStateProgress.FromSaveData(JsonUtility.FromJson<LocationStateProgressSaveData>(json), slot, player);
        }

        public static void Save(LocationStateProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player") return FileName;
            return "location-state-progress-" + Sanitize(playerId) + ".json";
        }

        private static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
