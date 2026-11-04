using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Time;
using Rootborn.Game.Lighting;
using Rootborn.UI.Lighting;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    public sealed class DayNightLightingPlayModeTests
    {
        [UnityTest]
        public IEnumerator VISUAL_DAYNIGHT_001_LegacyFallbackShowsDayEveningNightAndDawnLighting()
        {
            SceneManager.LoadScene("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var clock = Object.FindFirstObjectByType<GameClock>();
            Assert.IsNotNull(clock, "Town PlayMode flow must include GameClock.");

            // Explicit legacy adapter coverage, not a player-flow claim about Town time.
            var source = LifeTimePresentation.FindInScene(SceneManager.GetActiveScene());
            Assert.IsNotNull(source);
            source.enabled = false;
            float before = clock.CurrentHour;
            clock.TimeScale = 60f;
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(clock.CurrentHour, before, "GameClock must advance through normal PlayMode time flow.");

            yield return CapturePhase(clock, "day", 12f);
            yield return CapturePhase(clock, "evening", 18f);
            yield return CapturePhase(clock, "night", 22f);
            yield return CapturePhase(clock, "dawn", 5f);
        }

        [UnityTest]
        public IEnumerator VISUAL_DAYNIGHT_002_TownLightingUsesAuthoritativeLifeTime()
        {
            SceneManager.LoadScene("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var clock = Object.FindFirstObjectByType<GameClock>();
            Assert.IsNotNull(clock);

            var source = LifeTimePresentation.FindInScene(SceneManager.GetActiveScene());
            Assert.IsNotNull(source);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!source.HasTime && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(source.HasTime, "Town must bind its real restored life owner.");
            var manager = Object.FindFirstObjectByType<LightingManager>();
            Assert.IsNotNull(manager);
            var profile = (DayNightLightingProfile)typeof(LightingManager).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var global = (Light2D)typeof(LightingManager).GetField("_globalLight", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.IsNotNull(profile);
            Assert.IsNotNull(global);
            float expected = profile.EvaluateIntensity(source.Hour);
            Assert.AreEqual(expected, global.intensity, 0.01f);
            clock.SetCurrentHour(22f);
            yield return null;
            yield return null;
            Assert.AreEqual(expected, global.intensity, 0.01f, "Unrelated legacy clock cannot override Town life lighting.");

        }

        [UnityTest]
        public IEnumerator VISUAL_DAYNIGHT_003_TownHidesDebugCycleAndLegacyFallbackRetainsIt()
        {
            SceneManager.LoadScene("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var clock = Object.FindFirstObjectByType<GameClock>();
            Assert.IsNotNull(clock);

            var source = LifeTimePresentation.FindInScene(SceneManager.GetActiveScene());
            Assert.IsNotNull(source);
            Assert.IsNull(GameObject.Find("DayNightDebugCycleButton"), "Life-driven Town must not show a contradictory debug clock.");
            var debug = Object.FindFirstObjectByType<DayNightDebugCycleButton>();
            Assert.IsNotNull(debug);
            float before = clock.CurrentHour;
            debug.HandleClick();
            Assert.AreEqual(before, clock.CurrentHour, 0.001f);
            source.enabled = false;
            yield return null;
            var buttonObject = GameObject.Find("DayNightDebugCycleButton");
            Assert.IsNotNull(buttonObject, "Legacy fallback must retain the debug cycle.");
            var button = buttonObject.GetComponent<Button>();
            Assert.IsNotNull(button);
            Assert.IsTrue(button.IsInteractable());

            yield return ClickButton(button);
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(18f, clock.CurrentHour, 0.25f, "First click should switch to evening.");

            yield return ClickButton(button);
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(22f, clock.CurrentHour, 0.25f, "Second click should switch to night.");
            Assert.IsTrue(clock.IsNight);

            var path = Path.Combine("Builds", "Logs", "day-night-debug-button-night.png");
            if (File.Exists(path)) File.Delete(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) yield break;
                yield return null;
            }
            Assert.Fail("Debug button screenshot was not captured: " + path);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Object.DontDestroyOnLoad(eventSystem);
            }

            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
        private static IEnumerator CapturePhase(GameClock clock, string phase, float hour)
        {
            clock.SetCurrentHour(hour);
            yield return new WaitForSeconds(2f);

            var path = Path.Combine("Builds", "Logs", "day-night-town-" + phase + ".png");
            if (File.Exists(path)) File.Delete(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    yield break;
                }
                yield return null;
            }

            Assert.Fail("Screenshot was not captured for " + phase + " at " + path);
        }
    }
}