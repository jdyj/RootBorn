using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>The outfit a player chose for each body. Keyed by body sheet so every growth stage keeps its own choice.</summary>
    public sealed class GrowingWardrobeStore
    {
        [Serializable]
        private sealed class Entry
        {
            public string bodyId;
            public string appearanceId;
        }

        [Serializable]
        private sealed class Data
        {
            public Entry[] choices = Array.Empty<Entry>();
        }

        private readonly Dictionary<string, string> _choices = new Dictionary<string, string>();

        public static string FileNameFor(string playerId)
        {
            var safe = new StringBuilder("growing-wardrobe-");
            foreach (char c in playerId ?? string.Empty)
            {
                safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }

            return safe.Append(".json").ToString();
        }

        /// <summary>Every saved (body, appearance) choice, for the one-time move to wearable choices.</summary>
        public IEnumerable<KeyValuePair<string, string>> Choices => _choices;

        public string GetChoice(string bodyId)
        {
            return !string.IsNullOrEmpty(bodyId) && _choices.TryGetValue(bodyId, out var id) ? id : null;
        }

        public void SetChoice(string bodyId, string appearanceId)
        {
            if (!string.IsNullOrEmpty(bodyId)) _choices[bodyId] = appearanceId;
        }

        public bool Save(SaveService storage, string playerId)
        {
            if (storage == null) return false;
            var data = new Data { choices = new Entry[_choices.Count] };
            int index = 0;
            foreach (var pair in _choices)
            {
                data.choices[index++] = new Entry { bodyId = pair.Key, appearanceId = pair.Value };
            }

            try
            {
                storage.WriteJson(FileNameFor(playerId), JsonUtility.ToJson(data, true));
                return true;
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[ROOTBORN] Wardrobe could not be saved: " + exception.Message);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("[ROOTBORN] Wardrobe could not be saved: " + exception.Message);
                return false;
            }
        }

        public static GrowingWardrobeStore Load(SaveService storage, string playerId)
        {
            var store = new GrowingWardrobeStore();
            try
            {
                string json = storage != null ? storage.ReadJson(FileNameFor(playerId)) : null;
                if (string.IsNullOrEmpty(json)) return store;
                var data = JsonUtility.FromJson<Data>(json);
                if (data?.choices == null) return store;
                foreach (var entry in data.choices)
                {
                    if (entry != null) store.SetChoice(entry.bodyId, entry.appearanceId);
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
            {
                // A lost cosmetic choice falls back to the default outfit and nothing else is affected.
                Debug.LogWarning("[ROOTBORN] Wardrobe file could not be read: " + exception.Message);
            }

            return store;
        }
    }
}
