using System.Collections;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    public static class ModernUiPanelAutoInstaller
    {
        private const string BootSceneName = "Boot";
        private const string MainMenuSceneName = "MainMenu";
        private const string RunnerName = "[ModernUiPanelAutoInstaller]";
        private const string InventoryPanelName = "ModernInventoryPanel";
        private const string StatusPanelName = "ModernStatusPanel";
        private const string SettingsPanelName = "ModernSettingsPanel";

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

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 1000);

            var host = canvas.gameObject;
            var inventoryPanel = EnsurePanelHost<ModernUiInventoryPanel>(canvas.transform, InventoryPanelName);
            var statusPanel = EnsurePanelHost<ModernUiStatusPanel>(canvas.transform, StatusPanelName);
            var settingsPanel = EnsurePanelHost<SettingsPanel>(canvas.transform, SettingsPanelName);

            var router = host.GetComponent<ModernUiPanelInputRouter>();
            if (router == null)
            {
                router = host.AddComponent<ModernUiPanelInputRouter>();
            }

            if (playerInventory != null)
            {
                inventoryPanel.Bind(playerInventory);
            }

            router.Bind(inventoryPanel, statusPanel, settingsPanel);
            inventoryPanel.Hide();
            statusPanel.Hide();
            settingsPanel.Hide();
            host.SetActive(true);
        }

        private static T EnsurePanelHost<T>(Transform canvasTransform, string panelName) where T : Component
        {
            var child = canvasTransform.Find(panelName);
            GameObject panelGo;
            if (child == null)
            {
                panelGo = new GameObject(panelName, typeof(RectTransform));
                panelGo.transform.SetParent(canvasTransform, false);
            }
            else
            {
                panelGo = child.gameObject;
            }

            var rect = (RectTransform)panelGo.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = typeof(T) == typeof(ModernUiInventoryPanel)
                ? new Vector2(520f, 420f)
                : typeof(T) == typeof(SettingsPanel)
                    ? new Vector2(660f, 510f)
                    : new Vector2(440f, 320f);
            rect.SetAsLastSibling();

            var panel = panelGo.GetComponent<T>();
            if (panel == null)
            {
                panel = panelGo.AddComponent<T>();
            }

            return panel;
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
            return scene.IsValid() && scene.isLoaded && scene.name != BootSceneName && scene.name != MainMenuSceneName;
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
                var bootstrap = Rootborn.Game.Managers.Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted)
                {
                    yield return null;
                }

                if (bootstrap.IsFaulted)
                {
                    Debug.LogException(bootstrap.Exception);
                    yield break;
                }

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
                        playerInventory = FindPlayerInventoryInScene(scene);
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
