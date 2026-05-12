using System.Reflection;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Game.Bootstrap
{
    public static class FarmModernUiPanelBridge
    {
        private const string FarmSceneName = "Farm";
        private const string RunnerName = "[FarmModernUiPanelBridge]";

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
                StartRunner(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == FarmSceneName)
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
            go.AddComponent<FarmModernUiPanelBridgeRunner>();
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
    }

    public sealed class FarmModernUiPanelBridgeRunner : MonoBehaviour
    {
        private const string FarmSceneName = "Farm";
        private const string InstallerTypeName = "Rootborn.UI.Modern.ModernUiPanelAutoInstaller, Rootborn.UI";
        private const string InstallMethodName = "InstallOnCanvas";

        private bool _installed;

        private void Update()
        {
            if (_installed || SceneManager.GetActiveScene().name != FarmSceneName)
            {
                return;
            }

            var scene = SceneManager.GetActiveScene();
            var canvas = FindComponentInScene<Canvas>(scene);
            var playerInventory = FindPlayerInventoryInScene(scene);
            if (canvas == null || playerInventory == null)
            {
                return;
            }

            if (InstallModernPanels(canvas, playerInventory))
            {
                _installed = true;
            }
        }

        private static bool InstallModernPanels(Canvas canvas, PlayerInventory playerInventory)
        {
            var installerType = System.Type.GetType(InstallerTypeName);
            if (installerType == null)
            {
                Debug.LogWarning("[ROOTBORN/UI] Modern UI panel installer type not found.");
                return false;
            }

            var method = installerType.GetMethod(InstallMethodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                Debug.LogWarning("[ROOTBORN/UI] Modern UI panel install method not found.");
                return false;
            }

            method.Invoke(null, new object[] { canvas, playerInventory });
            return true;
        }

        private static PlayerInventory FindPlayerInventoryInScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindPlayerInventoryInChildren(roots[i].transform, true);
                if (match != null)
                {
                    return match;
                }
            }

            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindPlayerInventoryInChildren(roots[i].transform, false);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static PlayerInventory FindPlayerInventoryInChildren(Transform root, bool requirePlayerName)
        {
            if (!root.gameObject.activeInHierarchy)
            {
                return null;
            }

            if ((!requirePlayerName || root.name == "Player") && root.TryGetComponent<PlayerInventory>(out var inventory))
            {
                return inventory;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindPlayerInventoryInChildren(root.GetChild(i), requirePlayerName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
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
