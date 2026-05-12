using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Bootstrap
{
    public static class TownTilemapFallbackInstaller
    {
        private const string TownSceneName = "Town";
        private const string GridName = "Grid";
        private const string GroundTilemapName = "TownGroundTilemap";
        private const string DecorationTilemapName = "TownDecorationTilemap";

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
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            var grid = FindRoot(scene, GridName);
            if (grid == null)
            {
                grid = new GameObject(GridName, typeof(Grid));
                SceneManager.MoveGameObjectToScene(grid, scene);
            }
            else if (grid.GetComponent<Grid>() == null)
            {
                grid.AddComponent<Grid>();
            }

            EnsureTilemap(grid.transform, GroundTilemapName, 0);
            EnsureTilemap(grid.transform, DecorationTilemapName, 10);
            EnsureTownCue(scene, "TownApartment", new Vector3(-3f, 2f, 0f), new Color(0.48f, 0.58f, 0.72f, 1f));
            EnsureTownCue(scene, "TownStreet", new Vector3(0f, -1.75f, 0f), new Color(0.28f, 0.29f, 0.31f, 1f));
            EnsureTownCue(scene, "TownShop", new Vector3(3f, 1.25f, 0f), new Color(0.72f, 0.50f, 0.38f, 1f));
            EnsureTownCue(scene, "TownCommunityBoard", new Vector3(1.5f, -0.75f, 0f), new Color(0.58f, 0.36f, 0.18f, 1f));
        }

        private static void EnsureTilemap(Transform grid, string name, int sortingOrder)
        {
            var child = grid.Find(name);
            GameObject go = child != null ? child.gameObject : new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            if (go.GetComponent<Tilemap>() == null)
            {
                go.AddComponent<Tilemap>();
            }

            var renderer = go.GetComponent<TilemapRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<TilemapRenderer>();
            }

            renderer.sortingOrder = sortingOrder;
        }

        private static void EnsureTownCue(Scene scene, string name, Vector3 position, Color color)
        {
            var go = FindRoot(scene, name);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.position = position;
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }

            if (renderer.sprite == null)
            {
                renderer.sprite = CreateCueSprite(name, color);
            }

            renderer.sortingOrder = 5;
        }

        private static Sprite CreateCueSprite(string name, Color color)
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = name + "Sprite" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, border ? Color.black : color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
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