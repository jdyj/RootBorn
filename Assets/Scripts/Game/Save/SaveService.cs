using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Game.Family;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.Save
{
    public sealed class SaveService
    {
        public const int MaxUiSlots = 3;

        private const string MetadataFileName = "metadata.json";

        private static string s_rootDirectoryOverride;

        private readonly string _slot;
        private readonly string _rootDir;
        private readonly string _dir;

        public SaveService(string slot)
            : this(slot, ResolveDefaultRootDirectory())
        {
        }

        public SaveService(string slot, string rootDirectory)
        {
            _slot = SanitizeOrThrow(string.IsNullOrEmpty(slot) ? "default" : slot);
            _rootDir = string.IsNullOrEmpty(rootDirectory)
                ? ResolveDefaultRootDirectory()
                : rootDirectory;
            _dir = Path.Combine(_rootDir, _slot);
            Directory.CreateDirectory(_dir);
        }

        public string Slot => _slot;
        public string DirectoryPath => _dir;

        public static void SetRootDirectoryForTests(string rootDirectory)
        {
            s_rootDirectoryOverride = rootDirectory;
        }

        public IReadOnlyList<SaveSlotSummary> ListUiSlots()
        {
            var result = new List<SaveSlotSummary>(MaxUiSlots);
            for (int i = 0; i < MaxUiSlots; i++)
            {
                string slotId = $"slot-{i}";
                result.Add(new SaveSlotSummary(slotId, LoadMetadata(slotId)));
            }

            return result;
        }

        public SaveSlotMetadata CreateUiMetadata(string slotId, CharacterCustomization character, int worldSeed, int tileSeed)
        {
            string safeSlotId = SanitizeOrThrow(slotId);
            bool uiSlot = false;
            for (int i = 0; i < MaxUiSlots; i++)
            {
                if (safeSlotId == $"slot-{i}")
                {
                    uiSlot = true;
                    break;
                }
            }

            if (!uiSlot)
            {
                throw new ArgumentException($"UI save slot must be slot-0 through slot-{MaxUiSlots - 1}: {slotId}", nameof(slotId));
            }

            return CreateMetadata(safeSlotId, character, worldSeed, tileSeed);
        }

        public SaveSlotMetadata CreateDeterministicMetadata(string slotId, CharacterCustomization character)
        {
            string safeSlotId = SanitizeOrThrow(slotId);
            int worldSeed = StableHash(safeSlotId, 0x13579BDF);
            int tileSeed = StableHash(safeSlotId, 0x2468ACE);
            return CreateMetadata(safeSlotId, character, worldSeed, tileSeed);
        }

        public SaveSlotMetadata CreateMetadata(string slotId, CharacterCustomization character, int worldSeed, int tileSeed)
        {
            string safeSlotId = SanitizeOrThrow(slotId);
            long now = DateTime.UtcNow.Ticks;
            return new SaveSlotMetadata
            {
                SlotId = safeSlotId,
                DisplayName = safeSlotId,
                CreatedAtUtcTicks = now,
                UpdatedAtUtcTicks = now,
                WorldSeed = worldSeed,
                TileSeed = tileSeed,
                Character = character ?? new CharacterCustomization(),
                Appearance = new CharacterAppearance(),
            };
        }

        public void SaveMetadata(SaveSlotMetadata metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            metadata.SlotId = SanitizeOrThrow(metadata.SlotId);
            if (string.IsNullOrEmpty(metadata.DisplayName))
            {
                metadata.DisplayName = metadata.SlotId;
            }

            if (metadata.CreatedAtUtcTicks <= 0)
            {
                metadata.CreatedAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            if (metadata.Character == null)
            {
                metadata.Character = new CharacterCustomization();
            }
            if (metadata.Appearance == null)
            {
                metadata.Appearance = new CharacterAppearance();
            }

            metadata.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

            string slotDir = SlotDirectory(metadata.SlotId);
            Directory.CreateDirectory(slotDir);
            File.WriteAllText(Path.Combine(slotDir, MetadataFileName), JsonUtility.ToJson(metadata, true));
        }

        public SaveSlotMetadata LoadMetadata(string slotId)
        {
            string safeSlotId = SanitizeOrThrow(slotId);
            string path = Path.Combine(SlotDirectory(safeSlotId), MetadataFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var metadata = JsonUtility.FromJson<SaveSlotMetadata>(File.ReadAllText(path));
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return null;
                }

                metadata.SlotId = SanitizeOrThrow(metadata.SlotId);
                if (metadata.Character == null)
                {
                    metadata.Character = new CharacterCustomization();
                }
                if (metadata.Appearance == null)
                {
                    metadata.Appearance = new CharacterAppearance();
                }

                return metadata;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException)
            {
                return null;
            }
        }

        public bool DeleteSlot(string slotId)
        {
            string safeSlotId = SanitizeOrThrow(slotId);
            string slotDir = SlotDirectory(safeSlotId);
            if (!Directory.Exists(slotDir))
            {
                return false;
            }

            Directory.Delete(slotDir, true);
            return true;
        }

        public void WriteJson(string fileName, string json)
        {
            var path = Path.Combine(_dir, fileName);
            File.WriteAllText(path, json);
        }

        public string ReadJson(string fileName)
        {
            var path = Path.Combine(_dir, fileName);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private string SlotDirectory(string slotId)
        {
            return Path.Combine(_rootDir, SanitizeOrThrow(slotId));
        }

        private static string ResolveDefaultRootDirectory()
        {
            return string.IsNullOrEmpty(s_rootDirectoryOverride)
                ? Path.Combine(Application.persistentDataPath, "saves")
                : s_rootDirectoryOverride;
        }

        private static string SanitizeOrThrow(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                throw new ArgumentException("Slot id must not be empty.", nameof(slotId));
            }

            for (int i = 0; i < slotId.Length; i++)
            {
                char c = slotId[i];
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-';
                if (!valid)
                {
                    throw new ArgumentException($"Unsafe save slot id: {slotId}", nameof(slotId));
                }
            }

            return slotId;
        }

        private static int StableHash(string value, int salt)
        {
            unchecked
            {
                int hash = salt;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 16777619) ^ value[i];
                }

                return hash;
            }
        }
    }
}
