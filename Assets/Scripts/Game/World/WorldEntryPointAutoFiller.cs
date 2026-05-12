using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    public static class WorldEntryPointAutoFiller
    {
        private const string FarmSceneName = "Farm";
        private const string TownSceneName = "Town";
        private const string FarmPortalName = "FarmPortal";
        private const string TownReturnPortalName = "TownReturnPortal";
        private const string FarmSpawnName = "FarmSpawnPoint";
        private const string TownSpawnName = "TownSpawnPoint";
        private const string FarmSpawnId = "farm-entry";
        private const string TownSpawnId = "town-entry";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FarmSceneName)
            {
                EnsureFarmEntryPoints(scene);
            }
            else if (scene.name == TownSceneName)
            {
                EnsureTownEntryPoints(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == FarmSceneName)
            {
                EnsureFarmEntryPoints(scene);
            }
            else if (scene.name == TownSceneName)
            {
                EnsureTownEntryPoints(scene);
            }
        }

        private static void EnsureFarmEntryPoints(Scene scene)
        {
            EnsureSpawnPoint(scene, FarmSpawnName, FarmSpawnId, new Vector3(1f, 1f, 0f));
            EnsurePortal(scene, FarmPortalName, "Farm Gate", TownSceneName, TownSpawnId, new Vector3(27f, 10f, 0f));
            EnsureExistingPlayerRuntimeContext(scene);
        }

        private static void EnsureTownEntryPoints(Scene scene)
        {
            EnsureCamera();
            EnsureTownGround(scene);
            EnsureSpawnPoint(scene, TownSpawnName, TownSpawnId, new Vector3(0f, 0f, 0f));
            EnsurePortal(scene, TownReturnPortalName, "Town Gate", FarmSceneName, FarmSpawnId, new Vector3(-3f, 0f, 0f));
        }

        private static void EnsureExistingPlayerRuntimeContext(Scene scene)
        {
            var player = FindInScene(scene, "Player");
            if (player == null)
            {
                return;
            }

            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;

            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = player.AddComponent<PlayerInventory>();
            }
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

            if (registry == null || interactor.EquippedTool != null)
            {
                return;
            }

            var firstToolItem = FindFirstToolItem(registry);
            if (firstToolItem != null)
            {
                inventory.EquipTool(firstToolItem);
            }
        }

        private static ItemDefinition FindFirstToolItem(GameDataRegistry registry)
        {
            if (registry.Items == null)
            {
                return null;
            }

            for (int i = 0; i < registry.Items.Length; i++)
            {
                var item = registry.Items[i];
                if (item != null && item.Category == ItemCategory.Tool)
                {
                    return item;
                }
            }

            return null;
        }

        private static void EnsureSpawnPoint(Scene scene, string objectName, string spawnId, Vector3 position)
        {
            var spawn = FindInScene(scene, objectName);
            if (spawn == null)
            {
                spawn = new GameObject(objectName);
                SceneManager.MoveGameObjectToScene(spawn, scene);
            }

            spawn.transform.position = position;

            var spawnPoint = spawn.GetComponent<WorldSpawnPoint>();
            if (spawnPoint == null)
            {
                spawnPoint = spawn.AddComponent<WorldSpawnPoint>();
            }
            spawnPoint.Bind(spawnId);
        }

        private static void EnsurePortal(Scene scene, string objectName, string displayName, string destinationScene, string destinationSpawnId, Vector3 position)
        {
            var portal = FindInScene(scene, objectName);
            if (portal == null)
            {
                portal = new GameObject(objectName);
                SceneManager.MoveGameObjectToScene(portal, scene);
            }

            portal.transform.position = position;

            var renderer = portal.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = portal.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = MakePortalSprite();
            }
            renderer.sortingOrder = 50;

            var collider = portal.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = portal.AddComponent<CircleCollider2D>();
            }
            collider.radius = 0.75f;
            collider.isTrigger = true;

            var worldPortal = portal.GetComponent<WorldPortal>();
            if (worldPortal == null)
            {
                worldPortal = portal.AddComponent<WorldPortal>();
            }
            worldPortal.Bind(displayName, destinationScene, destinationSpawnId);
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                camera = go.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.30f, 0.36f, 0.42f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
        }

        private static void EnsureTownGround(Scene scene)
        {
            if (FindInScene(scene, "TownGround") != null)
            {
                return;
            }

            var ground = new GameObject("TownGround");
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(8f, 5f, 1f);
            var renderer = ground.AddComponent<SpriteRenderer>();
            renderer.sprite = MakeSolidSprite(new Color(0.33f, 0.33f, 0.36f, 1f));
            renderer.sortingOrder = -20;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindInChildren(roots[i].transform, name);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindInChildren(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindInChildren(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Sprite MakePortalSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 7.5f;
                    float dy = y - 7.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color color = dist < 5.5f ? new Color(0.25f, 0.85f, 0.95f, 1f) : Color.clear;
                    if (dist > 3.8f && dist < 6.8f)
                    {
                        color = new Color(0.1f, 0.35f, 0.8f, 1f);
                    }
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        private static Sprite MakeSolidSprite(Color color)
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int i = 0; i < size * size; i++)
            {
                tex.SetPixel(i % size, i / size, color);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
