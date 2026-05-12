using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.StudentLife
{
    public static class OutsideSchoolRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[OutsideSchoolRuntimeInstaller]";
        private const string ActivityPointName = "OutsideSchoolActivityPoint";

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
            TownHelpActionRuntimeInstaller.EnsureScene(scene);
            if (scene.name != TownSceneName || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        public static OutsideSchoolActivityDefinition ResolveFirstActivityForTests(GameDataRegistry registry)
        {
            var activities = ResolveActivitiesForTests(registry);
            return activities.Length > 0 ? activities[0] : null;
        }

        public static OutsideSchoolActivityDefinition[] ResolveActivitiesForTests(GameDataRegistry registry)
        {
            return registry != null && registry.OutsideSchoolActivities != null
                ? registry.OutsideSchoolActivities
                : new OutsideSchoolActivityDefinition[0];
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
                OutsideSchoolActivityDefinition[] activities = new OutsideSchoolActivityDefinition[0];
                float elapsed = 0f;
                while (elapsed < 8f)
                {
                    activities = ResolveActivities();
                    if (FindRoot(scene, "Player") != null && activities.Length > 0)
                    {
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                EnsureActivityPoints(scene, activities);
            }
        }

        private static OutsideSchoolActivityDefinition[] ResolveActivities()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            var activities = ResolveActivitiesForTests(registry);
            if (activities.Length > 0)
            {
                return activities;
            }

#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            return ResolveActivitiesForTests(registry);
#else
            return new OutsideSchoolActivityDefinition[0];
#endif
        }

        private static void EnsureActivityPoints(Scene scene, OutsideSchoolActivityDefinition[] activities)
        {
            if (activities == null)
            {
                return;
            }

            int visibleIndex = 0;
            for (int i = 0; i < activities.Length; i++)
            {
                var activity = activities[i];
                if (activity == null)
                {
                    continue;
                }

                string pointName = visibleIndex == 0 ? ActivityPointName : ActivityPointName + "_" + visibleIndex.ToString();
                EnsureActivityPoint(scene, activity, pointName, PositionForIndex(visibleIndex));
                visibleIndex++;
            }
        }

        private static Vector3 PositionForIndex(int index)
        {
            int column = index % 3;
            int row = index / 3;
            return new Vector3(2.4f + column * 1.15f, -3.0f + row * 1.05f, 0f);
        }

        private static void EnsureActivityPoint(Scene scene, OutsideSchoolActivityDefinition activity, string pointName, Vector3 position)
        {
            if (activity == null)
            {
                return;
            }

            var go = FindByName(scene, pointName);
            if (go == null)
            {
                go = new GameObject(pointName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = position;
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.28f, 0.64f, 0.38f, 1f));
            }
            renderer.sortingOrder = 49;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.85f, 0.85f);

            var interactor = go.GetComponent<OutsideSchoolActivityInteractor>();
            if (interactor == null)
            {
                interactor = go.AddComponent<OutsideSchoolActivityInteractor>();
            }
            interactor.Bind(activity);
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "OutsideSchoolActivityMarker";
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
