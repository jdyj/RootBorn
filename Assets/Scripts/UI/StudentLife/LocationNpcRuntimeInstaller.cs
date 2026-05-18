using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class LocationNpcRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[LocationNpcRuntimeInstaller]";
        private const string FallbackCanvasName = "LocationNpcCanvas";
        private const string FallbackDialoguePanelName = "LocationNpcDialoguePanel";
        private const string NpcVisualChildName = "NpcVisual";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName) EnsureScene(scene);
        }

        public static void EnsureForActiveScene()
        {
            var scene = GetSafeScene();
            if (scene.IsValid() && scene.name == TownSceneName) EnsureScene(scene);
        }

        public static void RefreshSchedulesForActiveScene()
        {
            var scene = GetSafeScene();
            if (!scene.IsValid() || scene.name != TownSceneName) return;
            EnsureScene(scene);
            var runnerRoot = FindRoot(scene, RunnerName);
            var runner = runnerRoot != null ? runnerRoot.GetComponent<Runner>() : null;
            if (runner != null) runner.RefreshNpcSchedules(force: true);
        }

        public static void EnsureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != TownSceneName || FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        public static string LocationObjectName(string locationId) => "LocationVisit_" + SanitizeName(locationId);
        public static string NpcObjectName(string npcId) => "TownNpc_" + SanitizeName(npcId);

        private sealed class Runner : MonoBehaviour
        {
            private Scene _scene;
            private GameDataRegistry _registry;
            private GameDataLookupCache _cache;
            private NpcScheduleResolver _scheduleResolver;
            private DialoguePanel _dialoguePanel;
            private LocationIdentityPanel _identityPanel;
            private StudentLifeProgressComponent _student;
            private int _lastTimeMinutes = int.MinValue;

            private IEnumerator Start()
            {
                var bootstrap = Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted) yield return null;
                if (bootstrap.IsFaulted)
                {
                    Debug.LogException(bootstrap.Exception);
                    yield break;
                }

                _scene = GetSafeScene(gameObject);
                _registry = ResolveRegistry();
                _cache = new GameDataLookupCache(_registry);
                _scheduleResolver = new NpcScheduleResolver(_cache);
                var canvas = EnsureCanvas(_scene);
                _dialoguePanel = EnsureDialoguePanel(_scene, canvas);
                _identityPanel = LocationIdentityPanel.EnsureInScene(canvas);
                EnsureLocations(_scene, _registry, _identityPanel);
                RefreshNpcSchedules(force: true);
                NpcInteractor.OnAnyInteracted += HandleNpcInteracted;
                LocationActivityInteractor.OnAnyInteracted += HandleLocationIdentityInteracted;
                DailyEventPanel.OnAnyChoiceApplied += HandleDailyEventChoiceApplied;
            }

            private void Update()
            {
                var student = ResolveStudent();
                if (student == null || student.Progress == null) return;
                int minutes = student.Progress.TimeMinutes;
                if (minutes == _lastTimeMinutes) return;
                RefreshNpcSchedules(force: true);
            }

            private void OnDestroy()
            {
                NpcInteractor.OnAnyInteracted -= HandleNpcInteracted;
                LocationActivityInteractor.OnAnyInteracted -= HandleLocationIdentityInteracted;
                DailyEventPanel.OnAnyChoiceApplied -= HandleDailyEventChoiceApplied;
            }

            private void HandleNpcInteracted(NpcInteractor npc, StudentLifeProgress progress)
            {
                if (_dialoguePanel == null) _dialoguePanel = EnsureDialoguePanel(_scene, EnsureCanvas(_scene));
                if (_dialoguePanel == null || npc == null || npc.Session.Current == null) return;
                _dialoguePanel.Open(npc.Session.Current, new DialogueChoiceContext(null, default(RewardRuntimeContext)));
            }

            private void HandleLocationIdentityInteracted(LocationActivityInteractor interactor, GameObject player)
            {
                if (_identityPanel == null) _identityPanel = LocationIdentityPanel.EnsureInScene(EnsureCanvas(_scene));
                if (_identityPanel == null || interactor == null) return;
                _identityPanel.Show(interactor.Identity, player);
            }

            private void HandleDailyEventChoiceApplied(StudentLifeProgress progress)
            {
                RefreshNpcSchedules(force: true);
            }

            public void RefreshNpcSchedules(bool force)
            {
                if (_registry == null)
                {
                    _scene = GetSafeScene(gameObject);
                    _registry = ResolveRegistry();
                    _cache = new GameDataLookupCache(_registry);
                    _scheduleResolver = new NpcScheduleResolver(_cache);
                }
                if (_registry == null || _registry.Npcs == null) return;
                var student = ResolveStudent();
                var progress = student != null ? student.Progress : null;
                int minutes = progress != null ? progress.TimeMinutes : 8 * 60;
                if (!force && minutes == _lastTimeMinutes) return;
                _lastTimeMinutes = minutes;
                string timeSlotId = NpcScheduleResolver.ResolveTimeSlotId(progress);
                var context = new NpcScheduleContext(progress != null ? progress.CurrentDay : 1, progress != null ? (progress.CurrentDay - 1) % 7 : 0, timeSlotId, progress);
                var locationCounts = new Dictionary<LocationDefinition, int>();
                for (int i = 0; i < _registry.Npcs.Length; i++)
                {
                    var npc = _registry.Npcs[i];
                    if (npc == null) continue;
                    var result = ResolveSchedule(npc, context);
                    var location = result.Location != null ? result.Location : npc.HomeLocation;
                    if (location == null) continue;
                    int slotIndex = NextLocationSlot(locationCounts, location);
                    if (!ApplyExistingNpcObjects(_scene, npc, location, timeSlotId, result, slotIndex)) ApplyNpcObject(CreateNpcObject(_scene, npc), npc, location, timeSlotId, result, slotIndex);
                }
            }

            private NpcScheduleResult ResolveSchedule(NpcDefinition npc, NpcScheduleContext context)
            {
                if (_cache != null && npc != null && _cache.TryGetNpcSchedule(npc.Id, out var schedule)) return _scheduleResolver.Resolve(schedule, context);
                return new NpcScheduleResult(npc, npc != null ? npc.HomeLocation : null, npc != null ? npc.DefaultDialogue : null, string.Empty, string.Empty, string.Empty, null);
            }

            private StudentLifeProgressComponent ResolveStudent()
            {
                if (_student != null) return _student;
                _scene = GetSafeScene(gameObject);
                var player = FindByName(_scene, "Player");
                _student = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
                return _student;
            }
        }

        private static bool ApplyExistingNpcObjects(Scene scene, NpcDefinition npc, LocationDefinition location, string timeSlotId, NpcScheduleResult result, int slotIndex)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;
            bool applied = false;
            string objectName = NpcObjectName(npc.Id);
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) applied |= ApplyExistingNpcObjects(roots[i].transform, objectName, npc, location, timeSlotId, result, slotIndex);
            return applied;
        }

        private static bool ApplyExistingNpcObjects(Transform root, string objectName, NpcDefinition npc, LocationDefinition location, string timeSlotId, NpcScheduleResult result, int slotIndex)
        {
            bool applied = false;
            if (root.name == objectName)
            {
                ApplyNpcObject(root.gameObject, npc, location, timeSlotId, result, slotIndex);
                applied = true;
            }
            for (int i = 0; i < root.childCount; i++) applied |= ApplyExistingNpcObjects(root.GetChild(i), objectName, npc, location, timeSlotId, result, slotIndex);
            return applied;
        }

        private static GameObject CreateNpcObject(Scene scene, NpcDefinition npc)
        {
            var go = new GameObject(NpcObjectName(npc.Id));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static void ApplyNpcObject(GameObject go, NpcDefinition npc, LocationDefinition location, string timeSlotId, NpcScheduleResult result, int slotIndex)
        {
            Vector2 target = location.WorldPosition + SlotOffset(slotIndex);
            go.transform.position = new Vector3(target.x, target.y, 0f);
            EnsureMarker(go, new Color(0.95f, 0.72f, 0.35f, 1f), 54);
            var interactor = go.GetComponent<NpcInteractor>();
            if (interactor == null) interactor = go.AddComponent<NpcInteractor>();
            interactor.Bind(npc);
            interactor.BindSchedule(location, timeSlotId, result.Dialogue, result.DialogueKey, result.EventNoticeKey);
        }

        private static int NextLocationSlot(Dictionary<LocationDefinition, int> locationCounts, LocationDefinition location)
        {
            if (!locationCounts.TryGetValue(location, out int index))
            {
                locationCounts[location] = 1;
                return 0;
            }

            locationCounts[location] = index + 1;
            return index;
        }

        private static Vector2 SlotOffset(int slotIndex)
        {
            int ring = slotIndex / 8;
            int side = slotIndex % 8;
            float radius = 2.25f + ring * 0.15f;
            switch (side)
            {
                case 0: return new Vector2(radius, radius);
                case 1: return new Vector2(-radius, radius);
                case 2: return new Vector2(radius, -radius);
                case 3: return new Vector2(-radius, -radius);
                case 4: return new Vector2(0f, radius + 0.45f);
                case 5: return new Vector2(radius + 0.45f, 0f);
                case 6: return new Vector2(0f, -radius - 0.45f);
                default: return new Vector2(-radius - 0.45f, 0f);
            }
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var existing = FindComponentInScene<Canvas>(scene, true);
            if (existing != null && existing.name == FallbackCanvasName) return existing;
            var canvasGo = FindByName(scene, FallbackCanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(FallbackCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
            }
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private static DialoguePanel EnsureDialoguePanel(Scene scene, Canvas canvas)
        {
            var existing = FindComponentInScene<DialoguePanel>(scene, true);
            DestroyOtherDialoguePanels(scene, existing);
            if (existing != null) return existing;
            var panelGo = new GameObject(FallbackDialoguePanelName, typeof(RectTransform), typeof(Image), typeof(DialoguePanel));
            panelGo.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panelGo.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 36f);
            rect.sizeDelta = new Vector2(760f, 210f);
            panelGo.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.14f, 0.94f);
            panelGo.SetActive(false);
            return panelGo.GetComponent<DialoguePanel>();
        }

        // QuestHudAutoFiller historically created its own "DialoguePanel" GameObject
        // because it searched by name instead of component type. Surviving orphans
        // from older builds show up as a faint second panel behind the active one.
        // Destroy any DialoguePanel GameObject that isn't the one we'll keep.
        private static void DestroyOtherDialoguePanels(Scene scene, DialoguePanel keep)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<DialoguePanel>(true);
                for (int j = 0; j < components.Length; j++)
                {
                    var candidate = components[j];
                    if (candidate == null || candidate == keep) continue;
                    UnityEngine.Object.Destroy(candidate.gameObject);
                }
            }
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            if (registry != null && registry.NpcSchedules != null && registry.NpcSchedules.Length > 0) return registry;
#if UNITY_EDITOR
            var editorRegistry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            if (editorRegistry != null) return editorRegistry;
