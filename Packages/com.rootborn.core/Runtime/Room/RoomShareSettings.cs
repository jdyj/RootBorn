using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 방 설정 중 "무엇을 같이 쓰는가"(멀티플레이 설계 2절, 4절). 같이 쓰는 공유 조각의 이름 목록이다.
    /// - 시간은 항상 같이 쓴다(방의 시계는 조각 목록과 따로 있다). 가방·개인 성장은 항상 각자다(조각이 아니다).
    /// - 돈·집과 가구·마을 상태는 고를 수 있는 조각이다. 조각 이름은 <see cref="RoomPieceCatalog"/>에 등록된 것만 쓴다.
    /// - 방을 처음 만들 때 정하고 방의 세이브(<see cref="SaveFileNames.RoomSettings"/>)에 쓴다. 만든 뒤에는 바꾸지 않는다(설계 10절 3번).
    /// 혼자 하는 판에는 방 설정이 없다(모든 조각이 각자인 것과 같다).
    /// </summary>
    public sealed class RoomShareSettings
    {
        private readonly string[] _shared;

        /// <param name="sharedPieces">같이 쓰는 조각 이름. 빈 이름과 겹친 이름은 하나로 친다. 순서는 이름 순으로 맞춘다(저장·비교가 같게).</param>
        public RoomShareSettings(IEnumerable<string> sharedPieces)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (sharedPieces != null)
                foreach (var name in sharedPieces)
                    if (!string.IsNullOrWhiteSpace(name)) set.Add(name.Trim());
            _shared = new string[set.Count];
            set.CopyTo(_shared);
        }

        /// <summary>아무것도 같이 쓰지 않는 방(시간만 같이 쓴다).</summary>
        public static RoomShareSettings Nothing { get; } = new RoomShareSettings(null);

        public IReadOnlyList<string> SharedPieces => _shared;

        public bool Shares(string pieceName)
        {
            if (string.IsNullOrEmpty(pieceName)) return false;
            return Array.BinarySearch(_shared, pieceName, StringComparer.Ordinal) >= 0;
        }

        /// <summary>모르는 조각 이름(등록되지 않음)을 into에 채운다. 비어 있으면 모두 안다.</summary>
        public void CopyUnknown(RoomPieceCatalog catalog, List<string> into)
        {
            if (into == null) return;
            into.Clear();
            foreach (var name in _shared)
                if (catalog == null || !catalog.Knows(name)) into.Add(name);
        }

        public bool SameAs(RoomShareSettings other)
        {
            if (other == null || other._shared.Length != _shared.Length) return false;
            for (int i = 0; i < _shared.Length; i++)
                if (!string.Equals(_shared[i], other._shared[i], StringComparison.Ordinal)) return false;
            return true;
        }

        public RoomSettingsSaveData ToSaveData() => new RoomSettingsSaveData { Version = RoomSettingsSaveData.KnownVersion, SharedPieces = (string[])_shared.Clone() };

        public static RoomShareSettings FromSaveData(RoomSettingsSaveData data) => new RoomShareSettings(data?.SharedPieces);

        public override string ToString() => _shared.Length == 0 ? "(시간만)" : string.Join(", ", _shared);
    }

    /// <summary>방 설정 파일(&lt;슬롯&gt;/<see cref="SaveFileNames.RoomSettings"/>)의 모양. MonoGame 판이 처음 쓰는 파일이다.</summary>
    [Serializable]
    public sealed class RoomSettingsSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string[] SharedPieces = Array.Empty<string>();

        /// <summary>정원(설계 6절 "정원은 방 설정이다"). 0이면 적지 않은 옛 파일이다 — 그때는 방을 열 때 받은 정원을 쓴다.</summary>
        public int MaxPlayers;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>방 설정을 방의 세이브 슬롯에 읽고 쓴다. 방(서버) 쪽이 쓴다.</summary>
    public static class RoomSettingsSaveFiles
    {
        /// <summary>
        /// 이 슬롯의 방 설정. 파일이 있으면 그것(만들 때 정한 것)이고 requested는 버린다. 파일이 없고 방이 이미 있으면(설정 파일보다 먼저 만든 방)
        /// 아무것도 같이 쓰지 않는 방이다 — 그 방은 지금까지 각자 쓰고 있었다. 새 방이면 requested로 만들고 바로 쓴다.
        /// </summary>
        /// <param name="isNewRoom">이 슬롯에 방 상태가 하나도 없다(새 방).</param>
        public static RoomShareSettings LoadOrCreate(ISaveStore store, string saveSlot, bool isNewRoom, RoomShareSettings requested, out bool created)
            => LoadOrCreate(store, saveSlot, isNewRoom, requested, 0, out created, out _);

        /// <summary>
        /// 위와 같고 정원도 다룬다: 새 방이면 requestedMaxPlayers를 같이 쓰고, 파일에 정원이 있으면 그것(만들 때의 값), 없으면(옛 파일, 설정 파일보다 먼저 만든 방)
        /// requestedMaxPlayers를 maxPlayers로 준다. 옛 파일에 정원을 덧쓰지 않는다(파일은 만들 때 한 번만 쓴다).
        /// </summary>
        public static RoomShareSettings LoadOrCreate(ISaveStore store, string saveSlot, bool isNewRoom, RoomShareSettings requested, int requestedMaxPlayers,
            out bool created, out int maxPlayers)
        {
            created = false;
            maxPlayers = requestedMaxPlayers;
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (store.TryRead(saveSlot, SaveFileNames.RoomSettings, out RoomSettingsSaveData data))
            {
                if (data.MaxPlayers > 0) maxPlayers = data.MaxPlayers;
                return RoomShareSettings.FromSaveData(data);
            }

            if (!isNewRoom) return RoomShareSettings.Nothing;
            var settings = requested ?? RoomShareSettings.Nothing;
            var save = settings.ToSaveData();
            save.MaxPlayers = Math.Max(0, requestedMaxPlayers);
            store.Write(saveSlot, SaveFileNames.RoomSettings, save);
            created = true;
            return settings;
        }

        /// <summary>그 슬롯에 방 설정 파일이 있으면 true와 그 설정, 정원(적지 않았으면 0). 방을 다시 열기 전에 화면이 보이려고 읽는다.</summary>
        public static bool TryReadSaved(ISaveStore store, string saveSlot, out RoomShareSettings settings, out int maxPlayers)
        {
            settings = null;
            maxPlayers = 0;
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.RoomSettings, out RoomSettingsSaveData data)) return false;
            settings = RoomShareSettings.FromSaveData(data);
            maxPlayers = Math.Max(0, data.MaxPlayers);
            return true;
        }
    }
}
