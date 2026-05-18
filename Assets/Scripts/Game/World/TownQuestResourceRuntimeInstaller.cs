using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    public static class TownQuestResourceRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownQuestResourceRuntimeInstaller]";
        private const string RootName = "[TownQuestResources]";

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
                StartRunner(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                StartRunner(scene);
            }
        }

        private static void StartRunner(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                GameDataRegistry registry = null;
                float elapsed = 0f;
                while (registry == null && elapsed < 5f)
                {
                    registry = Managers.Managers.Data != null ? Managers.Managers.Data.Registry : null;
                    if (registry == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                if (registry == null)
                {
                    yield break;
                }

                EnsureQuestResources(SceneManager.GetActiveScene(), registry);
            }
        }

        private static void EnsureQuestResources(Scene scene, GameDataRegistry registry)
        {
            var target = ResolveFirstGatherObjectiveResource(registry);
            if (target == null)
            {
                return;
            }

            var root = FindRoot(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            for (int i = 0; i < 3; i++)
            {
                string nodeName = "QuestResource_" + i.ToString("00");
                if (FindChild(root.transform, nodeName) != null)
                {
                    continue;
                }

                SpawnNode(root.transform, target, new Vector3(14f + i * 1.25f, -1.25f, 0f), nodeName);
            }
        }

        private static ResourceNodeDefinition ResolveFirstGatherObjectiveResource(GameDataRegistry registry)
        {
            if (registry.Quests == null)
            {
                return null;
            }

            for (int i = 0; i < registry.Quests.Length; i++)
            {
                var quest = registry.Quests[i];
                if (quest == null || quest.Objectives == null)
                {
                    continue;
                }

                for (int j = 0; j < quest.Objectives.Length; j++)
                {
                    if (quest.Objectives[j] is GatherQuestObjective gatherObjective && gatherObjective.TargetResource != null)
                    {
                        return gatherObjective.TargetResource;
                    }
                }
            }

            return null;
        }

        private static void SpawnNode(Transform parent, ResourceNodeDefinition definition, Vector3 position, string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = definition.Sprite != null ? definition.Sprite : MakeFallbackSprite();
            renderer.sortingOrder = 35;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            collider.isTrigger = true;

            var node = go.AddComponent<ResourceNode>();
            node.BindForRuntime(definition, renderer);
        }

        private static Sprite MakeFallbackSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool trunk = x >= 6 && x <= 9 && y < 8;
                    bool leaf = (x - 7.5f) * (x - 7.5f) + (y - 10f) * (y - 10f) < 30f;
                    tex.SetPixel(x, y, trunk ? new Color(0.42f, 0.25f, 0.12f, 1f) : leaf ? new Color(0.22f, 0.55f, 0.28f, 1f) : Color.clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
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

        private static Transform FindChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
