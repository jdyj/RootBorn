using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    public static class ClueInterpretationProgressPersistence
    {
        private const string FilePrefix = "clue-interpretations-";
        private const string FileExtension = ".json";

        public static string FileNameFor(string playerId)
        {
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            return FilePrefix + player + FileExtension;
        }

        public static ClueInterpretationProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json)) return new ClueInterpretationProgress(slot, player);
            try
            {
                return ClueInterpretationProgress.FromSaveData(JsonUtility.FromJson<ClueInterpretationProgressSaveData>(json));
            }
            catch (ArgumentException)
            {
                return new ClueInterpretationProgress(slot, player);
            }
        }

        public static void Save(ClueInterpretationProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }
    }
}
