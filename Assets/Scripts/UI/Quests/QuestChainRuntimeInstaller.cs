using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.World;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Quests
{
    public static class QuestChainRuntimeInstaller
    {
        private const string RunnerName = "[QuestChainRuntimeInstaller]";
        private static Runner s_activeRunner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => s_activeRunner = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static bool IsGameplayScene(Scene scene) => scene.name == "Town" || scene.name == "House" || AuthoredWorldScene.Find(scene) != null;
        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) { if (IsGameplayScene(scene)) EnsureScene(scene); }
        public static void EnsureForActiveScene() { var scene = SceneManager.GetActiveScene(); if (IsGameplayScene(scene)) EnsureScene(scene); }
        public static string BuildDayEndSummary() => s_activeRunner != null ? s_activeRunner.BuildDayEndSummary() : string.Empty;

        private static void EnsureScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects()) if (root.name == RunnerName) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private QuestChainDefinition[] _chains;
            private QuestChainLog _log;
            private QuestChainLogPanel _panel;
            private ObjectiveJournalRuntimeProvider _objectiveProvider;
            private StudentLifeProgressComponent _student;
            private QuestChainRuntimeService _service;

            private IEnumerator Start()
            {
                s_activeRunner = this;
                var bootstrap = Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted) yield return null;
                if (bootstrap.IsFaulted) { Debug.LogException(bootstrap.Exception); yield break; }
                var registry = ResolveRegistry();
                if (registry == null) yield break;
                _service = QuestChainRuntimeService.Ensure(registry);
                _chains = registry.QuestChains;
                Canvas canvas = null;
                float elapsed = 0f;
                while (elapsed < 10f)
                {
                    _student = FindInScene<StudentLifeProgressComponent>(gameObject.scene);
                    canvas = FindInScene<Canvas>(gameObject.scene);
                    if (_student != null && canvas != null) break;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (_student == null || canvas == null)
                {
                    Debug.LogWarning("[ROOTBORN] Quest journal binding needs the gameplay player and shared canvas.");
                    yield break;
                }
                _log = _service.GetLog(_student.EnsureProgress());
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, _student.GetComponent<PlayerInventory>());
                _objectiveProvider = canvas.GetComponent<ObjectiveJournalRuntimeProvider>();
                _panel = QuestChainLogPanel.EnsureInScene(canvas);
                _panel.Hide();
                _service.ProgressChanged += HandleProgressChanged;
                Refresh();
            }

            private void OnDestroy()
            {
                if (s_activeRunner == this) s_activeRunner = null;
                if (_service != null) _service.ProgressChanged -= HandleProgressChanged;
            }

            private void HandleProgressChanged(StudentLifeProgress student)
            {
                var current = _student != null ? _student.EnsureProgress() : null;
                if (current == null || student == null || current.SaveSlot != student.SaveSlot || current.PlayerId != student.PlayerId) return;
                _log = _service.GetLog(current);
                Refresh();
            }

            private void SaveAndRefresh()
            {
                Refresh();
            }

            private void Refresh()
            {
                var student = _student != null ? _student.EnsureProgress() : null;
                if (student == null || _log == null || _service == null) return;
                var rewards = _service.ResolveRewards(student);
                if (_panel != null)
                {
                    _panel.BindCommandExecutor(command => _service.ExecuteUserCommand(_student.EnsureProgress(), command));
                    _panel.Bind(_chains, _log, SaveAndRefresh, rewards);
                    _panel.Refresh();
                }
                if (_objectiveProvider != null) _objectiveProvider.RefreshNow();
            }

            public string BuildDayEndSummary()
            {
                if (_chains == null || _log == null) return string.Empty;
                foreach (var chain in _chains)
                {
                    if (chain == null || _log.GetState(chain) == QuestChainState.Available) continue;
                    var step = _log.GetCurrentStep(chain);
                    var objective = step?.Objectives != null && step.Objectives.Length > 0 ? step.Objectives[0] : null;
                    return QuestChainSummaryPresentation.Build(chain, _log.GetState(chain), objective, objective != null ? _log.GetObjectiveProgress(chain, 0) : 0);
                }
                return string.Empty;
            }
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

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
#if UNITY_EDITOR
            if (registry == null) registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
#endif
            return registry;
        }
    }
}
