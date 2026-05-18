using System.Collections;
using Rootborn.Game.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Rootborn.Tests.PlayMode
{
    internal static class InputSystemUiModulePlayModeTestGuard
    {
        private static bool s_installed;
        private static EventSystem s_eventSystem;

        public static void InstallForCurrentTest()
        {
            UiInputModuleInstaller.PreferPassiveInputModule = true;
            EnsurePassiveEventSystemSurvivesSceneLoads();
            RemoveDuplicateEventSystems();
            if (s_installed)
            {
                ReplaceInputModules();
                return;
            }

            s_installed = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ReplaceInputModules();
        }

        public static void UninstallForCurrentTest()
        {
            if (!s_installed)
            {
                UiInputModuleInstaller.PreferPassiveInputModule = false;
                return;
            }

            s_installed = false;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ReplaceInputModules();
            UiInputModuleInstaller.PreferPassiveInputModule = false;
        }

        public static void UseRealInputSystemForCurrentTest()
        {
            s_installed = false;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UiInputModuleInstaller.PreferPassiveInputModule = false;
            DestroyGuardEventSystems();
            s_eventSystem = null;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsurePassiveEventSystemSurvivesSceneLoads();
            RemoveDuplicateEventSystems();
            ReplaceInputModules();
            CoroutineHost.Run(ReplaceInputModulesAfterFrame());
        }

        private static IEnumerator ReplaceInputModulesAfterFrame()
        {
            yield return null;
            EnsurePassiveEventSystemSurvivesSceneLoads();
            RemoveDuplicateEventSystems();
            ReplaceInputModules();
        }

        private static void EnsurePassiveEventSystemSurvivesSceneLoads()
        {
            if (s_eventSystem == null)
            {
                s_eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            }

            if (s_eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(PassiveInputModule));
                Object.DontDestroyOnLoad(go);
                s_eventSystem = go.GetComponent<EventSystem>();
                return;
            }

            if (!s_eventSystem.gameObject.activeSelf)
            {
                s_eventSystem.gameObject.SetActive(true);
            }

            if (s_eventSystem.GetComponent<PassiveInputModule>() == null)
            {
                s_eventSystem.gameObject.AddComponent<PassiveInputModule>();
            }

            Object.DontDestroyOnLoad(s_eventSystem.gameObject);
        }

        private static void ReplaceInputModules()
        {
            RemoveDuplicateEventSystems();

            var modules = Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                if (module == null)
                {
                    continue;
                }

                var go = module.gameObject;
                module.enabled = false;
                if (go.GetComponent<PassiveInputModule>() == null)
                {
                    go.AddComponent<PassiveInputModule>();
                }

                Object.DestroyImmediate(module);
            }

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                if (eventSystems[i] != null)
                {
                    eventSystems[i].UpdateModules();
                }
            }
        }

        private static void RemoveDuplicateEventSystems()
        {
            if (s_eventSystem == null)
            {
                return;
            }

            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                var eventSystem = eventSystems[i];
                if (eventSystem == null || eventSystem == s_eventSystem)
                {
                    continue;
                }

                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        private static void DestroyGuardEventSystems()
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = eventSystems.Length - 1; i >= 0; i--)
            {
                var eventSystem = eventSystems[i];
                if (eventSystem == null)
                {
                    continue;
                }

                if (eventSystem.GetComponent<PassiveInputModule>() != null)
                {
                    Object.DestroyImmediate(eventSystem.gameObject);
                }
            }
        }

        private sealed class CoroutineHost : MonoBehaviour
        {
            private static CoroutineHost s_instance;

            public static void Run(IEnumerator routine)
            {
                if (s_instance == null)
                {
                    var go = new GameObject("[InputSystemUiModulePlayModeTestGuard]");
                    Object.DontDestroyOnLoad(go);
                    s_instance = go.AddComponent<CoroutineHost>();
                }

                s_instance.StartCoroutine(routine);
            }
        }
    }
}
