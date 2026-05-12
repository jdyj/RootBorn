using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class PlayerGlobalStateScenarioTests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlayerGlobalState.ClearForTests();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-player-global-state", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            var service = new SaveService("slot-0", _saveRoot);
            var metadata = service.CreateUiMetadata("slot-0", new CharacterCustomization(), 2026051121, 2026051122);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAYER_GLOBAL_001_InventorySurvivesFreshScenePlayerCreationWithinSaveSlot()
        {
            yield return LoadScene("Town");
            yield return WaitForPlayerInventory(10f);

            var townInventory = GameObject.Find("Player").GetComponent<PlayerInventory>();
            var wood = townInventory.FindById("Wood");
            Assert.IsNotNull(wood);
            int expectedWood = townInventory.Inventory.CountOf(wood) + 9;
            townInventory.Inventory.Add(wood, 9);

            yield return LoadScene("Farm");
            yield return WaitForPlayerInventory(10f);

            var farmInventory = GameObject.Find("Player").GetComponent<PlayerInventory>();
            Assert.AreEqual(expectedWood, farmInventory.Inventory.CountOf(wood), "Inventory is player-owned state and must survive fresh scene player creation in the same save slot.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForPlayerInventory(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                if (player != null && player.GetComponent<PlayerInventory>() != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Scene did not expose PlayerInventory within timeout.");
        }
    }
}
