using System;
using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.WorldState;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rootborn.UI.WorldState
{
    public static class WorldStateLogRuntimeInstaller
    {
        private const string BootSceneName = "Boot";
        private const string MainMenuSceneName = "MainMenu";
        private const string RunnerName = "[WorldStateLogRuntimeInstaller]";
        private const string CanvasName = "WorldStateLogCanvas";
        private const string ButtonName = "WorldStateLogButton";
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        public static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (ShouldInstall(scene)) EnsureScene(scene);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShouldInstall(scene)) EnsureScene(scene);
        }

        private static bool ShouldInstall(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && scene.name != BootSceneName && scene.name != MainMenuSceneName;
        }

        private static void EnsureScene(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null) return;
            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private Scene _scene;
            private Canvas _canvas;
            private WorldStateLogPanel _panel;
            private GameDataRegistry _registry;

            private IEnumerator Start()
            {
                _scene = SceneManager.GetActiveScene();
                var bootstrap = Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted) yield return null;
                if (bootstrap.IsFaulted)
                {
                    Debug.LogException(bootstrap.Exception);
                    yield break;
                }

                _registry = ResolveRegistry();
                _canvas = EnsureCanvas(_scene);
                _panel = WorldStateLogPanel.EnsureInScene(_canvas);
                _panel.Hide();
                EnsureButton(_scene, _canvas, _panel, BuildWorldLogSummaries, RefreshSceneMarkers);
                RefreshSceneMarkers();
            }

            private WorldStateSummaryModel[] BuildWorldLogSummaries()
            {
                var progress = LoadProgress();
                return WorldStateSummaryBuilder.BuildForSurface(ResolveWorldStateFlags(_registry), progress, WorldStateSummarySurface.WorldLog);
            }

            private void RefreshSceneMarkers()
            {
                var progress = LoadProgress();
                WorldStateSceneMarkerInstaller.Refresh(_scene, ResolveWorldStateFlags(_registry), progress);
            }

            private static WorldStateProgress LoadProgress()
            {
                return WorldStateProgressPersistence.LoadOrCreate(ResolveActiveSaveSlot(), ResolvePlayerId());
            }
        }

        private static void EnsureButton(Scene scene, Canvas canvas, WorldStateLogPanel panel, Func<WorldStateSummaryModel[]> summaries, Action refreshMarkers)
        {
            var existing = FindByName(scene, ButtonName);
            if (existing != null) UnityEngine.Object.Destroy(existing);

            var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -74f);
            rect.sizeDelta = new Vector2(180f, 42f);
            go.GetComponent<Image>().color = Color.clear;
            var tile = go.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                refreshMarkers?.Invoke();
                panel.Show(summaries != null ? summaries() : Array.Empty<WorldStateSummaryModel>());
            });
            MakeText(rect, "Label", "World Log", Vector2.zero, rect.sizeDelta, 15, TextAnchor.MiddleCenter);
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var existing = FindComponentInScene<Canvas>(scene, true);
            if (existing != null)
            {
                existing.renderMode = RenderMode.ScreenSpaceOverlay;
                existing.sortingOrder = Mathf.Max(existing.sortingOrder, 1000);
                return existing;
            }

            var go = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            if (registry != null && registry.WorldStateFlags != null && registry.WorldStateFlags.Length > 0) return registry;
#if UNITY_EDITOR
            var editorRegistry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            if (editorRegistry != null) return editorRegistry;
#endif
            return registry;
        }

        private static WorldStateFlagDefinition[] ResolveWorldStateFlags(GameDataRegistry registry)
        {
            if (registry != null && registry.WorldStateFlags != null && registry.WorldStateFlags.Length > 0) return registry.WorldStateFlags;
#if UNITY_EDITOR
            var editorRegistry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            if (editorRegistry != null && editorRegistry.WorldStateFlags != null) return editorRegistry.WorldStateFlags;
#endif
            return Array.Empty<WorldStateFlagDefinition>();
        }

        private static string ResolveActiveSaveSlot()
        {
            var metadata = ActiveSaveContext.Metadata;
            return metadata != null && !string.IsNullOrEmpty(metadata.SlotId) ? metadata.SlotId : "default";
        }

        private static string ResolvePlayerId()
        {
            var player = GameObject.Find("Player");
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static T FindComponentInScene<T>(Scene scene, bool includeInactive) where T : Component
        {
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
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindChild(roots[i].transform, objectName);
                if (match != null) return match.gameObject;
            }

            return null;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), objectName);
                if (match != null) return match;
            }

            return null;
        }
    }

    public static class WorldStateSceneMarkerInstaller
    {
        private static Sprite s_markerSprite;

        public static void Refresh(Scene scene, WorldStateFlagDefinition[] flags, WorldStateProgress progress)
        {
            if (!scene.IsValid() || !scene.isLoaded || flags == null || progress == null) return;
            for (int i = 0; i < flags.Length; i++)
            {
                var flag = flags[i];
                if (flag == null || !progress.IsActive(flag)) continue;
                EnsureMarker(scene, flag);
            }
        }

        private static void EnsureMarker(Scene scene, WorldStateFlagDefinition flag)
        {
            string markerName = "WorldStateChange_" + SanitizeObjectName(flag.Id);
            if (FindRoot(scene, markerName) != null) return;

            var go = new GameObject(markerName, typeof(SpriteRenderer), typeof(BoxCollider2D));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = ResolveMarkerPosition(flag);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = EnsureSprite();
            renderer.color = new Color(0.98f, 0.82f, 0.25f, 1f);
            renderer.sortingOrder = 40;
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.75f, 0.75f);

            var label = new GameObject("Label", typeof(TextMesh));
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            var text = label.GetComponent<TextMesh>();
            text.text = Humanize(flag.DisplayNameKey);
            text.fontSize = 24;
            text.characterSize = 0.045f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
        }

        private static Vector3 ResolveMarkerPosition(WorldStateFlagDefinition flag)
        {
            if (flag != null && flag.RelatedLocation != null)
            {
                var position = flag.RelatedLocation.WorldPosition;
                return new Vector3(position.x, position.y + 0.75f, 0f);
            }

            var player = GameObject.Find("Player");
            if (player != null) return player.transform.position + new Vector3(0f, 1.25f, 0f);
            return Vector3.zero;
        }

        private static Sprite EnsureSprite()
        {
            if (s_markerSprite != null) return s_markerSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            s_markerSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return s_markerSprite;
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "empty";
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }
    }
}
