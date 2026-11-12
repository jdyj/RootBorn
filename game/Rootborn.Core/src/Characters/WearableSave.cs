using System;
using System.Collections.Generic;

namespace Rootborn.Core.Characters
{
    /// <summary>One saved choice: a slot key ("top" or "top@school") and the wearable worn there.</summary>
    [Serializable]
    public sealed class WearableSaveEntry
    {
        public string key;
        public string wearableId;
    }

    /// <summary>
    /// Save file shape for wearable choices. Plain public fields so any serializer (Unity JsonUtility included) can write it.
    /// <see cref="legacyImported"/> records that the old wardrobe and creator choices were moved in, so that happens once.
    /// </summary>
    [Serializable]
    public sealed class WearableSaveData : Rootborn.Core.Save.IVersionedSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // 파일의 필드 이름은 Unity 판 그대로 소문자 version이다. 저장소의 버전 검사(더 새 파일 거절)는 이 인터페이스로 받는다.
        int Rootborn.Core.Save.IVersionedSave.Version { get => version; set => version = value; }
        int Rootborn.Core.Save.IVersionedSave.KnownVersion => CurrentVersion;
        public bool legacyImported;
        public WearableSaveEntry[] entries = Array.Empty<WearableSaveEntry>();

        public static WearableSaveData From(WearableState state, bool legacyImported)
        {
            var pairs = state != null ? state.ToEntries() : new List<KeyValuePair<string, string>>();
            var data = new WearableSaveData { legacyImported = legacyImported, entries = new WearableSaveEntry[pairs.Count] };
            for (int i = 0; i < pairs.Count; i++)
                data.entries[i] = new WearableSaveEntry { key = pairs[i].Key, wearableId = pairs[i].Value };
            return data;
        }

        /// <summary>Rebuilds the state. Blank or broken entries are skipped; unknown items fall back to defaults when drawn.</summary>
        public WearableState ToState()
        {
            var pairs = new List<KeyValuePair<string, string>>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.key) || string.IsNullOrEmpty(entry.wearableId)) continue;
                    pairs.Add(new KeyValuePair<string, string>(entry.key, entry.wearableId));
                }
            }

            return WearableState.FromEntries(pairs);
        }
    }

    /// <summary>Reads the old per-body outfit choices into look ids that <see cref="WearableRules.MigrateLegacyChoices"/> understands.</summary>
    public static class LegacyWardrobe
    {
        /// <summary>
        /// Old look ids in priority order: wardrobe choices (made later, while playing) before the creator's starting outfit.
        /// Creator parts count only when their category starts with <paramref name="outfitCategoryPrefix"/>.
        /// </summary>
        public static List<string> LookIds(IEnumerable<KeyValuePair<string, string>> wardrobeChoices,
            IEnumerable<KeyValuePair<string, string>> creatorParts, string outfitCategoryPrefix)
        {
            var ids = new List<string>();
            if (wardrobeChoices != null)
            {
                foreach (var pair in wardrobeChoices)
                    if (!string.IsNullOrEmpty(pair.Value)) ids.Add(pair.Value);
            }

            if (creatorParts != null && !string.IsNullOrEmpty(outfitCategoryPrefix))
            {
                foreach (var pair in creatorParts)
                {
                    if (pair.Key == null || !pair.Key.StartsWith(outfitCategoryPrefix, StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrEmpty(pair.Value)) ids.Add(pair.Value);
                }
            }

            return ids;
        }
    }

    public readonly struct WearableLoadResult
    {
        public WearableLoadResult(WearableState state, bool needsSave)
        {
            State = state;
            NeedsSave = needsSave;
        }

        public WearableState State { get; }
        /// <summary>True when the legacy import just ran; write <see cref="ToSaveData"/> so it does not run again.</summary>
        public bool NeedsSave { get; }

        public WearableSaveData ToSaveData() => WearableSaveData.From(State, legacyImported: true);
    }

    public static class WearableSaveRules
    {
        /// <summary>
        /// Loads saved choices. Until the legacy import is recorded, old look ids are moved in without overriding saved choices.
        /// </summary>
        public static WearableLoadResult Load(WearableCatalog catalog, WearableSaveData saved, IEnumerable<string> legacyLookIds,
            IReadOnlyDictionary<string, string> wearableIdByLegacyLook)
        {
            var state = saved != null ? saved.ToState() : new WearableState();
            if (saved != null && saved.legacyImported) return new WearableLoadResult(state, needsSave: false);
            var migrated = WearableRules.MigrateLegacyChoices(catalog, state, legacyLookIds, wearableIdByLegacyLook);
            return new WearableLoadResult(migrated, needsSave: true);
        }
    }
}
