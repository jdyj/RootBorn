using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.StudentLife
{
    public static class TownExplorationInteractionRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownExplorationInteractionRuntimeInstaller]";
        private const string PointName = "TownExplorationInteractionPoint";

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
            if (scene.name == TownSceneName) EnsureScene(scene);
        }

        public static void EnsureScene(Scene scene)
        {
            if (scene.name != TownSceneName || FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        public static ExplorationInteractionDefinition ResolveFirstInteractionForTests(GameDataRegistry registry)
        {
            var interactions = registry.GetExplorationInteractions();
            if (interactions == null) return null;
            for (int i = 0; i < interactions.Length; i++)
            {
                var interaction = interactions[i];
                if (interaction != null && interaction.Choices != null && interaction.Choices.Count >= 2) return interaction;
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
                ExplorationInteractionDefinition interaction = null;
                float elapsed = 0f;
                while (elapsed < 8f)
                {
                    interaction = ResolveFirstInteraction();
                    if (FindRoot(scene, "Player") != null && interaction != null) break;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                EnsureInteractionPoint(scene, interaction);
            }
        }

        private static ExplorationInteractionDefinition ResolveFirstInteraction()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            var interaction = ResolveFirstInteractionForTests(registry);
            if (interaction != null) return interaction;
#if UNITY_EDITOR
            registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            return ResolveFirstInteractionForTests(registry);
#else
            return null;
#endif
        }

        private static void EnsureInteractionPoint(Scene scene, ExplorationInteractionDefinition interaction)
        {
            if (interaction == null) return;
            var go = FindByName(scene, PointName);
            if (go == null)
            {
                go = new GameObject(PointName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = new Vector3(interaction.WorldPosition.x, interaction.WorldPosition.y, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null) renderer.sprite = CreateMarkerSprite(new Color(0.35f, 0.82f, 0.55f, 1f));
            renderer.sortingOrder = 56;
            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null) collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            float size = Mathf.Max(0.65f, interaction.InteractionRadius);
            collider.size = new Vector2(size, size);
            var interactor = go.GetComponent<ExplorationInteractionPointInteractor>();
            if (interactor == null) interactor = go.AddComponent<ExplorationInteractionPointInteractor>();
            interactor.Bind(interaction);
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "TownExplorationInteractionMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
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
