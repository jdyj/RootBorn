using System;

namespace Rootborn.Core.Save
{
    /// <summary>
    /// 슬롯 정보 파일(metadata.json)의 모양. 필드 이름은 Unity 판 Assets/Scripts/Game/Save/SaveSlotMetadata.cs와 같다
    /// (Unity JsonUtility가 쓴 비공개 필드 이름 "_bodyVariant" 등도 그대로). Version은 MonoGame 판이 더한 필드다.
    /// Unity 판은 모르는 필드를 버리므로 그 판에서도 계속 읽힌다.
    /// </summary>
    [Serializable]
    public sealed class SaveSlotMetadata : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string SlotId;
        public string DisplayName;
        public long CreatedAtUtcTicks;
        public long UpdatedAtUtcTicks;
        public int WorldSeed;
        public int TileSeed;
        public CharacterCustomizationSaveData Character = new CharacterCustomizationSaveData();
        public CharacterAppearanceSaveData Appearance = new CharacterAppearanceSaveData();
        public CharacterAppearanceSnapshotSaveData CharacterAppearanceSnapshot;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;

        /// <summary>Unity SaveService.SanitizeOrThrow: 비어 있지 않고 영문자·숫자·'_'·'-'만.</summary>
        public static bool IsSafeSlotId(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) return false;
            foreach (char c in slotId)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Unity SaveService.StableHash.</summary>
        public static int StableHash(string value, int salt)
        {
            unchecked
            {
                int hash = salt;
                for (int i = 0; i < value.Length; i++) hash = (hash * 16777619) ^ value[i];
                return hash;
            }
        }

        /// <summary>Unity SaveService.CreateDeterministicMetadata: 시드는 슬롯 id에서 만든다. 시각은 부르는 쪽이 준다.</summary>
        public static SaveSlotMetadata CreateDeterministic(string slotId, long nowUtcTicks)
        {
            if (!IsSafeSlotId(slotId)) throw new ArgumentException("슬롯 id에 쓸 수 없는 글자가 있다: " + slotId, nameof(slotId));
            return new SaveSlotMetadata
            {
                SlotId = slotId,
                DisplayName = slotId,
                CreatedAtUtcTicks = nowUtcTicks,
                UpdatedAtUtcTicks = nowUtcTicks,
                WorldSeed = StableHash(slotId, 0x13579BDF),
                TileSeed = StableHash(slotId, 0x2468ACE),
            };
        }

        /// <summary>Unity LoadMetadata·SaveMetadata처럼 빠진 하위 모양을 채운다.</summary>
        public void FillMissing()
        {
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = SlotId;
            if (Character == null) Character = new CharacterCustomizationSaveData();
            if (Appearance == null) Appearance = new CharacterAppearanceSaveData();
            if (Appearance._parts == null) Appearance._parts = Array.Empty<CharacterPartSelectionSaveData>();
        }
    }

    /// <summary>Unity Rootborn.Game.Player.CharacterCustomization. _defaultFacing은 Down=0, Left=1, Right=2, Up=3.</summary>
    [Serializable]
    public sealed class CharacterCustomizationSaveData
    {
        public int _bodyVariant;
        public int _hairVariant;
        public int _outfitVariant;
        public int _defaultFacing;
    }

    /// <summary>Unity Rootborn.Game.Family.CharacterAppearance.</summary>
    [Serializable]
    public sealed class CharacterAppearanceSaveData
    {
        public CharacterPartSelectionSaveData[] _parts = Array.Empty<CharacterPartSelectionSaveData>();
    }

    [Serializable]
    public sealed class CharacterPartSelectionSaveData
    {
        public string _categoryId;
        public string _partId;
    }

    /// <summary>Unity Rootborn.Game.Characters.CharacterAppearanceSnapshot(새 게임 만들기 화면이 고른 부위).</summary>
    [Serializable]
    public sealed class CharacterAppearanceSnapshotSaveData
    {
        public int schemaVersion = 1;
        public string visualKind;
        public string appearanceDefinitionId;
        public string catalogId;
        public CharacterAppearancePartSaveData[] selectedParts = Array.Empty<CharacterAppearancePartSaveData>();
    }

    [Serializable]
    public sealed class CharacterAppearancePartSaveData
    {
        public string categoryId;
        public string partId;
    }
}
