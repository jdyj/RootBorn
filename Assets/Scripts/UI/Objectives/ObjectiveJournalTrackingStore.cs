using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    public static class ObjectiveJournalTrackingStore
    {
        private const string FileName = "objective-journal-tracking.json";

        public static void Save(SaveSlotMetadata metadata, string categoryId, string stableKey)
        {
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || string.IsNullOrEmpty(stableKey))
            {
                return;
            }

            var data = new ObjectiveJournalTrackingSaveData
            {
                CategoryId = string.IsNullOrEmpty(categoryId) ? string.Empty : categoryId,
                StableKey = stableKey
            };
            new SaveService(metadata.SlotId).WriteJson(FileName, JsonUtility.ToJson(data, true));
        }

        public static bool TryLoad(SaveSlotMetadata metadata, out string categoryId, out string stableKey)
        {
            categoryId = string.Empty;
            stableKey = string.Empty;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return false;
            }

            string json = new SaveService(metadata.SlotId).ReadJson(FileName);
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var data = JsonUtility.FromJson<ObjectiveJournalTrackingSaveData>(json);
            if (data == null || string.IsNullOrEmpty(data.StableKey))
            {
                return false;
            }

            categoryId = string.IsNullOrEmpty(data.CategoryId) ? string.Empty : data.CategoryId;
            stableKey = data.StableKey;
            return true;
        }

        [Serializable]
        private sealed class ObjectiveJournalTrackingSaveData
        {
            public string CategoryId;
            public string StableKey;
        }
    }
}
