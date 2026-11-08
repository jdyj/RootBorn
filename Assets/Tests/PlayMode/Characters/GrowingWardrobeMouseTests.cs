using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Careers;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using Rootborn.UI.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Characters
{
    /// <summary>No InputTestFixture here: the mouse goes through the same input system Town's UI module reads.</summary>
    public sealed class GrowingWardrobeMouseTests
    {
        private Mouse _mouse;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GROW_PLAY_003_MouseDevicePicksTheChefOutfitAsAnAdultAndReloadKeepsIt()
        {
            string saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-wardrobe-play-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot);
            SaveService.SetRootDirectoryForTests(saveRoot);
            var previousSave = ActiveSaveContext.Metadata;
            // Started from a save slot like the menu does, so Town creates the career owner and the driver finishes binding;
            // until then it re-applies the student look every half second and would undo the fixture below.
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "grow-wardrobe" });
            try
            {
                yield return LoadTown();
                var player = GameObject.Find("Player");
                var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                var driver = player.GetComponent<GrowingCharacterStageDriver>();
                Assert.That(driver, Is.Not.Null);
                float bound = 0f;
                while (bound < 10f && (player.GetComponent<AdultCareerRuntimeComponent>() == null || !player.GetComponent<AdultCareerRuntimeComponent>().CanMutate))
                {
                    bound += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(0.6f);
                // Fixture: reaching adulthood takes a long career flow, so grow up through the driver. Every click below is a
                // mouse device event read by Town's InputSystemUIInputModule (no direct handler calls).
                driver.Apply(40, true, "facility.none");
                yield return null;

                _mouse = InputSystem.AddDevice<Mouse>();
                _mouse.MakeCurrent();
                yield return null;
                var button = GameObject.Find(GrowingWardrobeRuntimeInstaller.ButtonName);
                yield return ClickAt(button);
                var panelObject = GameObject.Find(GrowingWardrobeRuntimeInstaller.PanelName);
                Assert.That(panelObject, Is.Not.Null, "A device click at the button should open the wardrobe.");
                var panel = panelObject.GetComponent<GrowingWardrobePanel>();
                int offered = 0;
                foreach (var section in driver.WardrobeSections()) offered += section.Cards.Count;
                Assert.That(panel.VisibleCardCount, Is.EqualTo(offered));
                Assert.That(panel.VisibleCardCount, Is.GreaterThanOrEqualTo(3), "The adult body has several outfits.");
                yield return Capture("wardrobe-open");

                var chef = GameObject.Find("Outfit_wearable.outfit_chef");
                Assert.That(chef, Is.Not.Null);
                yield return ClickAt(chef);
                Assert.That(view.LookKey, Does.Contain("chef"), "A device click on the chef card should change the outfit.");
                yield return Capture("wardrobe-chef-chosen");
                Assert.That(Rootborn.Game.Common.UiGameplayInputLock.IsBlocked, Is.True, "Movement stays locked while it is open.");
                yield return ClickAt(GameObject.Find(GrowingWardrobeRuntimeInstaller.ButtonName));
                Assert.That(panel.IsVisible, Is.False, "A second device click on the button closes the wardrobe.");
                Assert.That(Rootborn.Game.Common.UiGameplayInputLock.IsBlocked, Is.False);

                string slot = player.GetComponent<StudentLifeProgressComponent>().Progress.SaveSlot;
                string id = player.GetComponent<StudentLifeProgressComponent>().Progress.PlayerId;
                string savedJson = File.ReadAllText(Path.Combine(new SaveService(slot, saveRoot).DirectoryPath,
                    Rootborn.Game.Characters.Wearables.WearableWardrobeStore.FileNameFor(id)));
                Assert.That(savedJson, Does.Contain("wearable.outfit_chef"), "Choice must be written to the save folder.");

                // A fresh scene and driver must restore the saved outfit on their own.
                yield return LoadTown();
                player = GameObject.Find("Player");
                view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                driver = player.GetComponent<GrowingCharacterStageDriver>();
                bound = 0f;
                while (bound < 10f && (player.GetComponent<AdultCareerRuntimeComponent>() == null || !player.GetComponent<AdultCareerRuntimeComponent>().CanMutate))
                {
                    bound += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(0.6f);
                driver.Apply(40, true, "facility.none");
                yield return null;
                Assert.That(view.LookKey, Does.Contain("chef"), "Saved choice should come back after reload.");
                Assert.That(view.OutfitRenderer.sprite, Is.Not.Null);
            }
            finally
            {
                ActiveSaveContext.Set(previousSave);
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator GROW_PLAY_004_MouseDevicePicksAnOutfitAndReloadRestoresItWithoutHelp()
        {
            string saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-wardrobe-mouse-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot);
            SaveService.SetRootDirectoryForTests(saveRoot);
            try
            {
                yield return LoadTown();
                var player = GameObject.Find("Player");
                var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                Assert.That(view, Is.Not.Null);
                yield return SetStudentDay(player, 2);
                Assert.That(view.LookKey, Does.Contain("school_uniform"), "Day 2 should grow into the school look by itself.");

                var module = EventSystem.current.GetComponent<BaseInputModule>();
                Assert.That(module, Is.InstanceOf<InputSystemUIInputModule>(), "Town must read the real device in play.");
                _mouse = InputSystem.AddDevice<Mouse>();
                _mouse.MakeCurrent();
                yield return null;

                yield return ClickAt(GameObject.Find(GrowingWardrobeRuntimeInstaller.ButtonName));
                Assert.That(GameObject.Find(GrowingWardrobeRuntimeInstaller.PanelName), Is.Not.Null, "A device click at the button should open the wardrobe.");
                yield return null;
                var card = GameObject.Find("Outfit_wearable.outfit_school_casual");
                Assert.That(card, Is.Not.Null);
                yield return ClickAt(card);
                Assert.That(view.LookKey, Does.Contain("school_casual"), "A device click on a card should change the outfit.");
                yield return Capture("mouse-school-casual-panel");
                yield return ClickAt(GameObject.Find(GrowingWardrobeRuntimeInstaller.ButtonName));
                yield return Capture("mouse-school-casual-closed");

                // A fresh scene: nothing here calls the driver, the day event alone must bring the saved outfit back.
                yield return LoadTown();
                player = GameObject.Find("Player");
                view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                yield return SetStudentDay(player, 2);
                Assert.That(view.LookKey, Does.Contain("school_casual"), "The saved choice should return after reload.");
                Assert.That(view.OutfitRenderer.sprite, Is.Not.Null);
            }
            finally
            {
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator GROW_PLAY_005_CommittingAChefCareerGrowsThePlayerIntoTheChefLook()
        {
            string saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-adult-look-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveRoot);
            SaveService.SetRootDirectoryForTests(saveRoot);
            var previousSave = ActiveSaveContext.Metadata;
            // Same as starting from a save slot in the menu: this is what makes Town create the career owner.
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "grow-adult" });
            try
            {
                yield return LoadTown();
                var player = GameObject.Find("Player");
                var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                float waited = 0f;
                AdultCareerRuntimeComponent owner = null;
                while (waited < 10f && (owner == null || !owner.CanMutate))
                {
                    owner = player.GetComponent<AdultCareerRuntimeComponent>();
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                Assert.That(owner != null && owner.CanMutate, Is.True, "Town should give the player a career owner.");
                Assert.That(view.LookKey, Does.Not.Contain("chef"));
                var facility = UnityEditor.AssetDatabase.LoadAssetAtPath<FacilityDefinition>("Assets/Data/Careers/Facilities/Chef/Facility_Chef.asset");
                Assert.That(facility, Is.Not.Null);

                // Fixture: requirements are skipped, but the commit and its event are the real ones. The look must follow by itself.
                var select = typeof(AdultCareerProgress).GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic);
                var result = owner.ExecuteSessionCommand((session, service) =>
                {
                    select.Invoke(session.Progress, new object[] { facility, "grow-play-005" });
                    return new AdultCareerResult(AdultCareerResultKind.Applied);
                });
                Assert.That(result.Applied, Is.True);
                yield return null;
                Assert.That(view.LookKey, Does.Contain("chef"));
                yield return Capture("adult-chef-apron");

                yield return LoadTown();
                player = GameObject.Find("Player");
                view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
                waited = 0f;
                while (waited < 10f && !view.LookKey.Contains("chef"))
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                Assert.That(view.LookKey, Does.Contain("chef"), "A saved career should bring the look back after reload.");
            }
            finally
            {
                ActiveSaveContext.Set(previousSave);
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
            }
        }

        private static IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes($"output/assetpacks/character-parts-v1-20261104/previews/playmode/growing-{label}.png", image.EncodeToPNG());
            Object.Destroy(image);
        }

        private static IEnumerator LoadTown()
        {
            var load = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            float waited = 0f;
            while (waited < 25f && (GameObject.Find(GrowingWardrobeRuntimeInstaller.ButtonName) == null || !StudentReady()))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(GameObject.Find(GrowingWardrobeRuntimeInstaller.ButtonName), Is.Not.Null);
            Assert.That(StudentReady(), Is.True);
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private static bool StudentReady()
        {
            var player = GameObject.Find("Player");
            var student = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            return student != null && student.Progress != null && player.GetComponent<GrowingCharacterStageDriver>() != null;
        }

        // Fixture only: the day comes from a forced value, but the look follows through the real time event.
        private static IEnumerator SetStudentDay(GameObject player, int day)
        {
            var progress = player.GetComponent<StudentLifeProgressComponent>().Progress;
            typeof(StudentLifeProgress).GetProperty(nameof(StudentLifeProgress.CurrentDay)).SetValue(progress, day);
            var changed = (System.Action)typeof(StudentLifeProgress).GetField(nameof(StudentLifeProgress.TimeChanged), BindingFlags.Instance | BindingFlags.NonPublic).GetValue(progress);
            changed?.Invoke();
            yield return null;
        }

        private IEnumerator ClickAt(GameObject target)
        {
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = target.GetComponent<RectTransform>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }
    }
}
