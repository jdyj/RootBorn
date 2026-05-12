using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rootborn.Game.StudentLife
{
    public static class PartTimeWorkRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[PartTimeWorkRuntimeInstaller]";
        private const string WorkPointName = "PartTimeWorkPoint";
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

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
                Ensure(scene);
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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
                float elapsed = 0f;
                while (elapsed < 10f)
                {
                    var work = FirstWork(ResolveRegistry());
                    var player = GameObject.Find("Player");
                    if (work != null && player != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    {
                        EnsureWorkPoint(gameObject.scene, work);
                        Debug.Log("[ROOTBORN] Part-time work point installed work=" + work.Id);
                        yield break;
                    }

                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                }

                Debug.LogWarning("[ROOTBORN] Part-time work point install timed out.");
            }
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            if (FirstWork(registry) != null)
            {
                return registry;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
#else
            return registry;
#endif
        }

        private static PartTimeWorkDefinition FirstWork(GameDataRegistry registry)
        {
            if (registry == null || registry.PartTimeWorks == null)
            {
                return null;
            }

            for (int i = 0; i < registry.PartTimeWorks.Length; i++)
            {
                if (registry.PartTimeWorks[i] != null)
                {
                    return registry.PartTimeWorks[i];
                }
            }

            return null;
        }

        private static void EnsureWorkPoint(Scene scene, PartTimeWorkDefinition work)
        {
            var existing = GameObject.Find(WorkPointName);
            var go = existing != null ? existing : new GameObject(WorkPointName);
            if (existing == null)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = new Vector3(4.0f, 2.4f, 0f);

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.9f, 0.55f, 0.18f, 1f));
            }
            renderer.sortingOrder = 48;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.9f, 0.9f);

            var interactor = go.GetComponent<PartTimeWorkInteractor>();
            if (interactor == null)
            {
                interactor = go.AddComponent<PartTimeWorkInteractor>();
            }
            interactor.Bind(work);
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    bool border = x == 0 || y == 0 || x == 7 || y == 7;
                    texture.SetPixel(x, y, border ? Color.black : color);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
