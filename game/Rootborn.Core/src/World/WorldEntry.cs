using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.World
{
    /// <summary>
    /// 지금 있는 장면과 입구 파일(world-progress.json)의 모양. 필드 이름은 Unity 판 WorldPortal.cs의 WorldProgressSaveData와 같다.
    /// Version 0은 Version 필드가 없던 Unity 판 파일이다(빈 SaveSlot을 받아 준다).
    /// </summary>
    [Serializable]
    public sealed class WorldProgressSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string CurrentScene;
        public string CurrentEntryPointId;
        public string LastPortalDisplayName;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>들어갈 수 있는 장면 하나와 그 장면의 도착 지점들(Unity WorldDestinationDefinition).</summary>
    public sealed class WorldDestination
    {
        public readonly string SceneName;
        public readonly IReadOnlyList<string> SpawnIds;

        public WorldDestination(string sceneName, IReadOnlyList<string> spawnIds)
        {
            SceneName = sceneName ?? string.Empty;
            SpawnIds = spawnIds ?? Array.Empty<string>();
        }

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SceneName) || SpawnIds.Count == 0) return false;
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (string spawn in SpawnIds)
                    if (string.IsNullOrWhiteSpace(spawn) || !unique.Add(spawn)) return false;
                return true;
            }
        }

        public bool ContainsSpawn(string spawnId)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(spawnId)) return false;
            foreach (string spawn in SpawnIds) if (string.Equals(spawn, spawnId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>장면 목록과 새 게임의 시작 자리(Unity WorldTravelCatalogDefinition). 값은 데이터에서 온다.</summary>
    public sealed class WorldTravelCatalog
    {
        private readonly List<WorldDestination> _destinations = new List<WorldDestination>();

        public readonly string NewGameScene;
        public readonly string NewGameSpawnId;

        public WorldTravelCatalog(IEnumerable<WorldDestination> destinations, string newGameScene, string newGameSpawnId)
        {
            if (destinations != null) _destinations.AddRange(destinations);
            NewGameScene = newGameScene ?? string.Empty;
            NewGameSpawnId = newGameSpawnId ?? string.Empty;
        }

        public IReadOnlyList<WorldDestination> Destinations => _destinations;

        public bool IsValid
        {
            get
            {
                if (_destinations.Count == 0) return false;
                var scenes = new HashSet<string>(StringComparer.Ordinal);
                WorldDestination opening = null;
                foreach (WorldDestination d in _destinations)
                {
                    if (d == null || !d.IsValid || !scenes.Add(d.SceneName)) return false;
                    if (d.SceneName == NewGameScene) opening = d;
                }
                return opening != null && opening.ContainsSpawn(NewGameSpawnId);
            }
        }

        public WorldDestination Find(string sceneName)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(sceneName)) return null;
            foreach (WorldDestination d in _destinations) if (string.Equals(d.SceneName, sceneName, StringComparison.Ordinal)) return d;
            return null;
        }
    }

    /// <summary>들어갈 장면과 도착 지점.</summary>
    public readonly struct WorldEntryPoint : IEquatable<WorldEntryPoint>
    {
        public readonly string SceneName, SpawnId;

        public WorldEntryPoint(string sceneName, string spawnId)
        {
            SceneName = sceneName ?? string.Empty;
            SpawnId = spawnId ?? string.Empty;
        }

        public bool Equals(WorldEntryPoint other) => SceneName == other.SceneName && SpawnId == other.SpawnId;
        public override bool Equals(object obj) => obj is WorldEntryPoint other && Equals(other);
        public override int GetHashCode() => SceneName.GetHashCode() ^ (SpawnId.GetHashCode() * 31);
        public override string ToString() => SceneName + "/" + SpawnId;
    }

    /// <summary>어디서 시작하는가(Unity WorldEntryService의 Resolve* 규칙). 파일 읽기는 하지 않는다.</summary>
    public static class WorldEntryRules
    {
        public static WorldEntryPoint? ResolveNewGame(WorldTravelCatalog catalog)
        {
            if (catalog == null || !catalog.IsValid) return null;
            return new WorldEntryPoint(catalog.NewGameScene, catalog.NewGameSpawnId);
        }

        /// <summary>장면과 도착 지점이 목록에 있으면 그 자리.</summary>
        public static WorldEntryPoint? ResolvePair(WorldTravelCatalog catalog, string sceneName, string spawnId)
        {
            WorldDestination d = catalog?.Find(sceneName);
            return d != null && d.ContainsSpawn(spawnId) ? new WorldEntryPoint(d.SceneName, spawnId) : (WorldEntryPoint?)null;
        }

        /// <summary>
        /// 이어 하기. 파일이 없으면(saved가 null) 새 게임 자리. 버전이 0~1 밖이거나, 버전 1인데 슬롯이 다르거나,
        /// 버전 0인데 슬롯이 비지 않았고 다르거나, 목록에 없는 자리면 null(들어가지 않는다).
        /// </summary>
        public static WorldEntryPoint? ResolveContinue(WorldTravelCatalog catalog, string saveSlot, WorldProgressSaveData saved)
        {
            if (catalog == null || !catalog.IsValid) return null;
            if (saved == null) return ResolveNewGame(catalog);
            if (!IsForSlot(saveSlot, saved)) return null;
            return ResolvePair(catalog, saved.CurrentScene, saved.CurrentEntryPointId);
        }

        /// <summary>이 기록이 이 슬롯의 것이고 아는 버전인가(자리가 목록에 있는지는 보지 않는다).</summary>
        public static bool IsForSlot(string saveSlot, WorldProgressSaveData saved)
        {
            if (saved == null) return false;
            if (saved.Version < 0 || saved.Version > WorldProgressSaveData.KnownVersion) return false;
            if (saved.Version == 1 && !string.Equals(saved.SaveSlot, saveSlot, StringComparison.Ordinal)) return false;
            if (saved.Version == 0 && !string.IsNullOrEmpty(saved.SaveSlot) && saved.SaveSlot != saveSlot) return false;
            return true;
        }

        /// <summary>도착 기록. 목록에 없는 자리면 null.</summary>
        public static WorldProgressSaveData Arrival(WorldTravelCatalog catalog, string saveSlot, string sceneName, string spawnId, string displayName)
        {
            if (ResolvePair(catalog, sceneName, spawnId) == null) return null;
            return new WorldProgressSaveData
            {
                Version = WorldProgressSaveData.KnownVersion,
                SaveSlot = saveSlot,
                CurrentScene = sceneName,
                CurrentEntryPointId = spawnId,
                LastPortalDisplayName = displayName ?? string.Empty,
            };
        }
    }

    /// <summary>도착할 때마다 불린다. false면 그 맵으로 옮기지 않는다.</summary>
    public interface IWorldArrivals
    {
        bool Record(string mapId, string spawnId, string displayName);
        string LastProblem { get; }
    }

    /// <summary>슬롯 시작 결과.</summary>
    public sealed class WorldSlotStart
    {
        public bool Ok => Problem == null;
        public bool NewGame;
        public WorldEntryPoint Entry;
        public SaveSlotMetadata Metadata;
        public WorldProgressSaveData Saved;
        public string Problem;
        /// <summary>
        /// 저장된 자리("장면/도착 지점")가 지금 목록에 없어 새 게임 자리에서 시작했으면 그 옛 자리. 아니면 null.
        /// 맵이 바뀌어 사라진 자리 때문에 슬롯이 열리지 않는 일을 막는다. 진행은 그대로다.
        /// </summary>
        public string MovedFrom;
    }

    /// <summary>
    /// 세이브 슬롯 하나에서 월드를 시작하고 도착을 기록한다. 슬롯 정보가 없으면 새 게임(슬롯 정보를 쓰고 새 게임 자리),
    /// 있으면 이어 하기(world-progress의 자리, 파일이 없으면 새 게임 자리). 읽을 수 없는 파일은 덮어쓰지 않고 시작을 거절한다.
    /// 슬롯 id가 빈 슬롯 정보, 슬롯 정보 없이 남은 진행 파일도 거절한다(Unity 판은 슬롯 정보를 새로 썼다).
    /// 저장된 자리가 목록에서 사라졌으면(맵 교체) 거절하지 않고 새 게임 자리에서 이어 간다(MovedFrom, Unity 판은 거절했다).
    /// </summary>
    public sealed class WorldSlot : IWorldArrivals
    {
        private readonly ISaveStore _store;
        private readonly WorldTravelCatalog _catalog;

        public readonly string SlotId;
        public string LastProblem { get; private set; }

        public WorldSlot(ISaveStore store, string slotId, WorldTravelCatalog catalog)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            SlotId = slotId ?? string.Empty;
        }

        public WorldSlotStart Begin(long nowUtcTicks)
        {
            var start = new WorldSlotStart();
            if (!SaveSlotMetadata.IsSafeSlotId(SlotId)) { start.Problem = "슬롯 id에 쓸 수 없는 글자가 있다: '" + SlotId + "'"; return start; }
            if (!_catalog.IsValid) { start.Problem = "장면 목록이 올바르지 않다(새 게임 자리가 목록에 없다)"; return start; }
            try
            {
                bool hasInfo = _store.TryRead(SlotId, SaveFileNames.Metadata, out SaveSlotMetadata metadata) && metadata != null;
                // 슬롯 정보를 새로 쓰는 것은 정말 새 슬롯일 때만이다. 파일은 있는데 슬롯 id가 비었거나, 슬롯 정보 없이 진행 파일만 있으면
                // 덮어쓰지 않고 거절한다(새로 쓰면 시드·겉모습·방 식별 값이 바뀐 채 옛 진행이 이어진다).
                if (hasInfo && string.IsNullOrEmpty(metadata.SlotId))
                {
                    start.Problem = "슬롯 정보(" + SaveFileNames.Metadata + ")에 슬롯 id가 없다. 덮어쓰지 않았다";
                    return start;
                }
                if (!hasInfo && _store.TryRead(SlotId, SaveFileNames.WorldProgress, out WorldProgressSaveData orphan) && orphan != null)
                {
                    start.Problem = "슬롯 정보(" + SaveFileNames.Metadata + ")가 없는데 진행 파일(" + SaveFileNames.WorldProgress + ")이 있다. 새로 쓰지 않았다";
                    return start;
                }
                if (!hasInfo)
                {
                    metadata = SaveSlotMetadata.CreateDeterministic(SlotId, nowUtcTicks);
                    metadata.FillMissing();
                    _store.Write(SlotId, SaveFileNames.Metadata, metadata);
                    start.NewGame = true;
                    start.Metadata = metadata;
                    start.Entry = WorldEntryRules.ResolveNewGame(_catalog).Value;
                    return start;
                }
                metadata.FillMissing();
                start.Metadata = metadata;
                _store.TryRead(SlotId, SaveFileNames.WorldProgress, out WorldProgressSaveData saved);
                start.Saved = saved;
                WorldEntryPoint? entry = WorldEntryRules.ResolveContinue(_catalog, SlotId, saved);
                if (entry == null && WorldEntryRules.IsForSlot(SlotId, saved))
                {
                    // 이 슬롯의 기록인데 자리만 목록에 없다(맵이 바뀌었다). 새 게임 자리에서 이어 간다. 도착하면 새 자리가 기록된다.
                    start.MovedFrom = saved.CurrentScene + "/" + saved.CurrentEntryPointId;
                    entry = WorldEntryRules.ResolveNewGame(_catalog);
                }
                if (entry == null)
                {
                    start.Problem = "이어 할 자리가 올바르지 않다: " + (saved != null ? saved.SaveSlot + " " + saved.CurrentScene + "/" + saved.CurrentEntryPointId + " (버전 " + saved.Version + ")" : "?");
                    return start;
                }
                start.Entry = entry.Value;
                return start;
            }
            catch (Exception e)
            {
                start.Problem = "슬롯 " + SlotId + "을 읽지 못했다: " + e.Message;
                return start;
            }
        }

        /// <summary>도착을 world-progress에 쓴다. 목록에 없는 자리이거나 쓰지 못하면 false(Unity: 도착 기록 실패면 옮기지 않는다).</summary>
        public bool Record(string mapId, string spawnId, string displayName)
        {
            WorldProgressSaveData data = WorldEntryRules.Arrival(_catalog, SlotId, mapId, spawnId, displayName);
            if (data == null) { LastProblem = "장면 목록에 없는 자리: " + mapId + "/" + spawnId; return false; }
            try
            {
                _store.Write(SlotId, SaveFileNames.WorldProgress, data);
            }
            catch (Exception e)
            {
                LastProblem = "도착을 기록하지 못했다: " + e.Message;
                return false;
            }
            LastProblem = null;
            return true;
        }
    }
}
