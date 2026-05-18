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
    public sealed class SpumAppearanceSaveLoadE2ETests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-spum-saveload-" + System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator SPUM_UI_PM_006_SaveLoadKeepsAppearance()
        {
            yield return OpenCreatorFromMainMenu();
            yield return ClickButtonNamed("Tab_Hair");
            yield return ClickButtonNamed("PartCell_spum_hair_long");
            yield return ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);

            SaveSlotMetadata firstLoad = new SaveService("slot-0", _saveRoot).LoadMetadata("slot-0");
            Assert.IsNotNull(firstLoad.CharacterAppearanceSnapshot);
            string savedHair = firstLoad.CharacterAppearanceSnapshot.GetSelectedPartId("hair");
            Assert.AreEqual("spum.hair.long", savedHair);

            ActiveSaveContext.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return WaitForStableScene("MainMenu", 0.25f, 5f);
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            yield return ClickButtonNamed("LoadButton");
            yield return WaitForScene("Town", 10f);

            SaveSlotMetadata loaded = ActiveSaveContext.Metadata;
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.CharacterAppearanceSnapshot);
            Assert.AreEqual(savedHair, loaded.CharacterAppearanceSnapshot.GetSelectedPartId("hair"));

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.GetComponent<Rootborn.Game.Characters.Spum.SpumCharacterVisualView>(), "Loaded SPUM snapshots must attach the SPUM visual boundary to the Town player.");
        }

        private IEnumerator OpenCreatorFromMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return WaitForStableScene("MainMenu", 0.25f, 5f);
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            yield return ClickButtonNamed("NewGameButton");
            yield return null;
            Assert.IsNotNull(GameObject.Find("SpumCharacterCreatorRoot"));
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
