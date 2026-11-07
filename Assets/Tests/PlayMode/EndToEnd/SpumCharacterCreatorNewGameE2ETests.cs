using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Save;
using Rootborn.UI.Characters;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    // CREATOR-001..005: the new-game creator for the growing character, driven by button clicks.
    public sealed class SpumCharacterCreatorNewGameE2ETests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-creator-newgame-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            DestroyIfFound("EventSystem");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            DestroyIfFound("EventSystem");
            DestroyIfFound("Player");
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CREATOR_001_NewGameOpensCreatorWithoutSaving()
        {
            yield return OpenSaveSlotsFromMainMenu();
            yield return ClickButtonNamed("NewGameButton");

            var creator = Object.FindFirstObjectByType<SpumCharacterCreatorPanel>();
            Assert.IsNotNull(creator, "New Game must open the character creator before saving.");
            Assert.GreaterOrEqual(creator.StageCount, 2, "The creator offers a starting outfit for each growth stage from the appearance data.");
            Assert.IsNotNull(GameObject.Find("NameField"), "The creator asks for a name.");
            Assert.IsFalse(File.Exists(Path.Combine(_saveRoot, "slot-0", "metadata.json")), "Opening the creator must not create save metadata before Confirm.");
        }

        [UnityTest]
        public IEnumerator CREATOR_002_StageArrowChangesChoiceAndPreview()
        {
            var creator = default(SpumCharacterCreatorPanel);
            yield return OpenCreatorFromMainMenu(c => creator = c);
            int stage = FirstStageWithChoices(creator);
            string before = creator.ChoiceAt(stage).Id;

            yield return ClickButtonNamed("Stage" + stage + "_Next");

            Assert.AreNotEqual(before, creator.ChoiceAt(stage).Id, "The arrow must switch to another outfit.");
            Assert.AreEqual(stage, creator.PreviewStage, "The preview follows the stage being changed.");
            yield return Capture("output/creator-mom-20261107/creator-002-after-arrow.png");
            var shown = GameObject.Find("PreviewGrowing_Outfit");
            Assert.IsNotNull(shown, "The preview draws the chosen outfit.");
            Assert.AreSame(creator.ChoiceAt(stage).Outfit.GetFrame(GrowingCharacterVisualView.RowDown, GrowingCharacterVisualView.StandingColumn),
                shown.GetComponent<Image>().sprite, "The preview must show the newly chosen outfit.");
        }

        [UnityTest]
        public IEnumerator CREATOR_003_ConfirmSavesNameAndStartingOutfitThatTheTownCharacterWears()
        {
            var creator = default(SpumCharacterCreatorPanel);
            yield return OpenCreatorFromMainMenu(c => creator = c);
            int stage = FirstStageWithChoices(creator);
            yield return ClickButtonNamed("Stage" + stage + "_Next");
            var chosen = creator.ChoiceAt(stage);
            GameObject.Find("NameField").GetComponent<InputField>().text = "  Haru  ";

            yield return ClickButtonNamed("ConfirmButton");
            yield return WaitForScene(20f);

            SaveSlotMetadata metadata = new SaveService("slot-0", _saveRoot).LoadMetadata("slot-0");
            Assert.IsNotNull(metadata);
            Assert.AreEqual("Haru", metadata.DisplayName, "The typed name becomes the save's name.");
            Assert.AreEqual(GrowingCharacterAppearanceDefinition.VisualKindName, metadata.CharacterAppearanceSnapshot.VisualKind);
            Assert.AreEqual(chosen.Id, GrowingCharacterStageSelector.ChoiceFromSnapshot(metadata.CharacterAppearanceSnapshot, chosen.Body.Id));

            var driver = default(GrowingCharacterStageDriver);
            float elapsed = 0f;
            while (elapsed < 10f && (driver == null || driver.CurrentLook == null))
            {
                var player = GameObject.Find("Player");
                driver = player != null ? player.GetComponent<GrowingCharacterStageDriver>() : null;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(driver, "The town player must use the growing character.");
            // The stage's own rules pick the body; the creator's choice decides which outfit it wears.
            var worn = driver.Apply(chosen.MinimumStudentDay, chosen.RequiresAdult, null);
            Assert.AreEqual(chosen.Id, worn != null ? worn.Id : null, "The character must start that stage in the outfit picked in the creator.");
        }

        [UnityTest]
        public IEnumerator CREATOR_004_RandomThenConfirmSavesAnOutfitForEveryStage()
        {
            var creator = default(SpumCharacterCreatorPanel);
            yield return OpenCreatorFromMainMenu(c => creator = c);
            yield return ClickButtonNamed("RandomButton");
            var expected = new string[creator.StageCount];
            for (int i = 0; i < expected.Length; i++) expected[i] = creator.ChoiceAt(i).Id;

            yield return ClickButtonNamed("ConfirmButton");
            yield return WaitForScene(20f);

            var snapshot = new SaveService("slot-0", _saveRoot).LoadMetadata("slot-0").CharacterAppearanceSnapshot;
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], GrowingCharacterStageSelector.ChoiceFromSnapshot(snapshot, FindBodyId(expected[i])), "stage " + i);
        }

        [UnityTest]
        public IEnumerator CREATOR_005_BackDoesNotCreateSave()
        {
            yield return OpenCreatorFromMainMenu(null);
            yield return ClickButtonNamed("BackButton");
            yield return null;

            Assert.IsNull(GameObject.Find("SpumCharacterCreatorRoot"), "Back must close the creator.");
            Assert.IsFalse(File.Exists(Path.Combine(_saveRoot, "slot-0", "metadata.json")), "Back must not create save metadata for an empty slot.");
            Assert.IsNull(ActiveSaveContext.Metadata);
        }

        private static string FindBodyId(string appearanceId)
        {
            Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out var registry);
            foreach (var definition in registry.CharacterAppearances)
                if (definition is GrowingCharacterAppearanceDefinition look && look.Id == appearanceId) return look.Body.Id;
            return string.Empty;
        }

        private static int FirstStageWithChoices(SpumCharacterCreatorPanel creator)
        {
            for (int i = 0; i < creator.StageCount; i++)
                if (GameObject.Find("Stage" + i + "_Next") != null) return i;
            Assert.Fail("No growth stage offers more than one starting outfit.");
            return -1;
        }

        private IEnumerator OpenCreatorFromMainMenu(System.Action<SpumCharacterCreatorPanel> found)
        {
            yield return OpenSaveSlotsFromMainMenu();
            yield return ClickButtonNamed("NewGameButton");
            yield return null;
            var creator = Object.FindFirstObjectByType<SpumCharacterCreatorPanel>();
            Assert.IsNotNull(creator);
            found?.Invoke(creator);
        }

        private static IEnumerator OpenSaveSlotsFromMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return WaitForStableScene("MainMenu", 0.25f, 5f);
            yield return Rootborn.Tests.PlayMode.PlayModeScenarioStart.WaitForGameData();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            Assert.IsNotNull(GameObject.Find("SaveSlotSelectRoot"));
        }

        // A new game starts at home or in Town depending on the world entry data; either means the world loaded.
        private static IEnumerator WaitForScene(float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout && SceneManager.GetActiveScene().name != "Town" && SceneManager.GetActiveScene().name != "House")
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Town").Or.EqualTo("House"));
        }

        private static IEnumerator WaitForStableScene(string sceneName, float stableSeconds, float timeout)
        {
            float elapsed = 0f;
            float stable = 0f;
            while (elapsed < timeout)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    stable += Time.unscaledDeltaTime;
                    if (stable >= stableSeconds) yield break;
                }
                else stable = 0f;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator ClickButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.IsInteractable(), name + " must be interactable.");
            Assert.IsNotNull(EventSystem.current, "UI click tests require an EventSystem.");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator Capture(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            float elapsed = 0f;
            while (elapsed < 3f && !(File.Exists(path) && new FileInfo(path).Length > 0L)) { elapsed += Time.unscaledDeltaTime; yield return null; }
        }

        private static void DestroyIfFound(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
