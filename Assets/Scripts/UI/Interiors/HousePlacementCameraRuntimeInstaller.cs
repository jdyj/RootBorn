using System.Collections;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.UI.Interiors
{
    public sealed class HousePlacementCameraRuntimeInstaller : MonoBehaviour
    {
        private const string InstallerName = "[HousePlacementCameraRuntimeInstaller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterInitialSceneLoad()
        {
            EnsureForScene(SceneManager.GetActiveScene());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureForScene(Scene scene)
        {
            if (scene.name != "House")
            {
                return;
            }

            if (FindFirstObjectByType<HousePlacementCameraRuntimeInstaller>() != null)
            {
                return;
            }

            var go = new GameObject(InstallerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<HousePlacementCameraRuntimeInstaller>();
        }

        private IEnumerator Start()
        {
            yield return null;
            ConfigureCamera();
            yield return null;
            ConfigureCamera();
        }

        private void ConfigureCamera()
        {
            InitializeForScene(gameObject.scene);
        }

        public static void InitializeForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "House") return;
            var panel = FindInScene<InteriorFurniturePlacementPanel>(scene);
            bool active = AuthoredWorldScene.Find(scene) == null ||
                (panel != null && panel.gameObject.activeInHierarchy);
            SetPlacementMode(scene, active);
        }

        public static void SetPlacementMode(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "House") return;
            var camera = FindInScene<Camera>(scene);
            if (camera == null) return;
            var controller = camera.GetComponent<HousePlacementCameraController>();
            if (!active)
            {
                if (controller != null) controller.EndPlacementControl();
                return;
            }
            if (!camera.orthographic) camera.orthographic = true;
            if (controller == null) controller = camera.gameObject.AddComponent<HousePlacementCameraController>();
            controller.Configure(camera, ResolvePlacementBounds(scene));
            controller.BeginPlacementControl();
            controller.FullView();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static Bounds ResolvePlacementBounds(Scene scene)
        {
            foreach (var name in new[] { "HouseGroundTilemap", "HouseWallTilemap" })
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var tilemap in root.GetComponentsInChildren<Tilemap>(true))
                    {
                        if (tilemap.name == name && tilemap.cellBounds.size.x > 0 && tilemap.cellBounds.size.y > 0)
                            return TilemapWorldBounds(tilemap);
                    }
                }
            }
            return new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));
        }

        private static Bounds TilemapWorldBounds(Tilemap tilemap)
        {
            var local = tilemap.localBounds;
            var center = tilemap.transform.TransformPoint(local.center);
            var extents = Vector3.Scale(local.extents, tilemap.transform.lossyScale);
            extents.x = Mathf.Max(extents.x, 0.5f);
            extents.y = Mathf.Max(extents.y, 0.5f);
            extents.z = Mathf.Max(extents.z, 0.5f);
            return new Bounds(center, extents * 2f);
        }
    }
}
