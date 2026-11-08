using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rootborn.Core.Characters;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Characters.Wearables
{
    /// <summary>
    /// Saves the player's wearable choices (Rootborn.Core WearableSaveData) per player in the save slot. The first load
    /// moves the old per-body wardrobe and the creator's starting outfit in once (WearableSaveRules), then records that.
    /// </summary>
    public static class WearableWardrobeStore
    {
        public static string FileNameFor(string playerId)
        {
            var safe = new StringBuilder("wearables-");
            foreach (char c in playerId ?? string.Empty) safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return safe.Append(".json").ToString();
        }

        /// <summary>Loads choices; when the legacy import ran just now, writes the result so it does not run again.</summary>
        public static WearableState Load(SaveService storage, string playerId, WearableCatalog catalog, IEnumerable<string> legacyLookIds,
            IReadOnlyDictionary<string, string> wearableIdByLegacyLook)
        {
            var result = WearableSaveRules.Load(catalog, Read(storage, playerId), legacyLookIds, wearableIdByLegacyLook);
            if (result.NeedsSave) Write(storage, playerId, result.ToSaveData());
            return result.State;
        }

        public static bool Save(SaveService storage, string playerId, WearableState state)
            => Write(storage, playerId, WearableSaveData.From(state ?? new WearableState(), legacyImported: true));

        private static WearableSaveData Read(SaveService storage, string playerId)
        {
            if (storage == null) return null;
            try
            {
                string json = storage.ReadJson(FileNameFor(playerId));
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<WearableSaveData>(json);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            {
                // A lost cosmetic choice falls back to slot defaults and nothing else is affected.
                Debug.LogWarning("[ROOTBORN] Wearable choices could not be read: " + exception.Message);
                return null;
            }
        }

        private static bool Write(SaveService storage, string playerId, WearableSaveData data)
        {
            if (storage == null) return false;
            try
            {
                storage.WriteJson(FileNameFor(playerId), JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[ROOTBORN] Wearable choices could not be saved: " + exception.Message);
                return false;
            }
        }
    }
}
