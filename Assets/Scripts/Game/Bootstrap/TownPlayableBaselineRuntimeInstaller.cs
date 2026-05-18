using System;
using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Characters;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Game.Bootstrap
{
    public static class TownPlayableBaselineRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownPlayableBaselineRuntimeInstaller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                var bootstrap = Managers.Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted)
                {
                    yield return null;
                }

                if (bootstrap.IsFaulted)
                {
                    Debug.LogException(bootstrap.Exception);
                    yield break;
                }

                Scene scene = SceneManager.GetActiveScene();
                var players = new List<GameObject>();
                float elapsed = 0f;

                while (players.Count == 0 && elapsed < 5f)
                {
                    CollectPlayerRoots(scene, players);
                    if (players.Count == 0)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (players.Count == 0 && ShouldCreateLocalPlayerFallback())
                {
                    players.Add(CreateLocalPlayer(scene));
                }

                if (players.Count == 0)
                {
                    yield break;
                }

                GameObject cameraTarget = null;
                for (int i = 0; i < players.Count; i++)
                {
                    var candidate = ResolveLivePlayer(scene, players[i]);
                    if (candidate == null)
                    {
                        continue;
                    }

                    ReinforcePlayer(candidate);
                    yield return ConfigureLayeredPlayer(candidate);
                    if (cameraTarget == null && candidate != null)
                    {
                        cameraTarget = candidate;
                    }
                }

                cameraTarget = ResolveLivePlayer(scene, cameraTarget);
                if (cameraTarget != null)
                {
                    EnsureCamera(cameraTarget.transform);
                }
                EnsureCanvas(scene);
                EnsureEventSystem(scene);

                for (int i = 0; i < 12; i++)
                {
                    yield return null;
                    CollectPlayerRoots(scene, players);
                    if (players.Count == 0)
                    {
                        yield break;
                    }

                    foreach (var candidate in players)
                    {
                        var live = ResolveLivePlayer(scene, candidate);
                        if (live == null)
                        {
                            continue;
                        }

                        ReinforcePlayer(live);
                        if (CountVisiblePartSprites(live) < 5)
                        {
                            yield return ConfigureLayeredPlayer(live);
                        }
                    }

                    EnsureCanvas(scene);
                }
            }
        }

        private static GameObject CreateLocalPlayer(Scene scene)
        {
            var player = new GameObject("Player");
            player.transform.position = ResolveLocalPlayerSpawnPosition(scene);
            SceneManager.MoveGameObjectToScene(player, scene);
            player.AddComponent<PlayerIdentity>();
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInteractionRouter>();
            return player;
        }

        private static Vector3 ResolveLocalPlayerSpawnPosition(Scene scene)
        {
            var spawn = FindRoot(scene, "TownSpawnPoint");
            if (spawn != null) return spawn.transform.position;
            return new Vector3(TownSquareSpawnX, TownSquareSpawnY, 0f);
        }

        private const float TownSquareSpawnX = 5f;
        private const float TownSquareSpawnY = -4f;

        private static bool ShouldCreateLocalPlayerFallback()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "-mode", StringComparison.OrdinalIgnoreCase))
                {
                    string mode = i + 1 < args.Length ? args[i + 1] : string.Empty;
                    return IsSinglePlayerMode(mode);
                }

                const string prefix = "-mode=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return IsSinglePlayerMode(arg.Substring(prefix.Length));
                }
            }

            return true;
        }

        private static bool IsSinglePlayerMode(string mode)
        {
            return string.IsNullOrEmpty(mode) ||
                   (!string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mode, "client", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase));
        }

        private static GameObject ResolveLivePlayer(Scene scene, GameObject current)
        {
            if (current != null)
            {
                return current;
            }

            return FindRoot(scene, "Player");
        }

        private static void ReinforcePlayer(GameObject player)
        {
            if (player == null)
            {
                return;
            }

            EnsurePlayerInteractionRouter(player);

            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = player.AddComponent<PlayerInventory>();
            }

            var registry = Managers.Managers.Data != null ? Managers.Managers.Data.Registry : null;
            inventory.Bind(registry);

            var interactor = player.GetComponent<GatherInteractor>();
            if (interactor == null)
            {
                interactor = player.AddComponent<GatherInteractor>();
            }
            interactor.BindInventory(inventory);

            if (registry != null && interactor.KnowledgeProgress == null)
            {
                interactor.Bind(new KnowledgeProgress(registry.Knowledge));
            }

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = player.AddComponent<Rigidbody2D>();
            }
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (player.GetComponent<Collider2D>() == null)
            {
                player.AddComponent<BoxCollider2D>();
            }

            EnsureVisiblePlayerSprite(player, registry);
        }

        private static void EnsurePlayerInteractionRouter(GameObject player)
        {
            if (player.GetComponent<PlayerInteractionRouter>() == null)
            {
                player.AddComponent<PlayerInteractionRouter>();
            }
        }

        private static IEnumerator ConfigureLayeredPlayer(GameObject player)
        {
            var registry = Managers.Managers.Data != null ? Managers.Managers.Data.Registry : null;
            var resource = Managers.Managers.Resource;
            if (player == null || registry == null || registry.CharacterParts == null || registry.CharacterParts.Length == 0 || resource == null)
            {
                yield break;
            }

            var composer = player.GetComponent<CharacterPartComposer>();
            if (composer == null)
            {
                composer = player.AddComponent<CharacterPartComposer>();
            }
            composer.EnsureLayers(registry.CharacterParts);

            var animator = player.GetComponent<CharacterPartAnimator>();
            if (animator == null)
            {
                animator = player.AddComponent<CharacterPartAnimator>();
            }

            var pixelwoodView = player.GetComponent<PixelwoodCharacterVisualView>();
            if (pixelwoodView == null)
            {
                pixelwoodView = player.AddComponent<PixelwoodCharacterVisualView>();
            }
            pixelwoodView.ConfigureForTests(animator, composer);

            var visualAdapter = player.GetComponent<PlayerCharacterVisualAdapter>();
            if (visualAdapter == null)
            {
                visualAdapter = player.AddComponent<PlayerCharacterVisualAdapter>();
            }
            visualAdapter.ConfigureForTests(pixelwoodView);

            var appearance = CharacterAppearance.ResolveWithDefaults(ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.Appearance : null, registry.CharacterParts);
            for (int i = 0; i < registry.CharacterParts.Length; i++)
            {
                var part = registry.CharacterParts[i];
                if (part == null)
                {
                    continue;
                }

                string subName = CharacterPartComposer.BuildFrameSubSpriteName(part, 0, 0);
                var task = resource.LoadSubSpriteAsync(part.SheetAddress, subName);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (player == null)
                {
                    yield break;
                }

                if (task.IsFaulted)
                {
                    Debug.LogException(task.Exception);
                }
            }

            if (player == null)
            {
                yield break;
            }

            animator.Configure(composer, appearance, registry.CharacterParts, (part, subSpriteName) => resource.GetCachedSubSprite(part.SheetAddress, subSpriteName));
            animator.SetMotion(Vector2.zero, new Vector2(0f, -1f));
            animator.Tick(0f);

            if (CountVisiblePartSprites(player) >= 5)
            {
                var rootRenderer = player.GetComponent<SpriteRenderer>();
                if (rootRenderer != null)
                {
                    rootRenderer.sprite = null;
                    rootRenderer.enabled = false;
                }
            }
        }

        private static void EnsureVisiblePlayerSprite(GameObject player, GameDataRegistry registry)
        {
            if (HasVisibleSpriteRenderer(player))
            {
                return;
            }

            var renderer = player.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = player.AddComponent<SpriteRenderer>();
            }

            if (renderer.sprite == null)
            {
                renderer.sprite = registry != null ? registry.PlayerSprite : null;
            }

            renderer.enabled = renderer.sprite != null;
            renderer.sortingOrder = 20;
        }

        private static bool HasVisibleSpriteRenderer(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer.gameObject.activeInHierarchy && renderer.enabled && renderer.sprite != null && renderer.color.a > 0.01f && renderer.bounds.size.sqrMagnitude > 0.01f)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountVisiblePartSprites(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer.gameObject.name.StartsWith("Part_") && renderer.gameObject.name != "Part_tool" && renderer.gameObject.activeInHierarchy && renderer.enabled && renderer.sprite != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void EnsureCamera(Transform player)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<CameraFollow>();
            }
            follow.SetTarget(player);
        }

        private static void EnsureCanvas(Scene scene)
        {
            var canvas = FindComponentInScene<Canvas>(scene);
            if (canvas == null)
            {
                var canvasGo = new GameObject("[TownCanvas]", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            EnsureTownHud(canvas.transform);
        }

        private static void EnsureTownHud(Transform canvasTransform)
        {
            var child = canvasTransform.Find("TownHud");
            GameObject hudGo;
            if (child == null)
            {
                hudGo = new GameObject("TownHud", typeof(RectTransform), typeof(Image));
                hudGo.transform.SetParent(canvasTransform, false);
            }
            else
            {
                hudGo = child.gameObject;
                if (hudGo.GetComponent<Image>() == null)
                {
                    hudGo.AddComponent<Image>();
                }
            }

            hudGo.SetActive(true);
            var rect = (RectTransform)hudGo.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(288f, 96f);

            var image = hudGo.GetComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            var key = ModernHudSpriteKeys.HintPanel;
            var resource = Managers.Managers.Resource;
            image.sprite = resource != null ? resource.GetCachedSubSprite(key.SheetAddress, key.SubSpriteName) : null;
            image.enabled = image.sprite != null;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            var eventSystem = FindComponentInScene<EventSystem>(scene);
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                AddUiInputModule(go);
                SceneManager.MoveGameObjectToScene(go, scene);
                return;
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                AddUiInputModule(eventSystem.gameObject);
            }
        }

        private static void AddUiInputModule(GameObject go)
        {
            UiInputModuleInstaller.AddPreferredInputModule(go);
        }

        private static void CollectPlayerRoots(Scene scene, List<GameObject> players)
        {
            players.Clear();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsPlayerRoot(roots[i]))
                {
                    players.Add(roots[i]);
                }
            }
        }

        private static bool IsPlayerRoot(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return root.name == "Player" ||
                   root.name == "Player(Clone)" ||
                   root.GetComponent<PlayerIdentity>() != null ||
                   root.GetComponent<PlayerController>() != null;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
