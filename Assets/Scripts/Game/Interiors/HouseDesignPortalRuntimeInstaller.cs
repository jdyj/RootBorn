using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Interiors
{
    public sealed class HouseDesignPortalRuntimeInstaller : MonoBehaviour
    {
        private const string HouseSceneName = "House";
        private const string CompactSceneName = "House_Condominium_Design_2";
        private const string ApartmentSceneName = "House_Condominium_Design";
        private const string DesignEntrySpawnId = "house-design-entry";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureActiveScene()
        {
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureForScene(Scene scene)
        {
            if (scene.name == HouseSceneName)
            {
                EnsureHouseDesignPortals(scene);
                return;
            }

            if (scene.name == CompactSceneName || scene.name == ApartmentSceneName)
            {
                EnsureDesignSceneSpawn(scene);
            }
        }

        private static void EnsureHouseDesignPortals(Scene scene)
        {
            EnsurePortal(scene, "HouseDesignPortal_CondominiumDesign2", "Condominium Design 2", CompactSceneName, new Vector3(-5.5f, -4.2f, 0f));
            EnsurePortal(scene, "HouseDesignPortal_CondominiumDesign", "Condominium Design", ApartmentSceneName, new Vector3(5.5f, -4.2f, 0f));
        }

        private static void EnsureDesignSceneSpawn(Scene scene)
        {
            var spawn = FindInScene(scene, "HouseSpawnPoint");
            if (spawn == null)
            {
                spawn = new GameObject("HouseSpawnPoint");
                SceneManager.MoveGameObjectToScene(spawn, scene);
                spawn.transform.position = Vector3.zero;
            }

            var spawnPoint = spawn.GetComponent<WorldSpawnPoint>();
            if (spawnPoint == null)
            {
                spawnPoint = spawn.AddComponent<WorldSpawnPoint>();
            }

            spawnPoint.Bind(DesignEntrySpawnId);
        }

        private static void EnsurePortal(Scene scene, string objectName, string displayName, string destinationScene, Vector3 position)
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
                renderer.sprite = MakeDoorSprite();
            }
            renderer.sortingOrder = 220;

            var collider = portal.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = portal.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 1.4f);

            var worldPortal = portal.GetComponent<WorldPortal>();
            if (worldPortal == null)
            {
                worldPortal = portal.AddComponent<WorldPortal>();
            }
            worldPortal.Bind(displayName, destinationScene, DesignEntrySpawnId);
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindInChildren(roots[i].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindInChildren(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindInChildren(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Sprite MakeDoorSprite()
        {
            const int width = 16;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var edge = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                    var panel = x > 3 && x < 12 && y > 4 && y < 19;
                    var color = edge ? new Color(0.10f, 0.11f, 0.20f, 1f) : panel ? new Color(0.55f, 0.38f, 0.24f, 1f) : new Color(0.72f, 0.55f, 0.36f, 1f);
                    if (x == 12 && y == 11)
                    {
                        color = new Color(0.95f, 0.78f, 0.24f, 1f);
                    }
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 16f);
        }
    }
}
