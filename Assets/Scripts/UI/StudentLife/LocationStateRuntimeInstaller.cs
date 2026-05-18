using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class LocationStateRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[LocationStateRuntimeInstaller]";
        private const string CanvasName = "LocationStateCanvas";
        private const string LogButtonName = "LocationStateLogButton";

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
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.name == TownSceneName) EnsureScene(scene);
        }

        private static void EnsureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != TownSceneName || FindRoot(scene, RunnerName) != null) return;
            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private GameDataRegistry _registry;
            private GameDataLookupCache _dataCache;
            private LocationStateLookupCache _stateCache;
            private LocationStateResolver _resolver;
            private LocationStatePanel _panel;
            private LocationStateLogPanel _logPanel;
            private GameObject _lastPlayer;

            private void Awake()
            {
                LocationActivityInteractor.OnAnyInteracted += HandleLocationInteracted;
            }

            private void OnDestroy()
            {
                LocationActivityInteractor.OnAnyInteracted -= HandleLocationInteracted;
            }

            private void EnsureReady()
            {
                if (_resolver != null && _panel != null && _logPanel != null) return;
                _registry = ResolveRegistry();
                _dataCache = new GameDataLookupCache(_registry);
                _stateCache = new LocationStateLookupCache(GameDataRegistryLocationStateExtensions.GetLocationStates(_registry));
                var policies = GameDataRegistryLocationStateExtensions.GetLocationStateConflictPolicies(_registry);
                _resolver = new LocationStateResolver(_stateCache, policies != null && policies.Length > 0 ? policies[0] : LocationStateConflictPolicyDefinition.HighestPriority());
                var canvas = EnsureCanvas(SceneManager.GetActiveScene());
                _panel = LocationStatePanel.EnsureInScene(canvas);
                _logPanel = LocationStateLogPanel.EnsureInScene(canvas);
                EnsureLogButton(canvas, ShowLogForLastPlayer);
            }

            private void HandleLocationInteracted(LocationActivityInteractor interactor, GameObject player)
            {
                if (interactor == null || interactor.Identity == null || interactor.Identity.Location == null || player == null) return;
                _lastPlayer = player;
                EnsureReady();
                var studentComponent = player.GetComponent<StudentLifeProgressComponent>();
                var student = studentComponent != null ? studentComponent.EnsureProgress() : null;
                if (student == null) return;
                string timeSlotId = NpcScheduleResolver.ResolveTimeSlotId(student);
                _dataCache.TryGetTimeSlot(timeSlotId, out var timeSlot);
                string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId) ? ActiveSaveContext.Metadata.SlotId : student.SaveSlot;
                string playerId = ResolvePlayerId(player, student.PlayerId);
                var locationProgress = LocationStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
                var world = WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                QuestLog questLog = questLogPanel != null ? questLogPanel.QuestLog : null;
                var summary = _resolver.Resolve(new LocationStateContext(interactor.Identity.Location, timeSlot, student, world, questLog, new NpcScheduleResolver(_dataCache), locationProgress));
                LocationStateProgressPersistence.Save(locationProgress);
                _panel.Show(summary);
            }

            private void ShowLogForLastPlayer()
            {
                EnsureReady();
                var studentComponent = _lastPlayer != null ? _lastPlayer.GetComponent<StudentLifeProgressComponent>() : null;
                var student = studentComponent != null ? studentComponent.EnsureProgress() : null;
                if (student == null)
                {
                    _logPanel.Show(null);
                    return;
                }

                string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId) ? ActiveSaveContext.Metadata.SlotId : student.SaveSlot;
                string playerId = ResolvePlayerId(_lastPlayer, student.PlayerId);
                var progress = LocationStateProgressPersistence.LoadOrCreate(saveSlot, playerId);
                _logPanel.Show(progress.TodaySummary);
            }
        }

        private static void EnsureLogButton(Canvas canvas, UnityEngine.Events.UnityAction onClick)
        {
            if (canvas == null) return;
            var existing = FindByName(canvas.gameObject.scene, LogButtonName);
            if (existing != null) return;
            var go = new GameObject(LogButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -92f);
            rt.sizeDelta = new Vector2(168f, 36f);
            go.GetComponent<Image>().color = new Color(0.72f, 0.58f, 0.32f, 0.92f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            MakeText(rt, "Label", "Location Log", Vector2.zero, rt.sizeDelta, 16, TextAnchor.MiddleCenter);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static string ResolvePlayerId(GameObject player, string fallback)
        {
            var identity = player.GetComponent<PlayerIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(fallback) ? PlayerIdentity.DefaultPlayerId : fallback;
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
#if UNITY_EDITOR
            if (registry == null) registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
#endif
            return registry;
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var existing = FindComponentInScene<Canvas>(scene, true);
            if (existing != null && existing.name == CanvasName) return existing;
            var canvasGo = FindByName(scene, CanvasName);
            if (canvasGo == null)
            {
                canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
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
