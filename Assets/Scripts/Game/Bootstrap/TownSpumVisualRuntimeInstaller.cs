using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Bootstrap
{
    public sealed class TownSpumVisualRuntimeInstaller : MonoBehaviour
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownSpumVisualRuntimeInstaller]";
        private const string SpumVisualKind = "spum";
        private static readonly List<SpumCharacterVisualView> CandidateViews = new List<SpumCharacterVisualView>(4);

        public static bool TryBindExistingVisual(GameObject player)
        {
            return HasSpumSnapshot() && BindSpumVisual(player);
        }

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
                EnsureRunner();
            }
        }

        private static void EnsureForActiveScene()
        {
            if (SceneManager.GetActiveScene().name == TownSceneName)
            {
                EnsureRunner();
            }
        }

        private static void EnsureRunner()
        {
            if (!HasSpumSnapshot())
                return;

            var existing = GameObject.Find(RunnerName);
            if (existing != null)
                return;

            var runner = new GameObject(RunnerName);
            runner.AddComponent<TownSpumVisualRuntimeInstaller>();
        }

        private void Start()
        {
            StartCoroutine(BindWhenPlayerExists());
        }

        private IEnumerator BindWhenPlayerExists()
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + 10f;
            var retryDelay = new WaitForSecondsRealtime(0.1f);
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (!HasSpumSnapshot())
                    yield break;

                GameObject player = GameObject.Find("Player");
                if (player != null && BindSpumVisual(player))
                {
                    yield break;
                }

                yield return retryDelay;
            }
        }

        private static bool BindSpumVisual(GameObject player)
        {
            if (player == null)
                return false;

            CandidateViews.Clear();
            player.GetComponentsInChildren(false, CandidateViews);
            SpumCharacterVisualView selected = null;
            for (int i = 0; i < CandidateViews.Count; i++)
            {
                var candidate = CandidateViews[i];
                if (candidate != null && candidate.HasRenderableVisual)
                {
                    selected = candidate;
                    break;
                }
            }
            CandidateViews.Clear();

            // Snapshot intent alone never creates a view around another backend's renderers.
            if (selected == null)
                return false;

            var adapter = player.GetComponent<PlayerCharacterVisualAdapter>();
            if (adapter == null)
                adapter = player.AddComponent<PlayerCharacterVisualAdapter>();

            adapter.ConfigureForTests(selected);
            return true;
        }

        private static bool HasSpumSnapshot()
        {
            var snapshot = ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.CharacterAppearanceSnapshot : null;
            return snapshot != null && snapshot.VisualKind == SpumVisualKind;
        }
    }
}
