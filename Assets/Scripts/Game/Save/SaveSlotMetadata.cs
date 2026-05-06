using System;
using Rootborn.Game.Player;

namespace Rootborn.Game.Save
{
    [Serializable]
    public sealed class SaveSlotMetadata
    {
        public string SlotId;
        public string DisplayName;
        public long CreatedAtUtcTicks;
        public long UpdatedAtUtcTicks;
        public int WorldSeed;
        public int TileSeed;
        public CharacterCustomization Character = new CharacterCustomization();
    }

    public sealed class SaveSlotSummary
    {
        public SaveSlotSummary(string slotId, SaveSlotMetadata metadata)
        {
            SlotId = slotId;
            Metadata = metadata;
        }

        public string SlotId { get; }
        public bool Exists => Metadata != null;
        public SaveSlotMetadata Metadata { get; }
    }

    public static class ActiveSaveContext
    {
        public static SaveSlotMetadata Metadata { get; private set; }
        public static string SlotId => Metadata != null ? Metadata.SlotId : string.Empty;

        public static void Set(SaveSlotMetadata metadata)
        {
            Metadata = metadata;
        }

        public static void Clear()
        {
            Metadata = null;
        }
    }
}
