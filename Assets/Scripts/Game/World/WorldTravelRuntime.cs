using System;
using System.IO;
using System.Threading.Tasks;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    // This object survives the source scene. The explicit baseline hook supplies
    // the reinforced actor and destination camera; no per-frame actor search is used.
    public sealed class WorldTravelRuntime : MonoBehaviour
    {
        private static WorldTravelRuntime s_instance;
        private static Func<GameObject, bool> s_prepare;
        private static Func<Scene, GameObject, Task<bool>> s_bind;
        private static Action<GameObject> s_cancel;
        private Func<string, bool> _loadScene;
        private Func<GameObject, bool> _prepare;
        private Func<Scene, GameObject, Task<bool>> _bind;
        private Action<GameObject> _cancel;
        private WorldEntryPoint _entry;
        private WorldTravelCatalogDefinition _catalog;
        private SaveService _storage;
        private string _displayName;
        private bool _completing;
        private int _generation;
        private bool _nativeCompleting;
        private bool _recovering;
        private bool _initialEntry;
        private bool _ownerCancelled;
        private bool _arrivalWritten;
        private Scene _sourceScene;
        private string _sourceSceneName;
        private Vector3 _sourcePosition;
        private SaveSlotMetadata _previousMetadata;
        private byte[] _previousTravel;
        private bool _previousTravelExisted;
        public static event Action RecoveryFailed;
        public static bool IsRecoveringScene(Scene scene) => s_instance != null && s_instance._recovering
            && scene.IsValid() && scene.name == s_instance._sourceSceneName;
        public GameObject Actor { get; private set; }
        public bool IsBusy => _entry != null;
        public string LastError { get; private set; } = string.Empty;
        public static bool HasPendingTravel => s_instance != null && s_instance.IsBusy;
        public static GameObject TravellingActor => s_instance != null && s_instance.IsBusy ? s_instance.Actor : null;

        public static void ConfigureDefaultOwnerHandlers(Func<GameObject, bool> prepare, Func<Scene, GameObject, Task<bool>> bind, Action<GameObject> cancel)
        {
            s_prepare = prepare; s_bind = bind; s_cancel = cancel;
        }
        public void Configure(Func<string, bool> loadScene, Func<GameObject, bool> prepare, Func<Scene, GameObject, Task<bool>> bind, Action<GameObject> cancel)
        {
            if (IsBusy) return;
            _loadScene = loadScene; _prepare = prepare; _bind = bind; _cancel = cancel;
            s_instance = this;
        }
        public static bool TryRequestTravel(GameObject actor, string sceneName, string spawnId, string displayName)
        {
            var runtime = Ensure(); return runtime != null && runtime.BeginTravel(actor, sceneName, spawnId, displayName);
        }
        public static bool TryRequestEntry(SaveSlotMetadata metadata, string sceneName, string spawnId)
        {
            var runtime = Ensure(); return runtime != null && runtime.BeginEntry(metadata, sceneName, spawnId);
        }
        public static bool TryGetPendingEntry(Scene scene, out WorldEntryPoint entry)
        {
            entry = s_instance != null && s_instance.IsBusy && scene.IsValid() && s_instance._entry.SceneName == scene.name ? s_instance._entry : null;
            return entry != null;
        }
        public static bool TryCompleteEntry(Scene scene, GameObject actor, Camera camera)
        {
            if (s_instance != null && s_instance.IsBusy && s_instance._storage != null && ActiveSaveContext.SlotId != s_instance._storage.Slot)
            {
                s_instance.CancelPendingEntry();
                return false;
            }
            if (s_instance == null || s_instance._nativeCompleting || !s_instance.MatchesPendingScene(scene)
                || (s_instance.Actor != null && actor != s_instance.Actor)) return false;
            s_instance._nativeCompleting = true;
            if (s_instance.Actor == null && actor != null) s_instance.Actor = actor;
            _ = s_instance.CompleteWithRecoveryAsync(scene, actor, camera, s_instance._generation);
            return true;
        }
        public static bool ReportEntryFailure(Scene scene)
        {
            return s_instance != null && TryCompleteEntry(scene, s_instance.Actor, null);
        }
        public static bool TryBindSceneOwner(Scene scene, GameObject actor)
        {
            var binding = BindSceneOwnerAsync(scene, actor);
            return !binding.IsCompleted || binding.GetAwaiter().GetResult();
        }
        public static async Task<bool> BindSceneOwnerAsync(Scene scene, GameObject actor)
        {
            if (HasPendingTravel || !scene.IsValid() || !scene.isLoaded || actor == null || actor.scene != scene
                || AuthoredWorldScene.Find(scene) == null) return false;
            var bind = s_instance != null && s_instance._bind != null ? s_instance._bind : s_bind;
            if (bind == null) return false;
            try
            {
                bool bound = await bind(scene, actor);
                return bound && actor != null && actor.scene == scene && scene.isLoaded && !HasPendingTravel;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                return false;
            }
        }

        public bool BeginTravel(GameObject actor, string sceneName, string spawnId, string displayName)
        {
            if (IsBusy || actor == null || actor.transform.parent != null || _prepare == null || _bind == null
                || !TryResolve(sceneName, spawnId, out var entry, out var catalog) || ActiveSaveContext.Metadata == null) return false;
            SaveService storage;
            try { storage = new SaveService(ActiveSaveContext.SlotId); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { return Failure(); }
            if (!CaptureDeparture(storage, actor)) return Failure();
            if (!_prepare(actor)) return Failure();
            BeginState(entry, catalog, storage, actor, displayName);
            if (Application.isPlaying) DontDestroyOnLoad(actor);
            if (Load(sceneName)) return true;
            RestoreRejectedDeparture(); CancelPendingEntry(); return Failure();
        }
        public bool BeginEntry(SaveSlotMetadata metadata, string sceneName, string spawnId)
        {
            if (IsBusy || metadata == null || string.IsNullOrWhiteSpace(metadata.SlotId) || _bind == null
                || !TryResolve(sceneName, spawnId, out var entry, out var catalog)) return false;
            SaveService storage;
            try { storage = new SaveService(metadata.SlotId); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { return Failure(); }
            var previous = ActiveSaveContext.Metadata;
            if (!CaptureDeparture(storage, null)) return Failure();
            BeginState(entry, catalog, storage, null, string.Empty);
            ActiveSaveContext.Set(metadata);
            if (Load(sceneName)) return true;
            CancelPendingEntry(); ActiveSaveContext.Set(previous); return Failure();
        }
        public async Task<bool> CompleteEntryAsync(Scene scene, GameObject actor, Camera camera)
        {
            if (!CanComplete(scene, actor) || camera == null || WorldEntryService.FindUniqueSpawn(scene, _entry.SpawnId) == null) return false;
            _completing = true;
            int generation = _generation;
            try
            {
                // A failed arrival write cannot move the actor or publish a destination owner.
                if (!WorldEntryService.SaveTravel(_catalog, _storage, _entry.SceneName, _entry.SpawnId, _displayName)) return Failure();
                _arrivalWritten = true;
                if (!WorldEntryService.TryPlaceActor(scene, actor, _entry.SpawnId, camera)) return Failure();
                Actor = actor;
                TownReturnViewCleanup.Apply(scene);
                bool bound = await _bind(scene, actor);
                if (!OwnsAttempt(generation)) return false;
                if (!bound) return Failure();
                ClearState(); LastError = string.Empty;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            {
                return OwnsAttempt(generation) ? Failure() : false;
            }
            finally { if (this != null && generation == _generation) _completing = false; }
        }

        private bool MatchesPendingScene(Scene scene) => IsBusy && !_recovering && scene.IsValid() && scene.isLoaded
            && scene.name == _entry.SceneName && _storage != null && ActiveSaveContext.SlotId == _storage.Slot;
        private bool OwnsAttempt(int generation) => this != null && generation == _generation && IsBusy
            && _storage != null && ActiveSaveContext.SlotId == _storage.Slot;

        private async Task CompleteWithRecoveryAsync(Scene scene, GameObject actor, Camera camera, int generation)
        {
            try
            {
                for (int attempt = 0; attempt < 3 && OwnsAttempt(generation); attempt++)
                {
                    var completion = CompleteEntryAsync(scene, actor, camera);
                    var finished = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(5)));
                    if (!OwnsAttempt(generation)) return;
                    if (finished != completion)
                    {
                        // Invalidate the pending async publication before recovery starts.
                        generation = ++_generation;
                        _completing = false;
                        break;
                    }
                    if (await completion) return;
                    if (!OwnsAttempt(generation)) return;
                    if (attempt < 2) await Task.Yield();
                }
                if (OwnsAttempt(generation)) await SafeRecoverAsync(generation);
            }
            catch (Exception)
            {
                if (OwnsAttempt(generation)) await SafeRecoverAsync(generation);
            }
            finally
            {
                if (this != null && generation == _generation)
                {
                    _nativeCompleting = false;
                    if (IsBusy && _storage != null && ActiveSaveContext.SlotId != _storage.Slot) CancelPendingEntry();
                }
            }
        }

        private async Task SafeRecoverAsync(int generation)
        {
            try { await RecoverAsync(generation); }
            catch (Exception)
            {
                if (!OwnsAttempt(generation)) return;
                var actor = Actor;
                try { CancelOwnerOnce(); } catch (Exception) { }
                if (actor != null)
                {
                    actor.SetActive(false);
                    if (Application.isPlaying) Destroy(actor); else DestroyImmediate(actor);
                }
                if (_initialEntry) ActiveSaveContext.Set(_previousMetadata);
                ClearState();
                Failure();
                RecoveryFailed?.Invoke();
            }
        }

        private bool CaptureDeparture(SaveService storage, GameObject actor)
        {
            try
            {
                _sourceScene = actor != null ? actor.scene : SceneManager.GetActiveScene();
                _sourceSceneName = _sourceScene.name;
                _sourcePosition = actor != null ? actor.transform.position : Vector3.zero;
                _previousMetadata = ActiveSaveContext.Metadata;
                string path = Path.Combine(storage.DirectoryPath, WorldEntryService.FileName);
                _previousTravelExisted = File.Exists(path);
                _previousTravel = _previousTravelExisted ? File.ReadAllBytes(path) : null;
                _initialEntry = actor == null;
                _arrivalWritten = false;
                _ownerCancelled = false;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { return false; }
        }

        private void RestoreRejectedDeparture()
        {
            if (Actor == null || !_sourceScene.IsValid() || !_sourceScene.isLoaded) return;
            if (Actor.scene != _sourceScene) SceneManager.MoveGameObjectToScene(Actor, _sourceScene);
            Actor.transform.position = _sourcePosition;
        }

        private bool RestoreTravelSnapshot()
        {
            if (!_arrivalWritten) return true;
            string path = Path.Combine(_storage.DirectoryPath, WorldEntryService.FileName);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".recovery.tmp";
            try
            {
                if (!_previousTravelExisted)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return true;
                }
                File.WriteAllBytes(temporary, _previousTravel);
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

        private async Task RecoverAsync(int generation)
        {
            if (!OwnsAttempt(generation)) return;
            _recovering = true;
            Failure();
            var actor = Actor;
            bool recovered = RestoreTravelSnapshot();
            if (!OwnsAttempt(generation)) return;
            if (actor != null && Application.isPlaying) DontDestroyOnLoad(actor);
            if (!_sourceScene.IsValid() || !_sourceScene.isLoaded)
            {
                recovered &= !string.IsNullOrEmpty(_sourceSceneName) && Load(_sourceSceneName);
                if (!OwnsAttempt(generation)) return;
                _sourceScene = SceneManager.GetSceneByName(_sourceSceneName);
            }
            recovered &= _sourceScene.IsValid() && _sourceScene.isLoaded;
            if (recovered)
            {
                SceneManager.SetActiveScene(_sourceScene);
                if (!_initialEntry && actor != null)
                {
                    Camera sourceCamera = null;
                    foreach (var root in _sourceScene.GetRootGameObjects())
                    {
                        sourceCamera = root.GetComponentInChildren<Camera>();
                        if (sourceCamera != null) break;
                    }
                    recovered = WorldEntryService.TryRestoreActor(_sourceScene, actor, _sourcePosition, sourceCamera);
                    CancelOwnerOnce();
                    if (recovered)
                    {
                        var binding = _bind(_sourceScene, actor);
                        var finished = await Task.WhenAny(binding, Task.Delay(TimeSpan.FromSeconds(5)));
                        if (!OwnsAttempt(generation)) return;
                        recovered = finished == binding && await binding;
                    }
                }
            }
            if (!OwnsAttempt(generation)) return;
            bool showEntryError = _initialEntry || !recovered;
            CancelOwnerOnce();
            if (showEntryError && actor != null)
            {
                actor.SetActive(false);
                if (Application.isPlaying) Destroy(actor); else DestroyImmediate(actor);
            }
            if (_initialEntry) ActiveSaveContext.Set(_previousMetadata);
            ClearState();
            Failure();
            if (showEntryError) RecoveryFailed?.Invoke();
        }

        private void CancelOwnerOnce()
        {
            if (_ownerCancelled || Actor == null) return;
            _ownerCancelled = true;
            _cancel?.Invoke(Actor);
        }

        public void CancelPendingEntry()
        {
            CancelOwnerOnce();
            ClearState();
        }
        private bool CanComplete(Scene scene, GameObject actor)
        {
            return IsBusy && !_completing && _bind != null && scene.IsValid() && scene.isLoaded && scene.name == _entry.SceneName
                && actor != null && actor.transform.parent == null && (Actor == null || ReferenceEquals(Actor, actor))
                && _storage != null && ActiveSaveContext.SlotId == _storage.Slot;
        }
        private void BeginState(WorldEntryPoint entry, WorldTravelCatalogDefinition catalog, SaveService storage, GameObject actor, string displayName)
        {
            _generation++; _entry = entry; _catalog = catalog; _storage = storage; Actor = actor; _displayName = displayName ?? string.Empty;
            LastError = string.Empty; UiGameplayInputLock.Acquire(this);
        }
        private void ClearState()
        {
            _generation++; _entry = null; _catalog = null; _storage = null; Actor = null; _completing = false; _nativeCompleting = false; _recovering = false;
            UiGameplayInputLock.Release(this);
        }
        private bool Failure() { LastError = GameText.Get("world.travel.failed"); return false; }
        private bool Load(string sceneName)
        {
            try { return _loadScene != null && _loadScene(sceneName); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { return false; }
        }
        private static bool TryResolve(string sceneName, string spawnId, out WorldEntryPoint entry, out WorldTravelCatalogDefinition catalog)
        {
            catalog = null; entry = null;
            if (!GameDataRegistryProvider.TryGet(out var registry)) return false;
            catalog = registry.WorldTravelCatalog; entry = WorldEntryService.ResolvePair(catalog, sceneName, spawnId);
            return entry != null;
        }
        private static WorldTravelRuntime Ensure()
        {
            if (s_instance != null) return s_instance;
            if (!Application.isPlaying || s_prepare == null || s_bind == null) return null;
            var go = new GameObject("[WorldTravelRuntime]"); DontDestroyOnLoad(go);
            s_instance = go.AddComponent<WorldTravelRuntime>();
            s_instance.Configure(LoadScene, s_prepare, s_bind, s_cancel);
            return s_instance;
        }
        private static bool LoadScene(string sceneName)
        {
            if (!Application.isPlaying || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            SceneManager.LoadScene(sceneName); return true;
        }
        private void OnDestroy()
        {
            CancelPendingEntry();
            if (ReferenceEquals(s_instance, this)) s_instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_instance = null; s_prepare = null; s_bind = null; s_cancel = null; RecoveryFailed = null; }
    }
}
