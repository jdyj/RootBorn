using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public static class FarmWaterSurfaceInstaller
    {
        private const string FarmSceneName = "Farm";
        private const string ObjectName = "[WaterSurface]";

        private static GameObject s_waterSurface;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureWaterSurfaceForActiveScene();
        }

        public static void EnsureWaterSurfaceForActiveScene()
        {
            if (SceneManager.GetActiveScene().name != FarmSceneName)
            {
                return;
            }

            if (s_waterSurface == null)
            {
                s_waterSurface = new GameObject(ObjectName);
            }

            s_waterSurface.transform.position = new Vector3(1f, 0.25f, 0f);
            s_waterSurface.transform.localScale = Vector3.one;

            var collider = s_waterSurface.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = s_waterSurface.AddComponent<BoxCollider2D>();
            }
            collider.size = new Vector2(4f, 1f);
            collider.offset = Vector2.zero;
            collider.isTrigger = true;

            var zone = s_waterSurface.GetComponent<SurfaceTagZone>();
            if (zone == null)
            {
                zone = s_waterSurface.AddComponent<SurfaceTagZone>();
            }
            zone.SetSurfaceForRuntime("Water");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FarmSceneName)
            {
                s_waterSurface = null;
                EnsureWaterSurfaceForActiveScene();
            }
        }
    }
}
