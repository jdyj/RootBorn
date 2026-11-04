using System;
using System.IO;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    public sealed class WorldEntryPoint
    {
        public WorldDestinationDefinition Destination { get; }
        public string SceneName => Destination.SceneName;
        public string SpawnId { get; }
        internal WorldEntryPoint(WorldDestinationDefinition destination, string spawnId) { Destination = destination; SpawnId = spawnId; }
    }

    public static class WorldEntryService
    {
        public const string FileName = "world-progress.json";
        private static Func<GameObject, GameObject, bool> _legacyPlaceholderPolicy;
        public static void ConfigureLegacyPlaceholderPolicy(Func<GameObject, GameObject, bool> policy) => _legacyPlaceholderPolicy = policy;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLegacyPlaceholderPolicy() => _legacyPlaceholderPolicy = null;


        public static WorldEntryPoint ResolveNewGame(WorldTravelCatalogDefinition catalog)
        {
            return catalog != null && catalog.IsValid ? new WorldEntryPoint(catalog.NewGameDestination, catalog.NewGameSpawnId) : null;
        }
        public static WorldEntryPoint ResolvePair(WorldTravelCatalogDefinition catalog, string sceneName, string spawnId)
        {
            var destination = catalog != null ? catalog.FindDestination(sceneName) : null;
            return destination != null && destination.ContainsSpawn(spawnId) ? new WorldEntryPoint(destination, spawnId) : null;
        }
        public static WorldEntryPoint ResolveContinue(WorldTravelCatalogDefinition catalog, SaveService storage)
        {
            if (storage == null || catalog == null || !catalog.IsValid) return null;
            string path = Path.Combine(storage.DirectoryPath, FileName);
            if (!File.Exists(path)) return ResolveNewGame(catalog);
            try
            {
                var data = JsonUtility.FromJson<WorldProgressSaveData>(File.ReadAllText(path));
                if (data == null || data.Version < 0 || data.Version > 1) return null;
                if (data.Version == 1 && !string.Equals(data.SaveSlot, storage.Slot, StringComparison.Ordinal)) return null;
                if (data.Version == 0 && !string.IsNullOrEmpty(data.SaveSlot) && data.SaveSlot != storage.Slot) return null;
                return ResolvePair(catalog, data.CurrentScene, data.CurrentEntryPointId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { return null; }
        }
        public static bool SaveTravel(WorldTravelCatalogDefinition catalog, SaveService storage, string sceneName, string spawnId, string displayName)
        {
            if (storage == null || ResolvePair(catalog, sceneName, spawnId) == null) return false;
            string path = Path.Combine(storage.DirectoryPath, FileName);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var data = new WorldProgressSaveData { Version = 1, SaveSlot = storage.Slot, CurrentScene = sceneName, CurrentEntryPointId = spawnId, LastPortalDisplayName = displayName ?? string.Empty };
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
        }
        public static WorldSpawnPoint FindUniqueSpawn(Scene scene, string spawnId)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(spawnId)) return null;
            WorldSpawnPoint found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<WorldSpawnPoint>(true))
                {
                    if (!string.Equals(candidate.SpawnId, spawnId, StringComparison.Ordinal)) continue;
                    if (found != null) return null;
                    found = candidate;
                }
            return found;
        }
        public static bool TryPlaceActor(Scene scene, GameObject actor, string spawnId, Camera camera)
        {
            var spawn = FindUniqueSpawn(scene, spawnId);
            if (actor == null || actor.transform.parent != null || spawn == null) return false;
            return TryRestoreActor(scene, actor, spawn.transform.position, camera);
        }
        public static bool TryRestoreActor(Scene scene, GameObject actor, Vector3 position, Camera camera)
        {
            if (!scene.IsValid() || !scene.isLoaded || actor == null || actor.transform.parent != null) return false;
            if (actor.scene != scene) SceneManager.MoveGameObjectToScene(actor, scene);
            var identity = actor.GetComponent<PlayerIdentity>();
            if (identity != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var candidate in root.GetComponentsInChildren<PlayerIdentity>(true))
                    {
                        if (candidate == null || candidate == identity || candidate.PlayerId != identity.PlayerId) continue;
                        candidate.gameObject.SetActive(false);
                        if (Application.isPlaying) UnityEngine.Object.Destroy(candidate.gameObject);
                        else UnityEngine.Object.DestroyImmediate(candidate.gameObject);
                    }
            }
            // The UI/network boundary authorizes only the known offline legacy placeholder.
            // Game itself never infers network ownership from an absent identity.
            if (_legacyPlaceholderPolicy != null)
                foreach (var candidate in scene.GetRootGameObjects())
                {
                    if (candidate == null || candidate == actor || !_legacyPlaceholderPolicy(actor, candidate)) continue;
                    candidate.SetActive(false);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(candidate);
                    else UnityEngine.Object.DestroyImmediate(candidate);
                }
            actor.transform.position = position;
            if (camera == null) return true;
            if (ScenePlayerRenderPolicy.TryGetForScene(scene, out var policy) && policy.Apply(actor, camera)) return true;
            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null) follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(actor.transform);
            follow.SetBounds(FindUniqueCameraBounds(scene));
            follow.SnapToTarget();
            return true;
        }
        private static BoxCollider2D FindUniqueCameraBounds(Scene scene)
        {
            BoxCollider2D found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<BoxCollider2D>(true))
                {
                    if (candidate.name != "CameraBounds" || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
                    if (found != null) return null;
                    found = candidate;
                }
            return found;
        }
    }
}
