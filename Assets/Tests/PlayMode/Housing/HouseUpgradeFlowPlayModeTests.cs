using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Housing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Housing
{
    public sealed class HouseUpgradeFlowPlayModeTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "house-upgrade-flow-" + TestContext.CurrentContext.Test.ID);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-0", DisplayName = "slot-0", WorldSeed = 1205, TileSeed = 1205 });
        }

        [TearDown]
        public void TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_001_SavedStageSelectsExpandedHouseGeneration()
        {
            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { CurrentStageIndex = 0 });
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var baselineFloorCount = CountTiles(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>());

            HouseStatePersistence.Save("slot-0", new HouseStateSaveData { CurrentStageIndex = 1 });
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var expandedFloorCount = CountTiles(GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>());

            Assert.Greater(baselineFloorCount, 100, "Stage 0 House should still generate a playable one-room baseline.");
            Assert.Greater(expandedFloorCount, baselineFloorCount, "Saved stage 1 should generate a visibly larger House floor area.");
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_002_NonInteriorPlayerSeesHireRouteOnly()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "Town should install the House upgrade panel through the runtime provider flow.");

            panel.ShowForTests(CreateStageForPanelTests(includeDirectCondition: false), new HouseStateSaveData(), new HouseCurrencyWallet(500), directEligible: false);

            Assert.IsTrue(panel.HireButtonVisibleForTests);
            Assert.IsTrue(panel.HireButtonInteractableForTests);
            Assert.IsFalse(panel.DirectButtonVisibleForTests);
            StringAssert.Contains("300", panel.VisibleTextForTests);
        }

        [UnityTest]
        public IEnumerator HOUSE_UPGRADE_PM_003_InteriorEligiblePlayerSeesHireAndDirectRoutes()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel);

            panel.ShowForTests(CreateStageForPanelTests(includeDirectCondition: true), new HouseStateSaveData(), new HouseCurrencyWallet(500), directEligible: true);

            Assert.IsTrue(panel.HireButtonVisibleForTests);
            Assert.IsTrue(panel.DirectButtonVisibleForTests);
            Assert.IsTrue(panel.DirectButtonInteractableForTests);
            StringAssert.Contains("120", panel.VisibleTextForTests);
        }

        private static HouseUpgradeStageDefinition CreateStageForPanelTests(bool includeDirectCondition)
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.panel",
                new RectInt(0, 0, 3, 3),
                new[] { HouseConstructionCellRequirement.Floor(1, 1) });
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.panel", 1, 300, 120, InteriorGenerationProfile.CreateExpandedOfficeForTests(), null, blueprint);
            if (includeDirectCondition)
            {
                stage.ConfigureConditionsForTests(null, new[] { ScriptableObject.CreateInstance<HouseAlwaysCondition>() });
            }

            return stage;
        }

        private static int CountTiles(Tilemap tilemap)
        {
            Assert.IsNotNull(tilemap, "HouseGroundTilemap should exist for House generation checks.");
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}