using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Bootstrap
{
    public static class TownTilemapFallbackInstaller
    {
        private const string TownSceneName = "Town";
        private const string GridName = "Grid";
        private const string GroundTilemapName = "GroundTilemap";
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
            DisableRuntimeCue(scene, "TownApartment");
            DisableRuntimeCue(scene, "TownStreet");
            DisableRuntimeCue(scene, "TownShop");
            DisableRuntimeCue(scene, "TownCommunityBoard");
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

            renderer.enabled = true;
            renderer.sortingOrder = sortingOrder;
        }

        private static void DisableRuntimeCue(Scene scene, string name)
        {
            var go = FindRoot(scene, name);
            if (go == null)
            {
                return;
            }

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
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