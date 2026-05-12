using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    public static class WorldNpcAutoFiller
    {
        private const string FarmSceneName = "Farm";
        private const string TownSceneName = "Town";
        private const string NpcRootName = "[NPCs]";
        private const string GuideNpcName = "GuideNpc";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SupportsNpcAutofill(scene.name))
            {
                return;
            }

            StartRunner(scene);
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (SupportsNpcAutofill(scene.name))
            {
                StartRunner(scene);
            }
        }

        private static bool SupportsNpcAutofill(string sceneName)
        {
            return sceneName == FarmSceneName || sceneName == TownSceneName;
        }

        private static void StartRunner(Scene scene)
        {
            if (FindRoot(scene, "[WorldNpcAutoFiller]") != null)
            {
                return;
            }

            var go = new GameObject("[WorldNpcAutoFiller]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<WorldNpcAutoFillRunner>();
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

        private sealed class WorldNpcAutoFillRunner : MonoBehaviour
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

                if (registry == null || registry.Npcs == null || registry.Npcs.Length == 0 || registry.Npcs[0] == null)
                {
                    yield break;
                }

                EnsureNpc(registry);
            }

            private static void EnsureNpc(GameDataRegistry registry)
            {
                var scene = SceneManager.GetActiveScene();
                var root = FindRoot(scene, NpcRootName);
                if (root == null)
                {
                    root = new GameObject(NpcRootName);
                    SceneManager.MoveGameObjectToScene(root, scene);
                }

                var npcDefinition = registry.Npcs[0];
                var npcGo = FindChild(root.transform, GuideNpcName);
                if (npcGo == null)
                {
                    npcGo = new GameObject(GuideNpcName);
                    npcGo.transform.SetParent(root.transform, false);
                    npcGo.transform.position = scene.name == TownSceneName ? new Vector3(2f, 0f, 0f) : new Vector3(4f, 3f, 0f);
                }

                var renderer = npcGo.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    renderer = npcGo.AddComponent<SpriteRenderer>();
                }
                if (renderer.sprite == null || string.IsNullOrEmpty(renderer.sprite.texture.name))
                {
                    renderer.sprite = CreateWorldSprite(npcDefinition);
                }
                renderer.enabled = renderer.sprite != null;
                renderer.sortingOrder = 60;

                var collider = npcGo.GetComponent<CircleCollider2D>();
                if (collider == null)
                {
                    collider = npcGo.AddComponent<CircleCollider2D>();
                }
                collider.radius = 0.65f;
                collider.isTrigger = true;

                var interactor = npcGo.GetComponent<NpcInteractor>();
                if (interactor == null)
                {
                    interactor = npcGo.AddComponent<NpcInteractor>();
                }
                interactor.Bind(npcDefinition);

                var provider = npcGo.GetComponent<QuestProvider>();
                if (provider == null)
                {
                    provider = npcGo.AddComponent<QuestProvider>();
                }
                provider.Bind(registry.Quests);
            }

            private static GameObject FindChild(Transform root, string name)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i).gameObject;
                    if (child.name == name)
                    {
                        return child;
                    }
                }

                return null;
            }

            private static Sprite CreateWorldSprite(NpcDefinition definition)
            {
                if (definition == null || definition.WorldTexture == null)
                {
                    return null;
                }

                Rect rect = definition.WorldSpriteRect;
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    rect = new Rect(0f, 0f, 16f, 16f);
                }

                float ppu = definition.WorldSpritePixelsPerUnit > 0f ? definition.WorldSpritePixelsPerUnit : 16f;
                var sprite = Sprite.Create(definition.WorldTexture, rect, new Vector2(0.5f, 0.5f), ppu);
                sprite.name = definition.WorldTexture.name + "_world";
                return sprite;
            }
        }
    }
}
