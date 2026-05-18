using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class MainMenuPointerInputE2ETests : InputTestFixture
    {
        private const string EvidencePath = "production/qa/evidence/main-menu-pointer-save-slot.png";

        [UnityTest]
        public IEnumerator BOOT_E2E_003_MainMenuSinglePlayButtonRespondsToRealPointerClick()
        {
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);
            yield return null;

            var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            Assert.IsNotNull(eventSystem, "MainMenu must expose an EventSystem after Boot loads it.");
            Assert.IsNotNull(eventSystem.GetComponent<BaseInputModule>(), "MainMenu EventSystem must have a real input module for pointer clicks.");
            Assert.IsNotNull(eventSystem.GetComponent<InputSystemUIInputModule>(), "MainMenu EventSystem must use InputSystemUIInputModule for real pointer clicks.");

            var button = GameObject.Find("SinglePlayButton");
            Assert.IsNotNull(button, "MainMenu should expose the SinglePlayButton entry point.");

            Vector2 clickPoint = FindRaycastPointFor(button, eventSystem);
            yield return ClickWithMouse(clickPoint);
            yield return WaitForSaveSlotPanel(3f);
            yield return CaptureEvidence(EvidencePath);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForSaveSlotPanel(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);
                var root = GameObject.Find("SaveSlotSelectRoot");
                if (panel != null && root != null && root.activeInHierarchy)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("SinglePlayButton left-click did not open SaveSlotSelectPanel.");
        }

        private static Vector2 FindRaycastPointFor(GameObject target, EventSystem eventSystem)
        {
            var results = new List<RaycastResult>();
            for (int yStep = 1; yStep <= 9; yStep++)
            {
                for (int xStep = 1; xStep <= 9; xStep++)
                {
                    var point = new Vector2(Screen.width * xStep / 10f, Screen.height * yStep / 10f);
                    var eventData = new PointerEventData(eventSystem) { position = point };
                    results.Clear();
                    eventSystem.RaycastAll(eventData, results);
                    for (int i = 0; i < results.Count; i++)
                    {
                        var hit = results[i].gameObject;
                        if (hit == target || hit.transform.IsChildOf(target.transform))
                        {
                            return point;
                        }
                    }
                }
            }

            Assert.Fail("Could not find a screen point that raycasts to " + target.name + ".");
            return Vector2.zero;
        }

        private IEnumerator ClickWithMouse(Vector2 screenPoint)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, screenPoint);
            InputSystem.Update();
            yield return null;
            yield return null;

            Press(mouse.leftButton);
            InputSystem.Update();
            yield return null;
            yield return null;

            Release(mouse.leftButton);
            InputSystem.Update();
            yield return null;
            yield return null;
        }

        private static IEnumerator CaptureEvidence(string relativePath)
        {
            string fullPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (File.Exists(fullPath)) File.Delete(fullPath);
            ScreenCapture.CaptureScreenshot(fullPath);
            float elapsed = 0f;
            while (!File.Exists(fullPath) && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(File.Exists(fullPath), "MainMenu pointer evidence screenshot was not written: " + fullPath);
        }
    }
}
