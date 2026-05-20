using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.Game.Common
{
    public static class DirectValidationCareerDailyPlanAutoplayInstaller
    {
        private const string Flag = "-directValidationAutoplayCareerDailyPlanRoute";
        private const string ObserveOnlyFlag = "-directValidationAutoplayCareerDailyPlanRouteObserveOnly";
        private const string ScreenshotFlag = "-directValidationScreenshot";
        private const string RunnerName = "[DirectValidationCareerDailyPlanAutoplay]";
        private const string TownSceneName = "Town";
        private const string CareerPracticeBoardName = "CareerPracticeBoard";
        private const string DailyPlanBoardName = "DailyPlanBoard";
        private const string DailyPlanRouteRowName = "DailyPlanRouteRow";
        private const string DailyPlanPanelTypeName = "Rootborn.UI.StudentLife.DailyPlanPanel";
        private static bool? s_enabled;

        private static bool Enabled
        {
            get
            {
                if (!s_enabled.HasValue)
                {
                    s_enabled = HasFlag(Flag) || HasFlag(ObserveOnlyFlag);
                }

                return s_enabled.Value;
            }
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
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            if (!Enabled || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private static bool HasFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadArgumentValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
                }

                string prefix = flag + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(prefix.Length);
                }
            }

            return string.Empty;
        }

        private sealed class Runner : MonoBehaviour
        {
            private static readonly List<PlayerIdentity> Players = new List<PlayerIdentity>(8);

            private IEnumerator Start()
            {
                EnsureKeyboardDevice();
                DirectValidationTrace.Log("autoplay started career-daily-plan-route scene=" + SceneManager.GetActiveScene().name + " observeOnly=" + HasFlag(ObserveOnlyFlag));

                PlayerIdentity identity = null;
                yield return WaitForLocalInputPlayer(value => identity = value);
                if (identity == null)
                {
                    DirectValidationTrace.Log("autoplay failed: career-daily-plan-route local player not found");
                    yield break;
                }

                StudentLifeProgressComponent student = null;
                DailyPlanProgressComponent dailyPlan = null;
                yield return WaitForProgress(identity, value => student = value, value => dailyPlan = value);
                if (student == null || dailyPlan == null)
                {
                    DirectValidationTrace.Log("autoplay failed: career-daily-plan-route progress not ready player=" + identity.PlayerId);
                    yield break;
                }

                if (HasFlag(ObserveOnlyFlag))
                {
                    yield return CaptureAndLogObserveOnly(identity);
                    yield break;
                }

                bool success = false;
                yield return RunPlayerFlow(identity, value => success = value);
                ReleaseAllKeys();

                if (!success)
                {
                    yield break;
                }

                yield return CaptureAndLogActionCompletion(identity);
            }

            private static IEnumerator CaptureAndLogObserveOnly(PlayerIdentity identity)
            {
                string screenshotPath = ReadArgumentValue(ScreenshotFlag);
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    CaptureScreenshotEvidence(screenshotPath);
                    yield return null;
                    yield return null;
                }

                DirectValidationTrace.Log("autoplay career snapshot player=" + identity.PlayerId + " " + CareerSnapshot(identity));
                DirectValidationTrace.Log("autoplay route snapshot player=" + identity.PlayerId + " " + DailyPlanSnapshot(identity));
                DirectValidationTrace.Log("autoplay completed career-daily-plan-route-observe player=" + identity.PlayerId);
            }

            private static IEnumerator CaptureAndLogActionCompletion(PlayerIdentity identity)
            {
                string screenshotPath = ReadArgumentValue(ScreenshotFlag);
                if (!string.IsNullOrEmpty(screenshotPath))
                {
                    CaptureScreenshotEvidence(screenshotPath);
                    yield return null;
                    yield return null;
                }

                DirectValidationTrace.Log("autoplay career snapshot player=" + identity.PlayerId + " " + CareerSnapshot(identity));
                DirectValidationTrace.Log("autoplay route snapshot player=" + identity.PlayerId + " " + DailyPlanSnapshot(identity));
                DirectValidationTrace.Log("autoplay completed career-daily-plan-route player=" + identity.PlayerId);
            }

            private static IEnumerator RunPlayerFlow(PlayerIdentity identity, Action<bool> assign)
            {
                GameObject careerBoard = null;
                yield return WaitForSceneObject(CareerPracticeBoardName, 12f, value => careerBoard = value);
                if (careerBoard == null)
                {
                    DirectValidationTrace.Log("autoplay failed: career practice board not found");
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + CareerPracticeBoardName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, careerBoard.transform.position, 0.35f, 12f);
                yield return PressKey(Key.E);
                yield return null;
                yield return PressKey(Key.E);
                DirectValidationTrace.Log("autoplay interacted target=" + CareerPracticeBoardName + " player=" + identity.PlayerId + " " + CareerSnapshot(identity));

                GameObject dailyPlanBoard = null;
                yield return WaitForSceneObject(DailyPlanBoardName, 12f, value => dailyPlanBoard = value);
                if (dailyPlanBoard == null)
                {
                    DirectValidationTrace.Log("autoplay failed: Daily Plan board not found");
                    assign(false);
                    yield break;
                }

                DirectValidationTrace.Log("autoplay move target=" + DailyPlanBoardName + " player=" + identity.PlayerId);
                yield return MovePlayerTo(identity.transform, dailyPlanBoard.transform.position, 0.35f, 12f);
                yield return PressKey(Key.E);
                Button routeButton = null;
                yield return WaitForRouteButton(value => routeButton = value);
                if (routeButton == null)
                {
                    DirectValidationTrace.Log("autoplay failed: career Daily Plan route button not selectable " + DescribeDailyPlanUiState());
                    assign(false);
                    yield break;
                }

                yield return ClickButton(routeButton);
                DirectValidationTrace.Log("autoplay selected career daily route player=" + identity.PlayerId + " " + DailyPlanSnapshot(identity));
                assign(true);
            }

            private static IEnumerator WaitForLocalInputPlayer(Action<PlayerIdentity> assign)
            {
                PlayerIdentity identity = null;
                float elapsed = 0f;
                while (identity == null && elapsed < 15f)
                {
                    identity = FindLocalInputPlayer();
                    if (identity == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(identity);
            }

            private static IEnumerator WaitForProgress(PlayerIdentity identity, Action<StudentLifeProgressComponent> assignStudent, Action<DailyPlanProgressComponent> assignDaily)
            {
                StudentLifeProgressComponent student = null;
                DailyPlanProgressComponent daily = null;
                float elapsed = 0f;
                while ((student == null || daily == null) && elapsed < 12f)
                {
                    student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                    daily = identity != null ? identity.GetComponent<DailyPlanProgressComponent>() : null;
                    if (student == null || daily == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assignStudent(student);
                assignDaily(daily);
            }

            private static PlayerIdentity FindLocalInputPlayer()
            {
                CollectPlayers(SceneManager.GetActiveScene(), Players);
                for (int i = 0; i < Players.Count; i++)
                {
                    var identity = Players[i];
                    if (identity == null)
                    {
                        continue;
                    }

                    var controller = identity.GetComponent<PlayerController>();
                    var router = identity.GetComponent<PlayerInteractionRouter>();
                    if (controller != null && controller.enabled && router != null && router.enabled)
                    {
                        return identity;
                    }
                }

                return null;
            }

            private static IEnumerator WaitForSceneObject(string objectName, float timeoutSeconds, Action<GameObject> assign)
            {
                GameObject found = null;
                float elapsed = 0f;
                while (found == null && elapsed < timeoutSeconds)
                {
                    found = FindSceneObjectByName(SceneManager.GetActiveScene(), objectName);
                    if (found == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(found);
            }

            private static IEnumerator WaitForRouteButton(Action<Button> assign)
            {
                Button found = null;
                float elapsed = 0f;
                while (found == null && elapsed < 8f)
                {
                    var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var button = buttons[i];
                        if (button != null && button.interactable && button.gameObject.activeInHierarchy && button.gameObject.name == DailyPlanRouteRowName)
                        {
                            found = button;
                            break;
                        }
                    }

                    if (found == null)
                    {
                        elapsed += UnityEngine.Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                assign(found);
            }

            private static IEnumerator ClickButton(Button button)
            {
                string buttonName = button.gameObject.name;
                if (EventSystem.current != null)
                {
                    ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                }
                else
                {
                    button.onClick.Invoke();
                }

                DirectValidationTrace.Log("autoplay clicked button=" + buttonName);
                yield return null;
            }

            private static IEnumerator MovePlayerTo(Transform player, Vector3 target, float tolerance, float timeoutSeconds)
            {
                float elapsed = 0f;
                while (player != null && elapsed < timeoutSeconds && Vector3.Distance(player.position, target) > tolerance)
                {
                    Vector3 delta = target - player.position;
                    Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                    HoldKey(key);
                    elapsed += UnityEngine.Time.unscaledDeltaTime;
                    yield return null;
                    yield return new WaitForFixedUpdate();
                }

                ReleaseAllKeys();
                yield return null;
            }

            private static IEnumerator PressKey(Key key)
            {
                HoldKey(key);
                yield return null;
                yield return new WaitForFixedUpdate();
                ReleaseAllKeys();
                yield return null;
            }

            private static string DescribeDailyPlanUiState()
            {
                var panel = FindDailyPlanPanel();
                if (panel == null)
                {
                    return "dailyPlanUi=panel:missing";
                }

                int rowCount = ReadInt(panel, "RouteRowCountForTests");
                string summary = "dailyPlanUi=panel:present,active:" + panel.gameObject.activeInHierarchy
                    + ",rows:" + rowCount
                    + ",empty:" + Safe(ReadString(panel, "EmptyStateTextForTests"));
                for (int i = 0; i < rowCount; i++)
                {
                    var button = InvokeGetRouteButtonForTests(panel, i);
                    var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
                    summary += " row" + i + "=";
                    if (button == null)
                    {
                        summary += "missing";
                    }
                    else
                    {
                        summary += "active:" + button.gameObject.activeInHierarchy + ",interactable:" + button.interactable + ",text:" + Safe(text != null ? text.text : string.Empty);
                    }
                }

                return summary;
            }

            private static MonoBehaviour FindDailyPlanPanel()
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var behaviour = behaviours[i];
                    if (behaviour != null && behaviour.GetType().FullName == DailyPlanPanelTypeName)
                    {
                        return behaviour;
                    }
                }

                return null;
            }

            private static int ReadInt(MonoBehaviour target, string propertyName)
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null && property.GetValue(target) is int value ? value : 0;
            }

            private static string ReadString(MonoBehaviour target, string propertyName)
            {
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null ? property.GetValue(target) as string : string.Empty;
            }

            private static Button InvokeGetRouteButtonForTests(MonoBehaviour target, int index)
            {
                var method = target.GetType().GetMethod("GetRouteButtonForTests", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                return method != null ? method.Invoke(target, new object[] { index }) as Button : null;
            }

            private static string DailyPlanSnapshot(PlayerIdentity identity)
            {
                var component = identity != null ? identity.GetComponent<DailyPlanProgressComponent>() : null;
                var student = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var progress = component != null ? component.EnsureProgress(student != null ? student.EnsureProgress() : null) : null;
                if (progress == null)
                {
                    return "dailyPlan=missing";
                }

                return "dailyPlan=day:" + progress.CurrentDay + ",selected:" + Safe(progress.SelectedRouteId) + ",completed:" + Safe(progress.CompletedRouteId);
            }

            private static string CareerSnapshot(PlayerIdentity identity)
            {
                var component = identity != null ? identity.GetComponent<StudentLifeProgressComponent>() : null;
                var progress = component != null ? component.EnsureProgress() : null;
                if (progress == null)
                {
                    return "careerHints=missing";
                }

                var ids = progress.GetCareerHintIds();
                if (ids == null || ids.Length == 0)
                {
                    return "careerHints=empty";
                }

                string summary = string.Empty;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (string.IsNullOrEmpty(ids[i]))
                    {
                        continue;
                    }

                    summary += (summary.Length == 0 ? string.Empty : ",") + ids[i];
                }

                return "careerHints=" + (summary.Length == 0 ? "empty" : summary);
            }

            private static string Safe(string value)
            {
                return string.IsNullOrEmpty(value) ? "empty" : value;
            }

            private static void CaptureScreenshotEvidence(string screenshotPath)
            {
                string directory = Path.GetDirectoryName(screenshotPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                ScreenCapture.CaptureScreenshot(screenshotPath);
                DirectValidationTrace.Log("autoplay screenshot path=" + screenshotPath);
            }

            private static Keyboard EnsureKeyboardDevice()
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    return keyboard;
                }

                keyboard = InputSystem.AddDevice<Keyboard>();
                DirectValidationTrace.Log("autoplay created keyboard device for career-daily-plan-route batchmode input");
                return keyboard;
            }

            private static void HoldKey(Key key)
            {
                var keyboard = EnsureKeyboardDevice();
                if (keyboard == null)
                {
                    return;
                }

                var state = new KeyboardState();
                state.Set(key, true);
                InputSystem.QueueStateEvent(keyboard, state);
            }

            private static void ReleaseAllKeys()
            {
                var keyboard = EnsureKeyboardDevice();
                if (keyboard == null)
                {
                    return;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }

            private static void CollectPlayers(Scene scene, List<PlayerIdentity> players)
            {
                players.Clear();
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    roots[i].GetComponentsInChildren(false, players);
                }
            }
        }

        private static GameObject FindSceneObjectByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindChildByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChildByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
