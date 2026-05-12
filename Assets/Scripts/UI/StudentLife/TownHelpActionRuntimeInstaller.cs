using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.StudentLife
{
    public static class TownHelpActionRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownHelpActionRuntimeInstaller]";
        private const string HelpPointName = "TownHelpActionPoint";

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

        public static TownHelpActionDefinition ResolveFirstActionForTests(GameDataRegistry registry)
        {
            if (registry == null || registry.TownHelpActions == null)
            {
                return null;
            }

            for (int i = 0; i < registry.TownHelpActions.Length; i++)
            {
                var action = registry.TownHelpActions[i];
                if (HasUsableOutcomes(action))
                {
                    return action;
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
                TownHelpActionDefinition action = null;
                float elapsed = 0f;
                while (elapsed < 8f)
                {
                    action = ResolveFirstAction();
                    if (FindRoot(scene, "Player") != null && action != null)
                    {
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                EnsureHelpPoint(scene, action);
            }
        }

        private static TownHelpActionDefinition ResolveFirstAction()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            var action = ResolveFirstActionForTests(registry);
            if (action != null)
            {
                return action;
            }

#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            return ResolveFirstActionForTests(registry);
#else
            return null;
#endif
        }

        private static bool HasUsableOutcomes(TownHelpActionDefinition action)
        {
            if (action == null || action.Outcomes == null || action.Outcomes.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < action.Outcomes.Count; i++)
            {
                if (action.Outcomes[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureHelpPoint(Scene scene, TownHelpActionDefinition action)
        {
            if (action == null)
            {
                return;
            }

            var go = FindByName(scene, HelpPointName);
            if (go == null)
            {
                go = new GameObject(HelpPointName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = new Vector3(-2.4f, -3.0f, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.36f, 0.54f, 0.92f, 1f));
            }
            renderer.sortingOrder = 50;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.85f, 0.85f);

            var interactor = go.GetComponent<TownHelpActionInteractor>();
            if (interactor == null)
            {
                interactor = go.AddComponent<TownHelpActionInteractor>();
            }
            interactor.Bind(action);
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "TownHelpActionMarker";
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
