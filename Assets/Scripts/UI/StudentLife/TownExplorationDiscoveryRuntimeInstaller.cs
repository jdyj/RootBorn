using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.StudentLife
{
    public static class TownExplorationDiscoveryRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownExplorationDiscoveryRuntimeInstaller]";
        private const string DiscoveryPointName = "TownDiscoveryPoint";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        public static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                EnsureScene(scene);
            }
        }

        public static void EnsureScene(Scene scene)
        {
            if (scene.name != TownSceneName || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        public static DiscoveryDefinition ResolveFirstDiscoveryForTests(GameDataRegistry registry)
        {
            if (registry == null || registry.Discoveries == null)
            {
                return null;
            }

            for (int i = 0; i < registry.Discoveries.Length; i++)
            {
                var discovery = registry.Discoveries[i];
                if (HasUsableOutcomes(discovery))
                {
                    return discovery;
                }
            }

            return null;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureScene(scene);
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                Scene scene = SceneManager.GetActiveScene();
                DiscoveryDefinition discovery = null;
                float elapsed = 0f;
                while (elapsed < 8f)
                {
                    discovery = ResolveFirstDiscovery();
                    if (FindRoot(scene, "Player") != null && discovery != null)
                    {
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                EnsureDiscoveryPoint(scene, discovery);
            }
        }

        private static DiscoveryDefinition ResolveFirstDiscovery()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            var discovery = ResolveFirstDiscoveryForTests(registry);
            if (discovery != null)
            {
                return discovery;
            }

#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            return ResolveFirstDiscoveryForTests(registry);
#else
            return null;
#endif
        }

        private static bool HasUsableOutcomes(DiscoveryDefinition discovery)
        {
            if (discovery == null || discovery.Outcomes == null || discovery.Outcomes.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < discovery.Outcomes.Count; i++)
            {
                if (discovery.Outcomes[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureDiscoveryPoint(Scene scene, DiscoveryDefinition discovery)
        {
            if (discovery == null)
            {
                return;
            }

            var go = FindByName(scene, DiscoveryPointName);
            if (go == null)
            {
                go = new GameObject(DiscoveryPointName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = new Vector3(discovery.WorldPosition.x, discovery.WorldPosition.y, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.82f, 0.68f, 0.22f, 1f));
            }
            renderer.sortingOrder = 55;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            float size = Mathf.Max(0.65f, discovery.InteractionRadius);
            collider.size = new Vector2(size, size);

            var interactor = go.GetComponent<DiscoveryPointInteractor>();
            if (interactor == null)
            {
                interactor = go.AddComponent<DiscoveryPointInteractor>();
            }
            interactor.Bind(discovery);
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "TownDiscoveryMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
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

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
