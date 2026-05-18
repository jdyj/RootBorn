using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class CareerCandidateRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[CareerCandidateRuntimeInstaller]";
        private const string ButtonName = "CareerCandidateOpenButton";

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
            private CareerCandidateProgressComponent _component;
            private CareerCandidatePanel _panel;

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
                if (_registry == null) return;
                _component = player.GetComponent<CareerCandidateProgressComponent>();
                if (_component == null) _component = player.AddComponent<CareerCandidateProgressComponent>();
                _component.Bind(_registry.CareerCandidates, _registry.CareerHints);
                var canvas = EnsureCanvas();
                _panel = CareerCandidatePanel.EnsureInScene(canvas);
                EnsureOpenButton(canvas, player.GetComponent<StudentLifeProgressComponent>());
            }

            private void EnsureOpenButton(Canvas canvas, StudentLifeProgressComponent student)
            {
                if (canvas == null || FindChild(canvas.transform, ButtonName) != null) return;
                var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(24f, -84f);
                rt.sizeDelta = new Vector2(190f, 38f);
                go.GetComponent<Image>().color = new Color(0.72f, 0.58f, 0.32f, 1f);
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(go.transform, false);
                var labelRt = (RectTransform)label.transform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var text = label.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.text = "Career";
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (_panel != null && _component != null) _panel.Show(_registry.CareerCandidates, _component.Progress, student != null ? student.Progress : null);
                });
            }
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
#if UNITY_EDITOR
            if (registry == null) registry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
#endif
            return registry;
        }

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null) return canvas;
            var go = new GameObject("CareerCandidateCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
