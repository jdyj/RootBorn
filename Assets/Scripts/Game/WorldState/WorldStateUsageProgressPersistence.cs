using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    public static class WorldStateUsageProgressPersistence
    {
        private const string FilePrefix = "world-state-usage-";
        private const string FileExtension = ".json";

        public static WorldStateUsageProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json)) return new WorldStateUsageProgress(slot, player);
            try
            {
                return WorldStateUsageProgress.FromSaveData(JsonUtility.FromJson<WorldStateUsageProgressSaveData>(json));
            }
            catch (ArgumentException)
            {
                return new WorldStateUsageProgress(slot, player);
            }
        }

        public static void Save(WorldStateUsageProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        private static string FileNameFor(string playerId)
        {
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            return FilePrefix + SanitizeFileName(player) + FileExtension;
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
