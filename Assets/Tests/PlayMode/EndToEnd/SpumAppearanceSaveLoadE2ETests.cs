using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;
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
        public IEnumerator SPUM_SAVE_LOAD_001_LoadedSpumSnapshotRestoresTownPlayerVisualBoundary()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;

            ClickButtonNamed("NewGameButton");
            yield return null;
            ClickButtonNamed("Tab_Hair");
            ClickFirstPartCell();
            ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);

            var service = new SaveService("slot-0", _saveRoot);
            SaveSlotMetadata firstLoad = service.LoadMetadata("slot-0");
            Assert.IsNotNull(firstLoad.CharacterAppearanceSnapshot);
            string savedHair = firstLoad.CharacterAppearanceSnapshot.GetSelectedPartId("hair");
            Assert.IsNotEmpty(savedHair);

            ActiveSaveContext.Clear();
            ActiveSaveContext.Set(firstLoad);
            yield return SceneManager.LoadSceneAsync("Town");
            yield return null;

            SaveSlotMetadata loaded = ActiveSaveContext.Metadata;
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.CharacterAppearanceSnapshot);
            Assert.AreEqual(savedHair, loaded.CharacterAppearanceSnapshot.GetSelectedPartId("hair"));

            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.GetComponent<SpumCharacterVisualView>(), "Loaded SPUM snapshots must attach the SPUM visual boundary to the Town player.");
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

        private static void ClickFirstPartCell()
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith("PartCell_"))
                {
                    buttons[i].onClick.Invoke();
                    return;
                }
            }

            Assert.Fail("Expected at least one selectable SPUM part cell.");
        }

        private static void ClickButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name);
            button.onClick.Invoke();
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
