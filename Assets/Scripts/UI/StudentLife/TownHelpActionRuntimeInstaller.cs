using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.World;
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
            if (AuthoredWorldScene.Find(scene) != null) return;
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
            var actions = ResolveActionsForTests(registry);
            return actions.Length > 0 ? actions[0] : null;
        }

        public static TownHelpActionDefinition[] ResolveActionsForTests(GameDataRegistry registry)
        {
            if (registry == null || registry.TownHelpActions == null)
            {
                return new TownHelpActionDefinition[0];
            }

            var actions = new List<TownHelpActionDefinition>();
            for (int i = 0; i < registry.TownHelpActions.Length; i++)
            {
                var action = registry.TownHelpActions[i];
                if (HasUsableOutcomes(action))
                {
                    actions.Add(action);
                }
            }

            return actions.ToArray();
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
                TownHelpActionDefinition[] actions = new TownHelpActionDefinition[0];
                float elapsed = 0f;
                while (elapsed < 8f)
                {
                    actions = ResolveActions();
                    if (FindRoot(scene, "Player") != null && actions.Length > 0)
                    {
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                EnsureHelpPoints(scene, actions);
            }
        }

        private static TownHelpActionDefinition[] ResolveActions()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            var actions = ResolveActionsForTests(registry);
            if (actions.Length > 0)
            {
                return actions;
            }

#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            return ResolveActionsForTests(registry);
#else
            return new TownHelpActionDefinition[0];
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

        private static void EnsureHelpPoints(Scene scene, TownHelpActionDefinition[] actions)
        {
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                EnsureHelpPoint(scene, actions[i], i);
            }
        }

        private static void EnsureHelpPoint(Scene scene, TownHelpActionDefinition action, int index)
        {
            // Saved interactors already contain their SO binding; never generate or reposition prototypes.
            if (AuthoredWorldScene.Find(scene) != null) return;
            if (action == null)
            {
                return;
            }

            string objectName = index == 0 ? HelpPointName : HelpPointName + "_" + Sanitize(action.Id);
            var go = FindByName(scene, objectName);
            if (go == null)
            {
                go = new GameObject(objectName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = new Vector3(-4.25f + index * 1.2f, -3.2f, 0f);
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

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "action";
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