#endif
            return registry;
        }

        private static void EnsureLocations(Scene scene, GameDataRegistry registry, LocationIdentityPanel identityPanel)
        {
            if (registry == null || registry.Locations == null) return;
            var cache = new GameDataLookupCache(registry);
            for (int i = 0; i < registry.Locations.Length; i++)
            {
                var location = registry.Locations[i];
                if (location == null) continue;
                var go = FindByName(scene, LocationObjectName(location.Id));
                if (go == null)
                {
                    go = new GameObject(LocationObjectName(location.Id));
                    SceneManager.MoveGameObjectToScene(go, scene);
                }
                go.transform.position = new Vector3(location.WorldPosition.x, location.WorldPosition.y, 0f);
                EnsureMarker(go, new Color(0.35f, 0.72f, 0.95f, 1f), 52);
                var visit = go.GetComponent<LocationVisitInteractor>();
                if (visit == null) visit = go.AddComponent<LocationVisitInteractor>();
                visit.Bind(location);
                if (cache.TryGetLocationIdentity(location.Id, out var identity))
                {
                    var identityInteractor = go.GetComponent<LocationActivityInteractor>();
                    if (identityInteractor == null) identityInteractor = go.AddComponent<LocationActivityInteractor>();
                    identityInteractor.Bind(identity);
                }
            }
        }

        private static void EnsureMarker(GameObject go, Color color, int sortingOrder)
        {
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null) renderer.sprite = CreateMarkerSprite(color);
            renderer.sortingOrder = sortingOrder;
            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null) collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.65f, 0.65f);
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "TownLocationNpcMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
        }

        private static Scene GetSafeScene(GameObject owner = null)
        {
            var scene = owner != null ? owner.scene : SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = SceneManager.GetActiveScene();
            }

            return scene;
        }

        private static string SanitizeName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "empty";
            var chars = id.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        private static T FindComponentInScene<T>(Scene scene, bool includeInactive) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var component = roots[i].GetComponentInChildren<T>(includeInactive);
                if (component != null) return component;
            }
            return null;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null) return match;
            }
            return null;
        }

        private static GameObject FindByName(Transform root, string objectName)
        {
            if (root.name == objectName) return root.gameObject;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null) return match;
            }
            return null;
        }
    }
}
