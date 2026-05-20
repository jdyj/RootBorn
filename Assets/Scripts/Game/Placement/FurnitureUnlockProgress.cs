using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Placement
{
    [Serializable]
    public sealed class FurnitureUnlockProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public string[] UnlockTokens = Array.Empty<string>();
    }

    public sealed class FurnitureUnlockProgress
    {
        private readonly HashSet<string> _unlockTokens = new HashSet<string>(StringComparer.Ordinal);

        public FurnitureUnlockProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int UnlockedCount => _unlockTokens.Count;
        public IReadOnlyCollection<string> UnlockTokens => _unlockTokens;

        public bool IsUnlocked(string unlockToken)
        {
            return !string.IsNullOrEmpty(unlockToken) && _unlockTokens.Contains(unlockToken);
        }

        public bool TryUnlock(string unlockToken)
        {
            return !string.IsNullOrEmpty(unlockToken) && _unlockTokens.Add(unlockToken);
        }

        public FurnitureUnlockProgressSaveData ToSaveData()
        {
            return new FurnitureUnlockProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                UnlockTokens = _unlockTokens.OrderBy(token => token, StringComparer.Ordinal).ToArray(),
            };
        }

        public static FurnitureUnlockProgress FromSaveData(FurnitureUnlockProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId)
        {
            var progress = new FurnitureUnlockProgress(
                saveData != null && !string.IsNullOrEmpty(saveData.SaveSlot) ? saveData.SaveSlot : fallbackSaveSlot,
                saveData != null && !string.IsNullOrEmpty(saveData.PlayerId) ? saveData.PlayerId : fallbackPlayerId);
            if (saveData == null || saveData.UnlockTokens == null) return progress;
            for (int i = 0; i < saveData.UnlockTokens.Length; i++) progress.TryUnlock(saveData.UnlockTokens[i]);
            return progress;
        }
    }

    public static class FurnitureUnlockProgressPersistence
    {
        private const string FileName = "furniture-unlocks.json";

        public static FurnitureUnlockProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json)) return new FurnitureUnlockProgress(slot, player);
            return FurnitureUnlockProgress.FromSaveData(JsonUtility.FromJson<FurnitureUnlockProgressSaveData>(json), slot, player);
        }

        public static void Save(FurnitureUnlockProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player") return FileName;
            return "furniture-unlocks-" + Sanitize(playerId) + ".json";
        }

        private static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
