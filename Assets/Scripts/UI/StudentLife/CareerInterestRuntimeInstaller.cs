using Rootborn.Game.Localization;
using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.World;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class CareerInterestRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[CareerInterestRuntimeInstaller]";
        private const string ButtonName = "CareerInterestOpenButton";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if ((scene.name == TownSceneName || AuthoredWorldScene.Find(scene) != null)) EnsureScene(scene);
        }

        public static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if ((scene.name == TownSceneName || AuthoredWorldScene.Find(scene) != null)) EnsureScene(scene);
        }

        private static void EnsureScene(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private GameDataRegistry _registry;
            private CareerInterestProgressComponent _interestComponent;
            private CareerCandidateProgressComponent _candidateComponent;
            private StudentLifeProgressComponent _studentComponent;
            private CareerInterestPanel _panel;
            private ObjectiveJournalRuntimeProvider _objectiveProvider;

            private IEnumerator Start()
            {
                var bootstrap = Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted) yield return null;
                if (bootstrap.IsFaulted)
                {
                    Debug.LogException(bootstrap.Exception);
                    yield break;
                }

                _registry = ResolveRegistry();
                yield return BindWhenPlayerExists();
            }

            private IEnumerator BindWhenPlayerExists()
            {
                float elapsed = 0f;
                while (elapsed < 10f)
                {
                    var player = GameObject.Find("Player");
                    if (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    {
                        Bind(player);
                        yield break;
                    }
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            private void Bind(GameObject player)
            {
                if (_registry == null || _registry.CareerInterests == null || _registry.CareerInterests.Length == 0) return;
                _studentComponent = player.GetComponent<StudentLifeProgressComponent>();
                _candidateComponent = player.GetComponent<CareerCandidateProgressComponent>();
                if (_candidateComponent == null) _candidateComponent = player.AddComponent<CareerCandidateProgressComponent>();
                _candidateComponent.Bind(_registry.CareerCandidates, _registry.CareerHints);
                _interestComponent = player.GetComponent<CareerInterestProgressComponent>();
                if (_interestComponent == null) _interestComponent = player.AddComponent<CareerInterestProgressComponent>();
                _interestComponent.Bind(_registry.CareerInterests);
                var canvas = EnsureObjectiveCanvas(player, gameObject.scene);
                _panel = CareerInterestPanel.EnsureInScene(canvas);
                _panel.SelectionChanged -= RefreshObjectiveJournal;
                _panel.SelectionChanged += RefreshObjectiveJournal;
                RefreshObjectiveJournal();
                EnsureOpenButton(canvas);
            }

            private Canvas EnsureObjectiveCanvas(GameObject player, Scene scene)
            {
                _objectiveProvider = FindComponentInScene<ObjectiveJournalRuntimeProvider>(scene, true);
                if (_objectiveProvider != null)
                {
                    var existingCanvas = _objectiveProvider.GetComponent<Canvas>();
                    if (existingCanvas == null) existingCanvas = _objectiveProvider.GetComponentInParent<Canvas>(true);
                    if (existingCanvas != null) return existingCanvas;
                }

                var canvas = EnsureCanvas(scene);
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, player.GetComponent<PlayerInventory>());
                _objectiveProvider = canvas.GetComponent<ObjectiveJournalRuntimeProvider>();
                if (_objectiveProvider == null)
                {
                    _objectiveProvider = FindComponentInScene<ObjectiveJournalRuntimeProvider>(scene, true);
                }
                return canvas;
            }

            private void RefreshObjectiveJournal()
            {
                if (_objectiveProvider != null)
                {
                    _objectiveProvider.RefreshNow();
                }
            }

            private void EnsureOpenButton(Canvas canvas)
            {
                if (canvas == null || FindChild(canvas.transform, ButtonName) != null) return;
                var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(24f, -152f);
                rt.sizeDelta = new Vector2(240f, 56f);
                go.GetComponent<Image>().color = Color.clear;
                go.AddComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(go.transform, false);
                var labelRt = (RectTransform)label.transform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var text = label.GetComponent<Text>();
                text.font = LifeUiFont.Shared;
                text.fontSize = 20;
                text.raycastTarget = false;
                text.text = Ui("ui.career-interest.open", "장래희망", "My Dream");
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (_panel == null || _interestComponent == null) return;
                    var student = _studentComponent != null ? _studentComponent.Progress : null;
                    var candidates = _candidateComponent != null ? _candidateComponent.Progress : null;
                    int day = student != null ? student.CurrentDay : 0;
                    _panel.Show(_registry.CareerInterests, _interestComponent, candidates, student, day);
                    RefreshObjectiveJournal();
                });
            }
        }

        private static string Ui(string key, string ko, string en)
        {
            var value = GameText.Get(key);
            return value == key ? (GameText.Language == "ko" ? ko : en) : value;
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
#if UNITY_EDITOR
            if (registry == null) registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
#endif
            return registry;
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var canvas = FindComponentInScene<Canvas>(scene, true);
            if (canvas != null) return canvas;
            var go = new GameObject("CareerInterestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            var created = go.GetComponent<Canvas>();
            created.renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            return created;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
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

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
