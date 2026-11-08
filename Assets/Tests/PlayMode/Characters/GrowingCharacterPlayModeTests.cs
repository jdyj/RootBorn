using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Characters.Growing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Characters
{
    /// <summary>Walks the real Town player with keyboard input and checks the layered character on screen.</summary>
    public sealed class GrowingCharacterPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string EvidenceFolder = "output/assetpacks/character-parts-v1-20261104/previews/playmode";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem));
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GROW_PLAY_001_KeyboardWalkSwitchesRowsAndLayersStayVisible()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var load = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            while (!load.isDone) yield return null;

            GrowingCharacterVisualView view = null;
            GameObject player = null;
            float waited = 0f;
            while (view == null && waited < 25f)
            {
                player = GameObject.Find("Player");
                view = player != null ? player.GetComponentInChildren<GrowingCharacterVisualView>(true) : null;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(view, Is.Not.Null, "Town player has no layered character after bootstrap.");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(view.BodyRenderer.sprite, Is.Not.Null);
            Assert.That(view.BodyRenderer.enabled, Is.True);
            Assert.That(view.HeadRenderer.sprite, Is.Not.Null);
            Assert.That(view.OutfitRenderer.sprite, Is.Not.Null);
            yield return Capture("idle");

            yield return Walk(keyboard.dKey, view, player, 2, "right");
            yield return Walk(keyboard.aKey, view, player, 1, "left");
            // The spawn point sits right under a hedge, so step down first to make room to walk up.
            yield return Walk(keyboard.sKey, view, player, 0, "down");
            yield return Walk(keyboard.wKey, view, player, 3, "up");
        }

        [UnityTest]
        public IEnumerator GROW_PLAY_002_StageChangesSwapTheWholeLook()
        {
            InputSystem.AddDevice<Keyboard>();
            var load = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            GrowingCharacterVisualView view = null;
            GameObject player = null;
            float waited = 0f;
            while (view == null && waited < 25f)
            {
                player = GameObject.Find("Player");
                view = player != null ? player.GetComponentInChildren<GrowingCharacterVisualView>(true) : null;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(view, Is.Not.Null);
            var driver = player.GetComponent<Rootborn.UI.Characters.GrowingCharacterStageDriver>();
            Assert.That(driver, Is.Not.Null, "Stage driver should attach automatically.");
            yield return new WaitForSecondsRealtime(1f);
            var student = player.GetComponent<Rootborn.Game.StudentLife.StudentLifeProgressComponent>();
            Assert.That(student, Is.Not.Null);
            var progress = student.Progress;
            Assert.That(progress.CurrentDay, Is.EqualTo(1), "Test save should start on day 1.");
            yield return CheckLook(view, "casual", "stage-playground");

            // The real day change: ending day 1 and starting day 2 must switch to the school look by itself.
            Assert.That(progress.TryEndDay(null, out _), Is.True);
            Assert.That(progress.TryStartNextDay(), Is.True);
            yield return null;
            yield return CheckLook(view, "school_uniform", "stage-school");

            // Reaching adulthood takes a long career flow, so these frames only show each adult look on screen.
            var looks = Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out var registry) ? registry.CharacterAppearances : null;
            Object.Destroy(driver);
            yield return null;
            view.Apply(GrowingCharacterStageSelector.Select(looks, 40, true, "facility.chef"));
            yield return CheckLook(view, "chef", "stage-adult-chef");
            view.Apply(GrowingCharacterStageSelector.Select(looks, 40, true, "facility.interior"));
            yield return CheckLook(view, "interior", "stage-adult-interior");
            view.Apply(GrowingCharacterStageSelector.Select(looks, 40, true, "facility.soldier"));
            yield return CheckLook(view, "office", "stage-adult-office");
        }

        [UnityTest]
        public IEnumerator GROW_PLAY_003_WardrobeButtonChangesOutfitAndChoiceSurvivesSceneReload()
        {
            InputSystem.AddDevice<Keyboard>();
            string saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-wardrobe-play-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot);
            Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(saveRoot);
            try
            {
                yield return LoadTown();
                var player = GameObject.Find("Player");
                var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                var driver = player.GetComponent<Rootborn.UI.Characters.GrowingCharacterStageDriver>();
                Assert.That(driver, Is.Not.Null);

                // The adult body has several outfits today; the wardrobe must show all of them.
                var looks = Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out var registry) ? registry.CharacterAppearances : null;
                var adult = GrowingCharacterStageSelector.Select(looks, 40, true, "facility.none");
                view.Apply(adult);
                yield return null;

                var button = GameObject.Find(Rootborn.UI.Characters.GrowingWardrobeRuntimeInstaller.ButtonName);
                float waited = 0f;
                while (button == null && waited < 15f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                    button = GameObject.Find(Rootborn.UI.Characters.GrowingWardrobeRuntimeInstaller.ButtonName);
                }

                Assert.That(button, Is.Not.Null, "Wardrobe button should appear in Town.");
                yield return Click(button);
                yield return null;
                var panelObject = GameObject.Find(Rootborn.UI.Characters.GrowingWardrobeRuntimeInstaller.PanelName);
                Assert.That(panelObject, Is.Not.Null, "A real click should open the wardrobe.");
                var panel = panelObject.GetComponent<Rootborn.UI.Characters.GrowingWardrobePanel>();
                int offered = 0;
                foreach (var section in driver.WardrobeSections()) offered += section.Cards.Count;
                Assert.That(panel.VisibleCardCount, Is.EqualTo(offered));
                Assert.That(panel.VisibleCardCount, Is.GreaterThanOrEqualTo(3));
                yield return CaptureScreen("wardrobe-open");

                var chef = GameObject.Find("Outfit_wearable.outfit_chef");
                Assert.That(chef, Is.Not.Null);
                yield return Click(chef);
                Assert.That(view.LookKey, Does.Contain("chef"));
                yield return CaptureScreen("wardrobe-chef-chosen");
                Assert.That(Rootborn.Game.Common.UiGameplayInputLock.IsBlocked, Is.True, "Movement stays locked while it is open.");
                panel.Hide();
                Assert.That(Rootborn.Game.Common.UiGameplayInputLock.IsBlocked, Is.False);

                string slot = player.GetComponent<Rootborn.Game.StudentLife.StudentLifeProgressComponent>().Progress.SaveSlot;
                string id = player.GetComponent<Rootborn.Game.StudentLife.StudentLifeProgressComponent>().Progress.PlayerId;
                string savedJson = File.ReadAllText(Path.Combine(new Rootborn.Game.Save.SaveService(slot, saveRoot).DirectoryPath,
                    Rootborn.Game.Characters.Wearables.WearableWardrobeStore.FileNameFor(id)));
                Assert.That(savedJson, Does.Contain("wearable.outfit_chef"), "Choice must be written to the save folder.");

                // A fresh scene and driver must restore the saved outfit on their own.
                yield return LoadTown();
                player = GameObject.Find("Player");
                view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                driver = player.GetComponent<Rootborn.UI.Characters.GrowingCharacterStageDriver>();
                Assert.That(driver, Is.Not.Null);
                driver.Apply(40, true, "facility.none");
                yield return null;
                Assert.That(view.LookKey, Does.Contain("chef"), "Saved choice should win over the adult default after reload.");
                Assert.That(view.OutfitRenderer.sprite, Is.Not.Null);
            }
            finally
            {
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
            }
        }

        private IEnumerator LoadTown()
        {
            var load = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            float waited = 0f;
            while (waited < 25f)
            {
                var candidate = GameObject.Find("Player");
                if (candidate != null && candidate.GetComponentInChildren<GrowingCharacterVisualView>(true) != null) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        private IEnumerator Click(GameObject target)
        {
            // Same frame-driven pointer as MainMenuPointerInputE2ETests, so the real EventSystem handles the click.
            var rect = target.GetComponent<RectTransform>();
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "Nothing under the pointer at " + point);
            Assert.That(hits[0].gameObject.transform.IsChildOf(target.transform), Is.True, target.name + " is covered by " + hits[0].gameObject.name);
            // InputTestFixture hides the device from Town's input module, so dispatch at the raycast hit here.
            // This proves what is on top and that its handler works. GrowingWardrobeMouseTests covers the device path.
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.Not.Null);
            ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator CaptureScreen(string label)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(EvidenceFolder);
            File.WriteAllBytes(Path.Combine(EvidenceFolder, "growing-" + label + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }

        private IEnumerator CheckLook(GrowingCharacterVisualView view, string expected, string label)
        {
            yield return null;
            Assert.That(view.LookKey, Does.Contain(expected), label);
            Assert.That(view.BodyRenderer.sprite, Is.Not.Null, label);
            Assert.That(view.OutfitRenderer.sprite, Is.Not.Null, label);
            yield return Capture(label);
        }

        private IEnumerator Walk(KeyControl key, GrowingCharacterVisualView view, GameObject player, int expectedRow, string label)
        {
            var start = player.transform.position;
            var columns = new System.Collections.Generic.HashSet<int>();
            Press(key);
            float time = 0f;
            bool captured = false;
            while (time < 0.8f)
            {
                time += Time.unscaledDeltaTime;
                columns.Add(view.CurrentColumn);
                if (!captured && time > 0.4f)
                {
                    captured = true;
                    yield return Capture(label);
                }
                yield return null;
            }

            Assert.That(view.CurrentRow, Is.EqualTo(expectedRow), label);
            Assert.That(columns.Count, Is.GreaterThan(1), label + " should animate");
            Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(0.1f), label + " should move");
            Release(key);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            var camera = Camera.main;
            if (camera == null) yield break;
            const int width = 1920;
            const int height = 1080;
            var target = new RenderTexture(width, height, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            Directory.CreateDirectory(EvidenceFolder);
            File.WriteAllBytes(Path.Combine(EvidenceFolder, "growing-" + label + ".png"), image.EncodeToPNG());

            var player = GameObject.Find("Player");
            if (player != null)
            {
                var previousRect = camera.rect;
                var point = camera.WorldToViewportPoint(player.transform.position);
                int size = 300;
                int x = Mathf.Clamp(Mathf.RoundToInt(point.x * width) - size / 2, 0, width - size);
                int y = Mathf.Clamp(Mathf.RoundToInt(point.y * height) - size / 2, 0, height - size);
                var crop = new Texture2D(size, size, TextureFormat.RGB24, false);
                crop.SetPixels(image.GetPixels(x, y, size, size));
                crop.Apply();
                File.WriteAllBytes(Path.Combine(EvidenceFolder, "growing-" + label + "-closeup.png"), crop.EncodeToPNG());
                Object.Destroy(crop);
                camera.rect = previousRect;
            }

            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }
}
