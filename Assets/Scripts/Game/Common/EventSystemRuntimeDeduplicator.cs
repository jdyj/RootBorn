using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Common
{
    public static class EventSystemRuntimeDeduplicator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RemoveDuplicates();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureActiveScene()
        {
            RemoveDuplicates();
            CoroutineHost.Run(RemoveDuplicatesAfterFrame());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RemoveDuplicates();
            CoroutineHost.Run(RemoveDuplicatesAfterFrame());
        }

        private static IEnumerator RemoveDuplicatesAfterFrame()
        {
            yield return null;
            RemoveDuplicates();
        }

        private static void RemoveDuplicates()
        {
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (systems.Length == 0)
            {
                CreateEventSystem();
                return;
            }

            if (systems.Length == 1)
            {
                EnsureUsable(systems[0]);
                return;
            }

            EventSystem keep = EventSystem.current != null ? EventSystem.current : null;
            if (keep == null)
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] != null && systems[i].gameObject.activeInHierarchy)
                    {
                        keep = systems[i];
                        break;
                    }
                }
            }
            if (keep == null)
            {
                keep = systems[0];
            }

            EnsureUsable(keep);

            for (int i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                if (system == null || system == keep)
                {
                    continue;
                }

                DestroyEventSystem(system.gameObject);
            }
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            UiInputModuleInstaller.AddPreferredInputModule(go);
            MoveToActiveScene(go);
        }

        private static void EnsureUsable(EventSystem eventSystem)
        {
            if (eventSystem == null)
            {
                return;
            }

            if (!eventSystem.gameObject.activeSelf)
            {
                eventSystem.gameObject.SetActive(true);
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                UiInputModuleInstaller.AddPreferredInputModule(eventSystem.gameObject);
            }

            MoveToActiveScene(eventSystem.gameObject);
        }

        private static void MoveToActiveScene(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded && go.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
        }

        private static void DestroyEventSystem(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Object.DestroyImmediate(go);
        }

        private sealed class CoroutineHost : MonoBehaviour
        {
            private static CoroutineHost s_instance;

            public static void Run(IEnumerator routine)
            {
                if (!Application.isPlaying)
                {
                    return;
                }

                if (s_instance == null)
                {
                    var go = new GameObject("[EventSystemRuntimeDeduplicator]");
                    Object.DontDestroyOnLoad(go);
                    s_instance = go.AddComponent<CoroutineHost>();
                }

                s_instance.StartCoroutine(routine);
            }
        }
    }
}
