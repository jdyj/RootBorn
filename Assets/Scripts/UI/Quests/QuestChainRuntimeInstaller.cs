using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    public static class QuestChainRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[QuestChainRuntimeInstaller]";
        private const string CanvasName = "QuestChainCanvas";
        private const string OpenButtonName = "QuestChainLogOpenButton";
        private const string ProgressFileName = "quest-chain-progress.json";

        private static Runner s_activeRunner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName) EnsureScene(scene);
        }

        public static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName) EnsureScene(scene);
        }

        public static string BuildDayEndSummary()
        {
            return s_activeRunner != null ? s_activeRunner.BuildDayEndSummary() : string.Empty;
        }

        private static void EnsureScene(Scene scene)
        {
            if (scene.name != TownSceneName || FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private Scene _scene;
            private QuestChainDefinition[] _chains;
            private QuestChainLog _log;
            private QuestChainLogPanel _panel;
            private ObjectiveJournalPanel _journal;
            private TrackedObjectiveHud _trackedHud;
            private StudentLifeProgressComponent _student;
            private PlayerInventory _playerInventory;

            private IEnumerator Start()
            {
                s_activeRunner = this;
                var bootstrap = Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted) yield return null;
                if (bootstrap.IsFaulted)
                {
                    Debug.LogException(bootstrap.Exception);
                    yield break;
                }

                _scene = SceneManager.GetActiveScene();
                var registry = ResolveRegistry();
                _chains = registry != null && registry.QuestChains != null ? registry.QuestChains : new QuestChainDefinition[0];
                _log = new QuestChainLog(_chains);
                LoadProgress();

                var canvas = EnsureCanvas(_scene);
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, ResolvePlayerInventory());
                _journal = canvas.GetComponentInChildren<ObjectiveJournalPanel>(true);
                _trackedHud = canvas.GetComponentInChildren<TrackedObjectiveHud>(true);
                if (_journal != null) _journal.BindTrackedHud(_trackedHud);
                _panel = QuestChainLogPanel.EnsureInScene(canvas);
                _panel.Bind(_chains, _log, SaveAndRefresh, ResolveRewardContext());
                _panel.Hide();
                EvaluateAllStates();
                RefreshObjectiveJournal();
                EnsureOpenButton(_scene, canvas, _panel);

                LocationIdentityPanel.OnAnyActivityApplied -= HandleLocationActivityApplied;
                LocationIdentityPanel.OnAnyActivityApplied += HandleLocationActivityApplied;
            }

            private void OnDestroy()
            {
                if (s_activeRunner == this) s_activeRunner = null;
                LocationIdentityPanel.OnAnyActivityApplied -= HandleLocationActivityApplied;
            }

            private void HandleLocationActivityApplied(LocationActivityDefinition activity, string requestId)
            {
                if (_log == null || activity == null) return;
                EvaluateAllStates();
                _log.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, requestId, activity: activity));
                SaveAndRefresh();
            }

            private void SaveAndRefresh()
            {
                EvaluateAllStates();
                if (_panel != null) _panel.Bind(_chains, _log, SaveAndRefresh, ResolveRewardContext());
                SaveProgress();
                if (_panel != null) _panel.Refresh();
                RefreshObjectiveJournal();
            }

            public string BuildDayEndSummary()
            {
                if (_chains == null || _log == null || _chains.Length == 0) return string.Empty;
                for (int i = 0; i < _chains.Length; i++)
                {
                    var chain = _chains[i];
                    if (chain == null) continue;
                    var state = _log.GetState(chain);
                    if (state == QuestChainState.Available) continue;
                    return chain.Id + "\n" + state + "\n" + BuildObjectiveSummary(chain);
                }

                return string.Empty;
            }

            private void EvaluateAllStates()
            {
                if (_chains == null || _log == null) return;
                var context = new QuestRuntimeContext(null, null, null, null, ResolveStudentProgress());
                for (int i = 0; i < _chains.Length; i++)
                {
                    var chain = _chains[i];
                    if (chain == null) continue;
                    _log.EvaluateTerminalState(chain, context);
                    _log.EvaluateBlockState(chain, context);
                }
            }

            private void RefreshObjectiveJournal()
            {
                if (_chains == null || _log == null || _journal == null) return;
                var items = new ObjectiveJournalItem[_chains.Length];
                ObjectiveJournalItem? tracked = null;
                for (int i = 0; i < _chains.Length; i++)
                {
                    var chain = _chains[i];
                    var state = chain != null ? _log.GetState(chain) : QuestChainState.Available;
                    string objectiveProgress = chain != null ? BuildObjectiveSummary(chain) : string.Empty;
                    string progress = string.IsNullOrEmpty(objectiveProgress) ? state.ToString() : state + " | " + objectiveProgress;
                    bool isTracked = chain != null && _log.IsTracked(chain);
                    var item = new ObjectiveJournalItem(
                        chain != null ? chain.Id : "questchain.empty",
                        "Quests",
                        chain != null ? chain.Id : "Quest Chain",
                        chain != null ? chain.RelatedCareerInterestId : string.Empty,
                        state.ToString(),
                        progress,
                        state == QuestChainState.Completed ? "Claim rewards or review the next route" : "Open quest details or visit the related location",
                        isTracked,
                        chain != null ? chain.SortPriority : 0);
                    items[i] = item;
                    if (isTracked || (tracked == null && state == QuestChainState.Completed)) tracked = item;
                }

                _journal.SetItems("Quests", items);
                if (_trackedHud == null) return;
                if (tracked.HasValue) _trackedHud.Show(tracked.Value);
                else _trackedHud.Hide();
            }

            private RewardRuntimeContext ResolveRewardContext()
            {
                var inventory = ResolvePlayerInventory();
                return new RewardRuntimeContext(null, inventory != null ? inventory.Inventory : null, null, null, ResolveStudentProgress());
            }

            private PlayerInventory ResolvePlayerInventory()
            {
                if (_playerInventory == null)
                {
                    var player = FindByName(_scene, "Player");
                    _playerInventory = player != null ? player.GetComponent<PlayerInventory>() : null;
                }

                return _playerInventory;
            }

            private StudentLifeProgress ResolveStudentProgress()
            {
                if (_student == null)
                {
                    var player = FindByName(_scene, "Player");
                    _student = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
                }

                return _student != null ? _student.EnsureProgress() : null;
            }

            private string BuildObjectiveSummary(QuestChainDefinition chain)
            {
                var step = chain != null && chain.Steps != null && chain.Steps.Length > 0 ? chain.Steps[0] : null;
                var objective = step != null && step.Objectives != null && step.Objectives.Length > 0 ? step.Objectives[0] : null;
                if (objective == null) return string.Empty;
                return objective.DisplayKey + " " + _log.GetObjectiveProgress(chain, 0) + " / " + objective.RequiredCount;
            }

            private void LoadProgress()
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || _log == null) return;
                string json = new SaveService(metadata.SlotId).ReadJson(ProgressFileName);
                if (string.IsNullOrEmpty(json)) return;
                var saveData = JsonUtility.FromJson<QuestChainLogSaveData>(json);
                _log.LoadFromSaveData(saveData);
            }

            private void SaveProgress()
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || _log == null) return;
                new SaveService(metadata.SlotId).WriteJson(ProgressFileName, JsonUtility.ToJson(_log.ToSaveData(), true));
            }
        }

        private static void EnsureOpenButton(Scene scene, Canvas canvas, QuestChainLogPanel panel)
        {
            var existing = FindByName(scene, OpenButtonName);
            if (existing != null) UnityEngine.Object.Destroy(existing);

            var go = new GameObject(OpenButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(180f, 42f);
            go.GetComponent<Image>().color = new Color(0.72f, 0.58f, 0.32f, 1f);
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => panel.Open());
            MakeText(rect, "Label", "Quest Chains", Vector2.zero, rect.sizeDelta, 15, TextAnchor.MiddleCenter);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var existing = FindComponentInScene<Canvas>(scene, true);
            if (existing != null) return existing;

            var canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            if (registry != null && registry.QuestChains != null && registry.QuestChains.Length > 0) return registry;
#if UNITY_EDITOR
            var editorRegistry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            if (editorRegistry != null) return editorRegistry;
#endif
            return registry;
        }

        private static T FindComponentInScene<T>(Scene scene, bool includeInactive) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var component = roots[i].GetComponentInChildren<T>(includeInactive);
                if (component != null) return component;
            }
            return null;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null) return match;
            }
            return null;
        }

        private static GameObject FindByName(Transform root, string objectName)
        {
            if (root.name == objectName) return root.gameObject;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null) return match;
            }
            return null;
        }
    }
}
