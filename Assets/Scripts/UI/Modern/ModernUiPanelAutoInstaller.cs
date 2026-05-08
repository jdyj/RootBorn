using System.Collections;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    public static class ModernUiPanelAutoInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[ModernUiPanelAutoInstaller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        public static void InstallOnCanvas(Canvas canvas, PlayerInventory playerInventory)
        {
            if (canvas == null)
            {
                return;
            }

            var host = canvas.gameObject;
            var inventoryPanel = host.GetComponent<ModernUiInventoryPanel>();
            if (inventoryPanel == null)
            {
                inventoryPanel = host.AddComponent<ModernUiInventoryPanel>();
            }

            var statusPanel = host.GetComponent<ModernUiStatusPanel>();
            if (statusPanel == null)
            {
                statusPanel = host.AddComponent<ModernUiStatusPanel>();
            }

            var router = host.GetComponent<ModernUiPanelInputRouter>();
            if (router == null)
            {
                router = host.AddComponent<ModernUiPanelInputRouter>();
            }

            if (playerInventory != null)
            {
                inventoryPanel.Bind(playerInventory);
            }

            router.Bind(inventoryPanel, statusPanel);
            inventoryPanel.Hide();
            statusPanel.Hide();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShouldInstallForScene(scene))
            {
                StartRunner(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (ShouldInstallForScene(scene))
            {
                StartRunner(scene);
            }
        }

        private static bool ShouldInstallForScene(Scene scene)
        {
            return scene.name == TownSceneName;
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

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                Canvas canvas = null;
                PlayerInventory playerInventory = null;
                float elapsed = 0f;

                while ((canvas == null || playerInventory == null) && elapsed < 15f)
                {
                    var scene = SceneManager.GetActiveScene();
                    if (canvas == null)
                    {
                        canvas = FindComponentInScene<Canvas>(scene);
                    }

                    if (playerInventory == null)
                    {
                        playerInventory = FindComponentInScene<PlayerInventory>(scene);
                    }

                    if (canvas == null || playerInventory == null)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                InstallOnCanvas(canvas, playerInventory);
            }
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindComponentInChildren<T>(roots[i].transform);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponentInChildren<T>(Transform root) where T : Component
        {
            if (root.TryGetComponent<T>(out var component))
            {
                return component;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindComponentInChildren<T>(root.GetChild(i));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}