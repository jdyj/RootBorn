using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class SpumCharacterCreatorNewGameE2ETests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-spum-newgame-" + System.Guid.NewGuid().ToString("N"));
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
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SPUM_UI_PM_001_NewGameOpensCreator()
        {
            yield return OpenSaveSlotsFromMainMenu();

            yield return ClickButtonNamed("NewGameButton");

            Assert.IsNotNull(GameObject.Find("SpumCharacterCreatorRoot"), "New Game must open the SPUM character creator before saving.");
            Assert.IsFalse(File.Exists(Path.Combine(_saveRoot, "slot-0", "metadata.json")), "Opening the creator must not create save metadata before Confirm.");
        }

        [UnityTest]
        public IEnumerator SPUM_UI_PM_002_ClickPartUpdatesPreview()
        {
            yield return OpenCreatorFromMainMenu();

            yield return ClickButtonNamed("Tab_Hair");
            yield return ClickButtonNamed("PartCell_spum_hair_long");

            var preview = GameObject.Find("PreviewSelected_hair");
            Assert.IsNotNull(preview, "Part cell clicks must update a visible preview state for the selected category.");
            StringAssert.Contains("spum.hair.long", preview.GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator SPUM_UI_PM_003_RandomCreatesResolvedAppearance()
        {
            yield return OpenCreatorFromMainMenu();

            yield return ClickButtonNamed("RandomButton");
            yield return ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);

            SaveSlotMetadata metadata = new SaveService("slot-0", _saveRoot).LoadMetadata("slot-0");
            AssertResolvedRequiredCategory(metadata, "body");
            AssertResolvedRequiredCategory(metadata, "skin");
            AssertResolvedRequiredCategory(metadata, "eye");
            AssertResolvedRequiredCategory(metadata, "hair");
            AssertResolvedRequiredCategory(metadata, "outfit");
            AssertResolvedRequiredCategory(metadata, "accessory");
        }

        [UnityTest]
        public IEnumerator SPUM_UI_PM_004_ConfirmCreatesSaveAndLoadsTown()
        {
            yield return OpenCreatorFromMainMenu();

            yield return ClickButtonNamed("Tab_Hair");
            yield return ClickButtonNamed("PartCell_spum_hair_long");
            yield return ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);

            SaveSlotMetadata metadata = new SaveService("slot-0", _saveRoot).LoadMetadata("slot-0");
            Assert.IsNotNull(metadata);
            Assert.IsNotNull(metadata.CharacterAppearanceSnapshot);
            Assert.AreEqual("spum", metadata.CharacterAppearanceSnapshot.VisualKind);
            Assert.AreEqual("spum.hair.long", metadata.CharacterAppearanceSnapshot.GetSelectedPartId("hair"));
            Assert.IsNotNull(ActiveSaveContext.Metadata);
            Assert.AreEqual(metadata.CharacterAppearanceSnapshot.GetSelectedPartId("hair"), ActiveSaveContext.Metadata.CharacterAppearanceSnapshot.GetSelectedPartId("hair"));

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.GetComponent<Rootborn.Game.Characters.Spum.SpumCharacterVisualView>(), "Town Player visual must bind through the SPUM visual boundary for SPUM snapshots.");
        }

        [UnityTest]
        public IEnumerator SPUM_UI_PM_005_CancelDoesNotCreateSave()
        {
            yield return OpenCreatorFromMainMenu();

            yield return ClickButtonNamed("CancelButton");
            yield return null;

            Assert.IsNull(GameObject.Find("SpumCharacterCreatorRoot"), "Cancel must close the draft creator flow.");
            Assert.IsFalse(File.Exists(Path.Combine(_saveRoot, "slot-0", "metadata.json")), "Cancel must not create save metadata for an empty slot.");
            Assert.IsNull(ActiveSaveContext.Metadata);
        }

        private IEnumerator OpenCreatorFromMainMenu()
        {
            yield return OpenSaveSlotsFromMainMenu();
            yield return ClickButtonNamed("NewGameButton");
            yield return null;
            Assert.IsNotNull(GameObject.Find("SpumCharacterCreatorRoot"));
        }

        private IEnumerator OpenSaveSlotsFromMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return WaitForStableScene("MainMenu", 0.25f, 5f);
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            Assert.IsNotNull(GameObject.Find("SaveSlotSelectRoot"));
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForStableScene(string sceneName, float stableSeconds, float timeout)
        {
            float elapsed = 0f;
            float stable = 0f;
            while (elapsed < timeout)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    stable += Time.deltaTime;
                    if (stable >= stableSeconds)
                        yield break;
                }
                else
                {
                    stable = 0f;
                }

                elapsed += Time.deltaTime;
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

        private static void AssertResolvedRequiredCategory(SaveSlotMetadata metadata, string categoryId)
        {
            Assert.IsNotNull(metadata);
            Assert.IsNotNull(metadata.CharacterAppearanceSnapshot);
            Assert.IsNotEmpty(metadata.CharacterAppearanceSnapshot.GetSelectedPartId(categoryId), categoryId + " must resolve to a saved SPUM part.");
        }

        private static void DestroyIfFound(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
